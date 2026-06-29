using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketViewer.Application.Services;

public interface IGpuSmaCalculationService
{
    Dictionary<string, List<(long Timestamp, double Value)>> CalculateSmaBatch(
        Dictionary<string, StocksResponse> tickerData,
        int period);
    
    void Dispose();
}

public class GpuSmaCalculationService : IGpuSmaCalculationService, IDisposable
{
    private readonly Context? _context;
    private readonly Accelerator? _accelerator;
    private bool _gpuAvailable;
    private readonly ILogger<GpuSmaCalculationService>? _logger;
    private bool _disposed = false;

    public GpuSmaCalculationService(ILogger<GpuSmaCalculationService>? logger = null)
    {
        _logger = logger;
        try
        {
            // Configure ILGPU to use system CUDA Toolkit if available
            // This helps ensure it uses the correct PTX version
            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (!string.IsNullOrEmpty(cudaPath))
            {
                _logger?.LogInformation("Using CUDA_PATH: {CudaPath}", cudaPath);
            }
            
            // Note: ILGPU uses its own bundled PTX compiler
            // For sm_89 (Ada Lovelace), we need PTX 7.8+ which requires:
            // - CUDA Toolkit 11.8+ (you have 13.1 ✓)
            // - NVIDIA Driver that supports CUDA 13.1 (your driver 576.52 supports CUDA 12.9)
            // Solution: Update NVIDIA driver to support CUDA 13.1
            _context = Context.Create(builder => builder.Cuda());
            var device = _context.GetPreferredDevice(preferCPU: false);
            if (device != null)
            {
                // Try to create accelerator, but handle compute capability issues
                try
                {
                    _accelerator = device.CreateAccelerator(_context);
                    
                    // Test kernel loading to detect PTX/compute capability issues early
                    // This will fail if PTX version doesn't support the GPU's compute capability
                    try
                    {
                        TestKernelLoading();
                        _gpuAvailable = true;
                        _logger?.LogInformation("GPU accelerator initialized and kernel loading tested successfully");
                    }
                    catch (CudaException kernelEx)
                    {
                        var errorMsg = kernelEx.Message.Contains("PTX") || kernelEx.Message.Contains("sm_89")
                            ? "GPU accelerator created but kernel loading failed due to PTX/compute capability mismatch. " +
                              "Your GPU (sm_89/Ada Lovelace) requires PTX 7.8+, but ILGPU is generating PTX 7.6. " +
                              "Solution: Update your NVIDIA driver to support CUDA 13.1 (current driver supports CUDA 12.9). " +
                              "Disabling GPU and falling back to CPU."
                            : "GPU accelerator created but kernel loading failed. Disabling GPU and falling back to CPU.";
                        
                        _logger?.LogWarning(kernelEx, errorMsg);
                        _gpuAvailable = false;
                        _accelerator?.Dispose();
                        _accelerator = null;
                    }
                }
                catch (CudaException ex)
                {
                    _logger?.LogWarning(ex, "Failed to create GPU accelerator due to CUDA/compute capability issue. Falling back to CPU.");
                    _gpuAvailable = false;
                    _accelerator = null;
                }
            }
            else
            {
                _gpuAvailable = false;
            }
        }
        catch (Exception ex)
        {
            // GPU not available, fallback to CPU
            _logger?.LogWarning(ex, "GPU initialization failed. Falling back to CPU.");
            _gpuAvailable = false;
            _context = null;
            _accelerator = null;
        }
    }

    public Dictionary<string, List<(long Timestamp, double Value)>> CalculateSmaBatch(
        Dictionary<string, StocksResponse> tickerData,
        int period)
    {
        if (tickerData == null || tickerData.Count == 0)
            return new Dictionary<string, List<(long Timestamp, double Value)>>();

        // Filter out tickers with insufficient data
        var validTickers = new Dictionary<string, (double[] closePrices, long[] timestamps, int dataCount)>();
        foreach (var kvp in tickerData)
        {
            var ticker = kvp.Key;
            var response = kvp.Value;

            if (response?.Results == null || response.Results.Count < period)
                continue;

            var data = response.Results;
            var dataCount = data.Count;
            var outputCount = dataCount - period + 1;

            if (outputCount <= 0)
                continue;

            var closePrices = data.Select(b => (double)b.Close).ToArray();
            var timestamps = data.Select(b => b.Timestamp).ToArray();
            validTickers[ticker] = (closePrices, timestamps, dataCount);
        }

        if (validTickers.Count == 0)
            return new Dictionary<string, List<(long Timestamp, double Value)>>();

        // Use GPU batch processing if available, otherwise fall back to CPU
        if (_gpuAvailable && _accelerator != null)
        {
            return CalculateSmaBatchGpu(validTickers, period);
        }
        else
        {
            // Fallback to CPU processing
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();
            foreach (var kvp in validTickers)
            {
                results[kvp.Key] = CalculateSmaCpu(kvp.Value.closePrices, kvp.Value.timestamps, period);
            }
            return results;
        }
    }

    private Dictionary<string, List<(long Timestamp, double Value)>> CalculateSmaBatchGpu(
        Dictionary<string, (double[] closePrices, long[] timestamps, int dataCount)> tickerData,
        int period)
    {
        if (_accelerator == null)
        {
            // Fallback to CPU
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();
            foreach (var kvp in tickerData)
            {
                results[kvp.Key] = CalculateSmaCpu(kvp.Value.closePrices, kvp.Value.timestamps, period);
            }
            return results;
        }

        // Process in chunks to handle large numbers of tickers efficiently
        // This avoids memory issues and ensures good GPU utilization
        // Chunk size of 2000 balances memory usage and GPU utilization
        // For 10,000 tickers, this will process in 5 chunks
        const int chunkSize = 10000;
        var tickerList = tickerData.ToList();
        var totalTickers = tickerList.Count;
        var allResults = new Dictionary<string, List<(long Timestamp, double Value)>>();

        if (totalTickers > chunkSize)
        {
            _logger?.LogInformation("Processing {TotalTickers} tickers in chunks of {ChunkSize}", totalTickers, chunkSize);
        }

        for (int chunkStart = 0; chunkStart < tickerList.Count; chunkStart += chunkSize)
        {
            var chunkEnd = Math.Min(chunkStart + chunkSize, tickerList.Count);
            var chunk = tickerList.Skip(chunkStart).Take(chunkEnd - chunkStart)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (totalTickers > chunkSize)
            {
                _logger?.LogDebug("Processing chunk {ChunkNum}/{TotalChunks} ({Start}-{End} tickers)",
                    (chunkStart / chunkSize) + 1,
                    (int)Math.Ceiling((double)totalTickers / chunkSize),
                    chunkStart,
                    chunkEnd - 1);
            }

            var chunkResults = ProcessTickerChunkGpu(chunk, period);
            foreach (var result in chunkResults)
            {
                allResults[result.Key] = result.Value;
            }
        }

        return allResults;
    }

    private Dictionary<string, List<(long Timestamp, double Value)>> ProcessTickerChunkGpu(
        Dictionary<string, (double[] closePrices, long[] timestamps, int dataCount)> tickerData,
        int period)
    {
        if (_accelerator == null)
        {
            // Fallback to CPU
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();
            foreach (var kvp in tickerData)
            {
                results[kvp.Key] = CalculateSmaCpu(kvp.Value.closePrices, kvp.Value.timestamps, period);
            }
            return results;
        }

        try
        {
            var tickerList = tickerData.ToList();
            var numTickers = tickerList.Count;

            // Build concatenated arrays with offsets
            var allClosePrices = new List<double>();
            var allTimestamps = new List<long>();
            var dataOffsets = new int[numTickers];
            var dataLengths = new int[numTickers];
            var outputOffsets = new int[numTickers];
            var outputLengths = new int[numTickers];

            int currentDataOffset = 0;
            int currentOutputOffset = 0;

            for (int i = 0; i < numTickers; i++)
            {
                var (closePrices, timestamps, dataCount) = tickerList[i].Value;
                var outputCount = dataCount - period + 1;

                dataOffsets[i] = currentDataOffset;
                dataLengths[i] = dataCount;
                outputOffsets[i] = currentOutputOffset;
                outputLengths[i] = outputCount;

                allClosePrices.AddRange(closePrices);
                allTimestamps.AddRange(timestamps);

                currentDataOffset += dataCount;
                currentOutputOffset += outputCount;
            }

            // Load the batch kernel
            var kernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView<double>,
                ArrayView<long>,
                ArrayView<double>,
                ArrayView<int>,
                ArrayView<int>,
                ArrayView<int>,
                ArrayView<int>,
                int>(SmaBatchKernel);

            // Allocate GPU memory
            using var closePricesBuffer = _accelerator.Allocate1D(allClosePrices.ToArray());
            using var timestampsBuffer = _accelerator.Allocate1D(allTimestamps.ToArray());
            using var outputBuffer = _accelerator.Allocate1D<double>(currentOutputOffset);
            using var dataOffsetsBuffer = _accelerator.Allocate1D(dataOffsets);
            using var dataLengthsBuffer = _accelerator.Allocate1D(dataLengths);
            using var outputOffsetsBuffer = _accelerator.Allocate1D(outputOffsets);
            using var outputLengthsBuffer = _accelerator.Allocate1D(outputLengths);

            // Launch kernel - one thread per ticker
            var index = new Index1D(numTickers);
            kernel(index,
                closePricesBuffer.View,
                timestampsBuffer.View,
                outputBuffer.View,
                dataOffsetsBuffer.View,
                dataLengthsBuffer.View,
                outputOffsetsBuffer.View,
                outputLengthsBuffer.View,
                period);

            // Synchronize
            _accelerator.Synchronize();

            // Copy results back
            var allResults = outputBuffer.GetAsArray1D();
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();

            for (int i = 0; i < numTickers; i++)
            {
                var ticker = tickerList[i].Key;
                var outputOffset = outputOffsets[i];
                var outputLength = outputLengths[i];
                var (_, timestamps, dataCount) = tickerList[i].Value;

                var tickerResults = new List<(long Timestamp, double Value)>(outputLength);
                for (int j = 0; j < outputLength; j++)
                {
                    var timestampIndex = period - 1 + j;
                    var resultIndex = outputOffset + j;
                    tickerResults.Add((timestamps[timestampIndex], allResults[resultIndex]));
                }
                results[ticker] = tickerResults;
            }

            return results;
        }
        catch (CudaException ex)
        {
            _logger?.LogWarning(ex, "GPU chunk calculation failed. Falling back to CPU for this chunk.");
            // Fallback to CPU for this chunk only (don't disable GPU for all chunks)
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();
            foreach (var kvp in tickerData)
            {
                results[kvp.Key] = CalculateSmaCpu(kvp.Value.closePrices, kvp.Value.timestamps, period);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "GPU chunk calculation failed. Falling back to CPU for this chunk.");
            // Fallback to CPU for this chunk only
            var results = new Dictionary<string, List<(long Timestamp, double Value)>>();
            foreach (var kvp in tickerData)
            {
                results[kvp.Key] = CalculateSmaCpu(kvp.Value.closePrices, kvp.Value.timestamps, period);
            }
            return results;
        }
    }

    private List<(long Timestamp, double Value)> CalculateSmaCpu(
        double[] closePrices,
        long[] timestamps,
        int period)
    {
        var results = new List<(long Timestamp, double Value)>();
        var dataCount = closePrices.Length;

        for (int i = period - 1; i < dataCount; i++)
        {
            double sum = 0.0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += closePrices[j];
            }
            var value = sum / period;
            results.Add((timestamps[i], value));
        }

        return results;
    }

    // Test kernel to validate GPU compatibility during initialization
    private void TestKernelLoading()
    {
        if (_accelerator == null)
            return;

        // Create a simple test kernel to verify PTX compilation works
        var testKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D,
            ArrayView<double>,
            ArrayView<double>,
            int,
            int>(SmaKernel);

        // Test with minimal data to verify it compiles
        using var testInput = _accelerator.Allocate1D(new double[] { 1.0, 2.0, 3.0 });
        using var testOutput = _accelerator.Allocate1D<double>(1);
        
        var testIndex = new Index1D(1);
        testKernel(testIndex, testInput.View, testOutput.View, 2, 3);
        _accelerator.Synchronize();
    }

    // GPU kernel for calculating SMA for a single ticker
    private static void SmaKernel(
        Index1D index,
        ArrayView<double> closePrices,
        ArrayView<double> output,
        int period,
        int dataLength)
    {
        // Calculate which output index we're computing
        var outputIndex = index.X;
        var dataIndex = period - 1 + outputIndex;

        if (dataIndex >= dataLength)
            return;

        // Calculate sum for this window
        double sum = 0.0;
        for (int j = dataIndex - period + 1; j <= dataIndex; j++)
        {
            sum += closePrices[j];
        }

        // Store the result
        output[outputIndex] = sum / period;
    }

    // GPU kernel for batch processing multiple tickers in parallel
    // Each thread processes one ticker
    private static void SmaBatchKernel(
        Index1D tickerIndex,
        ArrayView<double> allClosePrices,
        ArrayView<long> allTimestamps,
        ArrayView<double> allOutputs,
        ArrayView<int> dataOffsets,
        ArrayView<int> dataLengths,
        ArrayView<int> outputOffsets,
        ArrayView<int> outputLengths,
        int period)
    {
        var tickerIdx = tickerIndex.X;
        
        // Get this ticker's data range
        var dataOffset = dataOffsets[tickerIdx];
        var dataLength = dataLengths[tickerIdx];
        var outputOffset = outputOffsets[tickerIdx];
        var outputLength = outputLengths[tickerIdx];

        // Process all SMA calculations for this ticker
        for (int outputIdx = 0; outputIdx < outputLength; outputIdx++)
        {
            var dataIdx = period - 1 + outputIdx;
            
            // Calculate sum for this window
            double sum = 0.0;
            for (int j = dataIdx - period + 1; j <= dataIdx; j++)
            {
                sum += allClosePrices[dataOffset + j];
            }

            // Store the result
            allOutputs[outputOffset + outputIdx] = sum / period;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _accelerator?.Dispose();
                _context?.Dispose();
            }
            _disposed = true;
        }
    }
}


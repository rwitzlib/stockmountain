using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MarketViewer.Core.Utilities;

public static class CompressionUtilities
{
    public static string CompressString(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var outputStream = new MemoryStream();
        using (var compressionStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            compressionStream.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(outputStream.ToArray());
    }

    public static TType DecompressString<TType>(string compressedDetails, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(compressedDetails))
        {
            return default;
        }
        var bytes = Convert.FromBase64String(compressedDetails);
        using var inputStream = new MemoryStream(bytes);
        using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressionStream, Encoding.UTF8);
        var decompressedData = reader.ReadToEnd();
        return JsonSerializer.Deserialize<TType>(decompressedData, options);
    }

    /// <summary>
    /// Compresses a string using Brotli compression and returns raw bytes.
    /// Brotli typically achieves 20-30% better compression than GZip for text.
    /// </summary>
    public static byte[] CompressBrotli(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var outputStream = new MemoryStream();
        using (var compressionStream = new BrotliStream(outputStream, CompressionLevel.Optimal))
        {
            compressionStream.Write(bytes, 0, bytes.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses Brotli-compressed bytes back to a string.
    /// </summary>
    public static string DecompressBrotli(byte[] compressedBytes)
    {
        using var inputStream = new MemoryStream(compressedBytes);
        using var decompressionStream = new BrotliStream(inputStream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressionStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SQS;
using Backtest.Lambda.Repository;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Backtest.Lambda;

/// <summary>
/// This lambda can be ignored for now
/// </summary>
/// <param name="serviceProvider"></param>
public class DispatcherFunction(IServiceProvider serviceProvider)
{
    public IServiceProvider ServiceProvider => serviceProvider; // Expose the service provider for testing purposes

    private readonly BacktestRepository _backtestRepository = serviceProvider.GetService<BacktestRepository>();
    private readonly UserRepository _userRepository = serviceProvider.GetService<UserRepository>();
    private readonly IAmazonSQS _sqs = serviceProvider.GetService<IAmazonSQS>();
    private readonly ILogger<OrchestratorFunction> _logger = serviceProvider.GetService<ILogger<OrchestratorFunction>>();

    private const int ESTIMATED_DAILY_CREDIT_COST = 120; // Estimated Credit Cost per Day

    public DispatcherFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var request = JsonSerializer.Deserialize<OrchestratorRequest>(record.Body);
                if (request == null)
                {
                    _logger.LogError("Failed to deserialize OrchestratorRequest from SQS message. MessageId: {MessageId}", record.MessageId);
                    continue;
                }

                await ProcessRequest(request);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing SQS record. MessageId: {MessageId}", record.MessageId);
                throw;
            }
        }
    }

    private async Task ProcessRequest(OrchestratorRequest request)
    {
        try
        {
            var sp = new Stopwatch();
            sp.Start();

            var record = await _backtestRepository.Get(request.Id);

            _logger.LogInformation("Processing backtest request with ID {RequestId}.", request.Id);

            if (record is null || record.Status is not BacktestStatus.Pending)
            {
                _logger.LogInformation("Backtest record not found or already completed for request ID {RequestId}.", request.Id);

                if (record != null)
                {
                    record.Status = BacktestStatus.Failed;
                    record.CreditsUsed = 0;
                    record.Errors = ["Backtest already completed or not found. Please try again."];

                    await _backtestRepository.Put(record);
                }
                return;
            }

            var estimatedCreditCost = ((request.End - request.Start).Days + 1) * ESTIMATED_DAILY_CREDIT_COST;

            var user = await _userRepository.Get(record.UserId);
            if (user == null || user.Credits < estimatedCreditCost)
            {
                _logger.LogInformation("Insufficient credits for user {UserId} to run backtest. Estimated cost: {EstimatedCost}, Available credits: {AvailableCredits}",
                    request.UserId, estimatedCreditCost, user.Credits);

                // TODO: In the future, check if there are S3 results for these request details and if so, check if the
                // user has enough credits to run backtest, excluding days from S3 results.
                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = ["Insufficient credits to run backtest. Please purchase more credits."];

                await _backtestRepository.Put(record);
                return;
            }

            record.Status = BacktestStatus.InProgress;
            await _backtestRepository.Put(record);

            var days = (request.End - request.Start).Days;

            var queueUrl = Environment.GetEnvironmentVariable("FILTER_QUEUE_URL");
            for (var i = 0; i <= days; i++)
            {
                var currentDate = request.Start.AddDays(i);
                
                _logger.LogInformation("Dispatching filters for date {Date}", currentDate.Date);
                await Parallel.ForEachAsync(request.EntrySettings.Filters, async (filter, cancellationToken) =>
                {
                    _logger.LogInformation("Dispatching filter {Filter} for date {Date}", filter, currentDate.Date);
                    var response = await _sqs.SendMessageAsync(queueUrl, filter, cancellationToken);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        _logger.LogError("Failed to dispatch filter {Filter} for date {Date}. SQS response code: {StatusCode}", filter, currentDate.Date, response.HttpStatusCode);
                    }
                });
            }

            sp.Stop();
            _logger.LogInformation("Dispatched {FilterCount} filters over {DayCount} days in {ElapsedMilliseconds} ms", request.EntrySettings.Filters.Count, days + 1, sp.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in DispatcherFunction for request {RequestId}", request.Id);
            throw;
        }
    }
}

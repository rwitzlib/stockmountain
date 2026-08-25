using Amazon.Lambda.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Billing.Lambda;

public class RefillFunction(IServiceProvider serviceProvider)
{
    private readonly MonthlyRefillService _refillService = serviceProvider.GetRequiredService<MonthlyRefillService>();
    private readonly IConfiguration _configuration = serviceProvider.GetRequiredService<IConfiguration>();
    private readonly ILogger<RefillFunction> _logger = serviceProvider.GetRequiredService<ILogger<RefillFunction>>();

    public RefillFunction() : this(Startup.ConfigureServices()) { }

    public async Task<RefillResult> FunctionHandler(RefillRequest request, ILambdaContext context)
    {
        var period = string.IsNullOrWhiteSpace(request?.Period)
            ? DateTime.UtcNow.ToString("yyyy-MM")
            : request.Period;
        var dryRun = request?.DryRun ?? false;

        var freeGrant = _configuration.GetSection("Tiers:Free").GetValue<float>("MonthlyCredits");

        _logger.LogInformation(
            "Starting monthly free-tier refill for period {Period} (grant {Grant}, dry run: {DryRun})",
            period, freeGrant, dryRun);

        return await _refillService.Run(period, freeGrant, dryRun);
    }
}

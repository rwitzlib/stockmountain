using Amazon.Lambda.Core;
using MarketViewer.Contracts.Enums;
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

        // One grant per parseable tier key ("Free", "Pro", "Premium"); annual subscribers
        // refill from their tier's grant, free/legacy users from Free's.
        var tierGrants = _configuration.GetSection("Tiers").GetChildren()
            .Where(tier => Enum.TryParse<UserRole>(tier.Key, out _))
            .ToDictionary(
                tier => Enum.Parse<UserRole>(tier.Key),
                tier => tier.GetValue<float>("MonthlyCredits"));

        _logger.LogInformation(
            "Starting monthly refill for period {Period} (grants {@Grants}, dry run: {DryRun})",
            period, tierGrants, dryRun);

        return await _refillService.Run(period, tierGrants, dryRun);
    }
}

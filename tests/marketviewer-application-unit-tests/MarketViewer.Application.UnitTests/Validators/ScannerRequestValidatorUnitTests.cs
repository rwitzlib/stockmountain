using MarketViewer.Application.Validators;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Filters.Registry;
using Xunit;

namespace MarketViewer.Application.UnitTests.Validators;

public class ScannerRequestValidatorUnitTests
{
    private readonly ScannerCreateRequestValidator _createValidator = new();
    private readonly ScannerUpdateRequestValidator _updateValidator = new();

    private static ScannerCreateRequest ValidRequest() => new()
    {
        Name = "Oversold bounce",
        EntrySettings = new StrategyEntrySettings { Filters = ["rsi(14) < 30 [1m]"] },
    };

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _createValidator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        var request = ValidRequest();
        request.Name = "";

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Scanner name is required.");
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var request = ValidRequest();
        request.Name = new string('x', 101);

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Scanner name must be 100 characters or fewer.");
    }

    [Fact]
    public void Validate_NullEntrySettings_Fails()
    {
        var request = ValidRequest();
        request.EntrySettings = null!;

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Entry settings are required.");
    }

    [Fact]
    public void Validate_EmptyFilters_Fails()
    {
        var request = ValidRequest();
        request.EntrySettings = new StrategyEntrySettings { Filters = [] };

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Entry settings must contain at least one filter.");
    }

    [Fact]
    public void Validate_UnparseableExpression_FailsWithParserMessage()
    {
        var request = ValidRequest();
        request.EntrySettings = new StrategyEntrySettings { Filters = ["rsl(14) < 30 [1m]"] };

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.StartsWith("Invalid filter 'rsl(14) < 30 [1m]':"));
    }

    [Fact]
    public void Validate_BlankExpression_Fails()
    {
        var request = ValidRequest();
        request.EntrySettings = new StrategyEntrySettings { Filters = ["   "] };

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Filter expressions cannot be empty.");
    }

    [Fact]
    public void Validate_OneBadExpressionAmongValid_FlagsOnlyTheBadOne()
    {
        var request = ValidRequest();
        request.EntrySettings = new StrategyEntrySettings { Filters = ["close > 5 [1m]", "close > > 5"] };

        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.StartsWith("Invalid filter 'close > > 5':", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_UpdateRequest_SameRules()
    {
        var request = new ScannerUpdateRequest
        {
            Id = "abc",
            Name = "",
            EntrySettings = new StrategyEntrySettings { Filters = ["not a filter ["] },
        };

        var result = _updateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Scanner name is required.");
        Assert.Contains(result.Errors, e => e.ErrorMessage.StartsWith("Invalid filter 'not a filter [':"));
    }

    [Fact]
    public void EntrySettingsValidator_ContextViolation_Fails()
    {
        // slope() is declared for scan/backtest but not chart — the chart context exercises
        // the context-violation branch until a scan-only or backtest-only function exists.
        var validator = new StrategyEntrySettingsValidator(FilterContext.Chart);

        var result = validator.Validate(new StrategyEntrySettings { Filters = ["slope(close, 5) > 0 [1m]"] });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("not available in chart filters"));
    }

    [Fact]
    public void EntrySettingsValidator_BacktestContext_AcceptsSharedFunctions()
    {
        var validator = new StrategyEntrySettingsValidator(FilterContext.Backtest);

        var result = validator.Validate(new StrategyEntrySettings { Filters = ["slope(close, 5) > 0 [1m]", "adv() > 2000000 [1d]"] });

        Assert.True(result.IsValid);
    }
}

using FluentValidation;
using MarketViewer.Application.Services;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Filters.Registry;

namespace MarketViewer.Application.Validators;

public class StrategyEntrySettingsValidator : AbstractValidator<StrategyEntrySettings>
{
    /// <param name="context">
    /// The evaluator the filters are destined for: Scan for scanners and live strategies,
    /// Backtest for backtests. Expressions are parsed with the real engine so a bad filter
    /// is rejected at create/update time instead of failing silently at scan time.
    /// </param>
    public StrategyEntrySettingsValidator(FilterContext context = FilterContext.Scan)
    {
        RuleFor(x => x.Filters)
            .NotNull()
            .WithMessage("Entry settings must contain filters.")
            .NotEmpty()
            .WithMessage("Entry settings must contain at least one filter.");

        RuleForEach(x => x.Filters).Custom((expression, validationContext) =>
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                validationContext.AddFailure("Filter expressions cannot be empty.");
                return;
            }

            var error = FilterExpressionValidator.GetError(expression, context);
            if (error is not null)
            {
                validationContext.AddFailure($"Invalid filter '{expression}': {error}");
            }
        });
    }
}

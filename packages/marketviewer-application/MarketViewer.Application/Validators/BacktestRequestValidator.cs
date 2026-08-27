using FluentValidation;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Filters.Registry;

namespace MarketViewer.Application.Validators
{
    public class BacktestRequestValidator : AbstractValidator<BacktestCreateRequest>
    {
        public BacktestRequestValidator()
        {
            RuleFor(x => x.Start)
                .LessThanOrEqualTo(x => x.End)
                .WithMessage("Start date must be on or before the end date.");

            RuleFor(x => x.EntrySettings)
                .NotNull()
                .WithMessage("Entry settings are required.")
                .SetValidator(new StrategyEntrySettingsValidator(FilterContext.Backtest));

            RuleFor(x => x.ExitSettings)
                .NotNull()
                .WithMessage("Exit settings are required.")
                .SetValidator(new StrategyExitSettingsValidator());
        }
    }
}

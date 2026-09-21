using FluentValidation;
using MarketViewer.Contracts.Models.Backtest;
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

            When(x => x.FillSettings is not null, () =>
            {
                RuleFor(x => x.FillSettings.EntryFill)
                    .IsInEnum()
                    .WithMessage("Entry fill must be nextBarOpen or signalClose.");

                RuleFor(x => x.FillSettings.SlippagePercent)
                    .InclusiveBetween(0f, BacktestFillSettings.MaxSlippagePercent)
                    .WithMessage($"Slippage must be between 0% and {BacktestFillSettings.MaxSlippagePercent}%.");

                RuleFor(x => x.FillSettings.StopSlippagePercent)
                    .InclusiveBetween(0f, BacktestFillSettings.MaxSlippagePercent)
                    .WithMessage($"Stop slippage must be between 0% and {BacktestFillSettings.MaxSlippagePercent}%.");
            });
        }
    }
}

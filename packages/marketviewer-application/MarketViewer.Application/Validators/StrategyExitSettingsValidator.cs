using FluentValidation;
using MarketViewer.Contracts.Models.Strategy;

namespace MarketViewer.Application.Validators;

/// <summary>
/// Stop loss, take profit, and timed exit are mandatory for every strategy and backtest;
/// the C# `required` modifier only enforces key presence, so explicit nulls are caught here.
/// </summary>
public class StrategyExitSettingsValidator : AbstractValidator<StrategyExitSettings>
{
    public StrategyExitSettingsValidator()
    {
        RuleFor(x => x.StopLoss)
            .NotNull()
            .WithMessage("A stop loss is required.");

        RuleFor(x => x.TakeProfit)
            .NotNull()
            .WithMessage("A take profit is required.");

        RuleFor(x => x.TimedExit)
            .NotNull()
            .WithMessage("A timed exit is required.");

        RuleFor(x => x.TimedExit.Timeframe)
            .NotNull()
            .When(x => x.TimedExit is not null)
            .WithMessage("The timed exit requires a timeframe.");
    }
}

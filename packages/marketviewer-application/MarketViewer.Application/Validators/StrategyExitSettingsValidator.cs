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

        // The trailing stop is optional; when present it needs a positive trail distance
        // (a zero trail would stop out on the first downtick) and a non-negative activation.
        RuleFor(x => x.TrailingStop.Value)
            .GreaterThan(0)
            .When(x => x.TrailingStop is not null)
            .WithMessage("The trailing stop distance must be greater than zero.");

        RuleFor(x => x.TrailingStop.Activation)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TrailingStop?.Activation is not null)
            .WithMessage("The trailing stop activation cannot be negative.");

        RuleFor(x => x.TimedExit)
            .NotNull()
            .WithMessage("A timed exit is required.");

        RuleFor(x => x.TimedExit.Timeframe)
            .NotNull()
            .When(x => x.TimedExit is not null)
            .WithMessage("The timed exit requires a timeframe.");
    }
}

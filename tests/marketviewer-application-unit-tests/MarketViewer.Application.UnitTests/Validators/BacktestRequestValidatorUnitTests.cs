using MarketViewer.Application.Validators;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Market.Backtest;
using System;
using Xunit;

namespace MarketViewer.Application.UnitTests.Validators;

public class BacktestRequestValidatorUnitTests
{
    private readonly BacktestRequestValidator _validator = new();

    private static BacktestCreateRequest ValidRequest(Action<BacktestCreateRequest>? mutate = null)
    {
        var request = new BacktestCreateRequest
        {
            Start = DateTimeOffset.Parse("2026-01-01"),
            End = DateTimeOffset.Parse("2026-02-01"),
            PositionSettings = new StrategyPositionSettings
            {
                StartingBalance = 10000,
                MaxConcurrentPositions = 3,
                Model = new PositionModel { Type = PositionType.Fixed, Size = 1000 },
            },
            EntrySettings = new StrategyEntrySettings
            {
                Filters = ["close > 1 [1m]"],
            },
            ExitSettings = new StrategyExitSettings
            {
                StopLoss = new Exit { Type = ExitValueType.percent, Value = -5 },
                TakeProfit = new Exit { Type = ExitValueType.percent, Value = 10 },
                TimedExit = new TimedExit
                {
                    AvoidOvernight = true,
                    Timeframe = new Timeframe(30, Timespan.minute),
                },
            },
        };

        mutate?.Invoke(request);
        return request;
    }

    [Fact]
    public void Validate_ValidRequestWithoutCooldown_Passes()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullStopLoss_Fails()
    {
        var request = ValidRequest(r => r.ExitSettings = new StrategyExitSettings
        {
            StopLoss = null!,
            TakeProfit = r.ExitSettings.TakeProfit,
            TimedExit = r.ExitSettings.TimedExit,
        });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A stop loss is required.");
    }

    [Fact]
    public void Validate_NullTakeProfit_Fails()
    {
        var request = ValidRequest(r => r.ExitSettings = new StrategyExitSettings
        {
            StopLoss = r.ExitSettings.StopLoss,
            TakeProfit = null!,
            TimedExit = r.ExitSettings.TimedExit,
        });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A take profit is required.");
    }

    [Fact]
    public void Validate_NullTimedExit_Fails()
    {
        var request = ValidRequest(r => r.ExitSettings = new StrategyExitSettings
        {
            StopLoss = r.ExitSettings.StopLoss,
            TakeProfit = r.ExitSettings.TakeProfit,
            TimedExit = null!,
        });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A timed exit is required.");
    }

    [Fact]
    public void Validate_TimedExitWithoutTimeframe_Fails()
    {
        var request = ValidRequest(r => r.ExitSettings = new StrategyExitSettings
        {
            StopLoss = r.ExitSettings.StopLoss,
            TakeProfit = r.ExitSettings.TakeProfit,
            TimedExit = new TimedExit { AvoidOvernight = true, Timeframe = null! },
        });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "The timed exit requires a timeframe.");
    }

    [Fact]
    public void Validate_EmptyFilters_Fails()
    {
        var request = ValidRequest(r => r.EntrySettings = new StrategyEntrySettings { Filters = [] });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_StartAfterEnd_Fails()
    {
        var request = ValidRequest();
        var flipped = new BacktestCreateRequest
        {
            Start = request.End,
            End = request.Start,
            PositionSettings = request.PositionSettings,
            EntrySettings = request.EntrySettings,
            ExitSettings = request.ExitSettings,
        };

        var result = _validator.Validate(flipped);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Start date must be on or before the end date.");
    }
}

using FluentValidation;
using MarketViewer.Contracts.Enums;

namespace MarketDataAggregator.Validation;

public class MarketDataAggregatorRequestValidator : AbstractValidator<MarketDataAggregatorRequest>
{
    public MarketDataAggregatorRequestValidator()
    {
        When(request => request.Type is null, () =>
        {
            RuleFor(request => request.Multiplier)
                .InclusiveBetween(1, 30)
                .WithMessage("Multiplier must be between 1 and 30.");
        });

        RuleFor(request => request.Timespan)
            .Must(timespan => timespan is Timespan.minute or Timespan.hour or Timespan.day)
            .WithMessage("Timespan {PropertyValue} is not supported.");

        RuleFor(request => request.Date)
            .Must(date => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .WithMessage((request, _) => $"No market data to gather on {request.Date.DayOfWeek}.");
    }
}

using FluentValidation;

namespace MarketDataAggregator.Validation;

public class MarketDataOrchestratorRequestValidator : AbstractValidator<MarketDataOrchestratorRequest>
{
    public MarketDataOrchestratorRequestValidator()
    {
        RuleFor(request => request.Start)
            .Must((request, _) => request.Start.Date <= request.End.Date)
            .WithMessage("Start must be before or equal to end.");

        RuleFor(request => request.Multiplier)
            .InclusiveBetween(1, 30)
            .WithMessage("Multiplier must be between 1 and 30.");

        RuleFor(request => request.Timespans)
            .NotEmpty()
            .WithMessage("At least one supported timespan is required.");
    }
}

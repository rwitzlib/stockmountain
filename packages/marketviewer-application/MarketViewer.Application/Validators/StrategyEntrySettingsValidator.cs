using FluentValidation;
using MarketViewer.Contracts.Models.Strategy;

namespace MarketViewer.Application.Validators;

public class StrategyEntrySettingsValidator : AbstractValidator<StrategyEntrySettings>
{
    public StrategyEntrySettingsValidator()
    {
        RuleFor(x => x.Filters)
            .NotNull()
            .WithMessage("Entry settings must contain filters.")
            .NotEmpty()
            .WithMessage("Entry settings must contain at least one filter.");
    }
}

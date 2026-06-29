using FluentValidation;
using MarketViewer.Contracts.Requests.Management.Strategy;

namespace MarketViewer.Application.Validators;

public class StrategyCreateRequestValidator : AbstractValidator<StrategyCreateRequest>
{
    public StrategyCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Strategy name is required.");

        RuleFor(x => x.EntrySettings)
            .NotNull()
            .WithMessage("Entry settings are required.")
            .SetValidator(new StrategyEntrySettingsValidator());
    }
}

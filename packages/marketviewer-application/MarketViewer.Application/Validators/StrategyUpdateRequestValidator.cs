using FluentValidation;
using MarketViewer.Contracts.Requests.Management.Strategy;

namespace MarketViewer.Application.Validators;

public class StrategyUpdateRequestValidator : AbstractValidator<StrategyUpdateRequest>
{
    public StrategyUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Strategy name is required.");

        RuleFor(x => x.EntrySettings)
            .NotNull()
            .WithMessage("Entry settings are required.")
            .SetValidator(new StrategyEntrySettingsValidator());

        RuleFor(x => x.ExitSettings)
            .NotNull()
            .WithMessage("Exit settings are required.")
            .SetValidator(new StrategyExitSettingsValidator());
    }
}

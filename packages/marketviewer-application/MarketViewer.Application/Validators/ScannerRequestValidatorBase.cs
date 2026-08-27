using FluentValidation;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Filters.Registry;

namespace MarketViewer.Application.Validators;

/// <summary>
/// Shared scanner rule set — create and update accept the same shape
/// (<see cref="ScannerUpdateRequest"/> only adds the route-bound id).
/// Names are limited to 100 characters and trimmed before persisting.
/// </summary>
public abstract class ScannerRequestValidatorBase<TRequest> : AbstractValidator<TRequest>
    where TRequest : ScannerCreateRequest
{
    protected ScannerRequestValidatorBase()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Scanner name is required.")
            .MaximumLength(100)
            .WithMessage("Scanner name must be 100 characters or fewer.");

        RuleFor(x => x.EntrySettings)
            .NotNull()
            .WithMessage("Entry settings are required.")
            .SetValidator(new StrategyEntrySettingsValidator(FilterContext.Scan));
    }
}

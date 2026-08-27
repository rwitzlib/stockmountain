using FluentValidation;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Filters.Registry;

namespace MarketViewer.Application.Validators;

public class ScannerUpdateRequestValidator : AbstractValidator<ScannerUpdateRequest>
{
    public ScannerUpdateRequestValidator()
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

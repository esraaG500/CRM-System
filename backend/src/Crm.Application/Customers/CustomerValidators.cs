using FluentValidation;

namespace Crm.Application.Customers;

internal sealed class CustomerUpsertValidator : AbstractValidator<CustomerUpsertDto>
{
    public CustomerUpsertValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PrimaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.PrimaryEmail));
        RuleFor(x => x.PrimaryPhone)
            .Matches(@"^\+[1-9][0-9]{6,14}$").WithMessage("Phone number must be in international format, e.g. +966501234567.")
            .When(x => !string.IsNullOrWhiteSpace(x.PrimaryPhone));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.PrimaryEmail) || !string.IsNullOrWhiteSpace(x.PrimaryPhone))
            .WithName("primaryEmail")
            .WithMessage("Enter an email address or a phone number.");
        RuleFor(x => x.PreferredLanguage).IsInEnum();
        RuleFor(x => x.ErpReference).MaximumLength(64);
        RuleFor(x => x.Address!.Line1).MaximumLength(200).When(x => x.Address is not null);
        RuleFor(x => x.Address!.City).MaximumLength(100).When(x => x.Address is not null);
        RuleFor(x => x.Address!.Country).Length(2).When(x => !string.IsNullOrWhiteSpace(x.Address?.Country));
    }
}

internal sealed class ContactPersonUpsertValidator : AbstractValidator<ContactPersonUpsertDto>
{
    public ContactPersonUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.JobTitle).MaximumLength(100);
    }
}

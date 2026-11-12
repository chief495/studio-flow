using FluentValidation;

public sealed class TariffEditValidator : AbstractValidator<TariffEdit>
{
    public TariffEditValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.HourlyRate).GreaterThan(0);
    }
}

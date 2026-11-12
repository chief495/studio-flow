using FluentValidation;

public sealed class RoomEditValidator : AbstractValidator<RoomEdit>
{
    public RoomEditValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Capacity).InclusiveBetween(1, 100);
    }
}

using FluentValidation;

public class BookingValidator : AbstractValidator<BookingRequest>
{
    public BookingValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.StartsAt)
            .Must(value => value.Kind == DateTimeKind.Utc && value > DateTime.UtcNow)
            .WithMessage("Начало должно быть в будущем и передаваться в UTC.");
        RuleFor(x => x.EndsAt)
            .Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("Окончание должно передаваться в UTC.");
        RuleFor(x => x)
            .Must(x => x.EndsAt > x.StartsAt &&
                       (x.EndsAt - x.StartsAt).TotalMinutes >= 30 &&
                       (x.EndsAt - x.StartsAt).TotalMinutes <= 480)
            .WithMessage("Длительность бронирования — от 30 минут до 8 часов.");
        RuleFor(x => x)
            .Must(x => x.StartsAt.Date == x.EndsAt.Date &&
                       x.StartsAt.TimeOfDay >= TimeSpan.FromHours(9) &&
                       x.EndsAt.TimeOfDay <= TimeSpan.FromHours(22))
            .WithMessage("Бронирование должно быть в пределах 09:00–22:00 UTC одного дня.");
    }
}

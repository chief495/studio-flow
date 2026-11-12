using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            BadHttpRequestException => new HttpValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["request"] = ["Некорректное тело запроса или параметры."],
                }
            )
            {
                Status = 400,
                Title = "Ошибка валидации.",
            },
            ResourceNotFoundException => new ProblemDetails
            {
                Status = 404,
                Title = "Ресурс не найден.",
                Detail = exception.Message,
            },
            BookingConflictException => new ProblemDetails
            {
                Status = 409,
                Title = "Конфликт бронирования.",
                Detail = exception.Message,
            },
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.ExclusionViolation,
                    ConstraintName: "booking_no_overlap"
                }
            } => new ProblemDetails
            {
                Status = 409,
                Title = "Конфликт бронирования.",
                Detail = "Это время уже занято.",
            },
            _ => new ProblemDetails { Status = 500, Title = "Внутренняя ошибка сервера." },
        };

        if (problem.Status == 500)
        {
            logger.LogError(
                exception,
                "Unexpected request failure. RequestId: {RequestId}",
                context.TraceIdentifier
            );
        }

        context.Response.StatusCode = problem.Status!.Value;
        await problemDetailsService.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }
        );
        return true;
    }

    private static HttpValidationProblemDetails ValidationProblem(ValidationException exception)
    {
        var errors = exception
            .Errors.GroupBy(error =>
                string.IsNullOrEmpty(error.PropertyName) ? "request" : error.PropertyName
            )
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray()
            );
        if (errors.Count == 0)
        {
            errors["request"] = [exception.Message];
        }

        return new HttpValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Ошибка валидации.",
        };
    }
}

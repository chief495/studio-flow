using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class BookingConflictMiddleware
{
    public static void UseBookingConflicts(this WebApplication app)
    {
        app.Use(async (context, next) => { try { await next(context); } catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23P01" }) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync("Это время уже занято."); } });

    }
}

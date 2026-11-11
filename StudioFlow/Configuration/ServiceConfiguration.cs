using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class ServiceConfiguration
{
    public static void AddStudioFlow(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) => config.WriteTo.Console());
        builder.Services.AddDbContext<StudioFlowDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
        builder.Services.AddSingleton<IValidator<RegisterRequest>, RegisterValidator>();
        builder.Services.AddSingleton<IValidator<RoomRequest>, RoomValidator>();
        builder.Services.AddSingleton<IValidator<BookingRequest>, BookingValidator>();
        var jwt = builder.Configuration.GetSection("Jwt");
        if (string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]) || Encoding.UTF8.GetByteCount(jwt["Key"] ?? "") < 32)
            throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience and Jwt:Key (at least 32 bytes).");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ClockSkew = TimeSpan.FromSeconds(30), ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)) });
        builder.Services.AddAuthorization(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

    }
}

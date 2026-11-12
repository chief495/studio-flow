using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

public static class SystemHandlers
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/studio", () => new { id = DemoStudio.Id, name = "StudioFlow" });

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }
}

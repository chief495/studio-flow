using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class TokenService
{
public static string TokenFor(User u, IConfiguration j) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(j["Issuer"], j["Audience"], new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Role, u.Role.ToString()), new Claim("tenant_id", u.TenantId.ToString()) }, expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j["Key"]!)), SecurityAlgorithms.HmacSha256)));
}

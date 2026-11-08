using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public class Tariff { public Guid Id { get; set; } = Guid.NewGuid(); public required string Name { get; set; } public decimal HourlyRate { get; set; } public Guid TenantId { get; set; } }

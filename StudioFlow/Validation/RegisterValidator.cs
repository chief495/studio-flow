using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public class RegisterValidator : AbstractValidator<RegisterRequest> { public RegisterValidator() { RuleFor(x => x.Email).EmailAddress(); RuleFor(x => x.Password).MinimumLength(8); RuleFor(x => x.TenantId).NotEmpty(); } }

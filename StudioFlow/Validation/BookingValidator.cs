using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public class BookingValidator : AbstractValidator<BookingRequest> { public BookingValidator() { RuleFor(x => x.RoomId).NotEmpty(); RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).WithMessage("EndsAt must be after StartsAt."); } }

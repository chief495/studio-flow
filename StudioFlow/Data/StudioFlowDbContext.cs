using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public class StudioFlowDbContext(DbContextOptions<StudioFlowDbContext> o) : DbContext(o) { public DbSet<User> Users => Set<User>(); public DbSet<Room> Rooms => Set<Room>(); public DbSet<Tariff> Tariffs => Set<Tariff>(); public DbSet<Booking> Bookings => Set<Booking>(); protected override void OnModelCreating(ModelBuilder m) { m.Entity<User>().HasIndex(x => x.Email).IsUnique(); m.Entity<Booking>().HasIndex(x => new { x.RoomId, x.StartsAt, x.EndsAt }); } }

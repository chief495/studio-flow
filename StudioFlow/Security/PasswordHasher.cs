using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

public static class PasswordHasher
{
    public static string Hash(string value)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt)
            + "."
            + Convert.ToBase64String(
                System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                    value,
                    salt,
                    100000,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    32
                )
            );
    }

    public static bool Verify(string value, string hash)
    {
        var x = hash.Split('.');
        return x.Length == 2
            && Convert.ToBase64String(
                System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                    value,
                    Convert.FromBase64String(x[0]),
                    100000,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    32
                )
            ) == x[1];
    }
}

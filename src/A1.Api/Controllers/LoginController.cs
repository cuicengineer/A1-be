using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Authorization;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class LoginController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtOptions _jwtOptions;

        public LoginController(ApplicationDbContext context, IOptions<JwtOptions> jwtOptions)
        {
            _context = context;
            _jwtOptions = jwtOptions.Value;
        }

        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginResponse
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string? Rank { get; set; }
            public string? Category { get; set; }
            public int? UnitId { get; set; }
            public int? BaseId { get; set; }
            public int? CmdId { get; set; }
            public byte? Status { get; set; }
            public string AccessToken { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
            {
                return BadRequest("Username and password are required.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || user.Password == null || user.PasswordSalt == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            if (user.Status != 1)
            {
                return Unauthorized("Account is inactive or locked.");
            }

            if (user.PasswordAttempts >= 3)
            {
                if (user.Status != 0)
                {
                    user.Status = 0;
                    await _context.SaveChangesAsync();
                }
                return Unauthorized("Account locked due to multiple failed attempts.");
            }

            var iterations = user.PasswordIterations.GetValueOrDefault();
            if (iterations <= 0) iterations = 150_000;

            var isValid = PasswordHasher.Verify(request.Password, user.Password, user.PasswordSalt, iterations);
            if (!isValid)
            {
                user.PasswordAttempts += 1;
                await _context.SaveChangesAsync();
                return Unauthorized("Invalid credentials.");
            }

            user.PasswordAttempts = 0;
            await _context.SaveChangesAsync();

            // Issue tokens
            var accessToken = TokenService.CreateAccessToken(user, _jwtOptions);
            var refreshToken = TokenService.CreateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
            await _context.SaveChangesAsync();

            SetRefreshTokenCookie(refreshToken, user.RefreshTokenExpiresAt);

            var response = new LoginResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Name = user.Name,
                Rank = user.Rank,
                Category = user.Category,
                UnitId = user.UnitId,
                BaseId = user.BaseId,
                CmdId = user.CmdId,
                Status = user.Status,
                AccessToken = accessToken
            };

            return Ok(response);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized("Refresh token missing.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiresAt > DateTime.UtcNow);

            if (user == null || user.Status != 1 || user.PasswordAttempts >= 3)
            {
                return Unauthorized("Invalid refresh token.");
            }

            var newAccessToken = TokenService.CreateAccessToken(user, _jwtOptions);
            var newRefreshToken = TokenService.CreateRefreshToken();
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
            await _context.SaveChangesAsync();

            SetRefreshTokenCookie(newRefreshToken, user.RefreshTokenExpiresAt);

            return Ok(new { accessToken = newAccessToken });
        }

        private void SetRefreshTokenCookie(string refreshToken, DateTime? expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = expires
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}


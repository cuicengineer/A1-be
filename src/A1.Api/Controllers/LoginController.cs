using A1.Api.Models;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
            public int? LevelId { get; set; }
            public byte? Status { get; set; }
            public string AccessToken { get; set; } = string.Empty;
            public string ClientIp { get; set; } = string.Empty;
            public List<UserPermissionDto> Permissions { get; set; } = new();
        }

        public class RefreshResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string ClientIp { get; set; } = string.Empty;
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string? Rank { get; set; }
            public string? Category { get; set; }
            public int? UnitId { get; set; }
            public int? BaseId { get; set; }
            public int? CmdId { get; set; }
            public int? LevelId { get; set; }
            public byte? Status { get; set; }
            public List<UserPermissionDto> Permissions { get; set; } = new();
        }

        public class UserPermissionDto
        {
            public string MenuName { get; set; } = string.Empty;
            public bool CanView { get; set; }
            public bool CanCreate { get; set; }
            public bool CanEdit { get; set; }
            public bool CanDelete { get; set; }
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

            var permissions = await GetUserPermissionsAsync(user.Id);

            var response = BuildLoginResponse(user, accessToken, permissions);
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

            var permissions = await GetUserPermissionsAsync(user.Id);
            return Ok(BuildRefreshResponse(user, newAccessToken, permissions));
        }

        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        public class VerifyPasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
        }

        [HttpPost("verify-password")]
        [Authorize]
        public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.CurrentPassword))
            {
                return BadRequest("Current password is required.");
            }

            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                return Unauthorized();
            }

            if (!VerifyUserPassword(user, request.CurrentPassword))
            {
                return Unauthorized("Current password is incorrect.");
            }

            return Ok(new { verified = true });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.CurrentPassword))
            {
                return BadRequest("Current password is required.");
            }

            if (string.IsNullOrWhiteSpace(request?.NewPassword))
            {
                return BadRequest("New password is required.");
            }

            var policyError = ValidatePasswordPolicy(request.NewPassword);
            if (policyError != null)
            {
                return BadRequest(policyError);
            }

            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                return Unauthorized();
            }

            if (!VerifyUserPassword(user, request.CurrentPassword))
            {
                return Unauthorized("Current password is incorrect.");
            }

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                return BadRequest("New password must be different from the current password.");
            }

            PasswordHasher.Hash(request.NewPassword, out var hash, out var salt, out var iterations);
            user.Password = hash;
            user.PasswordSalt = salt;
            user.PasswordIterations = iterations;
            user.PasswordAttempts = 0;
            user.Action = "UPDATE";
            user.ActionDate = DateTime.UtcNow;
            user.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, null);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Password updated successfully." });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await GetAuthenticatedUserAsync(asNoTracking: true);
            if (user == null || user.Status != 1)
            {
                return Unauthorized();
            }

            var permissions = await GetUserPermissionsAsync(user.Id);
            var response = BuildLoginResponse(user, string.Empty, permissions);
            response.AccessToken = string.Empty;
            return Ok(response);
        }

        private async Task<User?> GetAuthenticatedUserAsync(bool asNoTracking = false)
        {
            var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            var query = _context.Users.AsQueryable();
            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query.FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static bool VerifyUserPassword(User user, string password)
        {
            if (user.Password == null || user.PasswordSalt == null)
            {
                return false;
            }

            var iterations = user.PasswordIterations.GetValueOrDefault();
            if (iterations <= 0) iterations = 150_000;

            return PasswordHasher.Verify(password, user.Password, user.PasswordSalt, iterations);
        }

        private static string? ValidatePasswordPolicy(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6 || password.Length > 12)
            {
                return "Password must be 6-12 characters long and contain at least 1 special character.";
            }

            var hasSpecial = false;
            foreach (var ch in password)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    hasSpecial = true;
                    break;
                }
            }

            if (!hasSpecial)
            {
                return "Password must be 6-12 characters long and contain at least 1 special character.";
            }

            return null;
        }

        private LoginResponse BuildLoginResponse(User user, string accessToken, List<UserPermissionDto> permissions)
        {
            return new LoginResponse
            {
                UserId = user.Id,
                Username = user.Username,
                Name = user.Name,
                Rank = user.Rank,
                Category = user.Category,
                UnitId = user.UnitId,
                BaseId = user.BaseId,
                CmdId = user.CmdId,
                LevelId = user.LevelId,
                Status = user.Status,
                AccessToken = accessToken,
                ClientIp = ActionByHelper.GetClientIp(HttpContext),
                Permissions = permissions
            };
        }

        private RefreshResponse BuildRefreshResponse(User user, string accessToken, List<UserPermissionDto> permissions)
        {
            return new RefreshResponse
            {
                AccessToken = accessToken,
                ClientIp = ActionByHelper.GetClientIp(HttpContext),
                UserId = user.Id,
                Username = user.Username,
                Name = user.Name,
                Rank = user.Rank,
                Category = user.Category,
                UnitId = user.UnitId,
                BaseId = user.BaseId,
                CmdId = user.CmdId,
                LevelId = user.LevelId,
                Status = user.Status,
                Permissions = permissions
            };
        }

        private Task<List<UserPermissionDto>> GetUserPermissionsAsync(int userId)
        {
            return _context.UserPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId && (p.IsDeleted == null || p.IsDeleted == false))
                .Select(p => new UserPermissionDto
                {
                    MenuName = p.MenuName,
                    CanView = p.CanView,
                    CanCreate = p.CanCreate,
                    CanEdit = p.CanEdit,
                    CanDelete = p.CanDelete
                })
                .ToListAsync();
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


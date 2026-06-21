using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupplierCodePrefixesController : ControllerBase
    {
        private readonly IGenericRepository<SupplierCodePrefix> _repository;
        private readonly ApplicationDbContext _context;

        public SupplierCodePrefixesController(
            IGenericRepository<SupplierCodePrefix> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.SupplierCodePrefixes
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.PrefixAlpha)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SupplierCodePrefix entity)
        {
            if (!await CanManageCodePrefixesAsync())
            {
                return Forbid();
            }

            if (entity == null) return BadRequest("Code prefix data is required.");

            var prefixAlpha = NormalizePrefixAlpha(entity.PrefixAlpha);
            if (string.IsNullOrWhiteSpace(prefixAlpha))
            {
                return BadRequest("Code prefix is required.");
            }

            var descriptionError = ValidateDescription(entity.Description);
            if (descriptionError != null)
            {
                return BadRequest(descriptionError);
            }

            var duplicate = await _context.SupplierCodePrefixes
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.PrefixAlpha == prefixAlpha);

            if (duplicate)
            {
                return Conflict("This code prefix already exists.");
            }

            entity.PrefixAlpha = prefixAlpha;
            entity.Description = NormalizeDescription(entity.Description);
            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetAll), entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SupplierCodePrefix entity)
        {
            if (!await CanManageCodePrefixesAsync())
            {
                return Forbid();
            }

            if (entity == null) return BadRequest("Code prefix data is required.");

            var existing = await _context.SupplierCodePrefixes
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Code prefix not found.");
            }

            var prefixAlpha = NormalizePrefixAlpha(entity.PrefixAlpha);
            if (string.IsNullOrWhiteSpace(prefixAlpha))
            {
                return BadRequest("Code prefix is required.");
            }

            var descriptionError = ValidateDescription(entity.Description);
            if (descriptionError != null)
            {
                return BadRequest(descriptionError);
            }

            var duplicate = await _context.SupplierCodePrefixes
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.PrefixAlpha == prefixAlpha);

            if (duplicate)
            {
                return Conflict("This code prefix already exists.");
            }

            existing.PrefixAlpha = prefixAlpha;
            existing.Description = NormalizeDescription(entity.Description);
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await CanDeleteCodePrefixesAsync())
            {
                return Forbid();
            }

            var existing = await _context.SupplierCodePrefixes
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Code prefix not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.SupplierCodePrefixes.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static string NormalizePrefixAlpha(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeDescription(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string? ValidateDescription(string? value)
        {
            var text = NormalizeDescription(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Description is required.";
            }

            var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 20)
            {
                return "Description must not exceed 20 words.";
            }

            return null;
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanManageCodePrefixesAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            var category = User.FindFirstValue("category");
            return DataAccessScopeHelper.IsSupervisorCategory(category)
                && await DataAccessScopeHelper.IsAhqUserAsync(User, _context);
        }

        private Task<bool> CanDeleteCodePrefixesAsync()
        {
            return CanManageCodePrefixesAsync();
        }
    }
}

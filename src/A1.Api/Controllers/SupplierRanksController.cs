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
    public class SupplierRanksController : ControllerBase
    {
        private readonly IGenericRepository<SupplierRank> _repository;
        private readonly ApplicationDbContext _context;

        public SupplierRanksController(
            IGenericRepository<SupplierRank> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.SupplierRanks
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.RankName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SupplierRank entity)
        {
            if (!await CanManageRanksAsync())
            {
                return Forbid();
            }

            if (entity == null) return BadRequest("Rank data is required.");

            var rankName = (entity.RankName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(rankName))
                return BadRequest("Rank name is required.");

            var duplicate = await _context.SupplierRanks
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.RankName == rankName);

            if (duplicate)
                return Conflict("This rank already exists.");

            entity.RankName = rankName;
            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetAll), entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SupplierRank entity)
        {
            if (!await CanManageRanksAsync())
            {
                return Forbid();
            }

            if (entity == null) return BadRequest("Rank data is required.");

            var existing = await _context.SupplierRanks
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Rank not found.");
            }

            var rankName = (entity.RankName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(rankName))
                return BadRequest("Rank name is required.");

            var duplicate = await _context.SupplierRanks
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.RankName == rankName);

            if (duplicate)
                return Conflict("This rank already exists.");

            existing.RankName = rankName;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await CanManageRanksAsync())
            {
                return Forbid();
            }

            var existing = await _context.SupplierRanks
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Rank not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.SupplierRanks.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanManageRanksAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }
}

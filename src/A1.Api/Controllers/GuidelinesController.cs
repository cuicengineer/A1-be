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
    public class GuidelinesController : ControllerBase
    {
        public const int MaxGuidelineRows = 30;

        private readonly IGenericRepository<Guideline> _repository;
        private readonly ApplicationDbContext _context;

        public GuidelinesController(
            IGenericRepository<Guideline> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.Guidelines
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.Guidelines
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Guideline entity)
        {
            if (!await CanManageGuidelinesAsync())
            {
                return Forbid();
            }

            if (entity == null)
            {
                return BadRequest("Guideline data is required.");
            }

            var title = (entity.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required.");
            }

            if (title.Length > 200)
            {
                return BadRequest("Title must be 200 characters or fewer.");
            }

            var description = string.IsNullOrWhiteSpace(entity.Description)
                ? null
                : entity.Description.Trim();
            if (description != null && description.Length > 500)
            {
                return BadRequest("Description must be 500 characters or fewer.");
            }

            var activeCount = await _context.Guidelines.CountAsync();
            if (activeCount >= MaxGuidelineRows)
            {
                return BadRequest($"Maximum of {MaxGuidelineRows} guidelines allowed.");
            }

            entity.Title = title;
            entity.Description = description;
            entity.Status = entity.Status;
            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Guideline entity)
        {
            if (!await CanManageGuidelinesAsync())
            {
                return Forbid();
            }

            if (entity == null)
            {
                return BadRequest("Guideline data is required.");
            }

            var existing = await _context.Guidelines.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return NotFound("Guideline not found.");
            }

            var title = (entity.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required.");
            }

            if (title.Length > 200)
            {
                return BadRequest("Title must be 200 characters or fewer.");
            }

            var description = string.IsNullOrWhiteSpace(entity.Description)
                ? null
                : entity.Description.Trim();
            if (description != null && description.Length > 500)
            {
                return BadRequest("Description must be 500 characters or fewer.");
            }

            existing.Title = title;
            existing.Description = description;
            existing.Status = entity.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await CanManageGuidelinesAsync())
            {
                return Forbid();
            }

            var existing = await _context.Guidelines.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return NotFound("Guideline not found.");
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.Guidelines.Update(existing);
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

        private async Task<bool> CanManageGuidelinesAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }
}

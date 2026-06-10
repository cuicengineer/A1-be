using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractAnnotationsController : ControllerBase
    {
        private readonly IGenericRepository<ContractAnnotation> _repository;
        private readonly ApplicationDbContext _context;

        public ContractAnnotationsController(
            IGenericRepository<ContractAnnotation> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET /api/ContractAnnotations/ByContract/{contractId}
        /// </summary>
        [HttpGet("ByContract/{contractId}")]
        public async Task<IActionResult> GetByContractId(int contractId)
        {
            if (contractId <= 0)
            {
                return BadRequest("Valid contractId is required.");
            }

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = await DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id)
                .ToListAsync();

            if (!scopedContractIds.Contains(contractId))
            {
                return NotFound("Contract not found or access denied.");
            }

            var items = await _context.ContractAnnotations
                .AsNoTracking()
                .Where(x => x.ContractId == contractId && (x.IsDeleted == null || x.IsDeleted == false))
                .OrderByDescending(x => x.ActionDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContractAnnotation item)
        {
            if (item == null) return BadRequest("Data is required.");

            if (!IsLoginSupervisor(User))
            {
                return Forbid();
            }

            if (item.ContractId <= 0)
            {
                return BadRequest("Valid contractId is required.");
            }

            var remarks = (item.Remarks ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(remarks))
            {
                return BadRequest("Remarks are required.");
            }

            if (remarks.Length > 500)
            {
                return BadRequest("Remarks cannot exceed 500 characters.");
            }

            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = await DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id)
                .ToListAsync();

            if (!scopedContractIds.Contains(item.ContractId))
            {
                return NotFound("Contract not found or access denied.");
            }

            item.Remarks = remarks;
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            item.RemarksBy = ResolveRemarksByUsername(User, item.RemarksBy, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id);

            var item = await _context.ContractAnnotations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id
                                          && (x.IsDeleted == null || x.IsDeleted == false)
                                          && scopedContractIds.Contains(x.ContractId));

            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] ContractAnnotationDeleteRequest? request = null)
        {
            if (!IsLoginSuperuser(User))
            {
                return Forbid();
            }

            var existing = await _context.ContractAnnotations
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null) return NotFound();

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                actionBy = existing.ActionBy;
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, actionBy);

            _context.ContractAnnotations.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string ResolveRemarksByUsername(
            ClaimsPrincipal user,
            string? payloadRemarksBy,
            string? resolvedActionBy)
        {
            var fromClaims = ActionByHelper.GetActionBy(user);
            if (!string.IsNullOrWhiteSpace(fromClaims)
                && !string.Equals(fromClaims, "System", StringComparison.OrdinalIgnoreCase))
            {
                return fromClaims.Trim();
            }

            var fromPayload = (payloadRemarksBy ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(fromPayload))
            {
                return fromPayload.Split('|')[0].Trim();
            }

            var fromActionBy = (resolvedActionBy ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(fromActionBy))
            {
                return fromActionBy.Split('|')[0].Trim();
            }

            return string.IsNullOrWhiteSpace(fromClaims) ? "unknown" : fromClaims.Trim();
        }

        private static string GetLoginUsername(ClaimsPrincipal user)
        {
            return ResolveRemarksByUsername(user, null, null);
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoginSupervisor(ClaimsPrincipal user)
        {
            if (IsLoginSuperuser(user)) return true;

            var category = user.FindFirstValue("category") ?? string.Empty;
            return category
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token =>
                    token.Equals("category supervisor", StringComparison.OrdinalIgnoreCase)
                    || token.Contains("supervisor", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class ContractAnnotationDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}

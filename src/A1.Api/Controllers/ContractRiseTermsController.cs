using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractRiseTermsController : ControllerBase
    {
        private readonly IGenericRepository<ContractRiseTerm> _repository;
        private readonly ApplicationDbContext _context;

        public ContractRiseTermsController(IGenericRepository<ContractRiseTerm> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id);

            var items = await _context.ContractRiseTerms
                .AsNoTracking()
                .Where(c => (c.IsDeleted == null || c.IsDeleted == false) && scopedContractIds.Contains(c.ContractId))
                .OrderByDescending(c => c.Id)
                .ToListAsync();
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                items.Select(x => x.Id),
                "ContractRiseTerms", "ContractRiseTerm");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(items, x => x.Id, attachedIds);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id);

            var item = await _context.ContractRiseTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id
                                          && (c.IsDeleted == null || c.IsDeleted == false)
                                          && scopedContractIds.Contains(c.ContractId));

            if (item == null) return NotFound();
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { item.Id },
                "ContractRiseTerms", "ContractRiseTerm");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(item, attachedIds.Contains(item.Id)));
        }

        /// <summary>
        /// GET: Get all contract rise terms by ContractId (only returns records where IsDeleted = 0 or null)
        /// GET /api/ContractRiseTerms/ByContract/{contractId} - Get all rise terms for a contract
        /// </summary>
        [HttpGet("ByContract/{contractId}")]
        public async Task<IActionResult> GetByContractId(int contractId)
        {
            if (contractId <= 0)
            {
                return BadRequest("Valid contractId is required.");
            }

            // Check if user has access to this contract
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var scopedContractIds = await DataAccessScopeHelper.ApplyScope(
                    _context.Contracts.AsNoTracking().Where(c => c.IsDeleted == null || c.IsDeleted == false),
                    scope)
                .Select(c => c.Id)
                .ToListAsync();

            // Verify the contract exists and user has access
            if (!scopedContractIds.Contains(contractId))
            {
                return NotFound("Contract not found or access denied.");
            }

            // Get all non-deleted rise terms for this contract, ordered by sequence
            var items = await _context.ContractRiseTerms
                .AsNoTracking()
                .Where(c => c.ContractId == contractId && (c.IsDeleted == null || c.IsDeleted == false))
                .OrderBy(c => c.SequenceNo)
                .ToListAsync();

            // Add attachment flags
            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                items.Select(x => x.Id),
                "ContractRiseTerms", "ContractRiseTerm");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(items, x => x.Id, attachedIds);

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContractRiseTerm item)
        {
            if (item == null) return BadRequest("Data is required.");

            item.IsDeleted = false;
            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ContractRiseTerm item)
        {
            if (item == null) return BadRequest("Data is required.");

            if (item.Id == 0)
                item.Id = id;
            else if (item.Id != id)
                return BadRequest("ID mismatch.");

            var existing = await _context.ContractRiseTerms
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));
            if (existing == null) return NotFound();

            existing.ContractId = item.ContractId;
            existing.MonthsInterval = item.MonthsInterval;
            existing.RisePercent = item.RisePercent;
            existing.SequenceNo = item.SequenceNo;
            existing.Status = item.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] ContractRiseTermDeleteRequest? request = null)
        {
            var existing = await _context.ContractRiseTerms
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));
            if (existing == null) return NotFound();

            var actionBy = request?.ActionBy;
            if (string.IsNullOrWhiteSpace(actionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.ContractRiseTerms
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => c.ActionBy)
                    .FirstOrDefaultAsync();
                actionBy = existingActionBy;
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, actionBy);

            _context.ContractRiseTerms.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class ContractRiseTermDeleteRequest
    {
        public string? ActionBy { get; set; }
    }
}


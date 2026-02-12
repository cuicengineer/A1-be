using A1.Api.Models;
using A1.Api.Repositories;
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
            var items = await _context.ContractRiseTerms
                .AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false)
                .OrderByDescending(c => c.Id)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.ContractRiseTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (item == null) return NotFound();
            return Ok(item);
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

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.ContractRiseTerms
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));
            if (existing == null) return NotFound();

            await _repository.DeleteAsync(existing);
            return NoContent();
        }
    }
}


using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    /// <summary>
    /// BankLists Controller for managing bank list records
    /// 
    /// GET /api/BankLists - Get all bank lists (only non-deleted)
    /// GET /api/BankLists/{id} - Get bank list by ID
    /// POST /api/BankLists - Create a new bank list
    /// PUT /api/BankLists/{id} - Update a bank list
    /// DELETE /api/BankLists/{id} - Soft delete a bank list (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BankListsController : ControllerBase
    {
        private readonly IGenericRepository<BankList> _repository;
        private readonly ApplicationDbContext _context;

        public BankListsController(IGenericRepository<BankList> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET: Get all bank lists (only returns records where IsDeleted = 0 or null)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var bankLists = await _context.BankLists
                .AsNoTracking()
                .Where(b => b.IsDeleted == null || b.IsDeleted == false)
                .OrderByDescending(b => b.Id)
                .ToListAsync();
            return Ok(bankLists);
        }

        /// <summary>
        /// GET: Get bank list by ID (only returns if IsDeleted = 0 or null)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bankList = await _context.BankLists
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id && (b.IsDeleted == null || b.IsDeleted == false));

            if (bankList == null)
            {
                return NotFound();
            }

            return Ok(bankList);
        }

        /// <summary>
        /// POST: Create a new bank list (sets IsDeleted = false by default)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankList bankList)
        {
            if (bankList == null)
            {
                return BadRequest("Bank list data is required.");
            }

            // Set IsDeleted = false by default
            bankList.IsDeleted = false;
            bankList.ActionDate = DateTime.UtcNow;
            bankList.Action = "CREATE";
            // ActionBy comes from payload

            await _repository.AddAsync(bankList);
            return CreatedAtAction(nameof(GetById), new { id = bankList.Id }, bankList);
        }

        /// <summary>
        /// PUT: Update an existing bank list
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BankList bankList)
        {
            if (bankList == null)
            {
                return BadRequest("Bank list data is required.");
            }

            // If bankList.Id is not set (0), use the route parameter id
            if (bankList.Id == 0)
            {
                bankList.Id = id;
            }
            else if (id != bankList.Id)
            {
                return BadRequest("ID mismatch.");
            }

            // Check if bank list exists and is not deleted
            var existingBankList = await _context.BankLists
                .FirstOrDefaultAsync(b => b.Id == id && (b.IsDeleted == null || b.IsDeleted == false));

            if (existingBankList == null)
            {
                return NotFound("Bank list not found.");
            }

            // Update properties
            existingBankList.Name = bankList.Name;
            existingBankList.Code = bankList.Code;
            existingBankList.Address = bankList.Address;
            existingBankList.Status = bankList.Status;
            existingBankList.ActionDate = DateTime.UtcNow;
            existingBankList.Action = "UPDATE";
            existingBankList.ActionBy = bankList.ActionBy;

            await _repository.UpdateAsync(existingBankList);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a bank list (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var bankList = await _context.BankLists
                .FirstOrDefaultAsync(b => b.Id == id && (b.IsDeleted == null || b.IsDeleted == false));

            if (bankList == null)
            {
                return NotFound("Bank list not found.");
            }

            // Soft delete - set IsDeleted = true
            bankList.IsDeleted = true;
            bankList.Action = "DELETE";
            bankList.ActionDate = DateTime.UtcNow;
            // ActionBy should come from payload if provided, otherwise keep existing value
            if (string.IsNullOrWhiteSpace(bankList.ActionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.BankLists
                    .AsNoTracking()
                    .Where(b => b.Id == id)
                    .Select(b => b.ActionBy)
                    .FirstOrDefaultAsync();
                bankList.ActionBy = existingActionBy;
            }

            _context.BankLists.Update(bankList);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


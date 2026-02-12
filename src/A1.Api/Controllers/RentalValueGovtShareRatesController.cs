using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalValueGovtShareRatesController : ControllerBase
    {
        private readonly IGenericRepository<RentalValueGovtShareRate> _repository;
        private readonly ApplicationDbContext _context;

        public RentalValueGovtShareRatesController(IGenericRepository<RentalValueGovtShareRate> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }


        /// <summary>
        /// GET: Get all rental value govt share rates (only returns records where IsDeleted = false/null and Status = true)
        /// Supports pagination with pageNumber and pageSize query parameters
        /// Supports filtering by type query parameter
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, [FromQuery] int? type = null)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200; // safety cap for high-load scenarios

            var baseQuery = _context.RentalValueGovtShareRates
                .AsNoTracking()
                .Where(r => (r.IsDeleted == null || r.IsDeleted == false) && (r.Status == true));

            // Filter by type if provided
            if (type.HasValue)
            {
                baseQuery = baseQuery.Where(r => r.Type == type.Value);
            }

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var items = await baseQuery
                .OrderByDescending(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.RentalValueGovtShareRates
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RentalValueGovtShareRate item)
        {
            if (item == null) return BadRequest("Data is required.");

            item.IsDeleted = false;
            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalValueGovtShareRate item)
        {
            if (item == null) return BadRequest("Data is required.");

            if (item.Id == 0)
                item.Id = id;
            else if (item.Id != id)
                return BadRequest("ID mismatch.");

            var existing = await _context.RentalValueGovtShareRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            if (existing == null) return NotFound();

            existing.ClassId = item.ClassId;
            existing.ApplicableDate = item.ApplicableDate;
            existing.Rate = item.Rate;
            existing.Type = item.Type;
            existing.CmdId = item.CmdId;
            existing.BaseId = item.BaseId;
            existing.Description = item.Description;
            existing.Attachments = item.Attachments;
            existing.Status = item.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.RentalValueGovtShareRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));
            if (existing == null) return NotFound();

            await _repository.DeleteAsync(existing);
            return NoContent();
        }
    }
}


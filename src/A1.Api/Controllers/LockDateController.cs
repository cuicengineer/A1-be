using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LockDateController : ControllerBase
    {
        private readonly IGenericRepository<LockDate> _repository;
        private readonly ApplicationDbContext _context;

        public LockDateController(IGenericRepository<LockDate> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.LockDates
                .AsNoTracking() 
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.LockDates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] LockDate model)
        {
            if (model == null)
            {
                return BadRequest("Lock date data is required.");
            }

            if (model.Id == 0)
            {
                model.Id = id;
            }
            else if (id != model.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.LockDates
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Lock date not found.");
            }

            existing.LockingDate = model.LockingDate;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, model.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }
    }
}

using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    /// <summary>
    /// Lookup list for user appointment titles (Add New User dropdown).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UserAppointController : ControllerBase
    {
        private readonly IGenericRepository<UserAppoint> _repository;
        private readonly ApplicationDbContext _context;

        public UserAppointController(IGenericRepository<UserAppoint> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.UserAppoints
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.Name)
                .ToListAsync();
            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.UserAppoints
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserAppoint entity)
        {
            if (entity == null)
            {
                return BadRequest("Appointment data is required.");
            }

            var name = (entity.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Name is required.");
            }

            var duplicate = await _context.UserAppoints
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.Name != null &&
                    x.Name.Trim().ToLower() == name.ToLower());

            if (duplicate)
            {
                return Conflict("This appointment already exists.");
            }

            entity.Id = 0;
            entity.Name = name;
            entity.IsDeleted = false;
            entity.Status = entity.Status ?? 1;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _context.UserAppoints
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null)
            {
                return NotFound("Appointment not found.");
            }

            row.IsDeleted = true;
            row.Action = "DELETE";
            row.ActionDate = DateTime.UtcNow;
            row.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, row.ActionBy);

            await _repository.UpdateAsync(row);
            return NoContent();
        }
    }
}

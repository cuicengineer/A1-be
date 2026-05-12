using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    /// <summary>
    /// AccRacBase lookup and maintenance.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AccRacBaseController : ControllerBase
    {
        private readonly IGenericRepository<AccRacBase> _repository;
        private readonly ApplicationDbContext _context;

        public AccRacBaseController(IGenericRepository<AccRacBase> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET: Optional query params type, parentId. Open to callers without authentication.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? type = null, [FromQuery] int? parentId = null)
        {
            var query = _context.AccRacBases.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(type))
            {
                var t = type.Trim();
                query = query.Where(a => a.Type == t);
            }

            if (parentId.HasValue)
            {
                query = query.Where(a => a.ParentId == parentId.Value);
            }

            var results = await query.OrderByDescending(a => a.Id).ToListAsync();
            return Ok(results);
        }

        /// <summary>
        /// GET by Id (non-deleted). Open to callers without authentication.
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.AccRacBases
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id && (a.IsDeleted == null || a.IsDeleted == false));

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        /// <summary>
        /// POST: Create a new AccRacBase row (requires authentication).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AccRacBase entity)
        {
            if (entity == null)
            {
                return BadRequest("AccRacBase data is required.");
            }

            var type = entity.Type?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(type))
            {
                return BadRequest("Type is required.");
            }

            entity.Id = 0;
            entity.Type = type;
            entity.IsDeleted = false;
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        /// <summary>
        /// PUT: Update an existing AccRacBase (requires authentication).
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AccRacBase entity)
        {
            if (entity == null)
            {
                return BadRequest("AccRacBase data is required.");
            }

            if (entity.Id == 0)
            {
                entity.Id = id;
            }
            else if (id != entity.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var type = entity.Type?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(type))
            {
                return BadRequest("Type is required.");
            }

            var existing = await _context.AccRacBases
                .FirstOrDefaultAsync(a => a.Id == id && (a.IsDeleted == null || a.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("AccRacBase not found.");
            }

            existing.Name = entity.Name;
            existing.Type = type;
            existing.ParentId = entity.ParentId;
            existing.Status = entity.Status;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }
    }
}

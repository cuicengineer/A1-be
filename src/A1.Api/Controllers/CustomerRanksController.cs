using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerRanksController : ControllerBase
    {
        private readonly IGenericRepository<CustomerRank> _repository;
        private readonly ApplicationDbContext _context;

        public CustomerRanksController(
            IGenericRepository<CustomerRank> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.CustomerRanks
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.RankName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CustomerRank entity)
        {
            if (entity == null) return BadRequest("Rank data is required.");

            var rankName = (entity.RankName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(rankName))
                return BadRequest("Rank name is required.");

            var duplicate = await _context.CustomerRanks
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
    }
}

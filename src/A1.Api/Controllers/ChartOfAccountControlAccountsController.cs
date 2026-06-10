using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChartOfAccountControlAccountsController : ControllerBase
    {
        private readonly IGenericRepository<ChartOfAccountControlAccount> _repository;
        private readonly ApplicationDbContext _context;

        public ChartOfAccountControlAccountsController(
            IGenericRepository<ChartOfAccountControlAccount> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.ChartOfAccountControlAccounts
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderBy(x => x.ControlAccountName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChartOfAccountControlAccount entity)
        {
            if (entity == null) return BadRequest("Control account data is required.");

            if (string.IsNullOrWhiteSpace(entity.ControlAccountName))
                return BadRequest("Control account name is required.");

            var controlAccountName = entity.ControlAccountName.Trim();

            var duplicate = await _context.ChartOfAccountControlAccounts
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.IsDeleted == null || x.IsDeleted == false) &&
                    x.ControlAccountName == controlAccountName);

            if (duplicate)
                return Conflict("This control account already exists.");

            entity.ControlAccountName = controlAccountName;
            entity.IsDeleted = false;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            entity.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetAll), entity);
        }
    }
}

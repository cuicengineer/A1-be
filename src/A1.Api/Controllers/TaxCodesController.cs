using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaxCodesController : ControllerBase
    {
        private readonly IGenericRepository<TaxCode> _repository;
        private readonly ApplicationDbContext _context;

        public TaxCodesController(IGenericRepository<TaxCode> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);
            var baseQuery = _context.TaxCodes
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var rows = await PaginationHelper.ApplyPaging(
                    baseQuery.OrderBy(x => x.Code),
                    pageNumber,
                    pageSize)
                .ToListAsync();
            return Ok(rows);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaxCode item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Code))
                return BadRequest("Tax code is required.");

            item.Code = item.Code.Trim();
            item.Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim();
            item.IsDeleted = false;
            item.ActionDate = DateTime.UtcNow;
            item.Action = "CREATE";
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetAll), item);
        }
    }
}

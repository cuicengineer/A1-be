using A1.Api.Models;

using A1.Api.Repositories;

using A1.Api.Utilities;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;



namespace A1.Api.Controllers

{

    [Route("api/[controller]")]

    [ApiController]

    public class ProductUomsController : ControllerBase

    {

        private readonly IGenericRepository<ProductUom> _repository;

        private readonly ApplicationDbContext _context;



        public ProductUomsController(IGenericRepository<ProductUom> repository, ApplicationDbContext context)

        {

            _repository = repository;

            _context = context;

        }



        [HttpGet]

        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)

        {

            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.ProductUoms

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



        [HttpGet("{id}")]

        public async Task<IActionResult> GetById(int id)

        {

            var row = await _context.ProductUoms

                .AsNoTracking()

                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));



            if (row == null) return NotFound();

            return Ok(row);

        }



        [HttpPost]

        public async Task<IActionResult> Create([FromBody] ProductUom item)

        {

            if (item == null) return BadRequest("UoM data is required.");



            var validationError = await ValidateAsync(item);

            if (validationError != null) return BadRequest(validationError);



            Normalize(item);

            item.IsDeleted = false;

            item.ActionDate = DateTime.UtcNow;

            item.Action = "CREATE";

            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);

        }



        [HttpPut("{id}")]

        public async Task<IActionResult> Update(int id, [FromBody] ProductUom item)

        {

            if (item == null) return BadRequest("UoM data is required.");



            if (item.Id == 0) item.Id = id;

            else if (id != item.Id) return BadRequest("ID mismatch.");



            var validationError = await ValidateAsync(item, id);

            if (validationError != null) return BadRequest(validationError);



            var existing = await _context.ProductUoms

                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("UoM not found.");



            Normalize(item);

            existing.Code = item.Code;

            existing.Name = item.Name;

            existing.Status = item.Status;

            existing.ActionDate = DateTime.UtcNow;

            existing.Action = "UPDATE";

            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);

            return NoContent();

        }



        [HttpDelete("{id}")]

        public async Task<IActionResult> Delete(int id)

        {

            var existing = await _context.ProductUoms

                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null) return NotFound("UoM not found.");



            existing.IsDeleted = true;

            existing.Action = "DELETE";

            existing.ActionDate = DateTime.UtcNow;

            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);

            _context.ProductUoms.Update(existing);

            await _context.SaveChangesAsync();

            return NoContent();

        }



        private static void Normalize(ProductUom item)

        {

            item.Code = (item.Code ?? string.Empty).Trim().ToUpperInvariant();

            item.Name = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name.Trim();

            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim();

        }



        private async Task<string?> ValidateAsync(ProductUom item, int? excludeId = null)

        {

            if (string.IsNullOrWhiteSpace(item.Code))

                return "UoM code is required.";



            var code = item.Code.Trim().ToUpperInvariant();

            var duplicate = await _context.ProductUoms

                .AsNoTracking()

                .AnyAsync(x =>

                    x.Code == code &&

                    (x.IsDeleted == null || x.IsDeleted == false) &&

                    (!excludeId.HasValue || x.Id != excludeId.Value));



            if (duplicate) return "A UoM with this code already exists.";



            return null;

        }

    }

}



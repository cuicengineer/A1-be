using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductGoodsController : ControllerBase
    {
        private readonly IGenericRepository<ProductGood> _repository;
        private readonly ApplicationDbContext _context;

        public ProductGoodsController(
            IGenericRepository<ProductGood> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 0)
        {
            pageNumber = PaginationHelper.NormalizePageNumber(pageNumber);

            var baseQuery = _context.ProductGoods
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = PaginationHelper.FormatPageSizeHeader(pageSize, totalCount);

            var rows = await PaginationHelper.ApplyPaging(
                    baseQuery.OrderByDescending(x => x.Id),
                    pageNumber,
                    pageSize)
                .ToListAsync();

            foreach (var row in rows)
            {
                if (row.ControlAccountCoaId.HasValue && row.ControlAccountCoaId.Value > 0)
                {
                    var coa = await _context.ChartOfAccounts.AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.Id == row.ControlAccountCoaId.Value &&
                            (x.IsDeleted == null || x.IsDeleted == false));
                    row.ControlAccountDisplay = ProductAccountDisplayHelper.FormatCoaLabel(coa);
                }

                row.SaleAccountDisplay = await ProductAccountDisplayHelper.ResolveAccountDisplayAsync(
                    _context, null, row.SaleAccountIncomeStatementId);
                row.PurchaseAccountDisplay = await ProductAccountDisplayHelper.ResolveAccountDisplayAsync(
                    _context, null, row.PurchaseAccountIncomeStatementId);
                row.TaxCodeDisplay = await ProductAccountDisplayHelper.ResolveTaxCodeDisplayAsync(
                    _context, row.TaxCodeId);
            }

            return Ok(rows);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.ProductGoods
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (row == null) return NotFound();
            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductGood item)
        {
            if (item == null) return BadRequest("Goods product data is required.");

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
        public async Task<IActionResult> Update(int id, [FromBody] ProductGood item)
        {
            if (item == null) return BadRequest("Goods product data is required.");
            if (item.Id == 0) item.Id = id;
            else if (id != item.Id) return BadRequest("ID mismatch.");

            var validationError = await ValidateAsync(item, id);
            if (validationError != null) return BadRequest(validationError);

            var existing = await _context.ProductGoods
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null) return NotFound("Goods product not found.");

            Normalize(item);
            CopyFields(existing, item);
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);
            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.ProductGoods
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
            if (existing == null) return NotFound("Goods product not found.");

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionDate = DateTime.UtcNow;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, existing.ActionBy);
            _context.ProductGoods.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void Normalize(ProductGood item)
        {
            item.ItemCode = (item.ItemCode ?? string.Empty).Trim().ToUpperInvariant();
            item.ItemName = (item.ItemName ?? string.Empty).Trim();
            item.Uom = string.IsNullOrWhiteSpace(item.Uom) ? null : item.Uom.Trim();
            item.DefaultParticulars = string.IsNullOrWhiteSpace(item.DefaultParticulars)
                ? null
                : item.DefaultParticulars.Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim();
        }

        private static void CopyFields(ProductGood existing, ProductGood item)
        {
            existing.ItemCode = item.ItemCode;
            existing.ItemName = item.ItemName;
            existing.Uom = item.Uom;
            existing.ControlAccountCoaId = item.ControlAccountCoaId;
            existing.SaleAccountIncomeStatementId = item.SaleAccountIncomeStatementId;
            existing.PurchaseAccountIncomeStatementId = item.PurchaseAccountIncomeStatementId;
            existing.DefaultParticulars = item.DefaultParticulars;
            existing.DefaultUnitPriceSales = item.DefaultUnitPriceSales;
            existing.DefaultUnitPricePurchase = item.DefaultUnitPricePurchase;
            existing.TaxCodeId = item.TaxCodeId;
            existing.Status = item.Status;
        }

        private async Task<string?> ValidateAsync(ProductGood item, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(item.ItemCode)) return "Item Code is required.";
            if (string.IsNullOrWhiteSpace(item.ItemName)) return "Item Name is required.";
            if (!item.ControlAccountCoaId.HasValue || item.ControlAccountCoaId.Value <= 0)
                return "Control Account is required.";

            var code = item.ItemCode.Trim().ToUpperInvariant();
            var duplicate = await _context.ProductGoods.AsNoTracking().AnyAsync(x =>
                x.ItemCode.ToUpper() == code &&
                (x.IsDeleted == null || x.IsDeleted == false) &&
                (!excludeId.HasValue || x.Id != excludeId.Value));
            if (duplicate) return "Item Code must be unique.";

            return null;
        }
    }
}

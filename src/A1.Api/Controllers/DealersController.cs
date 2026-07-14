using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/Parties")]
    [ApiController]
    public class DealersController : ControllerBase
    {
        private readonly IGenericRepository<Dealer> _repository;
        private readonly ApplicationDbContext _context;

        public DealersController(IGenericRepository<Dealer> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var dealers = await _context.Dealers
                .AsNoTracking()
                .Where(d => d.IsDeleted == null || d.IsDeleted == false)
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            return Ok(dealers);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var dealers = await _context.Dealers
                .AsNoTracking()
                .Where(d =>
                    (d.IsDeleted == null || d.IsDeleted == false) &&
                    d.Status)
                .OrderBy(d => d.Name)
                .ThenBy(d => d.Id)
                .ToListAsync();

            return Ok(dealers);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dealer = await _context.Dealers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id && (d.IsDeleted == null || d.IsDeleted == false));

            if (dealer == null)
            {
                return NotFound();
            }

            return Ok(dealer);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Dealer dealer)
        {
            if (dealer == null)
            {
                return BadRequest("Dealer data is required.");
            }

            var validationError = await ValidateDealerAsync(dealer);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            NormalizeDealer(dealer);
            dealer.IsDeleted = false;
            dealer.ActionDate = DateTime.UtcNow;
            dealer.Action = "CREATE";
            dealer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, dealer.ActionBy);

            await _repository.AddAsync(dealer);
            return CreatedAtAction(nameof(GetById), new { id = dealer.Id }, dealer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Dealer dealer)
        {
            if (dealer == null)
            {
                return BadRequest("Dealer data is required.");
            }

            if (dealer.Id == 0)
            {
                dealer.Id = id;
            }
            else if (dealer.Id != id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = await ValidateDealerAsync(dealer, id);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existing = await _context.Dealers
                .FirstOrDefaultAsync(d => d.Id == id && (d.IsDeleted == null || d.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Dealer not found.");
            }

            NormalizeDealer(dealer);

            if (!string.Equals(existing.Code, dealer.Code, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Code cannot be changed.");
            }

            existing.Prefix = dealer.Prefix;
            existing.Rank = dealer.Rank;
            existing.Name = dealer.Name;
            existing.Address = dealer.Address;
            existing.Province = dealer.Province;
            existing.City = dealer.City;
            existing.NtnCnic = dealer.NtnCnic;
            existing.GSTNo = dealer.GSTNo;
            existing.TelNo = dealer.TelNo;
            existing.MobileNo = dealer.MobileNo;
            existing.Representative = dealer.Representative;
            existing.BankListsId = dealer.BankListsId;
            existing.IBAN = dealer.IBAN;
            existing.Status = dealer.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, dealer.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpPatch("bulkStatus")]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkPartyStatusRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest("At least one dealer ID is required.");
            }

            var dealers = await _context.Dealers
                .Where(d =>
                    request.Ids.Contains(d.Id) &&
                    (d.IsDeleted == null || d.IsDeleted == false))
                .ToListAsync();

            if (dealers.Count == 0)
            {
                return NotFound("No matching dealers found.");
            }

            foreach (var dealer in dealers)
            {
                dealer.Status = request.Status;
                dealer.ActionDate = DateTime.UtcNow;
                dealer.Action = "UPDATE";
                dealer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, dealer.ActionBy);
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = dealers.Count });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var dealer = await _context.Dealers
                .FirstOrDefaultAsync(d => d.Id == id && (d.IsDeleted == null || d.IsDeleted == false));

            if (dealer == null)
            {
                return NotFound("Dealer not found.");
            }

            dealer.IsDeleted = true;
            dealer.Action = "DELETE";
            dealer.ActionDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(dealer.ActionBy))
            {
                var existingActionBy = await _context.Dealers
                    .AsNoTracking()
                    .Where(d => d.Id == id)
                    .Select(d => d.ActionBy)
                    .FirstOrDefaultAsync();
                dealer.ActionBy = existingActionBy;
            }
            dealer.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, dealer.ActionBy);

            _context.Dealers.Update(dealer);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void NormalizeDealer(Dealer dealer)
        {
            dealer.Code = string.IsNullOrWhiteSpace(dealer.Code) ? null : dealer.Code.Trim().ToUpperInvariant();
            dealer.Prefix = string.IsNullOrWhiteSpace(dealer.Prefix) ? null : dealer.Prefix.Trim();
            dealer.Rank = string.IsNullOrWhiteSpace(dealer.Rank) ? null : dealer.Rank.Trim();
            dealer.Name = (dealer.Name ?? string.Empty).Trim();
            dealer.Address = string.IsNullOrWhiteSpace(dealer.Address) ? null : dealer.Address.Trim();
            dealer.Province = string.IsNullOrWhiteSpace(dealer.Province) ? null : dealer.Province.Trim();
            dealer.City = string.IsNullOrWhiteSpace(dealer.City) ? null : dealer.City.Trim();
            dealer.NtnCnic = string.IsNullOrWhiteSpace(dealer.NtnCnic) ? null : dealer.NtnCnic.Trim();
            dealer.GSTNo = string.IsNullOrWhiteSpace(dealer.GSTNo) ? null : dealer.GSTNo.Trim();
            dealer.TelNo = string.IsNullOrWhiteSpace(dealer.TelNo) ? null : dealer.TelNo.Trim();
            dealer.MobileNo = string.IsNullOrWhiteSpace(dealer.MobileNo) ? null : dealer.MobileNo.Trim();
            dealer.Representative = string.IsNullOrWhiteSpace(dealer.Representative) ? null : dealer.Representative.Trim();
            dealer.IBAN = string.IsNullOrWhiteSpace(dealer.IBAN) ? null : dealer.IBAN.Trim();
        }

        private async Task<string?> ValidateDealerAsync(Dealer dealer, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(dealer.Code))
            {
                return "Code is required.";
            }

            var normalizedCode = dealer.Code.Trim().ToUpperInvariant();
            var duplicateCode = await _context.Dealers
                .AsNoTracking()
                .AnyAsync(d =>
                    (d.IsDeleted == null || d.IsDeleted == false) &&
                    d.Status &&
                    d.Code != null &&
                    d.Code.ToUpper() == normalizedCode &&
                    (!excludeId.HasValue || d.Id != excludeId.Value));
            if (duplicateCode)
            {
                return "An active dealer with this Code already exists.";
            }

            if (string.IsNullOrWhiteSpace(dealer.Name))
            {
                return "Name is required.";
            }

            if (string.IsNullOrWhiteSpace(dealer.Address))
            {
                return "Address is required.";
            }

            if (string.IsNullOrWhiteSpace(dealer.Province))
            {
                return "Province is required.";
            }

            if (string.IsNullOrWhiteSpace(dealer.City))
            {
                return "City is required.";
            }

            if (string.IsNullOrWhiteSpace(dealer.NtnCnic))
            {
                return "NTN / CNIC is required.";
            }

            var normalizedName = dealer.Name.Trim().ToUpperInvariant();
            var normalizedCnic = dealer.NtnCnic.Trim().ToUpperInvariant();
            var normalizedIban = string.IsNullOrWhiteSpace(dealer.IBAN)
                ? null
                : dealer.IBAN.Trim().ToUpperInvariant();

            var query = _context.Dealers
                .AsNoTracking()
                .Where(d =>
                    (d.IsDeleted == null || d.IsDeleted == false) &&
                    d.Status &&
                    (!excludeId.HasValue || d.Id != excludeId.Value));

            var duplicateName = await query.AnyAsync(d => d.Name.ToUpper() == normalizedName);
            if (duplicateName)
            {
                return "An active dealer with this Name already exists.";
            }

            var duplicateCnic = await query.AnyAsync(d =>
                d.NtnCnic != null &&
                d.NtnCnic.ToUpper() == normalizedCnic);
            if (duplicateCnic)
            {
                return "An active dealer with this NTN / CNIC already exists.";
            }

            if (!string.IsNullOrWhiteSpace(normalizedIban))
            {
                var duplicateIban = await query.AnyAsync(d =>
                    d.IBAN != null &&
                    d.IBAN.ToUpper() == normalizedIban);
                if (duplicateIban)
                {
                    return "An active dealer with this IBAN already exists.";
                }
            }

            return null;
        }
    }
}

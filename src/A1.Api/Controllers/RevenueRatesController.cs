using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RevenueRatesController : ControllerBase
    {
        private readonly IGenericRepository<RevenueRate> _repository;
        private readonly ApplicationDbContext _context;

        public RevenueRatesController(IGenericRepository<RevenueRate> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var baseQuery = _context.RevenueRates
                .AsNoTracking()
                .Where(r => r.IsDeleted == null || r.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var payload = await (from r in baseQuery
                                 join p in _context.RentalProperties.Where(p => p.IsDeleted == null || p.IsDeleted == false)
                                     on r.PropertyId equals p.Id into propertyGroup
                                 from p in propertyGroup.DefaultIfEmpty()
                                 join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                     on p.CmdId equals cmd.Id into cmdGroup
                                 from cmd in cmdGroup.DefaultIfEmpty()
                                 join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                     on p.BaseId equals b.Id into baseGroup
                                 from b in baseGroup.DefaultIfEmpty()
                                 join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                     on p.ClassId equals cls.Id into classGroup
                                 from cls in classGroup.DefaultIfEmpty()
                                 orderby r.Id descending
                                 select new RevenueRateDto
                                 {
                                     Id = r.Id,
                                     PropertyId = r.PropertyId,
                                     CmdId = p != null ? p.CmdId : (int?)null,
                                     CmdName = cmd != null ? cmd.Name : string.Empty,
                                     BaseId = p != null ? p.BaseId : (int?)null,
                                     BaseName = b != null ? b.Name : string.Empty,
                                     ClassId = p != null ? p.ClassId : (int?)null,
                                     ClassName = cls != null ? cls.Name : string.Empty,
                                     PropertyIdentifier = p != null ? p.PId : null,
                                     UoM = p != null ? p.UoM : null,
                                     Area = p != null ? p.Area : null,
                                     Location = p != null ? p.Location : null,
                                     Remarks = p != null ? p.Remarks : null,
                                     ApplicableDate = r.ApplicableDate,
                                     Rate = r.Rate,
                                     Attachments = r.Attachments,
                                     Status = r.Status,
                                     ActionDate = r.ActionDate,
                                     ActionBy = r.ActionBy,
                                     Action = r.Action,
                                     IsDeleted = r.IsDeleted
                                 })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(payload);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var revenueRate = await (from r in _context.RevenueRates
                                     .AsNoTracking()
                                     .Where(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false))
                                     join p in _context.RentalProperties.Where(p => p.IsDeleted == null || p.IsDeleted == false)
                                         on r.PropertyId equals p.Id into propertyGroup
                                     from p in propertyGroup.DefaultIfEmpty()
                                     join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                         on p.CmdId equals cmd.Id into cmdGroup
                                     from cmd in cmdGroup.DefaultIfEmpty()
                                     join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                         on p.BaseId equals b.Id into baseGroup
                                     from b in baseGroup.DefaultIfEmpty()
                                     join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                         on p.ClassId equals cls.Id into classGroup
                                     from cls in classGroup.DefaultIfEmpty()
                                    select new RevenueRateDto
                                    {
                                        Id = r.Id,
                                        PropertyId = r.PropertyId,
                                        CmdId = p != null ? p.CmdId : (int?)null,
                                        CmdName = cmd != null ? cmd.Name : string.Empty,
                                        BaseId = p != null ? p.BaseId : (int?)null,
                                        BaseName = b != null ? b.Name : string.Empty,
                                        ClassId = p != null ? p.ClassId : (int?)null,
                                        ClassName = cls != null ? cls.Name : string.Empty,
                                        PropertyIdentifier = p != null ? p.PId : null,
                                        UoM = p != null ? p.UoM : null,
                                        Area = p != null ? p.Area : null,
                                        Location = p != null ? p.Location : null,
                                        Remarks = p != null ? p.Remarks : null,
                                        ApplicableDate = r.ApplicableDate,
                                        Rate = r.Rate,
                                        Attachments = r.Attachments,
                                        Status = r.Status,
                                        ActionDate = r.ActionDate,
                                        ActionBy = r.ActionBy,
                                        Action = r.Action,
                                        IsDeleted = r.IsDeleted
                                    })
                .FirstOrDefaultAsync();

            if (revenueRate == null)
            {
                return NotFound();
            }

            return Ok(revenueRate);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RevenueRate revenueRate)
        {
            if (revenueRate == null)
            {
                return BadRequest("Revenue rate data is required.");
            }

            revenueRate.IsDeleted = false;
            await _repository.AddAsync(revenueRate);
            return CreatedAtAction(nameof(GetById), new { id = revenueRate.Id }, revenueRate);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RevenueRate revenueRate)
        {
            if (revenueRate == null)
            {
                return BadRequest("Revenue rate data is required.");
            }

            if (revenueRate.Id == 0)
            {
                revenueRate.Id = id;
            }
            else if (id != revenueRate.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.RevenueRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("Revenue rate not found.");
            }

            existing.PropertyId = revenueRate.PropertyId;
            existing.ApplicableDate = revenueRate.ApplicableDate;
            existing.Rate = revenueRate.Rate;
            existing.Attachments = revenueRate.Attachments;
            existing.Status = revenueRate.Status;
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var revenueRate = await _context.RevenueRates
                .FirstOrDefaultAsync(r => r.Id == id && (r.IsDeleted == null || r.IsDeleted == false));

            if (revenueRate == null)
            {
                return NotFound("Revenue rate not found.");
            }

            revenueRate.IsDeleted = true;
            revenueRate.Action = "DELETE";
            revenueRate.ActionDate = DateTime.UtcNow;

            _context.RevenueRates.Update(revenueRate);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


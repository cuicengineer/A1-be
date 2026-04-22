using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    /// <summary>
    /// Tenant Controller for managing tenant records
    /// 
    /// GET /api/Tenant - Get all tenants (only non-deleted)
    /// GET /api/Tenant/{id} - Get tenant by ID
    /// POST /api/Tenant - Create a new tenant
    /// PUT /api/Tenant/{id} - Update a tenant
    /// DELETE /api/Tenant/{id} - Soft delete a tenant (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly IGenericRepository<Tenant> _repository;
        private readonly ApplicationDbContext _context;

        public TenantController(IGenericRepository<Tenant> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET: Get all tenants (only returns records where IsDeleted = 0 or null)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .Where(t => t.IsDeleted == null || t.IsDeleted == false)
                .ToListAsync();

            var tenantNos = tenants
                .Select(t => t.TenantNo)
                .Where(tn => !string.IsNullOrWhiteSpace(tn))
                .Distinct()
                .ToList();

            var contractCounts = await _context.Contracts
                .AsNoTracking()
                .Where(c =>
                    tenantNos.Contains(c.TenantNo) &&
                    c.Status &&
                    (c.IsDeleted == null || c.IsDeleted == false))
                .GroupBy(c => c.TenantNo)
                .Select(g => new { TenantNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TenantNo, x => x.Count);

            var result = tenants.Select(t => BuildTenantResponse(
                t,
                contractCounts.TryGetValue(t.TenantNo, out var count) ? count : 0));

            return Ok(result);
        }

        /// <summary>
        /// GET: Get tenant by ID (only returns if IsDeleted = 0 or null)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && (t.IsDeleted == null || t.IsDeleted == false));

            if (tenant == null)
            {
                return NotFound();
            }

            var totalContracts = await _context.Contracts
                .AsNoTracking()
                .Where(c =>
                    c.TenantNo == tenant.TenantNo &&
                    c.Status &&
                    (c.IsDeleted == null || c.IsDeleted == false))
                .CountAsync();

            return Ok(BuildTenantResponse(tenant, totalContracts));
        }

        /// <summary>
        /// POST: Create a new tenant (sets IsDeleted = false by default)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Tenant tenant)
        {
            if (tenant == null)
            {
                return BadRequest("Tenant data is required.");
            }

            // Set IsDeleted = false by default
            tenant.IsDeleted = false;
            tenant.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, tenant.ActionBy);
            // ActionDate and Action will be set by the repository

            await _repository.AddAsync(tenant);
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        /// <summary>
        /// PUT: Update an existing tenant
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Tenant tenant)
        {
            if (tenant == null)
            {
                return BadRequest("Tenant data is required.");
            }

            // If tenant.Id is not set (0), use the route parameter id
            if (tenant.Id == 0)
            {
                tenant.Id = id;
            }
            else if (id != tenant.Id)
            {
                return BadRequest("ID mismatch.");
            }

            // Check if tenant exists and is not deleted
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && (t.IsDeleted == null || t.IsDeleted == false));

            if (existingTenant == null)
            {
                return NotFound("Tenant not found.");
            }

            // Update properties
            existingTenant.TenantNo = tenant.TenantNo;
            existingTenant.OwnerName = tenant.OwnerName;
            existingTenant.Prefix = tenant.Prefix;
            existingTenant.BusinessName = tenant.BusinessName;
            existingTenant.Address = tenant.Address;
            existingTenant.Province = tenant.Province;
            existingTenant.City = tenant.City;
            existingTenant.TelephoneNo = tenant.TelephoneNo;
            existingTenant.CellNo = tenant.CellNo;
            existingTenant.NTNNo = tenant.NTNNo;
            existingTenant.GSTNo = tenant.GSTNo;
            existingTenant.Status = tenant.Status;
            existingTenant.Remarks = tenant.Remarks;
            existingTenant.ActionDate = DateTime.UtcNow;
            existingTenant.Action = "UPDATE";
            existingTenant.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, tenant.ActionBy);

            await _repository.UpdateAsync(existingTenant);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a tenant (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && (t.IsDeleted == null || t.IsDeleted == false));

            if (tenant == null)
            {
                return NotFound("Tenant not found.");
            }

            // Soft delete - set IsDeleted = true
            tenant.IsDeleted = true;
            tenant.Action = "DELETE";
            tenant.ActionDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(tenant.ActionBy))
            {
                var existingActionBy = await _context.Tenants
                    .AsNoTracking()
                    .Where(t => t.Id == id)
                    .Select(t => t.ActionBy)
                    .FirstOrDefaultAsync();
                tenant.ActionBy = existingActionBy;
            }
            tenant.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, tenant.ActionBy);

            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static object BuildTenantResponse(Tenant tenant, int totalContracts)
        {
            return new
            {
                tenant.Id,
                tenant.ActionDate,
                tenant.ActionBy,
                tenant.Action,
                tenant.IsDeleted,
                tenant.TenantNo,
                tenant.OwnerName,
                tenant.Prefix,
                tenant.BusinessName,
                tenant.Address,
                tenant.Province,
                tenant.City,
                tenant.TelephoneNo,
                tenant.CellNo,
                tenant.NTNNo,
                tenant.GSTNo,
                tenant.Status,
                tenant.Remarks,
                totalContracts,
                totalInvoices = 0
            };
        }
    }
}


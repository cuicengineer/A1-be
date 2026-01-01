using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    /// <summary>
    /// Contracts Controller for managing contract records
    /// 
    /// GET /api/Contracts - Get all contracts (only non-deleted)
    /// GET /api/Contracts/{id} - Get contract by ID
    /// POST /api/Contracts - Create a new contract
    /// PUT /api/Contracts/{id} - Update a contract
    /// DELETE /api/Contracts/{id} - Soft delete a contract (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ContractsController : ControllerBase
    {
        private readonly IGenericRepository<Contract> _repository;
        private readonly ApplicationDbContext _context;

        public ContractsController(IGenericRepository<Contract> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET: Get all contracts (only returns records where IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId, GrpId to their respective Name values
        /// Supports pagination with pageNumber and pageSize query parameters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200; // safety cap for high-load scenarios

            var baseQuery = _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            // Join with related tables to get names efficiently using left joins
            var contracts = await (from c in baseQuery
                                   join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                       on c.CmdId equals cmd.Id into cmdGroup
                                   from cmd in cmdGroup.DefaultIfEmpty()
                                   join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                       on c.BaseId equals b.Id into baseGroup
                                   from b in baseGroup.DefaultIfEmpty()
                                   join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                       on c.ClassId equals cls.Id into classGroup
                                   from cls in classGroup.DefaultIfEmpty()
                                   join pg in _context.PropertyGroups.Where(pg => pg.IsDeleted == null || pg.IsDeleted == false)
                                       on c.GrpId equals pg.Id into pgGroup
                                   from pg in pgGroup.DefaultIfEmpty()
                                   orderby c.Id descending
                                   select new
                                   {
                                       c.Id,
                                       c.ContractNo,
                                       CmdId = c.CmdId,
                                       CmdName = cmd != null ? cmd.Name : string.Empty,
                                       BaseId = c.BaseId,
                                       BaseName = b != null ? b.Name : string.Empty,
                                       ClassId = c.ClassId,
                                       ClassName = cls != null ? cls.Name : string.Empty,
                                       GrpId = c.GrpId,
                                       GrpName = pg != null ? (pg.GId ?? string.Empty) : string.Empty,
                                       c.TenantNo,
                                       c.BusinessName,
                                       c.NatureOfBusiness,
                                       c.ContractStartDate,
                                       c.ContractEndDate,
                                       c.CommercialOperationDate,
                                       c.InitialRentPM,
                                       c.InitialRentPA,
                                       c.PaymentTermMonths,
                                       c.IncreaseRatePercent,
                                       c.IncreaseIntervalMonths,
                                       c.SDRateMonths,
                                       c.SecurityDepositAmount,
                                       c.RentalValue,
                                       c.GovtShareCondition,
                                       c.PAFShare,
                                       c.Status,
                                       c.ActionDate,
                                       c.ActionBy,
                                       c.Action,
                                       c.IsDeleted
                                   })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(contracts);
        }

        /// <summary>
        /// GET: Get contract by ID (only returns if IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId, GrpId to their respective Name values
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var contract = await (from c in _context.Contracts
                                  .AsNoTracking()
                                  .Where(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false))
                                   join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                       on c.CmdId equals cmd.Id into cmdGroup
                                   from cmd in cmdGroup.DefaultIfEmpty()
                                   join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                       on c.BaseId equals b.Id into baseGroup
                                   from b in baseGroup.DefaultIfEmpty()
                                   join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                       on c.ClassId equals cls.Id into classGroup
                                   from cls in classGroup.DefaultIfEmpty()
                                   join pg in _context.PropertyGroups.Where(pg => pg.IsDeleted == null || pg.IsDeleted == false)
                                       on c.GrpId equals pg.Id into pgGroup
                                   from pg in pgGroup.DefaultIfEmpty()
                                   select new
                                   {
                                       c.Id,
                                       c.ContractNo,
                                       CmdId = c.CmdId,
                                       CmdName = cmd != null ? cmd.Name : string.Empty,
                                       BaseId = c.BaseId,
                                       BaseName = b != null ? b.Name : string.Empty,
                                       ClassId = c.ClassId,
                                       ClassName = cls != null ? cls.Name : string.Empty,
                                       GrpId = c.GrpId,
                                       GrpName = pg != null ? (pg.GId ?? string.Empty) : string.Empty,
                                       c.TenantNo,
                                       c.BusinessName,
                                       c.NatureOfBusiness,
                                       c.ContractStartDate,
                                       c.ContractEndDate,
                                       c.CommercialOperationDate,
                                       c.InitialRentPM,
                                       c.InitialRentPA,
                                       c.PaymentTermMonths,
                                       c.IncreaseRatePercent,
                                       c.IncreaseIntervalMonths,
                                       c.SDRateMonths,
                                       c.SecurityDepositAmount,
                                       c.RentalValue,
                                       c.GovtShareCondition,
                                       c.PAFShare,
                                       c.Status,
                                       c.ActionDate,
                                       c.ActionBy,
                                       c.Action,
                                       c.IsDeleted
                                   })
                .FirstOrDefaultAsync();

            if (contract == null)
            {
                return NotFound();
            }

            return Ok(contract);
        }

        /// <summary>
        /// POST: Create a new contract (sets IsDeleted = false by default)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Contract contract)
        {
            if (contract == null)
            {
                return BadRequest("Contract data is required.");
            }

            // Set IsDeleted = false by default
            contract.IsDeleted = false;
            // ActionDate and Action will be set by the repository

            await _repository.AddAsync(contract);
            return CreatedAtAction(nameof(GetById), new { id = contract.Id }, contract);
        }

        /// <summary>
        /// PUT: Update an existing contract
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Contract contract)
        {
            if (contract == null)
            {
                return BadRequest("Contract data is required.");
            }

            // If contract.Id is not set (0), use the route parameter id
            if (contract.Id == 0)
            {
                contract.Id = id;
            }
            else if (id != contract.Id)
            {
                return BadRequest("ID mismatch.");
            }

            // Check if contract exists and is not deleted
            var existingContract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existingContract == null)
            {
                return NotFound("Contract not found.");
            }

            // Update properties efficiently
            existingContract.ContractNo = contract.ContractNo;
            existingContract.CmdId = contract.CmdId;
            existingContract.BaseId = contract.BaseId;
            existingContract.ClassId = contract.ClassId;
            existingContract.GrpId = contract.GrpId;
            existingContract.TenantNo = contract.TenantNo;
            existingContract.BusinessName = contract.BusinessName;
            existingContract.NatureOfBusiness = contract.NatureOfBusiness;
            existingContract.ContractStartDate = contract.ContractStartDate;
            existingContract.ContractEndDate = contract.ContractEndDate;
            existingContract.CommercialOperationDate = contract.CommercialOperationDate;
            existingContract.InitialRentPM = contract.InitialRentPM;
            existingContract.InitialRentPA = contract.InitialRentPA;
            existingContract.PaymentTermMonths = contract.PaymentTermMonths;
            existingContract.IncreaseRatePercent = contract.IncreaseRatePercent;
            existingContract.IncreaseIntervalMonths = contract.IncreaseIntervalMonths;
            existingContract.SDRateMonths = contract.SDRateMonths;
            existingContract.SecurityDepositAmount = contract.SecurityDepositAmount;
            existingContract.RentalValue = contract.RentalValue;
            existingContract.GovtShareCondition = contract.GovtShareCondition;
            existingContract.PAFShare = contract.PAFShare;
            existingContract.Status = contract.Status;
            existingContract.ActionDate = DateTime.UtcNow;
            existingContract.Action = "UPDATE";

            await _repository.UpdateAsync(existingContract);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a contract (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (contract == null)
            {
                return NotFound("Contract not found.");
            }

            // Soft delete - set IsDeleted = true
            contract.IsDeleted = true;
            contract.Action = "DELETE";
            contract.ActionDate = DateTime.UtcNow;

            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


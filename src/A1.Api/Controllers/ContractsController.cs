using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

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
        private readonly IAuditLogService _auditLogService;

        public class SearchByGrpNameRequest
        {
            public string GrpName { get; set; } = string.Empty;
        }

        public ContractsController(IGenericRepository<Contract> repository, ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _repository = repository;
            _context = context;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// GET: Get all active contracts as of a given date using a stored procedure.
        /// This endpoint is optimized for large result sets and heavy stored procedure processing.
        /// Route: GET /api/Contracts/ActiveByAsOfDate?asOfDate=2025-01-01
        /// </summary>
        [HttpGet("ActiveByAsOfDate")]
        public async Task<IActionResult> GetActiveContractsByAsOfDate(
            [FromQuery] DateTime asOfDate,
            CancellationToken cancellationToken = default)
        {
            if (asOfDate == default)
            {
                return BadRequest("asOfDate query parameter is required.");
            }

            await using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.sp_GetActiveContractsAsOfDate";
            command.CommandType = CommandType.StoredProcedure;

            var param = command.CreateParameter();
            param.ParameterName = "@AsOfDate";
            param.DbType = DbType.Date;
            param.Value = asOfDate.Date;
            command.Parameters.Add(param);

            command.CommandTimeout = 120;

            var results = new List<Dictionary<string, object?>>();

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection,
                cancellationToken);

            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[names[i]] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }

            return Ok(results);
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
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            // Join with related tables to get names efficiently using left joins and include rise terms
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
                                       c.RiseTermType,
                                       c.RiseDate,
                                       c.RiseYear,
                                       c.InitialRentPM,
                                       c.InitialRentPA,
                                       c.currentRentPA,
                                       c.PaymentTermMonths,
                                       c.IncreaseRatePercent,
                                       c.IncreaseIntervalMonths,
                                       c.SDRateMonths,
                                       c.SecurityDepositAmount,
                                       c.RentalValue,
                                       c.GovtShare,
                                       c.PAFShare,
                                       c.GroupArea,
                                       c.GroupRate,
                                       c.RentalValueRate,
                                       c.VaArea,
                                       c.Dpc,
                                       c.Signatory,
                                       c.Status,
                                       c.Term,
                                       c.ActionDate,
                                       c.ActionBy,
                                       c.Action,
                                       c.IsDeleted,
                                       c.IsArchive,
                                       c.userIPAddress,
                                       c.Remarks,
                                       c.ProfitRate,
                                       ContractRiseTerms = _context.ContractRiseTerms
                                            .AsNoTracking()
                                            .Where(r => r.ContractId == c.Id && (r.IsDeleted == null || r.IsDeleted == false))
                                            .OrderBy(r => r.SequenceNo)
                                            .ToList()
                                   })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                contracts.Select(x => x.Id),
                "Contracts", "Contract");
            var response = AttachmentFlagHelper.ToDictionariesWithAttachmentFlag(contracts, x => x.Id, attachedIds);
            return Ok(response);
        }

        /// <summary>
        /// GET: Get contract by ID (only returns if IsDeleted = 0 or null)
        /// Maps CmdId, BaseId, ClassId, GrpId to their respective Name values
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var scope = await DataAccessScopeHelper.ResolveAsync(User, _context);
            var baseQuery = _context.Contracts
                .AsNoTracking()
                .Where(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));
            baseQuery = DataAccessScopeHelper.ApplyScope(baseQuery, scope);

            var contract = await (from c in baseQuery
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
                                       c.RiseTermType,
                                       c.RiseDate,
                                       c.RiseYear,
                                       c.InitialRentPM,
                                       c.InitialRentPA,
                                       c.currentRentPA,
                                       c.PaymentTermMonths,
                                       c.IncreaseRatePercent,
                                       c.IncreaseIntervalMonths,
                                       c.SDRateMonths,
                                       c.SecurityDepositAmount,
                                       c.RentalValue,
                                       c.GovtShare,
                                       c.PAFShare,
                                       c.GroupArea,
                                       c.GroupRate,
                                       c.RentalValueRate,
                                       c.VaArea,
                                       c.Dpc,
                                       c.Signatory,
                                       c.Status,
                                       c.Term,
                                       c.ActionDate,
                                       c.ActionBy,
                                       c.Action,
                                       c.IsDeleted,
                                       c.IsArchive,
                                       c.userIPAddress,
                                       c.Remarks,
                                       c.ProfitRate,
                                       ContractRiseTerms = _context.ContractRiseTerms
                                            .AsNoTracking()
                                            .Where(r => r.ContractId == c.Id && (r.IsDeleted == null || r.IsDeleted == false))
                                            .OrderBy(r => r.SequenceNo)
                                            .ToList()
                                   })
                .FirstOrDefaultAsync();

            if (contract == null)
            {
                return NotFound();
            }

            var attachedIds = await AttachmentFlagHelper.GetAttachedFormIdsAsync(
                _context,
                new[] { contract.Id },
                "Contracts", "Contract");
            return Ok(AttachmentFlagHelper.ToDictionaryWithAttachmentFlag(contract, attachedIds.Contains(contract.Id)));
        }

        /// <summary>
        /// GET: Get contracts by tenant number (returns only active and not deleted records)
        /// Route: GET /api/Contracts/by-tenant/{tenantNo}
        /// </summary>
        [HttpGet("by-tenant/{tenantNo}")]
        public async Task<IActionResult> GetByTenantNo(string tenantNo)
        {
            if (string.IsNullOrWhiteSpace(tenantNo))
            {
                return BadRequest("tenantNo is required.");
            }

            var contracts = await _context.Contracts
                .AsNoTracking()
                .Where(c =>
                    c.TenantNo == tenantNo &&
                    c.Status == true &&
                    (c.IsDeleted == null || c.IsDeleted == false))
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            return Ok(contracts);
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
            contract.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, contract.ActionBy);
            // ActionDate and Action will be set by the repository
            using var tx = await _context.Database.BeginTransactionAsync();
            await _repository.AddAsync(contract); // saves and sets Id

            if (contract.ContractRiseTerms != null && contract.ContractRiseTerms.Count > 0)
            {
                var now = DateTime.UtcNow;
                var actionByWithIp = ActionByHelper.GetActionByWithIp(User, HttpContext, contract.ActionBy);
                var terms = contract.ContractRiseTerms.Select(term => new ContractRiseTerm
                {
                    ContractId = contract.Id,
                    MonthsInterval = term.MonthsInterval,
                    RisePercent = term.RisePercent,
                    SequenceNo = term.SequenceNo,
                    Status = term.Status,
                    IsDeleted = false,
                    Action = "CREATE",
                    ActionDate = now,
                    ActionBy = actionByWithIp
                }).ToList();

                await _context.ContractRiseTerms.AddRangeAsync(terms);
                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return CreatedAtAction(nameof(GetById), new { id = contract.Id }, contract);
        }

        /// <summary>
        /// POST: Search contracts by group name (returns status=true and not deleted)
        /// </summary>
        [HttpPost("searchByGrpName")]
        public async Task<IActionResult> SearchByGrpName([FromBody] SearchByGrpNameRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GrpName))
            {
                return BadRequest("grpName is required.");
            }

            var grpName = request.GrpName;

            var contracts = await (from c in _context.Contracts
                                   .AsNoTracking()
                                   .Where(c => (c.IsDeleted == null || c.IsDeleted == false) && c.Status == true)
                                   join cmd in _context.Commands.Where(cmd => cmd.IsDeleted == null || cmd.IsDeleted == false)
                                       on c.CmdId equals cmd.Id into cmdGroup
                                   from cmd in cmdGroup.DefaultIfEmpty()
                                   join b in _context.Bases.Where(b => b.IsDeleted == null || b.IsDeleted == false)
                                       on c.BaseId equals b.Id into baseGroup
                                   from b in baseGroup.DefaultIfEmpty()
                                   join cls in _context.Classes.Where(cls => cls.IsDeleted == null || cls.IsDeleted == false)
                                       on c.ClassId equals cls.Id into classGroup
                                   from cls in classGroup.DefaultIfEmpty()
                                   join pg in _context.PropertyGroups.Where(pg => pg.IsDeleted == null || pg.IsDeleted == false && pg.GId == grpName)
                                       on c.GrpId equals pg.Id into pgGroup
                                   from pg in pgGroup.DefaultIfEmpty()
                                   where pg != null && pg.GId == grpName
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
                                       c.currentRentPA,
                                       c.PaymentTermMonths,
                                       c.IncreaseRatePercent,
                                       c.IncreaseIntervalMonths,
                                       c.SDRateMonths,
                                       c.SecurityDepositAmount,
                                       c.RentalValue,
                                       c.GovtShare,
                                       c.PAFShare,
                                       c.GroupArea,
                                       c.GroupRate,
                                       c.RentalValueRate,
                                       c.VaArea,
                                       c.Dpc,
                                       c.Signatory,
                                       c.Status,
                                       c.Term,
                                       c.ActionDate,
                                       c.ActionBy,
                                       c.Action,
                                       c.IsDeleted,
                                       c.IsArchive,
                                       c.userIPAddress,
                                       c.Remarks,
                                       c.ProfitRate
                                   })
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(contracts);
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

            var oldValuesJson = JsonSerializer.Serialize(new
            {
                existingContract.ContractNo,
                existingContract.CmdId,
                existingContract.BaseId,
                existingContract.ClassId,
                existingContract.GrpId,
                existingContract.TenantNo,
                existingContract.BusinessName,
                existingContract.NatureOfBusiness,
                existingContract.ContractStartDate,
                existingContract.ContractEndDate,
                existingContract.CommercialOperationDate,
                existingContract.RiseTermType,
                existingContract.RiseDate,
                existingContract.RiseYear,
                existingContract.InitialRentPM,
                existingContract.InitialRentPA,
                existingContract.currentRentPA,
                existingContract.PaymentTermMonths,
                existingContract.IncreaseRatePercent,
                existingContract.IncreaseIntervalMonths,
                existingContract.SDRateMonths,
                existingContract.SecurityDepositAmount,
                existingContract.RentalValue,
                existingContract.GovtShare,
                existingContract.PAFShare,
                existingContract.GroupArea,
                existingContract.GroupRate,
                existingContract.RentalValueRate,
                existingContract.VaArea,
                existingContract.Dpc,
                existingContract.Signatory,
                existingContract.Status,
                existingContract.Term,
                existingContract.userIPAddress,
                existingContract.Remarks,
                existingContract.ProfitRate,
                existingContract.IsArchive
            });

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
            existingContract.RiseTermType = contract.RiseTermType;
            existingContract.RiseDate = contract.RiseDate;
            existingContract.RiseYear = contract.RiseYear;
            existingContract.InitialRentPM = contract.InitialRentPM;
            existingContract.InitialRentPA = contract.InitialRentPA;
            existingContract.currentRentPA = contract.currentRentPA;
            existingContract.PaymentTermMonths = contract.PaymentTermMonths;
            existingContract.IncreaseRatePercent = contract.IncreaseRatePercent;
            existingContract.IncreaseIntervalMonths = contract.IncreaseIntervalMonths;
            existingContract.SDRateMonths = contract.SDRateMonths;
            existingContract.SecurityDepositAmount = contract.SecurityDepositAmount;
            existingContract.RentalValue = contract.RentalValue;
            existingContract.GovtShare = contract.GovtShare;
            existingContract.PAFShare = contract.PAFShare;
            existingContract.GroupArea = contract.GroupArea;
            existingContract.GroupRate = contract.GroupRate;
            existingContract.RentalValueRate = contract.RentalValueRate;
            existingContract.VaArea = contract.VaArea;
            existingContract.Dpc = contract.Dpc;
            existingContract.Signatory = contract.Signatory;
            existingContract.Status = contract.Status;
            existingContract.Term = contract.Term;
            existingContract.ActionDate = DateTime.UtcNow;
            existingContract.Action = "UPDATE";
            existingContract.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, contract.ActionBy);
            existingContract.userIPAddress = contract.userIPAddress;
            existingContract.Remarks = contract.Remarks;
            existingContract.ProfitRate = contract.ProfitRate;
            existingContract.IsArchive = contract.IsArchive;

            if (contract.ContractRiseTerms != null && contract.ContractRiseTerms.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var term in contract.ContractRiseTerms)
                {
                    var existingTerm = await _context.ContractRiseTerms
                        .FirstOrDefaultAsync(t => t.ContractId == existingContract.Id
                            && t.SequenceNo == term.SequenceNo
                            && (t.IsDeleted == null || t.IsDeleted == false));

                    if (existingTerm != null)
                    {
                        existingTerm.MonthsInterval = term.MonthsInterval;
                        existingTerm.RisePercent = term.RisePercent;
                        existingTerm.SequenceNo = term.SequenceNo;
                        existingTerm.Status = term.Status;
                        existingTerm.ActionDate = now;
                        existingTerm.Action = "UPDATE";
                        existingTerm.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, contract.ActionBy);
                        _context.ContractRiseTerms.Update(existingTerm);
                    }
                    else
                    {
                        var newTerm = new ContractRiseTerm
                        {
                            ContractId = existingContract.Id,
                            MonthsInterval = term.MonthsInterval,
                            RisePercent = term.RisePercent,
                            SequenceNo = term.SequenceNo,
                            Status = term.Status,
                            IsDeleted = false,
                            ActionDate = now,
                            Action = "CREATE",
                            ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, contract.ActionBy)
                        };
                        await _context.ContractRiseTerms.AddAsync(newTerm);
                    }
                }
            }

            await _repository.UpdateAsync(existingContract);

            var newValuesJson = JsonSerializer.Serialize(new
            {
                existingContract.ContractNo,
                existingContract.CmdId,
                existingContract.BaseId,
                existingContract.ClassId,
                existingContract.GrpId,
                existingContract.TenantNo,
                existingContract.BusinessName,
                existingContract.NatureOfBusiness,
                existingContract.ContractStartDate,
                existingContract.ContractEndDate,
                existingContract.CommercialOperationDate,
                existingContract.RiseTermType,
                existingContract.RiseDate,
                existingContract.RiseYear,
                existingContract.InitialRentPM,
                existingContract.InitialRentPA,
                existingContract.currentRentPA,
                existingContract.PaymentTermMonths,
                existingContract.IncreaseRatePercent,
                existingContract.IncreaseIntervalMonths,
                existingContract.SDRateMonths,
                existingContract.SecurityDepositAmount,
                existingContract.RentalValue,
                existingContract.GovtShare,
                existingContract.PAFShare,
                existingContract.GroupArea,
                existingContract.GroupRate,
                existingContract.RentalValueRate,
                existingContract.VaArea,
                existingContract.Dpc,
                existingContract.Signatory,
                existingContract.Status,
                existingContract.Term,
                existingContract.userIPAddress,
                existingContract.Remarks,
                existingContract.ProfitRate,
                existingContract.IsArchive
            });
            await _auditLogService.LogAsync(new AuditLog
            {
                EntityName = "Contract",
                EntityId = id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext),
                Action = "API"
            });

            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a contract (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] string userIPAddress)
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
            contract.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext);
            contract.userIPAddress = userIPAddress;

            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


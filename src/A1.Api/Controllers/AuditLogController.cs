using A1.Api.Models;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// POST: Log a single audit entry. ActionBy is set from the current user (login).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Log([FromBody] AuditLogRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EntityName))
                return BadRequest("EntityName is required.");

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext);
            var action = string.IsNullOrWhiteSpace(request.Action) ? "API" : request.Action.Trim();
            if (action.Length > 50) action = action.Substring(0, 50);

            var entry = new AuditLog
            {
                EntityName = request.EntityName.Trim(),
                EntityId = request.EntityId,
                OldValuesJson = request.OldValuesJson,
                NewValuesJson = request.NewValuesJson,
                ActionBy = actionBy,
                Action = action,
                ActionDateTime = DateTime.UtcNow
            };

            await _auditLogService.LogAsync(entry, cancellationToken).ConfigureAwait(false);
            return Accepted();
        }

        /// <summary>
        /// POST: Log multiple audit entries in one call (batch). ActionBy is set from the current user for all.
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> LogBatch([FromBody] List<AuditLogRequest> requests, CancellationToken cancellationToken)
        {
            if (requests == null || requests.Count == 0)
                return BadRequest("At least one audit entry is required.");

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext);
            var now = DateTime.UtcNow;
            var entries = new List<AuditLog>(requests.Count);

            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.EntityName)) continue;
                var action = string.IsNullOrWhiteSpace(request.Action) ? "API" : request.Action.Trim();
                if (action.Length > 50) action = action.Substring(0, 50);
                entries.Add(new AuditLog
                {
                    EntityName = request.EntityName.Trim(),
                    EntityId = request.EntityId,
                    OldValuesJson = request.OldValuesJson,
                    NewValuesJson = request.NewValuesJson,
                    ActionBy = actionBy,
                    Action = action,
                    ActionDateTime = now
                });
            }

            if (entries.Count == 0)
                return BadRequest("No valid audit entries (EntityName required).");

            await _auditLogService.LogBatchAsync(entries, cancellationToken).ConfigureAwait(false);
            return Accepted();
        }
    }
}

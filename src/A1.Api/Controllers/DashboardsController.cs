using A1.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: Loads dbo.GetPropertyDashboardSummary. Each returned result set becomes one JSON array of row objects (column names from the result set).
        /// Uses the DbContext connection only (no second connection); streams rows with SequentialAccess.
        /// Route: GET /api/Dashboards/property-summary
        /// </summary>
        [HttpGet("property-summary")]
        public async Task<IActionResult> GetPropertyDashboardSummary(CancellationToken cancellationToken = default)
        {
            await using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var category = User.FindFirst("category")?.Value;
            var cmdId = TryParseOptionalIntClaim(User.FindFirst("cmdId")?.Value);
            var baseId = TryParseOptionalIntClaim(User.FindFirst("baseId")?.Value);

            await using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[GetPropertyDashboardSummary]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            if (string.Equals(category, "Power", StringComparison.OrdinalIgnoreCase))
            {
                AddIntParameter(command, "@CmdId", null);
                AddIntParameter(command, "@BaseId", null);
            }
            else
            {
                if (cmdId.HasValue)
                {
                    AddIntParameter(command, "@CmdId", cmdId.Value);
                }

                if (baseId.HasValue)
                {
                    AddIntParameter(command, "@BaseId", baseId.Value);
                }
            }

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection,
                cancellationToken).ConfigureAwait(false);

            var resultSets = new List<List<Dictionary<string, object?>>>();

            do
            {
                resultSets.Add(await ReadResultSetAsync(reader, cancellationToken).ConfigureAwait(false));
            } while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

            return Ok(new { resultSets });
        }

        private static int? TryParseOptionalIntClaim(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value, out var n) ? n : null;
        }

        private static void AddIntParameter(DbCommand command, string name, int? value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Int32;
            p.Value = value.HasValue ? value.Value : DBNull.Value;
            command.Parameters.Add(p);
        }

        private static async Task<List<Dictionary<string, object?>>> ReadResultSetAsync(
            DbDataReader reader,
            CancellationToken cancellationToken)
        {
            var rows = new List<Dictionary<string, object?>>();
            var fieldCount = reader.FieldCount;
            if (fieldCount == 0)
            {
                return rows;
            }

            var names = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[names[i]] = value == DBNull.Value ? null : value;
                }

                rows.Add(row);
            }

            return rows;
        }
    }
}

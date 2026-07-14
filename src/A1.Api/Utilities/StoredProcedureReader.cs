using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Utilities
{
    public static class StoredProcedureReader
    {
        public static void AddIntParameter(DbCommand command, string name, int? value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Int32;
            p.Value = value.HasValue ? value.Value : DBNull.Value;
            command.Parameters.Add(p);
        }

        public static void AddBoolParameter(DbCommand command, string name, bool value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Boolean;
            p.Value = value;
            command.Parameters.Add(p);
        }

        public static void AddOutputIntParameter(DbCommand command, string name)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.Int32;
            p.Direction = ParameterDirection.Output;
            command.Parameters.Add(p);
        }

        public static int ReadOutputInt(DbCommand command, string name)
        {
            var value = command.Parameters[name]?.Value;
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

        public static async Task<(List<Dictionary<string, object?>> Rows, int TotalCount)> ExecuteWithOutputCountAsync(
            DbContext context,
            string procedureName,
            Action<DbCommand> configure,
            string totalCountParameterName = "@TotalCount",
            CancellationToken cancellationToken = default)
        {
            await using var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;
            configure(command);

            var results = new List<Dictionary<string, object?>>();

            await using (var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken))
            {
                var fieldCount = reader.FieldCount;
                if (fieldCount > 0)
                {
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
                }
            }

            return (results, ReadOutputInt(command, totalCountParameterName));
        }

        public static async Task<List<Dictionary<string, object?>>> ExecuteAsync(
            DbContext context,
            string procedureName,
            Action<DbCommand> configure,
            CancellationToken cancellationToken = default)
        {
            await using var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;
            configure(command);

            var results = new List<Dictionary<string, object?>>();

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);

            var fieldCount = reader.FieldCount;
            if (fieldCount > 0)
            {
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
            }

            return results;
        }

        public static string? ReadString(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                var text = Convert.ToString(value)?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        public static int? ReadInt(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is int intValue)
                {
                    return intValue;
                }

                if (int.TryParse(Convert.ToString(value), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        public static bool ReadBool(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is bool boolValue)
                {
                    return boolValue;
                }

                if (value is byte byteValue)
                {
                    return byteValue != 0;
                }

                if (value is int intValue)
                {
                    return intValue != 0;
                }

                if (bool.TryParse(Convert.ToString(value), out var parsed))
                {
                    return parsed;
                }
            }

            return false;
        }

        public static bool? ReadNullableBool(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                return ReadBool(row, key);
            }

            return null;
        }

        public static decimal ReadDecimal(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is decimal decimalValue)
                {
                    return decimalValue;
                }

                if (decimal.TryParse(Convert.ToString(value), out var parsed))
                {
                    return parsed;
                }
            }

            return 0m;
        }

        public static DateTime? ReadDateTime(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is DateTime dateTimeValue)
                {
                    return dateTimeValue;
                }

                if (DateTime.TryParse(Convert.ToString(value), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }
}

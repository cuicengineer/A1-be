using Microsoft.Data.SqlClient;
using A1.Api.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace A1.Api.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        private readonly IDbConnection _connection;

        public GenericRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        private string GetTableName()
        {
            var name = typeof(T).Name;
            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 1) + "ies";
            }
            else
            {
                name = name + "s";
            }
            return $"[{name}]";
        }

        private PropertyInfo GetPrimaryKey()
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var typeNameId = typeof(T).Name + "Id";
            var idProp = props.FirstOrDefault(p => p.Name.Equals(typeNameId, StringComparison.OrdinalIgnoreCase))
                        ?? props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                        ?? props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            if (idProp == null)
            {
                throw new InvalidOperationException($"No primary key property found on {typeof(T).Name}.");
            }
            return idProp;
        }

        private bool IsDefault(object? value, Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (value == null) return true;
            var defaultVal = t.IsValueType ? Activator.CreateInstance(t) : null;
            return Equals(value, defaultVal);
        }

        private static object? ConvertToType(object? value, Type targetType)
        {
            if (value == null || value is DBNull) return null;
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (type.IsEnum)
            {
                return Enum.ToObject(type, value);
            }
            if (type == typeof(Guid))
            {
                return Guid.Parse(value.ToString()!);
            }
            return Convert.ChangeType(value, type);
        }

        private static int TryGetOrdinal(IDataRecord record, string name)
        {
            try
            {
                return record.GetOrdinal(name);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        private static T MapEntityFromRecord(IDataRecord record)
        {
            var entity = Activator.CreateInstance<T>();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);
            foreach (var p in props)
            {
                var ordinal = TryGetOrdinal(record, p.Name);
                if (ordinal >= 0)
                {
                    var val = record.IsDBNull(ordinal) ? null : record.GetValue(ordinal);
                    var converted = ConvertToType(val, p.PropertyType);
                    p.SetValue(entity, converted);
                }
            }
            return entity;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var tableName = GetTableName();
            var sql = $"SELECT * FROM A1.{tableName}";
            var hasIsDeleted = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase));
            if (hasIsDeleted)
            {
                sql += " WHERE [IsDeleted] = 0";
            }
            if (_connection.State != ConnectionState.Open) _connection.Open();
            try
            {
                if (_connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
                {
                    using var cmd = sqlConn.CreateCommand();
                    cmd.CommandText = sql;
                    using var reader = await cmd.ExecuteReaderAsync();
                    var list = new List<T>();
                    while (await reader.ReadAsync())
                    {
                        list.Add(MapEntityFromRecord(reader));
                    }
                    return list;
                }
                else
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    using var reader = cmd.ExecuteReader();
                    var list = new List<T>();
                    while (reader.Read())
                    {
                        list.Add(MapEntityFromRecord(reader));
                    }
                    return list;
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            var tableName = GetTableName();
            var pk = GetPrimaryKey();
            var pkName = pk.Name;
            var sql = $"SELECT * FROM A1.{tableName} WHERE [{pkName}] = @{pkName}";
            var hasIsDeleted = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase));
            if (hasIsDeleted)
            {
                sql += " AND [IsDeleted] = 0";
            }
            if (_connection.State != ConnectionState.Open) _connection.Open();
            try
            {
                if (_connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
                {
                    using var cmd = sqlConn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue($"@{pkName}", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        return MapEntityFromRecord(reader);
                    }
                    return default;
                }
                else
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter();
                    p.ParameterName = $"@{pkName}";
                    p.Value = id;
                    cmd.Parameters.Add(p);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return MapEntityFromRecord(reader);
                    }
                    return default;
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }

        public async Task<int> AddAsync(T entity)
        {
            var tableName = GetTableName();
            var pkName = GetPrimaryKey().Name;
            var properties = typeof(T).GetProperties().Where(p => p.Name != pkName && p.CanWrite);
            var columns = string.Join(", ", properties.Select(p => $"[{p.Name}]"));
            var parameters = string.Join(", ", properties.Select(p => "@" + p.Name));
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            var sql = $"INSERT INTO A1.{tableName} ({columns}) OUTPUT INSERTED.[{pkName}] VALUES ({parameters})";
            if (_connection.State != ConnectionState.Open) _connection.Open();
            try
            {
                if (_connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
                {
                    using var cmd = sqlConn.CreateCommand();
                    cmd.CommandText = sql;
                    foreach (var p in properties)
                    {
                        var val = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue($"@{p.Name}", val);
                    }
                    var insertedIdObj = await cmd.ExecuteScalarAsync();
                    if (insertedIdObj != null && insertedIdObj != DBNull.Value)
                    {
                        entity.Id = Convert.ToInt32(insertedIdObj);
                    }
                    return 1;
                }
                else
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    foreach (var p in properties)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = $"@{p.Name}";
                        param.Value = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.Add(param);
                    }
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        entity.Id = Convert.ToInt32(result);
                    }
                    return 1;
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }

        public async Task<int> UpdateAsync(T entity)
        {
            var tableName = GetTableName();
            var pkName = GetPrimaryKey().Name;
            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "UPDATE";
            var properties = typeof(T).GetProperties().Where(p => p.Name != pkName && p.CanWrite);
            var setClauses = string.Join(", ", properties.Select(p => $"[{p.Name}] = @{p.Name}"));
            var sql = $"UPDATE A1.{tableName} SET {setClauses} WHERE [{pkName}] = @{pkName}";
            if (_connection.State != ConnectionState.Open) _connection.Open();
            try
            {
                if (_connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
                {
                    using var cmd = sqlConn.CreateCommand();
                    cmd.CommandText = sql;
                    foreach (var p in properties)
                    {
                        var val = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue($"@{p.Name}", val);
                    }
                    var pkValue = entity.GetType().GetProperty(pkName)!.GetValue(entity)!;
                    cmd.Parameters.AddWithValue($"@{pkName}", pkValue);
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows;
                }
                else
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    foreach (var p in properties)
                    {
                        var par = cmd.CreateParameter();
                        par.ParameterName = $"@{p.Name}";
                        par.Value = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.Add(par);
                    }
                    var pkPar = cmd.CreateParameter();
                    pkPar.ParameterName = $"@{pkName}";
                    pkPar.Value = entity.GetType().GetProperty(pkName)!.GetValue(entity)!;
                    cmd.Parameters.Add(pkPar);
                    var rows = cmd.ExecuteNonQuery();
                    return rows;
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }

        public async Task<int> DeleteAsync(int id)
        {
            var tableName = GetTableName();
            var pkName = GetPrimaryKey().Name;
            var hasIsDeleted = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase));
            if (!hasIsDeleted)
            {
                // Enforce soft delete only: if IsDeleted column does not exist, do not hard delete.
                return 0;
            }
            var sql = $"UPDATE A1.{tableName} SET [IsDeleted] = @IsDeleted, [Action] = @Action, [ActionDate] = @ActionDate WHERE [{pkName}] = @{pkName}";
            if (_connection.State != ConnectionState.Open) _connection.Open();
            try
            {
                if (_connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
                {
                    using var cmd = sqlConn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue($"@{pkName}", id);
                    cmd.Parameters.AddWithValue("@IsDeleted", 1);
                    cmd.Parameters.AddWithValue("@Action", "DELETE");
                    cmd.Parameters.AddWithValue("@ActionDate", DateTime.UtcNow);
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows;
                }
                else
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter();
                    p.ParameterName = $"@{pkName}";
                    p.Value = id;
                    cmd.Parameters.Add(p);
                    var p2 = cmd.CreateParameter();
                    p2.ParameterName = "@IsDeleted";
                    p2.Value = 1;
                    cmd.Parameters.Add(p2);
                    var p3 = cmd.CreateParameter();
                    p3.ParameterName = "@Action";
                    p3.Value = "DELETE";
                    cmd.Parameters.Add(p3);
                    var p4 = cmd.CreateParameter();
                    p4.ParameterName = "@ActionDate";
                    p4.Value = DateTime.UtcNow;
                    cmd.Parameters.Add(p4);
                    var rows = cmd.ExecuteNonQuery();
                    return rows;
                }
            }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }
    }
}
using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenericController<T> : ControllerBase where T : BaseEntity
    {
        private readonly IGenericRepository<T> _repository;
        private readonly ApplicationDbContext _context;

        public GenericController(IGenericRepository<T> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Always fetch via EF Core, then bind additional ids if the entity exposes them
            var entities = await _repository.GetAllAsync();
            var filteredEntities = FilterNotDeleted(entities).ToList();

            var entityType = filteredEntities.FirstOrDefault()?.GetType() ?? typeof(T);
            var hasCmdId = HasProperty(entityType, "cmdId");
            var hasBaseId = HasProperty(entityType, "baseId");
            var hasClassId = HasProperty(entityType, "classId");

            if (hasCmdId || hasBaseId || hasClassId)
            {
                var enriched = await EnrichEntitiesWithNames(filteredEntities, hasCmdId, hasBaseId, hasClassId);
                return Ok(enriched);
            }

            return Ok(filteredEntities);
        }

        private async Task<List<Dictionary<string, object?>>> EnrichEntitiesWithNames(List<T> entities, bool hasCmdId, bool hasBaseId, bool hasClassId)
        {
            var result = new List<Dictionary<string, object?>>();

            // Batch fetch related data for efficiency
            var cmdIdProp = typeof(T).GetProperty("CmdId");
            var baseIdProp = typeof(T).GetProperty("BaseId");
            var classIdProp = typeof(T).GetProperty("ClassId");

            Dictionary<int, Command>? commands = null;
            Dictionary<int, Base>? bases = null;
            Dictionary<int, Class>? classes = null;

            if (hasCmdId && cmdIdProp != null)
            {
                var cmdIds = entities.Select(e => (int)cmdIdProp.GetValue(e)!).Distinct().ToList();
                commands = await _context.Commands
                    .Where(c => cmdIds.Contains(c.Id) && (c.IsDeleted == null || c.IsDeleted == false))
                    .ToDictionaryAsync(c => c.Id);
            }

            if (hasBaseId && baseIdProp != null)
            {
                var baseIds = entities.Select(e => (int)baseIdProp.GetValue(e)!).Distinct().ToList();
                bases = await _context.Bases
                    .Where(b => baseIds.Contains(b.Id) && (b.IsDeleted == null || b.IsDeleted == false))
                    .ToDictionaryAsync(b => b.Id);
            }

            if (hasClassId && classIdProp != null)
            {
                var classIds = entities.Select(e => (int)classIdProp.GetValue(e)!).Distinct().ToList();
                classes = await _context.Classes
                    .Where(c => classIds.Contains(c.Id) && (c.IsDeleted == null || c.IsDeleted == false))
                    .ToDictionaryAsync(c => c.Id);
            }

            // Build result dictionaries
            foreach (var entity in entities)
            {
                var dict = new Dictionary<string, object?>();
                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in props)
                {
                    dict[prop.Name] = prop.GetValue(entity);
                }

                // Add names
                if (hasCmdId && cmdIdProp != null)
                {
                    var cmdId = (int?)cmdIdProp.GetValue(entity);
                    dict["CmdName"] = cmdId.HasValue && commands != null && commands.TryGetValue(cmdId.Value, out var cmd) ? cmd.Name : string.Empty;
                }

                if (hasBaseId && baseIdProp != null)
                {
                    var baseId = (int?)baseIdProp.GetValue(entity);
                    dict["BaseName"] = baseId.HasValue && bases != null && bases.TryGetValue(baseId.Value, out var b) ? b.Name : string.Empty;
                }

                if (hasClassId && classIdProp != null)
                {
                    var classId = (int?)classIdProp.GetValue(entity);
                    dict["ClassName"] = classId.HasValue && classes != null && classes.TryGetValue(classId.Value, out var cls) ? cls.Name : string.Empty;
                }

                result.Add(dict);
            }

            return result;
        }



        private bool HasProperty(Type type, string propertyName)
        {
            return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null;
        }

        private bool HasProperty(string propertyName)
        {
            return HasProperty(typeof(T), propertyName);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            if (IsEntityDeleted(entity))
            {
                return NotFound();
            }

            // Check if entity has CmdId, BaseId, ClassId properties and enrich with names
            var entityType = entity.GetType();
            var hasCmdId = HasProperty(entityType, "CmdId");
            var hasBaseId = HasProperty(entityType, "BaseId");
            var hasClassId = HasProperty(entityType, "ClassId");

            if (hasCmdId || hasBaseId || hasClassId)
            {
                var enriched = await EnrichEntitiesWithNames(new List<T> { entity }, hasCmdId, hasBaseId, hasClassId);
                var enrichedEntity = enriched.FirstOrDefault();
                return Ok(enrichedEntity ?? (object)entity);
            }

            return Ok(entity);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] T entity)
        {
            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] T entity)
        {
            var pkProp = GetPrimaryKey(typeof(T));
            var currentPkVal = pkProp.GetValue(entity);
            if (IsDefault(currentPkVal, pkProp.PropertyType))
            {
                var targetType = Nullable.GetUnderlyingType(pkProp.PropertyType) ?? pkProp.PropertyType;
                pkProp.SetValue(entity, Convert.ChangeType(id, targetType));
            }
            else
            {
                var entityIdInt = Convert.ToInt32(currentPkVal);
                if (id != entityIdInt) return BadRequest("ID mismatch.");
            }
            await _repository.UpdateAsync(entity);
            return NoContent();
        }

        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            await _repository.DeleteAsync(entity);
            return NoContent();
        }

        private static PropertyInfo GetPrimaryKey(Type type)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var typeNameId = type.Name + "Id";
            var idProp = props.FirstOrDefault(p => p.Name.Equals(typeNameId, StringComparison.OrdinalIgnoreCase))
                        ?? props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                        ?? props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            if (idProp == null)
            {
                throw new InvalidOperationException($"No primary key property found on {type.Name}.");
            }
            return idProp;
        }

        private static bool IsDefault(object? value, Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (value == null) return true;
            var defaultVal = t.IsValueType ? Activator.CreateInstance(t) : null;
            return Equals(value, defaultVal);
        }

        private static bool IsMarkedDeleted(object? value, Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (value == null) return false;
            if (t == typeof(bool)) return (bool)value;
            if (t == typeof(byte) || t == typeof(short) || t == typeof(int)) return Convert.ToInt32(value) != 0;
            if (t == typeof(long)) return Convert.ToInt64(value) != 0L;
            return false;
        }

        private static bool IsEntityDeleted(T entity)
        {
            var prop = typeof(T).GetProperty("IsDeleted", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;
            var val = prop.GetValue(entity);
            return IsMarkedDeleted(val, prop.PropertyType);
        }

        private static IEnumerable<T> FilterNotDeleted(IEnumerable<T> entities)
        {
            var prop = typeof(T).GetProperty("IsDeleted", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return entities;
            return entities.Where(e => !IsMarkedDeleted(prop.GetValue(e), prop.PropertyType));
        }
    }
}
using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Reflection;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenericController<T> : ControllerBase where T : BaseEntity
    {
        private readonly IGenericRepository<T> _repository;

        public GenericController(IGenericRepository<T> repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var entities = await _repository.GetAllAsync();
            var filtered = FilterNotDeleted(entities);
            return Ok(filtered);
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
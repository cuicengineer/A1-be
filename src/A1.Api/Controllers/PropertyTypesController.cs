using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{
    /// <summary>
    /// PropertyTypes Controller for managing property type records
    /// 
    /// GET /api/PropertyTypes - Get all property types (only non-deleted)
    /// GET /api/PropertyTypes/{id} - Get property type by ID
    /// POST /api/PropertyTypes - Create a new property type
    /// PUT /api/PropertyTypes/{id} - Update a property type
    /// DELETE /api/PropertyTypes/{id} - Soft delete a property type (sets IsDeleted = true)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PropertyTypesController : ControllerBase
    {
        private readonly IGenericRepository<PropertyType> _repository;
        private readonly ApplicationDbContext _context;

        public PropertyTypesController(IGenericRepository<PropertyType> repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>
        /// GET: Get all property types (only returns records where IsDeleted = 0 or null)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var propertyTypes = await _context.PropertyTypes
                .AsNoTracking()
                .Where(pt => pt.IsDeleted == null || pt.IsDeleted == false)
                .OrderByDescending(pt => pt.Id)
                .ToListAsync();
            return Ok(propertyTypes);
        }

        /// <summary>
        /// GET: Get property type by ID (only returns if IsDeleted = 0 or null)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var propertyType = await _context.PropertyTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.Id == id && (pt.IsDeleted == null || pt.IsDeleted == false));

            if (propertyType == null)
            {
                return NotFound();
            }

            return Ok(propertyType);
        }

        /// <summary>
        /// POST: Create a new property type (sets IsDeleted = false by default)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PropertyType propertyType)
        {
            if (propertyType == null)
            {
                return BadRequest("Property type data is required.");
            }

            // Set IsDeleted = false by default
            propertyType.IsDeleted = false;
            propertyType.ActionDate = DateTime.UtcNow;
            propertyType.Action = "CREATE";
            // ActionBy comes from payload

            await _repository.AddAsync(propertyType);
            return CreatedAtAction(nameof(GetById), new { id = propertyType.Id }, propertyType);
        }

        /// <summary>
        /// PUT: Update an existing property type
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PropertyType propertyType)
        {
            if (propertyType == null)
            {
                return BadRequest("Property type data is required.");
            }

            // If propertyType.Id is not set (0), use the route parameter id
            if (propertyType.Id == 0)
            {
                propertyType.Id = id;
            }
            else if (id != propertyType.Id)
            {
                return BadRequest("ID mismatch.");
            }

            // Check if property type exists and is not deleted
            var existingPropertyType = await _context.PropertyTypes
                .FirstOrDefaultAsync(pt => pt.Id == id && (pt.IsDeleted == null || pt.IsDeleted == false));

            if (existingPropertyType == null)
            {
                return NotFound("Property type not found.");
            }

            // Update properties
            existingPropertyType.Name = propertyType.Name;
            existingPropertyType.Status = propertyType.Status;
            existingPropertyType.ActionDate = DateTime.UtcNow;
            existingPropertyType.Action = "UPDATE";
            existingPropertyType.ActionBy = propertyType.ActionBy;

            await _repository.UpdateAsync(existingPropertyType);
            return NoContent();
        }

        /// <summary>
        /// DELETE: Soft delete a property type (sets IsDeleted = true)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var propertyType = await _context.PropertyTypes
                .FirstOrDefaultAsync(pt => pt.Id == id && (pt.IsDeleted == null || pt.IsDeleted == false));

            if (propertyType == null)
            {
                return NotFound("Property type not found.");
            }

            // Soft delete - set IsDeleted = true
            propertyType.IsDeleted = true;
            propertyType.Action = "DELETE";
            propertyType.ActionDate = DateTime.UtcNow;
            // ActionBy should come from payload if provided, otherwise keep existing value
            if (string.IsNullOrWhiteSpace(propertyType.ActionBy))
            {
                // If payload doesn't have ActionBy, preserve existing value
                var existingActionBy = await _context.PropertyTypes
                    .AsNoTracking()
                    .Where(pt => pt.Id == id)
                    .Select(pt => pt.ActionBy)
                    .FirstOrDefaultAsync();
                propertyType.ActionBy = existingActionBy;
            }

            _context.PropertyTypes.Update(propertyType);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}


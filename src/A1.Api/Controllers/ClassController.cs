using A1.Api.Models;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace A1.Api.Controllers
{
    [Route("api/Class")]
    [ApiController]
    public class ClassController : ControllerBase
    {
        private readonly IClassService _classService;

        public ClassController(IClassService classService)
        {
            _classService = classService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var classes = await _classService.GetAllAsync();
            return Ok(classes);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _classService.GetByIdAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            return Ok(entity);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Class entity)
        {
            var validationError = _classService.ValidateClass(entity);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);
            var created = await _classService.CreateAsync(entity, actionBy);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Class entity)
        {
            if (entity == null)
            {
                return BadRequest("Class data is required.");
            }

            if (entity.Id == 0)
            {
                entity.Id = id;
            }
            else if (id != entity.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = _classService.ValidateClass(entity);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            try
            {
                var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, entity.ActionBy);
                await _classService.UpdateAsync(entity, actionBy);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Class not found.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, null);
            var deleted = await _classService.SoftDeleteAsync(id, actionBy);
            if (!deleted)
            {
                return NotFound("Class not found.");
            }

            return NoContent();
        }
    }
}

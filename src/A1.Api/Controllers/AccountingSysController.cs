using A1.Api.Models;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountingSysController : ControllerBase
    {
        private readonly IAccountingSysService _service;

        public AccountingSysController(IAccountingSysService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _service.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AccountingSys model)
        {
            var validationError = _service.Validate(model);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            try
            {
                var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, model.ActionBy);
                var created = await _service.CreateAsync(model, actionBy);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AccountingSys model)
        {
            if (model == null)
            {
                return BadRequest("Accounting system data is required.");
            }

            if (model.Id == 0)
            {
                model.Id = id;
            }
            else if (id != model.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var validationError = _service.Validate(model);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            try
            {
                var actionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, model.ActionBy);
                await _service.UpdateAsync(model, actionBy);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}

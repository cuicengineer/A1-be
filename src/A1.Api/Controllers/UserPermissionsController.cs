using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserPermissionsController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new();
        private readonly IGenericRepository<UserPermission> _repository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public UserPermissionsController(IGenericRepository<UserPermission> repository, ApplicationDbContext context, IConfiguration configuration)
        {
            _repository = repository;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("GetInfo/{pakno}")]
        public async Task<IActionResult> GetInfo(string pakno)
        {
            if (string.IsNullOrWhiteSpace(pakno))
            {
                return BadRequest("pakno is required.");
            }

            var endpoint = _configuration["ExternalApis:GetInfoUrl"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return StatusCode(500, "GetInfo API URL is not configured.");
            }

            var payload = JsonSerializer.Serialize(new { pakno });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, responseBody);
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return Ok();
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(responseBody);
                return Ok(jsonDocument.RootElement.Clone());
            }
            catch (JsonException)
            {
                return Content(responseBody, response.Content.Headers.ContentType?.ToString() ?? "text/plain");
            }
        }

        [HttpGet("ByUser/{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            if (userId <= 0)
            {
                return BadRequest("Valid userId is required.");
            }

            var items = await _context.UserPermissions
                .AsNoTracking()
                .Where(x => x.UserId == userId && (x.IsDeleted == null || x.IsDeleted == false))
                .OrderBy(x => x.MenuName)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserPermission item)
        {
            if (item == null)
            {
                return BadRequest("User permission data is required.");
            }

            var normalizedMenuName = (item.MenuName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedMenuName))
            {
                return BadRequest("MenuName is required.");
            }

            var existingRights = await _context.UserPermissions
                .IgnoreQueryFilters()
                .Where(x => x.UserId == item.UserId && x.MenuName == normalizedMenuName)
                .ToListAsync();
            if (existingRights.Count > 0)
            {
                _context.UserPermissions.RemoveRange(existingRights);
                await _context.SaveChangesAsync();
            }

            item.MenuName = normalizedMenuName;
            item.IsDeleted = false;
            item.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.AddAsync(item);
            return CreatedAtAction(nameof(GetByUserId), new { userId = item.UserId }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserPermission item)
        {
            if (item == null)
            {
                return BadRequest("User permission data is required.");
            }

            if (item.Id == 0)
            {
                item.Id = id;
            }
            else if (id != item.Id)
            {
                return BadRequest("ID mismatch.");
            }

            var existing = await _context.UserPermissions
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                return NotFound("User permission not found.");
            }

            existing.UserId = item.UserId;
            existing.MenuName = item.MenuName;
            existing.CanView = item.CanView;
            existing.CanCreate = item.CanCreate;
            existing.CanEdit = item.CanEdit;
            existing.CanDelete = item.CanDelete;
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, item.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.UserPermissions
                .FirstOrDefaultAsync(x => x.Id == id);

            if (existing == null)
            {
                return NotFound("User permission not found.");
            }

            _context.UserPermissions.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}

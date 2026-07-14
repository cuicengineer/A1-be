using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NoticesController : ControllerBase
    {
        public const int MaxPlainTextLength = 1000;
        public const int MaxExcludedUsers = 5;

        private readonly IGenericRepository<Notice> _repository;
        private readonly ApplicationDbContext _context;

        public NoticesController(
            IGenericRepository<Notice> repository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        /// <summary>Returns the single notice row (0 or 1).</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.Notices
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(1)
                .ToListAsync();

            return Ok(rows);
        }

        /// <summary>
        /// Active notice for login popup.
        /// Empty when none, inactive, or current user is on the exclude list.
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var notice = await _context.Notices
                .AsNoTracking()
                .Where(x => x.Status)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (notice == null)
            {
                return Ok(new { contentHtml = "", status = false, excludedUserIds = Array.Empty<int>() });
            }

            var excludedIds = Notice.ParseExcludedUserIds(notice.ExcludedUserIdsJson);
            var currentUserId = TryGetCurrentUserId();
            if (currentUserId.HasValue && excludedIds.Contains(currentUserId.Value))
            {
                return Ok(new { contentHtml = "", status = false, excludedUserIds = excludedIds });
            }

            return Ok(new
            {
                notice.Id,
                notice.ContentHtml,
                notice.Status,
                notice.ExcludedUserIdsJson,
                excludedUserIds = excludedIds,
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var row = await _context.Notices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
            {
                return NotFound();
            }

            return Ok(row);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NoticeUpsertRequest request)
        {
            if (!await CanManageNoticeAsync())
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest("Notice data is required.");
            }

            var existingCount = await _context.Notices.CountAsync();
            if (existingCount > 0)
            {
                return BadRequest("A notice already exists. Use edit instead.");
            }

            var validationError = ValidateContent(request.ContentHtml);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var excludedError = await ValidateExcludedUserIdsAsync(request.ExcludedUserIds);
            if (excludedError != null)
            {
                return BadRequest(excludedError);
            }

            var entity = new Notice
            {
                ContentHtml = NormalizeContent(request.ContentHtml),
                Status = request.Status,
                ExcludedUserIdsJson = Notice.SerializeExcludedUserIds(request.ExcludedUserIds),
                IsDeleted = false,
                ActionDate = DateTime.UtcNow,
                Action = "CREATE",
                ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, request.ActionBy),
            };

            await _repository.AddAsync(entity);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] NoticeUpsertRequest request)
        {
            if (!await CanManageNoticeAsync())
            {
                return Forbid();
            }

            if (request == null)
            {
                return BadRequest("Notice data is required.");
            }

            var existing = await _context.Notices.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return NotFound("Notice not found.");
            }

            var validationError = ValidateContent(request.ContentHtml);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var excludedError = await ValidateExcludedUserIdsAsync(request.ExcludedUserIds);
            if (excludedError != null)
            {
                return BadRequest(excludedError);
            }

            existing.ContentHtml = NormalizeContent(request.ContentHtml);
            existing.Status = request.Status;
            existing.ExcludedUserIdsJson = Notice.SerializeExcludedUserIds(request.ExcludedUserIds);
            existing.ActionDate = DateTime.UtcNow;
            existing.Action = "UPDATE";
            existing.ActionBy = ActionByHelper.GetActionByWithIp(User, HttpContext, request.ActionBy);

            await _repository.UpdateAsync(existing);
            return NoContent();
        }

        private async Task<string?> ValidateExcludedUserIdsAsync(IEnumerable<int>? excludedUserIds)
        {
            var ids = (excludedUserIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count > MaxExcludedUsers)
            {
                return $"At most {MaxExcludedUsers} users can be excluded.";
            }

            if (ids.Count == 0) return null;

            var activeCount = await _context.Users
                .AsNoTracking()
                .CountAsync(u =>
                    ids.Contains(u.Id) &&
                    (u.IsDeleted == null || u.IsDeleted == false) &&
                    u.Status == 1);

            if (activeCount != ids.Count)
            {
                return "One or more excluded users are invalid or inactive.";
            }

            return null;
        }

        private int? TryGetCurrentUserId()
        {
            var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claim, out var id) && id > 0) return id;
            return null;
        }

        private static string? ValidateContent(string? contentHtml)
        {
            var plain = StripHtmlToPlainText(contentHtml);
            if (string.IsNullOrWhiteSpace(plain))
            {
                return "Notice text is required.";
            }

            if (plain.Length > MaxPlainTextLength)
            {
                return $"Notice text must be {MaxPlainTextLength} characters or fewer.";
            }

            return null;
        }

        private static string NormalizeContent(string? contentHtml)
        {
            return string.IsNullOrWhiteSpace(contentHtml) ? "" : contentHtml.Trim();
        }

        private static string StripHtmlToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var noTags = Regex.Replace(html, "<[^>]+>", " ");
            var decoded = System.Net.WebUtility.HtmlDecode(noTags);
            return Regex.Replace(decoded ?? "", @"\s+", " ").Trim();
        }

        private static bool IsLoginSuperuser(ClaimsPrincipal user)
        {
            var loginName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity?.Name;
            return string.Equals(loginName?.Trim(), "superuser", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanManageNoticeAsync()
        {
            if (IsLoginSuperuser(User))
            {
                return true;
            }

            return await DataAccessScopeHelper.IsAhqSupervisorAsync(User, _context);
        }
    }

    public class NoticeUpsertRequest
    {
        public string? ContentHtml { get; set; }
        public bool Status { get; set; } = true;
        public List<int>? ExcludedUserIds { get; set; }
        public string? ActionBy { get; set; }
    }
}

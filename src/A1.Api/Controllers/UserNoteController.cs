using A1.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A1.Api.Controllers
{

// https://localhost:5101/api/UserNote/emp_1042 First Search by User ID
// https://localhost:5101/api/UserNote/-1   UPSERT on Save Button
// CREATE NONCLUSTERED INDEX IX_UserNotes_UpdatedAt   -- Non clustured index for quick searching
// ON UserNotes (UpdatedAt DESC);


    // 
    [Route("api/[controller]")]
    [ApiController]
    public class UserNoteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserNoteController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var baseQuery = _context.UserNotes.AsNoTracking();

            var totalCount = await baseQuery.CountAsync();
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var notes = await baseQuery
                .OrderByDescending(n => n.UpdatedAt ?? DateTime.MinValue)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(notes);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var note = await _context.UserNotes.AsNoTracking().FirstOrDefaultAsync(n => n.UserId == id);
            if (note == null) return NotFound();
            return Ok(note);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserNote note)
        {
            if (note == null) return BadRequest("Note payload is required.");

            note.UpdatedAt = DateTime.UtcNow;
            _context.UserNotes.Add(note);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserNote note)
        {
            if (note == null)
                return BadRequest("Note payload is required.");

            // 🔹 CREATE when id == -1
            if (id == -1)
            {
                note.Id = 0; // ensure EF treats it as new
                note.UpdatedAt = DateTime.UtcNow;

                _context.UserNotes.Add(note);
                await _context.SaveChangesAsync();

                return Ok(note); // return newly created note
            }

            // 🔹 UPDATE flow
            if (note.Id != 0 && note.Id != id)
                return BadRequest("ID mismatch.");

            var existing = await _context.UserNotes
                .FirstOrDefaultAsync(n => n.Id == id);

            if (existing == null)
                return NotFound();

            existing.UserId = note.UserId;
            existing.Content = note.Content;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(existing); // return updated note
        }

            

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.UserNotes.FirstOrDefaultAsync(n => n.Id == id);
            if (existing == null) return NotFound();

            _context.UserNotes.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}


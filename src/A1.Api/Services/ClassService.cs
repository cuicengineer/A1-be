using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Services
{
    public class ClassService : IClassService
    {
        private static readonly string[] AllowedUoM = { "Marla", "Sq Ft", "Acre" };

        private readonly ApplicationDbContext _context;
        private readonly IGenericRepository<Class> _repository;

        public ClassService(ApplicationDbContext context, IGenericRepository<Class> repository)
        {
            _context = context;
            _repository = repository;
        }

        public IReadOnlyList<string> AllowedUoMValues => AllowedUoM;

        public string? ValidateClass(Class entity)
        {
            if (entity == null)
            {
                return "Class data is required.";
            }

            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                return "Name is required.";
            }

            if (string.IsNullOrWhiteSpace(entity.UoM) || !AllowedUoM.Contains(entity.UoM.Trim()))
            {
                return "UoM is required and must be one of: Marla, Sq Ft, Acre.";
            }

            return null;
        }

        public async Task<IReadOnlyList<Class>> GetAllAsync()
        {
            return await _context.Classes
                .AsNoTracking()
                .Where(c => c.IsDeleted == null || c.IsDeleted == false)
                .OrderByDescending(c => c.Id)
                .ToListAsync();
        }

        public async Task<Class?> GetByIdAsync(int id)
        {
            return await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));
        }

        public async Task<Class> CreateAsync(Class entity, string? actionBy)
        {
            entity.UoM = (entity.UoM ?? string.Empty).Trim();
            entity.Code = string.IsNullOrWhiteSpace(entity.Code) ? string.Empty : entity.Code.Trim().ToUpperInvariant();
            entity.IsDeleted = false;
            entity.Action = "CREATE";
            entity.ActionBy = actionBy;
            entity.ActionDate = DateTime.UtcNow;

            await _repository.AddAsync(entity);
            return entity;
        }

        public async Task UpdateAsync(Class entity, string? actionBy)
        {
            var existing = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == entity.Id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existing == null)
            {
                throw new KeyNotFoundException("Class not found.");
            }

            existing.Code = string.IsNullOrWhiteSpace(entity.Code) ? string.Empty : entity.Code.Trim().ToUpperInvariant();
            existing.Name = entity.Name;
            existing.Description = entity.Description;
            existing.UoM = (entity.UoM ?? string.Empty).Trim();
            existing.Status = entity.Status;
            existing.Action = "UPDATE";
            existing.ActionBy = actionBy;
            existing.ActionDate = DateTime.UtcNow;

            await _repository.UpdateAsync(existing);
        }

        public async Task<bool> SoftDeleteAsync(int id, string? actionBy)
        {
            var existing = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == id && (c.IsDeleted == null || c.IsDeleted == false));

            if (existing == null)
            {
                return false;
            }

            existing.IsDeleted = true;
            existing.Action = "DELETE";
            existing.ActionBy = actionBy;
            existing.ActionDate = DateTime.UtcNow;

            _context.Classes.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

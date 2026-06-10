using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace A1.Api.Services
{
    public class AccountingSysService : IAccountingSysService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGenericRepository<AccountingSys> _repository;

        public AccountingSysService(ApplicationDbContext context, IGenericRepository<AccountingSys> repository)
        {
            _context = context;
            _repository = repository;
        }

        public string? Validate(AccountingSys entity)
        {
            if (entity == null)
            {
                return "Accounting system data is required.";
            }

            if (string.IsNullOrWhiteSpace(entity.ParticularName))
            {
                return "Particular Name is required.";
            }

            if (string.IsNullOrWhiteSpace(entity.Address))
            {
                return "Address is required.";
            }

            if (string.IsNullOrWhiteSpace(entity.TelNo))
            {
                return "Tel No. is required.";
            }

            return null;
        }

        public async Task<IReadOnlyList<AccountingSys>> GetAllAsync()
        {
            return await _context.AccountingSys
                .AsNoTracking()
                .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                .OrderByDescending(x => x.Id)
                .Take(1)
                .ToListAsync();
        }

        public async Task<AccountingSys?> GetByIdAsync(int id)
        {
            return await _context.AccountingSys
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && (x.IsDeleted == null || x.IsDeleted == false));
        }

        public async Task<AccountingSys> CreateAsync(AccountingSys entity, string? actionBy)
        {
            var hasActive = await _context.AccountingSys
                .AnyAsync(x => x.IsDeleted == null || x.IsDeleted == false);

            if (hasActive)
            {
                throw new InvalidOperationException("Only one accounting system record is allowed.");
            }

            entity.ParticularName = entity.ParticularName.Trim();
            entity.Address = entity.Address?.Trim();
            entity.TelNo = entity.TelNo?.Trim();
            entity.IsDeleted = false;
            entity.Action = "CREATE";
            entity.ActionBy = actionBy;
            entity.ActionDate = DateTime.UtcNow;

            await _repository.AddAsync(entity);
            return entity;
        }

        public async Task UpdateAsync(AccountingSys entity, string? actionBy)
        {
            var existing = await _context.AccountingSys
                .FirstOrDefaultAsync(x => x.Id == entity.Id && (x.IsDeleted == null || x.IsDeleted == false));

            if (existing == null)
            {
                throw new KeyNotFoundException("Accounting system record not found.");
            }

            existing.ParticularName = entity.ParticularName.Trim();
            existing.Address = entity.Address?.Trim();
            existing.TelNo = entity.TelNo?.Trim();
            existing.Action = "UPDATE";
            existing.ActionBy = actionBy;
            existing.ActionDate = DateTime.UtcNow;

            await _repository.UpdateAsync(existing);
        }
    }
}

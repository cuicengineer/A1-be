using System;
using A1.Api.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using A1.Api.Utilities;

namespace A1.Api.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        private readonly ApplicationDbContext _context;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task<IEnumerable<T>> GetAllAsync() => await _context.Set<T>().ToListAsync();

        public async Task<T?> GetByIdAsync(int id) => await _context.Set<T>().FindAsync(id);

        public async Task AddAsync(T entity)
        {
            // If creating a User, hash the provided plain password and ensure salt/iterations
            if (entity is User user)
            {
                if (!string.IsNullOrWhiteSpace(user.PlainPassword))
                {
                    PasswordHasher.Hash(user.PlainPassword, out var hash, out var salt, out var iterations);
                    user.Password = hash;
                    user.PasswordSalt = salt;
                    user.PasswordIterations = iterations;
                }
                else if (user.PasswordSalt == null || user.PasswordSalt.Length == 0)
                {
                    // If no password provided, still ensure a salt exists
                    user.PasswordSalt = RandomNumberGenerator.GetBytes(16);
                }

                if (user.PasswordAttempts < 0)
                {
                    user.PasswordAttempts = 0;
                }
            }

            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "CREATE";
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            // Handle user password/salt updates safely
            if (entity is User user)
            {
                var existing = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);

                if (!string.IsNullOrWhiteSpace(user.PlainPassword))
                {
                    PasswordHasher.Hash(user.PlainPassword, out var hash, out var salt, out var iterations);
                    user.Password = hash;
                    user.PasswordSalt = salt;
                    user.PasswordIterations = iterations;
                    user.PasswordAttempts = 0;
                }
                else if (existing != null)
                {
                    // Preserve existing password info if no new password was provided
                    user.Password = existing.Password;
                    user.PasswordSalt = existing.PasswordSalt;
                    user.PasswordIterations = existing.PasswordIterations;
                    user.PasswordAttempts = user.PasswordAttempts < 0 ? 0 : user.PasswordAttempts;

                    // Preserve refresh token fields if not provided
                    if (string.IsNullOrWhiteSpace(user.RefreshToken))
                    {
                        user.RefreshToken = existing.RefreshToken;
                        user.RefreshTokenExpiresAt = existing.RefreshTokenExpiresAt;
                    }
                }
            }

            entity.ActionDate = DateTime.UtcNow;
            entity.Action = "UPDATE";
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            entity.IsDeleted = true;
            entity.Action = "DELETE";
            entity.ActionDate = DateTime.UtcNow;
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync();
        }
    }
}
using A1.Api.Models;

namespace A1.Api.Services
{
    public interface IClassService
    {
        IReadOnlyList<string> AllowedUoMValues { get; }

        string? ValidateClass(Class entity);

        Task<IReadOnlyList<Class>> GetAllAsync();

        Task<Class?> GetByIdAsync(int id);

        Task<Class> CreateAsync(Class entity, string? actionBy);

        Task UpdateAsync(Class entity, string? actionBy);

        Task<bool> SoftDeleteAsync(int id, string? actionBy);
    }
}

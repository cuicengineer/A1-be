using A1.Api.Models;

namespace A1.Api.Services
{
    public interface IAccountingSysService
    {
        string? Validate(AccountingSys entity);

        Task<IReadOnlyList<AccountingSys>> GetAllAsync();

        Task<AccountingSys?> GetByIdAsync(int id);

        Task<AccountingSys> CreateAsync(AccountingSys entity, string? actionBy);

        Task UpdateAsync(AccountingSys entity, string? actionBy);
    }
}

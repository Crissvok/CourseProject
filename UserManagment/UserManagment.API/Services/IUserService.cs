using UserManagement.Data;

namespace UserManagment.API.Services
{
    public interface IUserService
    {
        Task<(string? Token, User? User, string? Error)> LoginAsync(string username, string password);
        Task<IEnumerable<User>> GetAllAsync(string? search = null, bool? isActive = null, string? role = null);
        Task<User?> GetByIdAsync(int id);
        Task<(User? User, string? Error)> CreateAsync(User user);
        Task<(User? User, string? Error)> UpdateAsync(int id, User updatedUser);
        Task<(bool Success, string? Error)> ChangeRoleAsync(int id, string newRole);
        Task<(bool Success, string? Error)> SetActiveStatusAsync(int id, bool isActive);
        Task<(bool Success, string? Error)> HardDeleteAsync(int id);
        Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int count = 50);
    }
}

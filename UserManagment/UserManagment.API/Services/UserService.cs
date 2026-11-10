using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserManagement.Data;

namespace UserManagment.API.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public UserService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<(string? Token, User? User, string? Error)> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (null, null, "Укажите имя пользователя и пароль.");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Name == username && u.Password == password);

            if (user == null)
                return (null, null, "Неверное имя пользователя или пароль.");

            if (!user.IsActive)
                return (null, null, "Учетная запись заблокирована.");

            var token = GenerateJwtToken(user);
            return (token, user, null);
        }

        public async Task<IEnumerable<User>> GetAllAsync(string? search = null, bool? isActive = null, string? role = null)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            return await query.ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<(User? User, string? Error)> CreateAsync(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Name) || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
                return (null, "Имя, Email и пароль обязательны для заполнения.");

            if (await _context.Users.AnyAsync(u => u.Name == user.Name))
                return (null, $"Пользователь с логином '{user.Name}' уже существует.");

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                return (null, $"Email '{user.Email}' уже занят.");

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;
            if (string.IsNullOrWhiteSpace(user.Role))
            {
                user.Role = "User";
            }

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return (user, null);
        }

        public async Task<(User? User, string? Error)> UpdateAsync(int id, User updatedUser)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return (null, $"Пользователь с ID {id} не найден.");

            if (string.IsNullOrWhiteSpace(updatedUser.Name) || string.IsNullOrWhiteSpace(updatedUser.Email))
                return (null, "Имя и Email не могут быть пустыми.");

            if (await _context.Users.AnyAsync(u => u.Name == updatedUser.Name && u.Id != id))
                return (null, $"Имя '{updatedUser.Name}' уже используется другим пользователем.");

            if (await _context.Users.AnyAsync(u => u.Email == updatedUser.Email && u.Id != id))
                return (null, $"Email '{updatedUser.Email}' уже используется другим пользователем.");

            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;

            if (!string.IsNullOrWhiteSpace(updatedUser.Password))
                user.Password = updatedUser.Password;

            await _context.SaveChangesAsync();
            return (user, null);
        }

        public async Task<(bool Success, string? Error)> ChangeRoleAsync(int id, string newRole)
        {
            if (string.IsNullOrWhiteSpace(newRole))
                return (false, "Роль не может быть пустой.");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return (false, $"Пользователь с ID {id} не найден.");

            user.Role = newRole;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SetActiveStatusAsync(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return (false, $"Пользователь с ID {id} не найден.");

            user.IsActive = isActive;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> HardDeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return (false, $"Пользователь с ID {id} не найден.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForUserManagementCourseProject2026!";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "UserManagementAPI";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "UserManagementClient";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(4),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

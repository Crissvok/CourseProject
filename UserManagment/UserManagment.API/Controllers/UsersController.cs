using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;

namespace UserManagment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetById(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            return Ok(user);
        }

        [HttpPost]
        public async Task<ActionResult<User>> Create([FromBody] User user)
        {
            if (string.IsNullOrWhiteSpace(user.Name) || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new { message = "Имя, Email и пароль обязательны для заполнения." });
            }

            if (await _context.Users.AnyAsync(u => u.Name == user.Name))
                return Conflict(new { message = $"Пользователь с логином '{user.Name}' уже существует." });

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                return Conflict(new { message = $"Email '{user.Email}' уже занят." });
            
            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] User updatedUser)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = $"Пользователь с ID {id} не найден." });

            if (string.IsNullOrWhiteSpace(updatedUser.Name) || string.IsNullOrWhiteSpace(updatedUser.Email))
                return BadRequest(new { message = "Имя и Email не могут быть пустыми." });

            if (await _context.Users.AnyAsync(u => u.Name == updatedUser.Name && u.Id != id))
                return Conflict(new { message = $"Имя '{updatedUser.Name}' уже используется другим пользователем." });

            if (await _context.Users.AnyAsync(u => u.Email == updatedUser.Email && u.Id != id))
                return Conflict(new { message = $"Email '{updatedUser.Email}' уже используется другим пользователем." });

            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;

            if (!string.IsNullOrWhiteSpace(updatedUser.Password))
                user.Password = updatedUser.Password;

            await _context.SaveChangesAsync();
            return Ok(user);
        }
        [HttpPut("{id}/block")] //Блокировка
        public async Task<IActionResult> Deactivate(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден." });
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Пользователь {user.Name} успешно заблокирован (деактивирован)." });
        }
        [HttpPut("{id}/unblock")] //Разблокировка
        public async Task<IActionResult> UnblockUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден." });
            }

            user.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Пользователь {user.Name} успешно разблокирован." });
        }
        [HttpDelete("{id}")] // Полное удаление
        public async Task<IActionResult> HardDelete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден." });
            }

            // Полное стирание строки из базы данных (MySQL / SQLite)
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Пользователь {user.Name} полностью удален из базы данных." });
        }
    }
}
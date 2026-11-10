using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Data;
using UserManagment.API.Services;
using Microsoft.EntityFrameworkCore;

namespace UserManagment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }
        // Авторизация
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (token, user, error) = await _userService.LoginAsync(request.Username, request.Password);
            if (error != null)
            {
                if (error.Contains("заблокирована"))
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = error });

                return Unauthorized(new { message = error });
            }

            return Ok(new
            {
                token,
                user = new
                {
                    user!.Id,
                    user.Name,
                    user.Email,
                    user.Role,
                    user.IsActive
                }
            });
        }
        //Общий Get
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] string? role)
        {
            var users = await _userService.GetAllAsync(search, isActive, role);
            return Ok(users);
        }
        //Get по Id
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetById(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = $"Пользователь с ID {id} не найден." });

            return Ok(user);
        }
        //Создание учётки
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<User>> Create([FromBody] User user)
        {
            var (createdUser, error) = await _userService.CreateAsync(user);
            if (error != null)
            {
                if (error.Contains("существует") || error.Contains("занят"))
                    return Conflict(new { message = error });

                return BadRequest(new { message = error });
            }

            return CreatedAtAction(nameof(GetById), new { id = createdUser!.Id }, createdUser);
        }
        //Изменение учётки
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] User updatedUser)
        {
            var (user, error) = await _userService.UpdateAsync(id, updatedUser);
            if (error != null)
            {
                if (error.Contains("не найден"))
                    return NotFound(new { message = error });

                if (error.Contains("используется"))
                    return Conflict(new { message = error });

                return BadRequest(new { message = error });
            }

            return Ok(user);
        }
        //Смена роли
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
        {
            var (success, error) = await _userService.ChangeRoleAsync(id, request.Role);
            if (!success)
            {
                if (error != null && error.Contains("не найден"))
                    return NotFound(new { message = error });

                return BadRequest(new { message = error });
            }

            return Ok(new { message = $"Роль пользователя с ID {id} успешно изменена на '{request.Role}'." });
        }
        //Блокировка учётки
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/block")]
        public async Task<IActionResult> Block(int id)
        {
            var (success, error) = await _userService.SetActiveStatusAsync(id, isActive: false);
            if (!success)
                return NotFound(new { message = error });

            return Ok(new { message = $"Пользователь с ID {id} успешно заблокирован." });
        }
        //Разблокировка учётки
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/unblock")]
        public async Task<IActionResult> Unblock(int id)
        {
            var (success, error) = await _userService.SetActiveStatusAsync(id, isActive: true);
            if (!success)
                return NotFound(new { message = error });

            return Ok(new { message = $"Пользователь с ID {id} успешно разблокирован." });
        }
        //Полное удаление учётки пользователь
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> HardDelete(int id)
        {
            var (success, error) = await _userService.HardDeleteAsync(id);
            if (!success)
                return NotFound(new { message = error });

            return Ok(new { message = $"Пользователь с ID {id} полностью удален из базы данных." });
        }
        //Просомтр логов
        [Authorize(Roles = "Admin")]
        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs([FromServices] AppDbContext context)
        {
            var logs = await context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(50)
                .ToListAsync<AuditLog>();
            return Ok(logs);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ChangeRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}

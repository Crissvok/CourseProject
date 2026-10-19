using Microsoft.AspNetCore.Mvc;
using UserManagement.Data;

namespace UserManagment.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    
    private static readonly List<User> _users = new()
    {
        new User { Id = 1, Name = "admin", Email = "admin@system.ru", Password = "hash_123", CreatedAt = DateTime.UtcNow, IsActive = true },
        new User { Id = 2, Name = "ivan_petrov", Email = "ivan@mail.ru", Password = "hash_456", CreatedAt = DateTime.UtcNow, IsActive = true }
    };

    
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetUsers()
    {
        return Ok(_users);
    }

    
    [HttpGet("{id}")]
    public ActionResult<User> GetUser(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return NotFound("Пользователь не найден");
        }
        return Ok(user);
    }
}

using Microsoft.AspNetCore.Mvc;

namespace userservice.Controllers
{
    public class CreateUserRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        // GET https://localhost:7075/api/users
        [HttpGet]
        public IActionResult GetAll()
        {
            var users = new[]
            {
                new { Id = 1, Name = "Alice", Email = "alice@example.com" },
                new { Id = 2, Name = "Bob",   Email = "bob@example.com" }
            };

            return Ok(users);
        }

        // POST https://localhost:7075/api/users
        [HttpPost]
        public IActionResult Create([FromBody] CreateUserRequest request)
        {
            var createdUser = new
            {
                Id = 3,
                Name = request.Name,
                Email = request.Email
            };

            return Created(string.Empty, createdUser);
        }
    }
}
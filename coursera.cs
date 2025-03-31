// Model/User.cs
using System.ComponentModel.DataAnnotations;

namespace UserManagementApp.Models
{
    // Model for User with Data Annotations for validation
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
        public int Age { get; set; }
    }
}

// Service/IUserService.cs
using System.Collections.Generic;
using UserManagementApp.Models;

namespace UserManagementApp.Services
{
    // Service for User Management (Dependency Injection)
    public interface IUserService
    {
        IEnumerable<User> GetUsers();
        User GetUserById(int id);
        void AddUser(User user);
        void UpdateUser(int id, User user);
        void DeleteUser(int id);
    }
}

// Service/UserService.cs
using System;
using System.Collections.Generic;
using UserManagementApp.Models;

namespace UserManagementApp.Services
{
    public class UserService : IUserService
    {
        private readonly List<User> _users = new List<User>();
        private int _nextId = 1;

        public UserService()
        {
            // Initial dummy data
            _users.Add(new User { Id = _nextId++, Name = "John Doe", Email = "john.doe@example.com", Age = 30 });
            _users.Add(new User { Id = _nextId++, Name = "Jane Smith", Email = "jane.smith@example.com", Age = 25 });
        }

        public IEnumerable<User> GetUsers()
        {
            return _users;
        }

        public User GetUserById(int id)
        {
            return _users.Find(u => u.Id == id);
        }

        public void AddUser(User user)
        {
            user.Id = _nextId++;
            _users.Add(user);
        }

        public void UpdateUser(int id, User user)
        {
            var existingUser = _users.Find(u => u.Id == id);
            if (existingUser != null)
            {
                // Update only provided fields.  This is a good practice.
                existingUser.Name = user.Name ?? existingUser.Name; // null-coalescing assignment
                existingUser.Email = user.Email ?? existingUser.Email;
                existingUser.Age = user.Age != 0 ? user.Age : existingUser.Age; // 0 is not a valid age, so don't update if 0
            }
            else
            {
                throw new KeyNotFoundException($"User with id {id} not found"); //Use KeyNotFoundException
            }
        }

        public void DeleteUser(int id)
        {
            var userToRemove = _users.Find(u => u.Id == id);
            if (userToRemove != null)
            {
                _users.Remove(userToRemove);
            }
            else
            {
                 throw new KeyNotFoundException($"User with id {id} not found");
            }
        }
    }
}

// Controller/UserController.cs
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UserManagementApp.Models;
using UserManagementApp.Services;
using System;

namespace UserManagementApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // GET all users
        [HttpGet]
        public IActionResult GetUsers()
        {
            _logger.LogInformation("Getting all users");
            var users = _userService.GetUsers();
            return Ok(users);
        }

        // GET user by id
        [HttpGet("{id}")]
        public IActionResult GetUserById(int id)
        {
            _logger.LogInformation($"Getting user by id: {id}");
            var user = _userService.GetUserById(id);
            if (user == null)
            {
                _logger.LogWarning($"User with id {id} not found");
                return NotFound();
            }
            return Ok(user);
        }

        // POST a new user
        [HttpPost]
        public IActionResult AddUser([FromBody] User user)
        {
            _logger.LogInformation("Adding a new user");
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid user data");
                return BadRequest(ModelState);
            }
            _userService.AddUser(user);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        // PUT (update) an existing user
        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] User user)
        {
            _logger.LogInformation($"Updating user with id: {id}");
            if (!ModelState.IsValid)
            {
                 _logger.LogWarning($"Invalid user data for id: {id}");
                return BadRequest(ModelState);
            }
            try
            {
                _userService.UpdateUser(id, user);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, $"Error updating user: {ex.Message}");
                return NotFound(); // Return 404 for not found
            }
        }

        // DELETE a user
        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            _logger.LogInformation($"Deleting user with id: {id}");
            try
            {
                _userService.DeleteUser(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, $"Error deleting user: {ex.Message}");
                return NotFound();
            }
        }
    }
}

// Middleware/LoggingMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace UserManagementApp.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path}");
            await _next(context);
            _logger.LogInformation($"Response: {context.Response.StatusCode}");
        }
    }
}

// Startup.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserManagementApp.Services;
using UserManagementApp.Middleware;

namespace UserManagementApp
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<IUserService, UserService>(); // Register UserService
            services.AddLogging(builder =>
            {
                builder.AddConsole(); // Add console logging
                builder.AddDebug();   // Add debug logging (for Visual Studio)
            });
            services.AddSwaggerGen(); //add swagger
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            // Add the logging middleware *first* in the pipeline
            app.UseMiddleware<LoggingMiddleware>();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();  // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwaggerUI(); // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
                                 // specifying the Swagger JSON endpoint.
        }
    }
}

// Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace UserManagementApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}

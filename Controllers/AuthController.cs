using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkTimePro.Api.Data;
using WorkTimePro.Api.Models;
using WorkTimePro.Api.Services;

namespace WorkTimePro.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
                return Unauthorized("Invalid credentials");

            var isValid = PasswordService.Verify(
                request.Password,
                user.PasswordHash
            );

            if (!isValid)
                return Unauthorized("Invalid credentials");

            return Ok(new
            {
                user.Id,
                user.Username,
                user.IsAdmin
            });
        }
    }
}

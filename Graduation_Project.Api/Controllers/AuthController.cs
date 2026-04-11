using Graduation_Project.BLL.DTOs.Auth;
using Graduation_Project.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Graduation_Project.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return result.IsSuccess ? Ok(result) : Unauthorized(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
            return result.IsSuccess ? Ok(result) : Unauthorized(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
        {
            await _authService.LogoutAsync(dto.RefreshToken);
            return Ok(new { message = "Logged out successfully" });
        }
        
        
    }
    
}
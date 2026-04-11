using Graduation_Project.BLL.DTOs.Auth;

namespace Graduation_Project.BLL.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto model);
        Task<AuthResponseDto> LoginAsync(LoginDto model);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string refreshToken);
    }
}
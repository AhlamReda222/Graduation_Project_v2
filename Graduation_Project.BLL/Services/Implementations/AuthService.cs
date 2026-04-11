using Graduation_Project.BLL.DTOs.Auth;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Entities;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUnitOfWork unitOfWork,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        // Customer Register فقط - UserType دايماً Customer
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto model)
        {
            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
                return Fail("Email already exists");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                UserType = UserType.Customer, // مش بناخده من الـ DTO
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsBlocked = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "User registered successfully",
                Email = user.Email,
                UserType = user.UserType.ToString()
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null || user.IsBlocked)
                return Fail("Invalid credentials or blocked");

            var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!passwordCheck.Succeeded)
                return Fail("Invalid credentials");

            // الغاء كل التوكنات القديمة
            var oldTokens = await _unitOfWork.RefreshTokens
                .FindAsync(r => r.ApplicationUserId == user.Id && !r.IsRevoked);

            foreach (var token in oldTokens)
            {
                token.IsRevoked = true;
                _unitOfWork.RefreshTokens.Update(token);
            }

            var jwt = _tokenService.GenerateJwtToken(user);
            var refresh = _tokenService.GenerateRefreshToken();

            await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
            {
                Token = refresh,
                Expires = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                ApplicationUserId = user.Id
            });

            await _unitOfWork.SaveAsync();

            return Success(jwt, refresh, user, "Login successful");
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var tokenEntity = (await _unitOfWork.RefreshTokens
                .FindAsync(r => r.Token == refreshToken))
                .FirstOrDefault();

            if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.Expires < DateTime.UtcNow)
                return Fail("Invalid or expired refresh token");

            tokenEntity.IsRevoked = true;
            _unitOfWork.RefreshTokens.Update(tokenEntity);

            var user = await _userManager.FindByIdAsync(tokenEntity.ApplicationUserId.ToString());
            if (user == null || user.IsBlocked)
                return Fail("User not found or blocked");

            var newJwt = _tokenService.GenerateJwtToken(user);
            var newRefresh = _tokenService.GenerateRefreshToken();

            await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
            {
                Token = newRefresh,
                Expires = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                ApplicationUserId = user.Id
            });

            await _unitOfWork.SaveAsync();

            return Success(newJwt, newRefresh, user, "Token refreshed successfully");
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            var tokenEntity = (await _unitOfWork.RefreshTokens
                .FindAsync(r => r.Token == refreshToken))
                .FirstOrDefault();

            if (tokenEntity != null && !tokenEntity.IsRevoked)
            {
                tokenEntity.IsRevoked = true;
                _unitOfWork.RefreshTokens.Update(tokenEntity);
                await _unitOfWork.SaveAsync();
            }

            return true;
        }

        private AuthResponseDto Fail(string message)
            => new AuthResponseDto { IsSuccess = false, Message = message };

        private AuthResponseDto Success(string jwt, string refresh, ApplicationUser user, string message)
            => new AuthResponseDto
            {
                IsSuccess = true,
                Message = message,
                Token = jwt,
                RefreshToken = refresh,
                Email = user.Email,
                UserType = user.UserType.ToString()
            };
    }
}
using Graduation_Project.BLL.DTOs.Product;
using Microsoft.AspNetCore.Http;

namespace Graduation_Project.BLL.Services.Interfaces
{
    public interface IAiModerationService
    {
        Task<AiModerationResultDto> ModerateProductAsync(AiModerationRequestDto request);

Task<AiPredictionResultDto> PredictProductAsync(
    AiModerationRequestDto request,
    string? imageUrl,
    string? brandName
);

Task<ImageValidationResultDto> ValidateImageAsync(IFormFile image);    }

}
    

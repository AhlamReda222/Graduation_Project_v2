using Graduation_Project.BLL.DTOs.Product;

namespace Graduation_Project.BLL.Services.Interfaces
{
    public interface IAiModerationService
    {
        Task<AiModerationResultDto> ModerateProductAsync(AiModerationRequestDto request);
    }
}
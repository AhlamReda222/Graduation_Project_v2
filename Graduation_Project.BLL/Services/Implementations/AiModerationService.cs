using System.Net.Http;
using System.Text;
using System.Text.Json;
using Graduation_Project.BLL.DTOs.Product;
using Graduation_Project.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class AiModerationService : IAiModerationService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://stormy-rotative-leisa.ngrok-free.dev";

        public AiModerationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ======================================================
        // 🟢 1. IMAGE VALIDATION (NEW API)
        // ======================================================
       public async Task<ImageValidationResultDto> ValidateImageAsync(IFormFile file)
{
    try
    {
        var form = new MultipartFormDataContent();

        using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

        form.Add(fileContent, "file", file.FileName);

        var response = await _httpClient.PostAsync($"{BaseUrl}/validate-image", form);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new ImageValidationResultDto
            {
                IsValid = false,
                Status = "error",
                Message = $"AI server error: {response.StatusCode}"
            };
        }

        var json = JsonDocument.Parse(responseBody);
        var status = json.RootElement.GetProperty("status").GetString();

        return new ImageValidationResultDto
        {
            IsValid = status == "approved",
            Status = status,
            Message = json.RootElement.GetProperty("message").GetString()
        };
    }
    catch (Exception ex)
    {
        return new ImageValidationResultDto
        {
            IsValid = false,
            Status = "error",
            Message = ex.Message
        };
    }
}

        // ======================================================
        // 🟡 2. ANALYZE PRODUCT (CATEGORY + PRICE)
        // ======================================================
 public async Task<AiPredictionResultDto> PredictProductAsync(
    AiModerationRequestDto request,
    string? imageUrl,
    string? brandName
)
{
    var form = new MultipartFormDataContent();

    form.Add(new StringContent(request.ProductName ?? ""), "product_name");
    form.Add(new StringContent(request.Description ?? ""), "description");
    form.Add(new StringContent(brandName ?? ""), "brand");

    if (!string.IsNullOrEmpty(imageUrl))
        form.Add(new StringContent(imageUrl), "image_url");

    var response = await _httpClient.PostAsync(
        $"{BaseUrl}/analyze-product",
        form
    );

    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new Exception(responseBody);

    return JsonSerializer.Deserialize<AiPredictionResultDto>(
        responseBody,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    )!;
} // ======================================================
        // 🔴 3. MODERATE PRODUCT (APPROVAL)
        // ======================================================
    public async Task<AiModerationResultDto> ModerateProductAsync(AiModerationRequestDto request)
{
    var json = JsonSerializer.Serialize(request);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await _httpClient.PostAsync(
        $"{BaseUrl}/moderate-product",
        content
    );

    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new Exception($"AI failed: {response.StatusCode} - {responseBody}");

    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

    var status = result.GetProperty("status").GetString();

    return new AiModerationResultDto
    {
        IsApproved = status == "auto_approved",
        Status = status ?? "unknown",
        Message = result.TryGetProperty("message", out var msg) ? msg.GetString() : null,
        Reason = result.TryGetProperty("reason", out var reason) ? reason.GetString() : null
    };
}

    }
}
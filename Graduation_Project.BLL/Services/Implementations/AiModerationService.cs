using System.Net.Http;
using System.Text;
using System.Text.Json;
using Graduation_Project.BLL.DTOs.Product;
using Graduation_Project.BLL.Services.Interfaces;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class AiModerationService : IAiModerationService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://stormy-rotative-leisa.ngrok-free.dev";

        public AiModerationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
           // _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        }

        public async Task<AiModerationResultDto> ModerateProductAsync(AiModerationRequestDto request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

             var response = await _httpClient.PostAsync($"{BaseUrl}/moderate-product", content);
var responseBody = await response.Content.ReadAsStringAsync();

// 🔴 1. تأكدي إن الـ request نجح
if (!response.IsSuccessStatusCode)
{
    throw new Exception($"AI failed: {response.StatusCode} - {responseBody}");
}

// 🔴 2. تأكدي إن فيه response
if (string.IsNullOrWhiteSpace(responseBody))
{
    throw new Exception("AI returned empty response");
}

// 🔴 3. جرّبي تفكي JSON بشكل آمن
JsonElement result;

try
{
    result = JsonSerializer.Deserialize<JsonElement>(responseBody);
}
catch
{
    throw new Exception("AI did not return valid JSON: " + responseBody);
}

                var status = result.GetProperty("status").GetString();

                if (status == "auto_approved")
                {
                    return new AiModerationResultDto
                    {
                        IsApproved = true,
                        Status = "auto_approved",
                        Message = result.TryGetProperty("message", out var msg) ? msg.GetString() : null
                    };
                }
                else
                {
                    return new AiModerationResultDto
                    {
                        IsApproved = false,
                        Status = "rejected",
                        Reason = result.TryGetProperty("reason", out var reason) ? reason.GetString() : "تم رفض المنتج",
                        ToxicityScore = result.TryGetProperty("toxicity_score", out var score) ? score.GetDouble() : null,
                        IsDuplicate = result.TryGetProperty("duplicate_check", out var dup) && dup.GetBoolean()
                    };
                }
            }
          catch (Exception ex)
{
    throw new Exception("AI service failed: " + ex.Message);
}
        }
    }
}
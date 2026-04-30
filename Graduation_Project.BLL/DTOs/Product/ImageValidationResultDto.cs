public class ImageValidationResultDto
{
    public bool IsValid { get; set; }
    public string Status { get; set; } // approved / rejected / error
    public string? Message { get; set; }
}
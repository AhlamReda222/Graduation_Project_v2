using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Graduation_Project.BLL.DTOs.Product
{
    public class CreateProductDto
    {
        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
            public List<IFormFile>? Images { get; set; }

        [Required]
        public int CategoryId { get; set; }

    public string? VariantsJson { get; set; }
            public decimal? BasePrice { get; set; } 
    public int? StockQuantity { get; set; } //

public bool UseAiSuggestion { get; set; }
        public CreateCustomizationOptionsDto? Customization { get; set; } = null;
    }
}
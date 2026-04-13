using System.ComponentModel.DataAnnotations;

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
        public string ImageUrls { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public bool AllowsCustomization { get; set; } = false;

        // الأحجام والأسعار
        [Required]
        public List<CreateProductVariantDto> Variants { get; set; }

        // الأماكن المتاحة للطباعة (لو AllowsCustomization = true)
        public List<int> CustomizationZones { get; set; } = new();
    }
}
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

        // الأحجام والأسعار - مستقلة تماماً عن الـ Customization
        [Required]
        public List<CreateProductVariantDto> Variants { get; set; }
            public decimal BasePrice { get; set; } // 👈 الجديد
    public int? StockQuantity { get; set; } // ✅ أضفناها هنا


        // الـ Customization اختياري بالكامل
        // لو null يعني المنتج مش بيقبل customization
        public CreateCustomizationOptionsDto Customization { get; set; } = null;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Graduation_Project.BLL.DTOs.BrandOwnerRequest
{
    public class CreateBrandOwnerRequestDto
    {
        // بيانات الطلب
        [Required(ErrorMessage = "Business name is required")]
        [MaxLength(100)]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Business license is required")]
        [MaxLength(500)]
        public string BusinessLicense { get; set; }


        // ✅ بيانات البراند في نفس الطلب
        [Required(ErrorMessage = "Brand name is required")]
        [MaxLength(100)]
        public string BrandName { get; set; }

        [MaxLength(500)]
        public string BrandDescription { get; set; }

        public string BrandLogoUrl { get; set; }
    }
}
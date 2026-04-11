using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace Graduation_Project.BLL.DTOs.BrandOwnerRequest
{
    public class CreateBrandOwnerRequestDto
    {
        [Required(ErrorMessage = "Business name is required")]
        [MaxLength(100, ErrorMessage = "Business name cannot exceed 100 characters")]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Business license is required")]
        [MaxLength(500, ErrorMessage = "Business license cannot exceed 500 characters")]
        public string BusinessLicense { get; set; }

        [Required(ErrorMessage = "Tax ID is required")]
        [MaxLength(50, ErrorMessage = "Tax ID cannot exceed 50 characters")]
        public string TaxId { get; set; }
    }
}
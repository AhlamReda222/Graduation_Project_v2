using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Graduation_Project.DAL.Models.Enums;
using System;
namespace Graduation_Project.BLL.DTOs.BrandOwnerRequest
{
    public class BrandOwnerRequestDto
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessLicense { get; set; }
        public string? TaxId { get; set; }
        public RequestStatus RequestStatus { get; set; }
        public string? RequestStatusText { get; set; }
        public DateTime RequestDate { get; set; }
        public int? ReviewedBy { get; set; }
        public string? ReviewerName { get; set; }
        public DateTime? ReviewDate { get; set; }
    }
}
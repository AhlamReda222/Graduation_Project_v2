using System.ComponentModel.DataAnnotations;
using Graduation_Project.DAL.Models.Enums;

namespace Graduation_Project.BLL.DTOs.Order
{
    public class CreateOrderDto
    {
        public string FirstName { get; set; }
public string LastName { get; set; }
        [Required(ErrorMessage = "Shipping address is required")]
        public string ShippingAddress { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }
    }
}
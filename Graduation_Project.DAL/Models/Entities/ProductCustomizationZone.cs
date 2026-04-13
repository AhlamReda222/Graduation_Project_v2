using Graduation_Project.DAL.Models.Enums;

namespace Graduation_Project.DAL.Models.Entities
{
    public class ProductCustomizationZone
    {
        public int ZoneId { get; set; }
        public int ProductId { get; set; }
        public CustomizationZone Zone { get; set; }  // Front, Back, etc.
        public bool IsAvailable { get; set; } = true;

        // Navigation
        public virtual Product Product { get; set; }
    }
}
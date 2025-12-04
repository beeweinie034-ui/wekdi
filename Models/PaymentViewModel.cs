using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace wekdi.Models
{
    public class PaymentViewModel
    {
        // Payment fields
        [Required] public string CardName { get; set; } = string.Empty;
        [Required] public string CardNumber { get; set; } = string.Empty;
        [Required] public string Expiry { get; set; } = string.Empty;
        [Required] public string CVV { get; set; } = string.Empty;

        // Order info (hidden fields)
        public decimal TaxRate { get; set; } = 0m;
        public string OrderType { get; set; } = "Takeaway";
        public int? TableNumber { get; set; }

        // Cart items
        public List<Cart> CartItems { get; set; } = new List<Cart>();
    }
}

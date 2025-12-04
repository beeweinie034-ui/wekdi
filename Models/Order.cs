using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace wekdi.Models
{
    public class Order
    {
        public int OrderId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required, StringLength(200)]
        public string Items { get; set; } = string.Empty;

        [Required, Range(0.01, 1000.00)]
        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        [Required, StringLength(50)]
        public string Status { get; set; } = "Pending";

        public string? Notes { get; set; }

        public Member? Member { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

}
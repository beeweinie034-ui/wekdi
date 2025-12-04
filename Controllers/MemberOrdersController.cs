using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wekdi.Data;
using wekdi.Models;
using System;
using System.Linq;

namespace wekdi.Controllers
{
    [Authorize(Roles = "Member")]
    public class MemberOrdersController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MemberOrdersController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /MemberOrders/OrderHistory
        public IActionResult OrderHistory(DateTime? startDate, DateTime? endDate)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var member = _db.Members.FirstOrDefault(m => m.Username == username);
            if (member == null)
                return NotFound();

            var ordersQuery = _db.Orders
                .Where(o => o.MemberId == member.MemberId);

            if (startDate.HasValue)
                ordersQuery = ordersQuery.Where(o => o.OrderDate >= startDate.Value);
            if (endDate.HasValue)
                ordersQuery = ordersQuery.Where(o => o.OrderDate <= endDate.Value);

            var orderList = ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderHistoryViewModel
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status
                })
                .ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(orderList);
        }

        // GET: /MemberOrders/Details/5
        public IActionResult Details(int id)
        {
            var username = User.Identity?.Name;
            var member = _db.Members.FirstOrDefault(m => m.Username == username);
            if (member == null) return NotFound();

            var order = _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefault(o => o.OrderId == id && o.MemberId == member.MemberId);

            if (order == null) return NotFound();

            // Instead of ViewBag, just pass items in the model or keep for backward compatibility
            ViewBag.OrderItems = order.OrderItems.ToList();

            return View(order);
        }

    }
}

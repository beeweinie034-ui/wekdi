using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wekdi.Data;
using wekdi.Models;
using System.Linq;
using System.Globalization;

namespace wekdi.Controllers
{
    [Authorize(Roles = "SuperAdmin,StaffAdmin")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _db;

        public OrdersController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index(string? search, string? startDate, string? endDate, int page = 1)
        {
            const int pageSize = 10; // 10 orders per page

            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrWhiteSpace(startDate) &&
                DateTime.TryParseExact(startDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
                start = s.Date;

            if (!string.IsNullOrWhiteSpace(endDate) &&
                DateTime.TryParseExact(endDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
                end = e.Date;

            ViewBag.StartDate = start?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = end?.ToString("yyyy-MM-dd");
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;

            if (start.HasValue && end.HasValue && end < start)
                ModelState.AddModelError("", "End date cannot be earlier than start date.");

            var query = _db.Orders.Include(o => o.Member).AsQueryable();

            //  Text search
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (int.TryParse(search, out var userId))
                {
                    query = query.Where(o =>
                        o.Member != null &&
                        (o.Member.MemberId == userId || o.Member.Username.Contains(search)));
                }
                else
                {
                    query = query.Where(o => o.Member != null && o.Member.Username.Contains(search));
                }
            }

            //  Date range
            if (ModelState.IsValid)
            {
                if (start.HasValue)
                    query = query.Where(o => EF.Property<DateTime>(o, "OrderDate") >= start.Value);
                if (end.HasValue)
                    query = query.Where(o => EF.Property<DateTime>(o, "OrderDate") < end.Value.AddDays(1));
            }

            //  Paging
            var totalOrders = query.Count();
            var totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

            var orders = query
                .OrderByDescending(o => EF.Property<DateTime>(o, "OrderDate"))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalPages = totalPages;

            return View(orders);
        }



        //  Details with order items + member
        public IActionResult Details(int id)
        {
            var order = _db.Orders
                .Include(o => o.Member)      // include member name
                .FirstOrDefault(o => o.OrderId == id);
            if (order == null) return NotFound();

            var items = _db.OrderItems
                .Where(i => i.OrderId == id)
                .Include(i => i.Product)
                .ToList();

            ViewBag.OrderItems = items;
            return View(order);
        }

        
        public IActionResult Delete(int id)
        {
            var order = _db.Orders
                .Include(o => o.Member)
                .FirstOrDefault(o => o.OrderId == id);
            if (order == null) return NotFound();
            return View(order);
        }

        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == id);
            if (order == null) return NotFound();

            var items = _db.OrderItems.Where(i => i.OrderId == id);
            _db.OrderItems.RemoveRange(items);

            _db.Orders.Remove(order);
            _db.SaveChanges();
            TempData["Success"] = "Order deleted successfully!";
            return RedirectToAction(nameof(Index));
        }




    }
}

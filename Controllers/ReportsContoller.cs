using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using wekdi.Data;

namespace wekdi.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReportsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Page for the chart
        public IActionResult TopProducts()
        {
            return View();
        }

        // API endpoint that returns JSON for Chart.js
        [HttpGet]
        public IActionResult GetTopProductsData()
        {
            var topProducts = _db.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => oi.Product.Name)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(3)
                .ToList();

            return Json(new
            {
                labels = topProducts.Select(p => p.ProductName).ToList(),
                data = topProducts.Select(p => p.TotalQuantity).ToList()
            });
        }

        //  New: return the top 3 selling products as a normal list
        public IActionResult Favourites()
        {
            var topProducts = _db.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => oi.Product)
                .Select(g => new
                {
                    Product = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(3)
                .Select(x => x.Product)
                .ToList();

            return View("Favourites", topProducts);
        }
    }
}

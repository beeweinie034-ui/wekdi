using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wekdi.Data;
using wekdi.Models;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index(int? categoryId, string? search)
    {
        var products = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            products = products.Where(p =>
                EF.Functions.Like(p.Name, $"%{search}%") ||
                EF.Functions.Like(p.Description ?? string.Empty, $"%{search}%"));
        }

        ViewBag.Categories = _context.Categories.ToList();
        ViewBag.SelectedCategory = categoryId;
        ViewBag.Search = search;

        return View(products
            .OrderBy(p => p.Name)   // optional sorting
            .ToList());
    }

    [HttpGet]
    public IActionResult Search(string term, int? categoryId)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(new { products = new List<object>() });

        var products = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
            products = products.Where(p => p.CategoryId == categoryId.Value);

        products = products
            .Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Description ?? string.Empty, $"%{term}%"))
            .OrderBy(p => p.Name)
            .Take(20); // limit for performance

        var results = products.Select(p => new
        {
            id = p.ProductId,
            name = p.Name,
            price = p.Price,
            description = p.Description,
            imagePath = string.IsNullOrEmpty(p.ImagePath)
                            ? Url.Content("~/images/products/default.png")
                            : Url.Content("~/images/products/" + p.ImagePath)
        });

        return Json(new { products = results });
    }

}

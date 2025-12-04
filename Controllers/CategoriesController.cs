using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wekdi.Data;
using wekdi.Models;

namespace wekdi.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CategoriesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: Categories
        public async Task<IActionResult> Index(string search)
        {
            // Keep search value in ViewBag so it stays in the input box
            ViewBag.Search = search;

            // Start with all categories
            var categories = from c in _db.Categories
                             select c;

            // If search is not empty, filter
            if (!string.IsNullOrEmpty(search))
            {
                categories = categories.Where(c => c.Name.Contains(search));
            }

            var categoryList = await categories.ToListAsync();

            // ✅ Get the first category (if any exist)
            var firstCategory = categoryList.FirstOrDefault();
            if (firstCategory != null)
            {
                // ✅ Query top 3 best-selling products for that category
                var topProducts = await _db.OrderItems
                    .Where(oi => oi.Product.CategoryId == firstCategory.CategoryId)
                    .GroupBy(oi => oi.Product)
                    .Select(g => new
                    {
                        Product = g.Key,
                        TotalSold = g.Sum(x => x.Quantity)
                    })
                    .OrderByDescending(x => x.TotalSold)
                    .Take(3)
                    .Select(x => x.Product)
                    .ToListAsync();

                // Pass to View
                ViewBag.TopProducts = topProducts;
                ViewBag.FirstCategoryName = firstCategory.Name;
            }

            return View(categoryList);
        }


        // GET: Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _db.Add(category);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        // POST: Categories/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.CategoryId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Update(category);
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_db.Categories.Any(e => e.CategoryId == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Delete
        public IActionResult Delete(int id)
        {
            var category = _db.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (category == null) return NotFound();

            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var category = _db.Categories.Find(id);
            if (category == null) return NotFound();

            _db.Categories.Remove(category);
            _db.SaveChanges();
            TempData["Success"] = "Category deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
        // ==================== BATCH DELETE ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BatchDelete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                var categories = _db.Categories.Where(c => selectedIds.Contains(c.CategoryId));
                _db.Categories.RemoveRange(categories);
                _db.SaveChanges();
                TempData["Success"] = "Selected categories deleted successfully.";
            }
            else
            {
                TempData["Error"] = "No categories selected for deletion.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

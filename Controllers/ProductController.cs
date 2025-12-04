using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using wekdi.Data;
using wekdi.Models;

namespace wekdi.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ProductsController(ApplicationDbContext db)
        {
            _db = db;
        }

        //  List all products
        public IActionResult Index(string search, int page = 1, int pageSize = 10)
        {
            var productsQuery = _db.Products.Include(p => p.Category).AsQueryable();

            //  Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                productsQuery = productsQuery.Where(p =>
                    EF.Functions.Like(p.Name.ToLower(), $"%{lowerSearch}%")
                );
            }

            // Total count for pagination after filtering
            int totalItems = productsQuery.Count();

            // Get products for current page
            var products = productsQuery
                .OrderBy(p => p.ProductId) // ensure consistent order
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Pass pagination info to the view
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search; // pass search term to view

            if (User.IsInRole("SuperAdmin") || User.IsInRole("StaffAdmin"))
            {
                return View("AdminIndex", products);
            }
            else
            {
                return View("MemberIndex", products);
            }
        }

        //  Create product (GET)
        public IActionResult Create()
        {
            ViewBag.Categories = _db.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.Name
                })
                .ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, IFormFile? productImage)
        {
            if (ModelState.IsValid)
            {
                // --- Save product image ---
                if (productImage != null && productImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(productImage.FileName);
                    var fileName = $"product_{Guid.NewGuid():N}{ext}";
                    var savePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(savePath, FileMode.Create))
                    {
                        productImage.CopyTo(stream);
                    }

                    product.ImagePath = fileName;
                }

                _db.Products.Add(product);
                _db.SaveChanges();

                TempData["Success"] = "Product created successfully!";
                return RedirectToAction(nameof(Index));
            }

            // Re-populate dropdown on validation error
            ViewBag.Categories = _db.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.Name
                })
                .ToList();

            return View(product);
        }

        // ✏ Edit product (GET)
        public IActionResult Edit(int id)
        {
            var product = _db.Products.Find(id);
            if (product == null) return NotFound();

            // Prepare category dropdown
            ViewBag.Categories = _db.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.Name,
                    Selected = c.CategoryId == product.CategoryId
                })
                .ToList();

            return View(product);
        }



        //  Edit product (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Product product, IFormFile? productImage)
        {
            var existingProduct = _db.Products.Find(product.ProductId);
            if (existingProduct == null) return NotFound();

            // Update normal fields
            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.CategoryId = product.CategoryId;

            // Handle image upload
            if (productImage != null && productImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                Directory.CreateDirectory(uploadsFolder);

                var ext = Path.GetExtension(productImage.FileName);
                var fileName = $"product_{Guid.NewGuid():N}{ext}";
                var savePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    productImage.CopyTo(stream);
                }

                // Delete old image if not default
                if (!string.IsNullOrEmpty(existingProduct.ImagePath) && existingProduct.ImagePath != "default.png")
                {
                    var oldPath = Path.Combine(uploadsFolder, existingProduct.ImagePath);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                existingProduct.ImagePath = fileName; 
            }
            // else: keep existing image

            _db.SaveChanges();
            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction(nameof(Index));
        }



        // GET: Product/Delete
        public IActionResult Delete(int id)
        {
            var product = _db.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Product/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var product = _db.Products.Find(id);
            if (product == null) return NotFound();

            // Remove related records if necessary (e.g. cart items, order details)

            // Delete product image file (optional)
            if (!string.IsNullOrEmpty(product.ImagePath) && product.ImagePath != "default.png")
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                var oldPath = Path.Combine(uploadsFolder, product.ImagePath);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            _db.Products.Remove(product);
            _db.SaveChanges();

            TempData["Success"] = "Product deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,StaffAdmin")]
        public IActionResult BatchDelete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                var products = _db.Products.Where(p => selectedIds.Contains(p.ProductId));
                _db.Products.RemoveRange(products);
                _db.SaveChanges();
                TempData["Success"] = "Selected products deleted successfully.";
            }
            else
            {
                TempData["Error"] = "No products selected for deletion.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

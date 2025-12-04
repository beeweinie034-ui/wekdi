using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using wekdi.Data;
using wekdi.Models;

namespace wekdi.Controllers
{
    [Authorize] // All admin maintenance requires login
    public class AdminsController : Controller
    {
        private readonly ApplicationDbContext db;
        private static readonly string[] ValidRoles = new[] { "SuperAdmin" };

        public AdminsController(ApplicationDbContext db)
        {
            this.db = db;
        }

        // ==================== LIST ADMINS ====================
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Index(string search)
        {
            ViewBag.Search = search;
            var admins = db.Admins.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                admins = admins.Where(a =>
                    EF.Functions.Like(a.Username.ToLower(), $"%{lowerSearch}%") ||
                    EF.Functions.Like(a.Email.ToLower(), $"%{lowerSearch}%")
                );
            }

            return View(admins.ToList());
        }

        // ==================== CREATE ADMIN ====================
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Create()
        {
            return View(new Admin { Role = "StaffAdmin", IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Create(Admin admin, string password)
        {
            // Validate password requirements
            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                return View(admin);
            }

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            if (password.Length < 6 || !hasUpper || !hasLower || !hasDigit || !hasSpecial)
            {
                ViewBag.Error = "Password must be at least 6 characters and include at least one uppercase, one lowercase, one number, and one special character.";
                return View(admin);
            }

            // Validate username: only letters and digits
            if (string.IsNullOrWhiteSpace(admin.Username) || !admin.Username.All(char.IsLetterOrDigit))
            {
                ViewBag.Error = "Username can only contain letters and digits.";
                return View(admin);
            }

            admin.Role = "StaffAdmin";

            if (db.Admins.Any(a => a.Username == admin.Username))
            {
                ViewBag.Error = "Username already exists.";
                return View(admin);
            }

            if (db.Admins.Any(a => a.Email == admin.Email))
            {
                ViewBag.Error = "Email already exists.";
                return View(admin);
            }

            var hasher = new PasswordHasher<Admin>();
            admin.PasswordHash = hasher.HashPassword(admin, password);
            admin.FailedLoginAttempts = 0;
            admin.LockoutEnd = null;
            admin.PasswordResetToken = null!;
            admin.PasswordResetExpiry = null!;
            admin.IsActive = true;

            db.Admins.Add(admin);
            db.SaveChanges();

            return RedirectToAction(nameof(Index));
        }


        // ==================== EDIT ADMIN ====================
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Edit(int id)
        {
            var admin = db.Admins.AsNoTracking().FirstOrDefault(a => a.AdminId == id);
            if (admin == null)
                return NotFound();

            // Prevent StaffAdmin from editing SuperAdmin
            if (admin.Role == "SuperAdmin" && !User.IsInRole("SuperAdmin"))
            {
                TempData["Error"] = "You cannot edit a SuperAdmin.";
                return RedirectToAction(nameof(Index));
            }

            return View(admin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Edit(Admin admin)
        {
            var existingAdmin = db.Admins.Find(admin.AdminId);
            if (existingAdmin == null)
                return NotFound();

            // Prevent StaffAdmin from editing SuperAdmin
            if (existingAdmin.Role == "SuperAdmin" && !User.IsInRole("SuperAdmin"))
            {
                TempData["Error"] = "You cannot edit a SuperAdmin.";
                return RedirectToAction(nameof(Index));
            }

            existingAdmin.Username = admin.Username;
            existingAdmin.Email = admin.Email;
            existingAdmin.Role = admin.Role;
            existingAdmin.IsActive = admin.IsActive;

            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // ==================== DELETE ADMIN ====================
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Delete(int id)
        {
            var admin = db.Admins.Find(id);
            if (admin == null)
                return NotFound();

            return View(admin);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult DeleteConfirmed(int id)
        {
            var admin = db.Admins.Find(id);
            if (admin != null)
            {
                db.Admins.Remove(admin);
                db.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================== BATCH DELETE ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BatchDelete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                var admins = db.Admins.Where(a => selectedIds.Contains(a.AdminId));
                db.Admins.RemoveRange(admins);
                db.SaveChanges();
                TempData["Success"] = "Selected admins deleted successfully.";
            }
            else
            {
                TempData["Error"] = "No admins selected for deletion.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================== SELF-REGISTRATION FOR SUPERADMIN ====================
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new Admin { Role = "SuperAdmin", IsActive = true });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Register(Admin admin, string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View(admin);
            }

            admin.Role = "SuperAdmin";

            if (db.Admins.Any(a => a.Username == admin.Username))
            {
                ViewBag.Error = "Username already exists.";
                return View(admin);
            }

            if (db.Admins.Any(a => a.Email == admin.Email))
            {
                ViewBag.Error = "Email already exists.";
                return View(admin);
            }

            var hasher = new PasswordHasher<Admin>();
            admin.PasswordHash = hasher.HashPassword(admin, password);
            admin.FailedLoginAttempts = 0;
            admin.LockoutEnd = null;
            admin.PasswordResetToken = null!;
            admin.PasswordResetExpiry = null!;
            admin.IsActive = true;

            db.Admins.Add(admin);
            db.SaveChanges();

            TempData["Success"] = "Admin registered successfully. You can now log in.";
            return RedirectToAction("Login", "Account");
        }
    }
}

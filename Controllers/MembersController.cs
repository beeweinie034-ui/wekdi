using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using wekdi.Data;
using wekdi.Models;

namespace wekdi.Controllers
{
    [Authorize(Roles = "SuperAdmin,StaffAdmin")]
    public class MembersController : Controller
    {
        private readonly ApplicationDbContext db;

        public MembersController(ApplicationDbContext db)
        {
            this.db = db;
        }

        // ==================== LIST MEMBERS ====================
        public IActionResult Index(string search)
        {
            ViewBag.Search = search;
            var members = db.Members.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                members = members.Where(m =>
                    EF.Functions.Like(m.Username.ToLower(), $"%{lowerSearch}%") ||
                    EF.Functions.Like(m.Email.ToLower(), $"%{lowerSearch}%") ||
                    EF.Functions.Like(m.FullName.ToLower(), $"%{lowerSearch}%")
                );
            }

            return View(members.ToList());
        }

        // ==================== CREATE MEMBER ====================
        public IActionResult Create()
        {
            return View(new Member());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Member member, string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View(member);
            }

            if (db.Members.Any(m => m.Email == member.Email))
            {
                ViewBag.Error = "Email already exists.";
                return View(member);
            }

            var hasher = new PasswordHasher<Member>();
            member.PasswordHash = hasher.HashPassword(member, password);

            db.Members.Add(member);
            db.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // ==================== EDIT MEMBER ====================
        public IActionResult Edit(int id)
        {
            var member = db.Members.AsNoTracking().FirstOrDefault(m => m.MemberId == id);
            if (member == null)
                return NotFound();

            return View(member);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Member member)
        {
            var existingMember = db.Members.Find(member.MemberId);
            if (existingMember == null)
                return NotFound();

            existingMember.Username = member.Username;
            existingMember.FullName = member.FullName;
            existingMember.Email = member.Email;
            existingMember.Phone = member.Phone;
            existingMember.Address = member.Address;
            existingMember.IsActive = member.IsActive;

            db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // ==================== DELETE MEMBER (SuperAdmin only) ====================
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Delete(int id)
        {
            var member = db.Members.Find(id);
            if (member == null)
                return NotFound();

            return View(member); // Delete confirmation page
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult DeleteConfirmed(int id)
        {
            var member = db.Members.Find(id);
            if (member != null)
            {
                db.Members.Remove(member);
                db.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================== BATCH DELETE ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult BatchDelete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                var members = db.Members.Where(m => selectedIds.Contains(m.MemberId));
                db.Members.RemoveRange(members);
                db.SaveChanges();
                TempData["Success"] = "Selected members deleted successfully.";
            }
            else
            {
                TempData["Error"] = "No members selected for deletion.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using wekdi.Data;
using wekdi.Models;
using System.Net.Http;
using System.Text.Json;

namespace wekdi.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ==================== LOGIN ====================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Check if a username query param exists (optional)
            string? username = Request.Query["username"];
            if (!string.IsNullOrEmpty(username))
            {
                var member = _db.Members.FirstOrDefault(m => m.Username == username);
                if (member != null && member.LockoutEnd.HasValue && member.LockoutEnd.Value > DateTime.Now)
                {
                    ViewBag.LockoutEnd = member.LockoutEnd.Value;
                }

                var admin = _db.Admins.FirstOrDefault(a => a.Username == username);
                if (admin != null && admin.LockoutEnd.HasValue && admin.LockoutEnd.Value > DateTime.Now)
                {
                    ViewBag.LockoutEnd = admin.LockoutEnd.Value;
                }
            }

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            // --- CAPTCHA token ---
            var captchaToken = Request.Form["g-recaptcha-response"];
            if (string.IsNullOrEmpty(captchaToken) || !VerifyRecaptcha(captchaToken))
            {
                ViewBag.Error = "Please verify that you are not a robot.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username/email and password are required.";
                return View();
            }

            // ================= ADMIN LOGIN =================
            var admin = _db.Admins.FirstOrDefault(a => a.Username == username && a.IsActive);
            if (admin != null)
            {
                // Check if currently locked
                if (admin.LockoutEnd.HasValue && admin.LockoutEnd.Value > DateTime.Now)
                {
                    ViewBag.LockoutEnd = admin.LockoutEnd.Value;
                    ViewBag.Error = "Account locked due to multiple failed attempts.";
                    return View();
                }

                var hasher = new PasswordHasher<Admin>();
                var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash, password);

                if (result == PasswordVerificationResult.Success)
                {
                    // Reset lockout info
                    admin.FailedLoginAttempts = 0;
                    admin.LockoutEnd = null;
                    _db.Update(admin);
                    _db.SaveChanges();

                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, admin.Username),
                new Claim(ClaimTypes.Role, admin.Role),
                new Claim(ClaimTypes.Email, admin.Email ?? "")
            };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("TopProducts", "Reports");
                }
                else
                {
                    // Increment failed attempts
                    admin.FailedLoginAttempts++;
                    if (admin.FailedLoginAttempts >= 3)
                    {
                        admin.LockoutEnd = DateTime.Now.AddSeconds(30);
                        ViewBag.LockoutEnd = admin.LockoutEnd.Value;
                        ViewBag.Error = "Account locked due to multiple failed attempts.";
                    }
                    else
                    {
                        ViewBag.Error = "Invalid username/email or password.";
                    }

                    _db.Update(admin);
                    _db.SaveChanges();
                    return View();
                }
            }
            // ================= MEMBER LOGIN =================
            var member = _db.Members.FirstOrDefault(m => m.Username == username);

            if (member == null)
            {
                ViewBag.Error = "Invalid username/email or password.";
                return View();
            }

            // Check if the account is active
            if (!member.IsActive)
            {
                ViewBag.Error = "Your account is deactivated. Please contact admin.";
                return View();
            }

            // Check if currently locked
            if (member.LockoutEnd.HasValue && member.LockoutEnd.Value > DateTime.Now)
            {
                ViewBag.LockoutEnd = member.LockoutEnd.Value;
                ViewBag.Error = "Account locked due to multiple failed attempts.";
                return View();
            }

            // Verify password
            var memberhasher = new PasswordHasher<Member>();
            var memberResult = memberhasher.VerifyHashedPassword(member, member.PasswordHash, password);

            if (memberResult == PasswordVerificationResult.Success)
            {
                // Reset lockout info
                member.FailedLoginAttempts = 0;
                member.LockoutEnd = null;
                _db.Update(member);
                _db.SaveChanges();

                // Create claims for cookie authentication
                var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, member.Username),
        new Claim(ClaimTypes.Role, "Member"),
        new Claim(ClaimTypes.Email, member.Email ?? "")
    };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // Redirect to return URL if specified
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Increment failed attempts
                member.FailedLoginAttempts++;
                if (member.FailedLoginAttempts >= 3)
                {
                    member.LockoutEnd = DateTime.Now.AddSeconds(30); // Lock for 5 minutes
                    ViewBag.LockoutEnd = member.LockoutEnd.Value;
                    ViewBag.Error = "Account locked due to multiple failed attempts.";
                }
                else
                {
                    ViewBag.Error = "Invalid username/email or password.";
                }

                _db.Update(member);
                _db.SaveChanges();
                return View();
            }
        }

        // ==================== REGISTER ====================
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(Member member, string password, string confirmPassword)
        {
            var captchaToken = Request.Form["g-recaptcha-response"];
            if (string.IsNullOrEmpty(captchaToken) || !VerifyRecaptcha(captchaToken))
            {
                ViewBag.Error = "Please verify that you are not a robot.";
                return View(member);
            }

            if (member.Password != confirmPassword)
            {
                ModelState.AddModelError("Password", "Passwords do not match.");
                return View(member);
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View(member);
            }

            if (_db.Members.Any(m => m.Username == member.Username))
            {
                ViewBag.Error = "Username already exists.";
                return View(member);
            }

            if (_db.Members.Any(m => m.Email == member.Email))
            {
                ViewBag.Error = "Email already exists.";
                return View(member);
            }

            // Hash the password for DB
            var hasher = new PasswordHasher<Member>();
            member.PasswordHash = hasher.HashPassword(member, member.Password);
            member.IsActive = true;
            member.FailedLoginAttempts = 0;
            member.JoinDate = DateTime.Now;

            _db.Members.Add(member);
            _db.SaveChanges();

            return RedirectToAction("Login");
        }

        // ==================== LOGOUT ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==================== ADMIN PROFILE ====================
        [Authorize(Roles = "SuperAdmin,StaffAdmin")]
        public IActionResult AdminProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var admin = _db.Admins.FirstOrDefault(a => a.Username == username);
            if (admin == null) return NotFound();

            return View(admin);
        }

        // ==================== MEMBER PROFILE ====================
        [Authorize(Roles = "Member")]
        public IActionResult MemberProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var member = _db.Members.FirstOrDefault(m => m.Username == username);
            if (member == null) return NotFound();

            return View(member);
        }

        [Authorize(Roles = "Member")]
        [HttpGet]
        public IActionResult EditProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var member = _db.Members.FirstOrDefault(m => m.Username == username);
            if (member == null) return NotFound();

            return View(member);
        }

        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(Member updatedMember, IFormFile? profileImage)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            // Load the tracked entity from the DB
            var member = _db.Members.FirstOrDefault(m => m.Username == username);
            if (member == null) return NotFound();

            // Update only editable fields
            member.FullName = updatedMember.FullName;
            member.Phone = updatedMember.Phone;
            member.Address = updatedMember.Address;

            // --- Profile picture upload ---
            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");
                Directory.CreateDirectory(uploadsFolder);

                var ext = Path.GetExtension(profileImage.FileName);
                var fileName = $"member_{member.MemberId}_{Guid.NewGuid():N}{ext}";
                var savePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    profileImage.CopyTo(stream);
                }

                // delete old picture if it isn't the default
                if (!string.IsNullOrEmpty(member.ProfileImagePath) &&
                    !member.ProfileImagePath.EndsWith("Default.png", StringComparison.OrdinalIgnoreCase))
                {
                    var oldPath = Path.Combine(uploadsFolder, Path.GetFileName(member.ProfileImagePath));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                member.ProfileImagePath = "/images/profiles/" + fileName;
            }

            _db.SaveChanges(); // tracked entity, changes are persisted
            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(MemberProfile));
        }

        // ----------------- RECAPTCHA VERIFY -----------------
        private bool VerifyRecaptcha(string token)
        {
            var secret = "6Ldz4dErAAAAAIbznsK-w4p5vUlpD__4T9sdC-jA"; // replace with your secret key
            using var client = new HttpClient();
            var response = client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}", null
            ).Result;

            var json = response.Content.ReadAsStringAsync().Result;
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(json);

            return result != null && result.success;
        }

        private class RecaptchaResponse
        {
            public bool success { get; set; }
            public DateTime challenge_ts { get; set; }
            public string hostname { get; set; }
            public string[] error_codes { get; set; }
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            var member = _db.Members.FirstOrDefault(m => m.Email == email);
            if (member == null)
            {
                ViewBag.Error = "Email is not registered.";
                return View();
            }

            // Generate reset token and expiry
            member.PasswordResetToken = Guid.NewGuid().ToString();
            member.PasswordResetExpiry = DateTime.Now.AddHours(1);
            _db.SaveChanges();

            // Send email with token link
            SendResetEmail(member.Email, member.PasswordResetToken);

            ViewBag.Success = "A password reset link has been sent to your email.";
            return View();
        }



        // ==================== RESET PASSWORD ====================

        // GET: show reset password form
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Invalid password reset link.";
                return View();
            }

            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        // POST: handle reset password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;

            // Validate password
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match or are empty.";
                return View();
            }

            // Validate password requirements
            var hasUpper = newPassword.Any(char.IsUpper);
            var hasLower = newPassword.Any(char.IsLower);
            var hasDigit = newPassword.Any(char.IsDigit);
            var hasSpecial = newPassword.Any(ch => !char.IsLetterOrDigit(ch));

            if (newPassword.Length < 6 || !hasUpper || !hasLower || !hasDigit || !hasSpecial)
            {
                ViewBag.Error = "Password must be at least 6 characters and include at least one uppercase, one lowercase, one number, and one special character.";
                return View();
            }

            // Lookup user by email and token
            var member = _db.Members.FirstOrDefault(
                m => m.Email == email && m.PasswordResetToken == token.Trim() && m.PasswordResetExpiry > DateTime.Now
            );

            var admin = _db.Admins.FirstOrDefault(
                a => a.Email == email && a.PasswordResetToken == token.Trim() && a.PasswordResetExpiry > DateTime.Now
            );

            if (member == null && admin == null)
            {
                ViewBag.Error = "Invalid or expired token.";
                return View();
            }

            // Update password
            if (member != null)
            {
                var hasher = new PasswordHasher<Member>();
                member.PasswordHash = hasher.HashPassword(member, newPassword);
                member.PasswordResetToken = null;
                member.PasswordResetExpiry = null;
                _db.Update(member);
            }

            if (admin != null)
            {
                var hasher = new PasswordHasher<Admin>();
                admin.PasswordHash = hasher.HashPassword(admin, newPassword);
                admin.PasswordResetToken = null;
                admin.PasswordResetExpiry = null;
                _db.Update(admin);
            }

            _db.SaveChanges();

            ViewBag.Message = "Password has been reset successfully. You can now login.";
            return View();
        }


        private void SendResetEmail(string toEmail, string token)
        {
            var resetLink = Url.Action("ResetPassword", "Account", new { email = toEmail, token = token }, Request.Scheme);


            var message = new System.Net.Mail.MailMessage();
            message.From = new System.Net.Mail.MailAddress("wolfieloverssthemaster@gmail.com", "Mekdi Password Reset");
            message.To.Add(toEmail);
            message.Subject = "Password Reset Request";
            message.Body = $"Click this link to reset your password: {resetLink}";
            message.IsBodyHtml = true;

            using (var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587))
            {
                client.Credentials = new System.Net.NetworkCredential("wolfieloverssthemaster@gmail.com", "qxfu ipmd bbnk fhmx");
                client.EnableSsl = true;
                client.Send(message);
            }
        }








    }
}


using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wekdi.Models
{
    public class Member
    {
        public int MemberId { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        [Required, StringLength(200)]
        public string PasswordHash { get; set; } = string.Empty;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpiry { get; set; }



        [StringLength(100)]
        public string? FullName { get; set; }

        public DateTime JoinDate { get; set; } = DateTime.Now;

        // for profile picture
        [StringLength(300)]
        public string? ProfileImagePath { get; set; }  // e.g. "/images/profiles/user1.png"

        // ----------------- ADD PASSWORD VALIDATION HERE -----------------
        [NotMapped]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{6,}$",
            ErrorMessage = "Password must be at least 6 characters and include at least one uppercase, one lowercase, one number, and one special character.")]
        public string Password { get; set; } = string.Empty;
    }
}

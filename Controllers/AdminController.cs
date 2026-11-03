using FamoBoard.Data;
using FamoBoard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamoBoard.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm)
        { _db = db; _userManager = um; _signInManager = sm; }

        // Only super-admin (first registered user) can access
        private async Task<bool> IsSuperAdminAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            // Super admin = first user ever created (lowest CreatedAt)
            var firstUser = await _db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();
            return firstUser?.Id == userId;
        }

        // ── USER LIST ─────────────────────────────────────
        public async Task<IActionResult> Users()
        {
            if (!await IsSuperAdminAsync()) return Forbid();
            var users = await _db.Users
                .OrderBy(u => u.CreatedAt)
                .Select(u => new UserAdminRow
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    DisplayName = u.DisplayName ?? "",
                    Email = u.Email ?? "",
                    CreatedAt = u.CreatedAt,
                    ProjectCount = _db.ProjectMembers.Count(m => m.UserId == u.Id)
                })
                .ToListAsync();
            return View(users);
        }

        // ── RESET PASSWORD ────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (!await IsSuperAdminAsync()) return Forbid();
            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return Json(new { success = false, message = "رمز جدید باید حداقل ۶ کاراکتر باشد" });

            var user = await _userManager.FindByIdAsync(req.UserId);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
            if (result.Succeeded)
                return Json(new { success = true });

            var error = string.Join(" | ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = error });
        }

        // ── UPDATE USERNAME / DISPLAYNAME ─────────────────
        [HttpPost]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest req)
        {
            if (!await IsSuperAdminAsync()) return Forbid();
            var user = await _userManager.FindByIdAsync(req.UserId);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            if (!string.IsNullOrWhiteSpace(req.DisplayName))
                user.DisplayName = req.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(req.UserName) && req.UserName != user.UserName)
            {
                var existing = await _userManager.FindByNameAsync(req.UserName);
                if (existing != null && existing.Id != user.Id)
                    return Json(new { success = false, message = "این نام کاربری قبلاً ثبت شده" });
                user.UserName = req.UserName.Trim();
            }

            var result = await _userManager.UpdateAsync(user);
            return Json(result.Succeeded
                ? new { success = true }
                : new { success = false, message = string.Join(" | ", result.Errors.Select(e => e.Description)) });
        }

        // ── DELETE USER ───────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromBody] Dictionary<string, string> body)
        {
            if (!await IsSuperAdminAsync()) return Forbid();
            var userId = body["userId"];
            var me = _userManager.GetUserId(User);
            if (userId == me) return Json(new { success = false, message = "نمی‌توانید حساب خود را حذف کنید" });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            var result = await _userManager.DeleteAsync(user);
            return Json(result.Succeeded
                ? new { success = true }
                : new { success = false, message = string.Join(" | ", result.Errors.Select(e => e.Description)) });
        }
    }

    // ── DTOs ──────────────────────────────────────────────
    public class UserAdminRow
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int ProjectCount { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string UserId { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class UpdateUserRequest
    {
        public string UserId { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? UserName { get; set; }
    }
}

using FamoBoard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FamoBoard.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ProfileController(UserManager<ApplicationUser> um, IWebHostEnvironment env)
        { _userManager = um; _env = env; }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] ProfileUpdateDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                user.DisplayName = dto.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                    return Json(new { success = false, message = "رمز فعلی را وارد کنید" });
                var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
                if (!result.Succeeded)
                    return Json(new { success = false, message = result.Errors.FirstOrDefault()?.Description ?? "خطا در تغییر رمز" });
            }

            await _userManager.UpdateAsync(user);
            return Json(new { success = true, message = "تغییرات ذخیره شد ✓" });
        }

        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
                return Json(new { success = false, message = "فایل خالی است" });
            if (avatar.Length > 5 * 1024 * 1024)
                return Json(new { success = false, message = "حداکثر حجم ۵ مگابایت" });
            if (!avatar.ContentType.StartsWith("image/"))
                return Json(new { success = false, message = "فقط فایل تصویری مجاز است" });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(avatar.FileName);
            var fname = user.Id + ext;
            var path = Path.Combine(folder, fname);

            using var stream = System.IO.File.Create(path);
            await avatar.CopyToAsync(stream);

            var url = $"/uploads/avatars/{fname}";
            user.AvatarUrl = url;
            await _userManager.UpdateAsync(user);

            return Json(new { success = true, url });
        }
    }

    public class ProfileUpdateDto
    {
        public string? DisplayName { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}

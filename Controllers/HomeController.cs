using FamoBoard.Data;
using FamoBoard.Models;
using FamoBoard.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamoBoard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> um, IProjectService ps) { _db = db; _userManager = um; _projectService = ps; }

        public IActionResult Landing() => View();

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var projects = await _db.ProjectMembers.Include(m => m.Project).Where(m => m.UserId == userId).Select(m => m.Project!).ToListAsync();
            return View(projects);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject(string name, string description)
        {
            var userId = _userManager.GetUserId(User)!;
            var p = await _projectService.CreateProjectAsync(name, description, userId);
            return RedirectToAction("Board", "Board", new { id = p.Id });
        }
    }

    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, ApplicationDbContext db) { _userManager = um; _signInManager = sm; _db = db; }

        [HttpGet] public IActionResult Login(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(); }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "نام کاربری و رمز عبور الزامی است" });
            var user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            if (user == null) return Json(new { success = false, message = "نام کاربری یا رمز عبور اشتباه است" });
            var result = await _signInManager.PasswordSignInAsync(user, password, true, false);
            if (result.Succeeded) return Json(new { success = true, returnUrl = returnUrl ?? "/" });
            return Json(new { success = false, message = "نام کاربری یا رمز عبور اشتباه است" });
        }

        [HttpGet] public IActionResult Register(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(); }

        [HttpPost]
        public async Task<IActionResult> Register(string displayName, string username, string password, string confirmPassword, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "همه فیلدها الزامی است" });
            if (password != confirmPassword) return Json(new { success = false, message = "رمز عبور و تکرار آن یکسان نیستند" });
            if (password.Length < 6) return Json(new { success = false, message = "رمز عبور باید حداقل ۶ کاراکتر باشد" });
            if (await _userManager.FindByNameAsync(username) != null) return Json(new { success = false, message = "این نام کاربری قبلاً ثبت شده است" });

            var user = new ApplicationUser { UserName = username, DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded) return Json(new { success = false, message = string.Join(" | ", result.Errors.Select(e => e.Description)) });
            await _signInManager.SignInAsync(user, true);
            return Json(new { success = true, returnUrl = returnUrl ?? "/" });
        }

        [HttpPost]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("GoogleCallback", "Account", new { returnUrl });
            var props = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction("Login");
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (result.Succeeded) return LocalRedirect(returnUrl ?? "/");

            var email = info.Principal.FindFirstValue(ClaimTypes.Email)!;
            var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, false);
                return LocalRedirect(returnUrl ?? "/");
            }
            return RedirectToAction("Login");
        }

        [HttpPost] [Authorize]
        public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return RedirectToAction("Landing", "Home"); }

        [HttpGet] [Authorize]
        public async Task<IActionResult> JoinProject(string token)
        {
            var p = await _db.Projects.FirstOrDefaultAsync(x => x.InviteToken == token);
            if (p == null) return NotFound("لینک دعوت نامعتبر است");
            return View(model: (p.Id, p.Name, token));
        }
    }
}

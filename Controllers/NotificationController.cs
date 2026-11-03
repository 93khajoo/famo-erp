using FamoBoard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FamoBoard.Models;

namespace FamoBoard.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notif;
        private readonly UserManager<ApplicationUser> _userManager;
        public NotificationController(INotificationService notif, UserManager<ApplicationUser> um) { _notif = notif; _userManager = um; }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = _userManager.GetUserId(User)!;
            var items = await _notif.GetUnreadAsync(userId);
            return Json(items.Select(n => new { id = n.Id, message = n.Message, type = n.Type.ToString(), cardId = n.CardId, projectId = n.ProjectId, createdAt = n.CreatedAt, isRead = n.IsRead }));
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead([FromBody] Dictionary<string,int> body)
        {
            var userId = _userManager.GetUserId(User)!;
            await _notif.MarkReadAsync(body["id"], userId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userManager.GetUserId(User)!;
            await _notif.MarkAllReadAsync(userId);
            return Ok();
        }
    }
}

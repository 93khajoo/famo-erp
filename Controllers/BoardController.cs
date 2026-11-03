using FamoBoard.Data;
using FamoBoard.Models;
using FamoBoard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FamoBoard.Controllers
{
    [Authorize]
    public class BoardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _proj;
        private readonly INotificationService _notif;
        public BoardController(ApplicationDbContext db, UserManager<ApplicationUser> um, IProjectService proj, INotificationService notif)
        { _db = db; _userManager = um; _proj = proj; _notif = notif; }

        public async Task<IActionResult> Board(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var p = await _proj.GetProjectAsync(id, userId);
            if (p == null) return Forbid();
            ViewBag.IsScrumMaster = await _proj.IsScrumMasterAsync(id, userId);
            ViewBag.UserId = userId;
            var currentUser = await _userManager.FindByIdAsync(userId);
            ViewBag.CurrentUserName = currentUser?.DisplayName ?? currentUser?.UserName ?? "";
            ViewBag.TaskTypes = await _db.TaskTypes.Where(tt => tt.ProjectId == id && tt.IsActive).ToListAsync();
            ViewBag.ClientProjects = await _db.ClientProjects.Where(cp => cp.ProjectId == id && cp.IsActive).ToListAsync();
            return View(p);
        }

        [HttpPost]
        public async Task<IActionResult> AddList(int projectId, [FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsProjectMemberAsync(projectId, userId)) return Forbid();
            var name = body["name"];
            var max = await _db.BoardLists.Where(l => l.ProjectId == projectId).MaxAsync(l => (int?)l.Order) ?? -1;
            _db.BoardLists.Add(new BoardList { Name = name, ProjectId = projectId, Order = max + 1 });
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateInvite(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var token = await _proj.GenerateInviteTokenAsync(projectId);
            var link = Url.Action("JoinProject", "Account", new { token }, Request.Scheme);
            return Json(new { link });
        }

        [HttpPost]
        public async Task<IActionResult> SendInvite(int projectId, [FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null) return NotFound();
            var token = project.InviteToken ?? await _proj.GenerateInviteTokenAsync(projectId);
            var link = Url.Action("JoinProject", "Account", new { token }, Request.Scheme);
            var invitedUser = await _userManager.FindByEmailAsync((body.ContainsKey("email") ? body["email"] : ""));
            var hasAccount = invitedUser != null;
            if (invitedUser != null)
                await _notif.CreateAsync(invitedUser.Id, $"🎉 به پروژه «{project.Name}» دعوت شدید!", NotificationType.Invite, projectId: project.Id);
            return Json(new { link, hasAccount });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMember(int projectId, [FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _proj.RemoveMemberAsync(projectId, body["memberId"], userId);
            return ok ? Ok() : Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> SetMemberRate(int projectId, [FromBody] SetMemberRateDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var m = await _db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == dto.MemberId);
            if (m == null) return NotFound();
            m.HourlyRate = dto.HourlyRate;
            m.TaskRate = dto.TaskRate;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> JoinProjectConfirm([FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _proj.JoinProjectAsync(body["token"], userId);
            if (!ok) return NotFound();
            var p = await _db.Projects.FirstOrDefaultAsync(x => x.InviteToken == body["token"]);
            return Json(new { projectId = p?.Id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProject(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var p = await _db.Projects.FindAsync(projectId);
            if (p == null) return NotFound();
            _db.Projects.Remove(p);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var p = await _db.Projects.FindAsync(projectId);
            if (p == null) return NotFound();
            p.Name = body.ContainsKey("name") ? body["name"] : p.Name;
            p.Description = body.ContainsKey("description") ? body["description"] : p.Description;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteList(int listId)
        {
            var userId = _userManager.GetUserId(User)!;
            var list = await _db.BoardLists.FindAsync(listId);
            if (list == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(list.ProjectId, userId)) return Forbid();
            _db.BoardLists.Remove(list);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateList(int listId, [FromBody] Dictionary<string,string> body)
        {
            var userId = _userManager.GetUserId(User)!;
            var list = await _db.BoardLists.FindAsync(listId);
            if (list == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(list.ProjectId, userId)) return Forbid();
            list.Name = body.ContainsKey("name") ? body["name"] : list.Name;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    [Authorize]
    public class CardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICardService _card;
        private readonly ITimeLogService _timer;
        private readonly IProjectService _proj;
        private readonly INotificationService _notif;
        private readonly IWebHostEnvironment _env;
        public CardController(ApplicationDbContext db, UserManager<ApplicationUser> um, ICardService card, ITimeLogService timer, IProjectService proj, INotificationService notif, IWebHostEnvironment env)
        { _db = db; _userManager = um; _card = card; _timer = timer; _proj = proj; _notif = notif; _env = env; }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCardDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var list = await _db.BoardLists.FindAsync(dto.ListId);
            if (list == null || !await _proj.IsProjectMemberAsync(list.ProjectId, userId)) return Forbid();
            var c = await _card.CreateCardAsync(dto.ListId, dto.Title, dto.Description ?? "", dto.Color ?? "#4DB6AC", userId);
            bool needsSave = false;
            if (dto.TaskTypeId.HasValue && dto.TaskTypeId > 0)
            {
                var tt = await _db.TaskTypes.FindAsync(dto.TaskTypeId.Value);
                if (tt != null && tt.ProjectId == list.ProjectId) { c.TaskTypeId = dto.TaskTypeId; needsSave = true; }
            }
            if (dto.ClientProjectId.HasValue && dto.ClientProjectId > 0)
            {
                var cp = await _db.ClientProjects.FindAsync(dto.ClientProjectId.Value);
                if (cp != null && cp.ProjectId == list.ProjectId) { c.ClientProjectId = dto.ClientProjectId; needsSave = true; }
            }
            if (needsSave) await _db.SaveChangesAsync();
            return Json(new { id = c.Id, title = c.Title, color = c.Color });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var c = await _card.GetCardAsync(id, userId);
            if (c == null) return NotFound();
            ViewBag.UserId = userId;
            ViewBag.IsScrumMaster = await _proj.IsScrumMasterAsync(c.ProjectId, userId);
            ViewBag.ProjectMembers = await _db.ProjectMembers.Include(m => m.User).Where(m => m.ProjectId == c.ProjectId).ToListAsync();
            ViewBag.ActiveTimer = await _timer.GetActiveTimerAsync(id, userId);
            ViewBag.TaskTypes = await _db.TaskTypes.Where(tt => tt.ProjectId == c.ProjectId && tt.IsActive).ToListAsync();
            return PartialView("_CardDetail", c);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCardDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            DateTime? deadline = !string.IsNullOrEmpty(dto.Deadline) ? DateTime.Parse(dto.Deadline) : null;
            DateTime? startedAt = !string.IsNullOrEmpty(dto.StartedAt) ? DateTime.Parse(dto.StartedAt) : null;
            var ok = await _card.UpdateCardAsync(id, dto.Title, dto.Description ?? "", dto.Color ?? "#4DB6AC", deadline, startedAt, userId);
            return ok ? Ok() : Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> Move([FromBody] MoveCardDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _card.MoveCardAsync(dto.CardId, dto.ToListId, userId);
            return ok ? Ok() : Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> Assign([FromBody] CardUserDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _card.AssignUserAsync(dto.CardId, dto.UserId, userId);
            return ok ? Ok() : Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> Unassign([FromBody] CardUserDto dto)
        {
            var ok = await _card.UnassignUserAsync(dto.CardId, dto.UserId);
            return ok ? Ok() : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int cardId, [FromForm] string content, [FromForm] int? parentId, IFormFile? attachment, [FromForm] string? linkUrl)
        {
            var userId = _userManager.GetUserId(User)!;
            var c = await _db.Cards.Include(x => x.Assignees).Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == cardId);
            if (c == null || !await _proj.IsProjectMemberAsync(c.ProjectId, userId)) return Forbid();
            var commenter = await _userManager.FindByIdAsync(userId);
            var comment = new Comment { Content = content, CardId = cardId, AuthorId = userId, ParentCommentId = parentId };
            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            if (attachment != null && attachment.Length > 0)
            {
                var ext = Path.GetExtension(attachment.FileName);
                var fname = $"{Guid.NewGuid()}{ext}";
                var dir = Path.Combine(_env.WebRootPath, "uploads", "comments");
                Directory.CreateDirectory(dir);
                using var fs = new FileStream(Path.Combine(dir, fname), FileMode.Create);
                await attachment.CopyToAsync(fs);
                var type = ext.ToLower() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" ? "image" : "pdf";
                _db.CommentAttachments.Add(new CommentAttachment { CommentId = comment.Id, FileName = attachment.FileName, FilePath = $"/uploads/comments/{fname}", FileType = type });
            }
            else if (!string.IsNullOrWhiteSpace(linkUrl))
            {
                _db.CommentAttachments.Add(new CommentAttachment { CommentId = comment.Id, FileName = linkUrl, FilePath = linkUrl, FileType = "link", LinkUrl = linkUrl });
            }

            var toNotify = c.Assignees.Where(a => a.UserId != userId).Select(a => a.UserId).ToList();
            if (parentId.HasValue)
            {
                var parent = await _db.Comments.FindAsync(parentId.Value);
                if (parent != null && parent.AuthorId != userId && !toNotify.Contains(parent.AuthorId)) toNotify.Add(parent.AuthorId);
            }
            if (toNotify.Any())
                await _notif.CreateForUsersAsync(toNotify, $"💬 {commenter?.Name} روی کارت «{c.Title}» کامنت گذاشت", NotificationType.Comment, c.Id, c.ProjectId);

            await _db.SaveChangesAsync();
            return Ok(new { id = comment.Id });
        }

        [HttpPost]
        public async Task<IActionResult> ReactToComment([FromBody] ReactDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var commentId = dto.CommentId;
            var emoji = dto.Emoji;
            var existing = await _db.CommentReactions.FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId && r.Emoji == emoji);
            if (existing != null) _db.CommentReactions.Remove(existing);
            else _db.CommentReactions.Add(new CommentReaction { CommentId = commentId, UserId = userId, Emoji = emoji });
            await _db.SaveChangesAsync();
            var count = await _db.CommentReactions.CountAsync(r => r.CommentId == commentId && r.Emoji == emoji);
            return Json(new { count, removed = existing != null });
        }

        [HttpPost]
        public async Task<IActionResult> AddCheckList([FromBody] AddCheckListDto dto)
        {
            var cardId = dto.CardId;
            var cl = new CheckList { Name = dto.Name, CardId = cardId };
            _db.CheckLists.Add(cl);
            await _db.SaveChangesAsync();
            return Json(new { id = cl.Id, name = cl.Name });
        }

        [HttpPost]
        public async Task<IActionResult> AddCheckListItem([FromBody] AddCheckListItemDto dto)
        {
            var item = new CheckListItem { Title = dto.Title, CheckListId = dto.CheckListId };
            _db.CheckListItems.Add(item);
            await _db.SaveChangesAsync();
            return Json(new { id = item.Id });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCheckListItem([FromBody] IdDto dto)
        {
            var item = await _db.CheckListItems.FindAsync(dto.ItemId);
            if (item == null) return NotFound();
            item.IsCompleted = !item.IsCompleted;
            await _db.SaveChangesAsync();
            return Json(new { isCompleted = item.IsCompleted });
        }

        [HttpPost]
        public async Task<IActionResult> StartTimer([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var log = await _timer.StartTimerAsync(dto.CardId, userId);
            return Json(new { logId = log.Id, startTime = log.StartTime.ToString("o") });
        }

        [HttpPost]
        public async Task<IActionResult> StopTimer([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var log = await _timer.StopTimerAsync(dto.CardId, userId);
            if (log == null) return NotFound();
            return Json(new { duration = log.DurationSeconds });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCard([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var card = await _db.Cards.FindAsync(dto.CardId);
            if (card == null) return NotFound();
            var isSM = await _proj.IsScrumMasterAsync(card.ProjectId, userId);
            if (!isSM && card.CreatedById != userId) return Forbid();
            _db.Cards.Remove(card);
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── ARCHIVE ───────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ArchiveCard([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var card = await _db.Cards.FindAsync(dto.CardId);
            if (card == null) return NotFound();
            if (!await _proj.IsProjectMemberAsync(card.ProjectId, userId)) return Forbid();
            card.IsArchived = true;
            card.ArchivedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UnarchiveCard([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var card = await _db.Cards.FindAsync(dto.CardId);
            if (card == null) return NotFound();
            if (!await _proj.IsProjectMemberAsync(card.ProjectId, userId)) return Forbid();
            card.IsArchived = false;
            card.ArchivedAt = null;
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> ArchivedCards(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsProjectMemberAsync(projectId, userId)) return Forbid();
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null) return NotFound();
            var cards = await _db.Cards
                .Include(c => c.BoardList)
                .Include(c => c.Assignees).ThenInclude(a => a.User)
                .Include(c => c.TaskType)
                .Where(c => c.ProjectId == projectId && c.IsArchived)
                .OrderByDescending(c => c.ArchivedAt)
                .ToListAsync();
            ViewBag.ProjectId = projectId;
            ViewBag.ProjectName = project.Name;
            ViewBag.IsScrumMaster = await _proj.IsScrumMasterAsync(projectId, userId);
            return View(cards);
        }
        // ─────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> DeleteComment([FromBody] IdDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var comment = await _db.Comments.Include(c => c.Card).FirstOrDefaultAsync(c => c.Id == dto.CommentId);
            if (comment == null) return NotFound();
            var isSM = await _proj.IsScrumMasterAsync(comment.Card!.ProjectId, userId);
            if (!isSM && comment.AuthorId != userId) return Forbid();
            _db.Comments.Remove(comment);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateComment([FromBody] UpdateCommentDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var comment = await _db.Comments.FindAsync(dto.CommentId);
            if (comment == null) return NotFound();
            if (comment.AuthorId != userId) return Forbid();
            comment.Content = dto.Content;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCheckList([FromBody] IdDto dto)
        {
            var cl = await _db.CheckLists.Include(c => c.Card).FirstOrDefaultAsync(c => c.Id == dto.CheckListId);
            if (cl == null) return NotFound();
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(cl.Card!.ProjectId, userId)) return Forbid();
            _db.CheckLists.Remove(cl);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCheckListItem([FromBody] IdDto dto)
        {
            var item = await _db.CheckListItems.FindAsync(dto.ItemId);
            if (item == null) return NotFound();
            _db.CheckListItems.Remove(item);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    public class ReportViewModel
    {
        public ProjectSummaryReport Summary { get; set; } = new();
        public List<UserTimeReport> UserReport { get; set; } = new();
        public List<CardTimeReport> CardReport { get; set; } = new();
        public List<UserSalaryReport> SalaryReport { get; set; } = new();
        public List<ProjectMember> Members { get; set; } = new();
        public List<FamoBoard.Models.TaskType> TaskTypes { get; set; } = new();
    }

    public class AddSettlementDto
    {
        public int ProjectId { get; set; }
        public string MemberId { get; set; } = "";
        public string SettledAt { get; set; } = ""; // yyyy/MM/dd شمسی
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    public class AddWageAdjustmentDto
    {
        public int ProjectId { get; set; }
        public string MemberId { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    public class DeleteWageAdjustmentDto
    {
        public int Id { get; set; }
    }

    public class SaveVideoUploadDto
    {
        public int CardId { get; set; }
        public bool IsUploaded { get; set; }
        public string? ScheduledUploadAt { get; set; } // "1403/07/15 14:30"
        public string? ProjectName { get; set; }
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public long? Shares { get; set; }
        public long? Reposts { get; set; }
        public decimal? SkipRate { get; set; }
    }

    public class SaveTaskTypeDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = "";
        public decimal Rate { get; set; }
    }

    public class SaveClientProjectDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#4DB6AC";
    }

    public class SaveContractDto
    {
        public int Id { get; set; }
        public int ClientProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Description { get; set; }
        public List<ContractItemDto> Items { get; set; } = new();
    }

    public class ContractItemDto
    {
        public int TaskTypeId { get; set; }
        public int CountPerMonth { get; set; }
        public string? Note { get; set; }
    }

    public class SetCardTaskTypeDto
    {
        public int CardId { get; set; }
        public int TaskTypeId { get; set; }
        public DateTime? StartedAt { get; set; }
    }

    [Authorize]
    public class ReportController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IReportService _report;
        private readonly IProjectService _proj;
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notif;
        public ReportController(UserManager<ApplicationUser> um, IReportService report, IProjectService proj, ApplicationDbContext db, INotificationService notif)
        { _userManager = um; _report = report; _proj = proj; _db = db; _notif = notif; }

        private static string ToJalali(DateTime dt, System.Globalization.PersianCalendar pc)
        {
            try { return $"{pc.GetYear(dt)}/{pc.GetMonth(dt):D2}/{pc.GetDayOfMonth(dt):D2}"; }
            catch { return ""; }
        }

        public async Task<IActionResult> Index(int projectId, string? from = null, string? to = null,
            string? tab = null, bool unsettledOnly = true)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsProjectMemberAsync(projectId, userId)) return Forbid();
            var isSM = await _proj.IsScrumMasterAsync(projectId, userId);
            var project = await _db.Projects.FindAsync(projectId);

            DateTime? fromDate = null, toDate = null;
            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) fromDate = f;
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var t)) toDate = t;

            ViewBag.ProjectId = projectId;
            ViewBag.ProjectName = project?.Name ?? "";
            ViewBag.IsScrumMaster = isSM;
            ViewBag.From = from ?? "";
            ViewBag.To = to ?? "";
            ViewBag.Tab = tab ?? "summary";
            ViewBag.UnsettledOnly = unsettledOnly;

            if (isSM)
            {
                var summary    = await _report.GetProjectSummaryAsync(projectId, fromDate, toDate);
                var userReport = await _report.GetProjectTimeReportAsync(projectId, fromDate, toDate);
                var cardReport = await _report.GetCardTimeReportAsync(projectId, fromDate, toDate);
                var salary     = await _report.GetSalaryReportAsync(projectId, fromDate, toDate, unsettledOnly);
                var members    = await _db.ProjectMembers.Include(m => m.User).Where(m => m.ProjectId == projectId).ToListAsync();
                var taskTypes  = await _db.TaskTypes.Where(tt => tt.ProjectId == projectId && tt.IsActive).ToListAsync();
                var clientProjects = await _db.ClientProjects.Where(cp => cp.ProjectId == projectId && cp.IsActive).ToListAsync();
                ViewBag.ClientProjectsForReport = clientProjects;
                return View(new ReportViewModel
                {
                    Summary = summary, UserReport = userReport,
                    CardReport = cardReport, SalaryReport = salary,
                    Members = members, TaskTypes = taskTypes
                });
            }
            else
            {
                var myReports  = await _report.GetMyReportAsync(userId, projectId, fromDate, toDate);
                var myReport   = myReports.FirstOrDefault();
                var allSalaries= await _report.GetSalaryReportAsync(projectId, fromDate, toDate, unsettledOnly);
                ViewBag.MySalary = allSalaries.FirstOrDefault(s => s.UserId == userId);
                return View("MyReport", myReport);
            }
        }

        // ── SETTLEMENT ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddSettlement([FromBody] AddSettlementDto dto)
        {
          try {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(dto.ProjectId, userId)) return Forbid();

            var member = await _db.ProjectMembers
                .Include(m => m.Settlements)
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.ProjectId == dto.ProjectId && m.UserId == dto.MemberId);
            if (member == null) return NotFound();

            var pc = new System.Globalization.PersianCalendar();
            DateTime settledAt;
            try {
                var parts = dto.SettledAt.Split('/');
                settledAt = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
            } catch { settledAt = DateTime.UtcNow; }

            // تعیین بازه زمانی این تصفیه
            var prevSettlement = member.Settlements
                .Where(s => s.SettledAt < settledAt)
                .OrderByDescending(s => s.SettledAt)
                .FirstOrDefault();
            var periodFrom = prevSettlement?.SettledAt ?? DateTime.MinValue;
            bool isFirst = prevSettlement == null;

            // ثبت تصفیه
            var settlement = new Settlement
            {
                ProjectMemberId = member.Id,
                SettledAt = settledAt,
                Amount = dto.Amount,
                Note = dto.Note
            };
            _db.Settlements.Add(settlement);
            await _db.SaveChangesAsync();

            // شناسه کارت‌هایی که این عضو روشون assignee هست
            var assignedCardIds = await _db.CardAssignees
                .Where(a => a.UserId == dto.MemberId)
                .Select(a => a.CardId)
                .ToListAsync();

            // کارت‌هایی که این عضو در بازه مربوطه داشته
            var cardsQuery = _db.Cards
                .Include(c => c.TaskType)
                .Include(c => c.TimeLogs)
                .Include(c => c.ClientProject)
                .Where(c => c.ProjectId == dto.ProjectId
                    && assignedCardIds.Contains(c.Id));

            if (!isFirst)
                cardsQuery = cardsQuery.Where(c => c.CreatedAt > periodFrom);
            cardsQuery = cardsQuery.Where(c => c.CreatedAt <= settledAt);

            var cards = await cardsQuery.ToListAsync();

            // ساخت JSON گزارش
            var cardItems = cards.Select(c =>
            {
                var lastLog = c.TimeLogs.OrderByDescending(tl => tl.StartTime).FirstOrDefault();
                DateTime? completedAt = lastLog?.EndTime;
                decimal rate = c.TaskType?.Rate ?? member.TaskRate ?? member.HourlyRate ?? 0;
                return new
                {
                    title = c.Title,
                    taskType = c.TaskType?.Name ?? "—",
                    clientProject = c.ClientProject?.Name ?? "",
                    rate,
                    createdAtJ = ToJalali(c.CreatedAt.ToLocalTime(), pc),
                    completedAtJ = completedAt.HasValue ? ToJalali(completedAt.Value.ToLocalTime(), pc) : (string?)null,
                    deadlineJ = c.Deadline.HasValue ? ToJalali(c.Deadline.Value.ToLocalTime(), pc) : (string?)null
                };
            }).ToList();

            var summary = cardItems
                .GroupBy(c => c.taskType)
                .Select(g => new { typeName = g.Key, count = g.Count(), total = g.Sum(x => x.rate) })
                .ToList();

            var settledAtJ = ToJalali(settledAt.ToLocalTime(), pc);
            var periodFromJ = isFirst ? "" : ToJalali(periodFrom.ToLocalTime(), pc);

            var reportData = new
            {
                memberName = member.User?.DisplayName ?? member.User?.Email ?? "",
                settledAtJ,
                periodFromJ,
                isFirstSettlement = isFirst,
                note = dto.Note ?? "",
                cards = cardItems,
                summary
            };
            var reportJson = JsonSerializer.Serialize(reportData);

            // ذخیره گزارش کارتابل
            var report = new SettlementReport
            {
                ProjectMemberId = member.Id,
                SettlementId = settlement.Id,
                PeriodFrom = isFirst ? DateTime.MinValue : periodFrom,
                PeriodTo = settledAt,
                Amount = dto.Amount,
                ReportJson = reportJson
            };
            _db.SettlementReports.Add(report);
            await _db.SaveChangesAsync();

            // ارسال نوتیفیکیشن به عضو
            var amountStr = dto.Amount.ToString("N0");
            var msg = $"💰 تصفیه حساب شما در تاریخ {settledAtJ} به مبلغ {amountStr} تومان انجام شد. گزارش آن در کارتابل شما قرار گرفت.";
            await _notif.CreateAsync(dto.MemberId, msg, NotificationType.Info, null, dto.ProjectId);

            return Json(new { success = true });
          } catch (Exception ex) {
            return Json(new { success = false, message = ex.Message + " | " + ex.InnerException?.Message });
          }
        }

        // ── KARTABL — لیست گزارش‌های عضو ────────────────────
        public async Task<IActionResult> Kartabl()
        {
            var userId = _userManager.GetUserId(User)!;
            var reports = await _db.SettlementReports
                .Include(r => r.ProjectMember!).ThenInclude(m => m.Project)
                .Where(r => r.ProjectMember!.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(reports);
        }

        public async Task<IActionResult> KartablReport(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var report = await _db.SettlementReports
                .Include(r => r.ProjectMember!).ThenInclude(m => m.Project)
                .FirstOrDefaultAsync(r => r.Id == id && r.ProjectMember!.UserId == userId);
            if (report == null) return NotFound();
            return View(report);
        }

        // ── PENDING WAGE ADJUSTMENT ───────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddWageAdjustment([FromBody] AddWageAdjustmentDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(dto.ProjectId, userId)) return Forbid();

            var member = await _db.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == dto.ProjectId && m.UserId == dto.MemberId);
            if (member == null) return NotFound();

            _db.PendingWageAdjustments.Add(new FamoBoard.Models.PendingWageAdjustment
            {
                ProjectMemberId = member.Id,
                Amount = dto.Amount,
                Note = dto.Note,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWageAdjustment([FromBody] DeleteWageAdjustmentDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var adj = await _db.PendingWageAdjustments
                .Include(a => a.ProjectMember)
                .FirstOrDefaultAsync(a => a.Id == dto.Id);
            if (adj == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(adj.ProjectMember!.ProjectId, userId)) return Forbid();

            _db.PendingWageAdjustments.Remove(adj);
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        // ── VIDEO UPLOADS ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetVideoCards(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();

            var videoTaskTypeIds = await _db.TaskTypes
                .Where(tt => tt.ProjectId == projectId && tt.IsActive && tt.Name.Contains("ویدئو"))
                .Select(tt => tt.Id)
                .ToListAsync();

            var cards = await _db.Cards
                .Include(c => c.TaskType)
                .Include(c => c.ClientProject)
                .Include(c => c.Assignees).ThenInclude(a => a.User)
                .Where(c => c.ProjectId == projectId && c.TaskTypeId != null && videoTaskTypeIds.Contains(c.TaskTypeId.Value))
                .ToListAsync();

            var cardIds = cards.Select(c => c.Id).ToList();
            var uploads = await _db.VideoUploads
                .Where(v => cardIds.Contains(v.CardId))
                .ToListAsync();

            var pc = new System.Globalization.PersianCalendar();
            string ToShamsi(DateTime dt) => $"{pc.GetYear(dt)}/{pc.GetMonth(dt):D2}/{pc.GetDayOfMonth(dt):D2}";

            var result = cards.Select(c => {
                var up = uploads.FirstOrDefault(v => v.CardId == c.Id);
                string? scheduledDateStr = null;
                string? scheduledTimeStr = null;
                string? scheduledDisplayStr = null;
                if (up?.ScheduledUploadAt != null)
                {
                    var local = up.ScheduledUploadAt.Value.ToLocalTime();
                    scheduledDateStr = ToShamsi(local);
                    scheduledTimeStr = local.ToString("HH:mm");
                    scheduledDisplayStr = $"{scheduledDateStr} {scheduledTimeStr}";
                }
                return new {
                    cardId = c.Id,
                    title = c.Title,
                    taskType = c.TaskType?.Name ?? "",
                    assignees = c.Assignees.Select(a => a.User?.DisplayName ?? a.User?.Email ?? "?"),
                    isUploaded = up?.IsUploaded ?? false,
                    scheduledUploadAt = scheduledDisplayStr,
                    scheduledDate = scheduledDateStr,
                    scheduledTime = scheduledTimeStr,
                    projectName = up?.ProjectName ?? "",
                    clientProjectName = c.ClientProject?.Name ?? "",
                    views = up?.Views,
                    likes = up?.Likes,
                    shares = up?.Shares,
                    reposts = up?.Reposts,
                    skipRate = up?.SkipRate,
                    hasRecord = up != null
                };
            }).ToList();

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveVideoUpload([FromBody] SaveVideoUploadDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var card = await _db.Cards.FindAsync(dto.CardId);
            if (card == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(card.ProjectId, userId)) return Forbid();

            var up = await _db.VideoUploads.FirstOrDefaultAsync(v => v.CardId == dto.CardId);
            if (up == null) { up = new FamoBoard.Models.VideoUpload { CardId = dto.CardId }; _db.VideoUploads.Add(up); }

            up.IsUploaded = dto.IsUploaded;
            up.ProjectName = dto.ProjectName;
            up.Views = dto.Views;
            up.Likes = dto.Likes;
            up.Shares = dto.Shares;
            up.Reposts = dto.Reposts;
            up.SkipRate = dto.SkipRate;
            up.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(dto.ScheduledUploadAt))
            {
                try {
                    var pc = new System.Globalization.PersianCalendar();
                    var parts = dto.ScheduledUploadAt.Split(' ');
                    var dateParts = parts[0].Split('/');
                    var timeParts = parts.Length > 1 ? parts[1].Split(':') : new[]{"0","0"};
                    up.ScheduledUploadAt = pc.ToDateTime(
                        int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]),
                        int.Parse(timeParts[0]), int.Parse(timeParts[1]), 0, 0
                    ).ToUniversalTime();
                } catch { up.ScheduledUploadAt = null; }
            }
            else up.ScheduledUploadAt = null;

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        // ── CLIENT PROJECTS ───────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetClientProjects(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsProjectMemberAsync(projectId, userId)) return Forbid();
            var list = await _db.ClientProjects
                .Where(cp => cp.ProjectId == projectId && cp.IsActive)
                .Select(cp => new { cp.Id, cp.Name, cp.Color })
                .ToListAsync();
            return Json(list);
        }

        [HttpPost]
        public async Task<IActionResult> SaveClientProject([FromBody] SaveClientProjectDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(dto.ProjectId, userId)) return Forbid();
            FamoBoard.Models.ClientProject cp;
            if (dto.Id > 0)
            {
                cp = await _db.ClientProjects.FindAsync(dto.Id) ?? new FamoBoard.Models.ClientProject();
                cp.Name = dto.Name;
                cp.Color = string.IsNullOrEmpty(dto.Color) ? cp.Color : dto.Color;
            }
            else
            {
                cp = new FamoBoard.Models.ClientProject { ProjectId = dto.ProjectId, Name = dto.Name, Color = dto.Color };
                _db.ClientProjects.Add(cp);
            }
            await _db.SaveChangesAsync();
            return Json(new { success = true, id = cp.Id, name = cp.Name, color = cp.Color });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteClientProject([FromBody] IdDto dto)
        {
            var cp = await _db.ClientProjects.FindAsync(dto.ItemId);
            if (cp == null) return NotFound();
            cp.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── CLIENT CONTRACTS ──────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetContracts(int projectId)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(projectId, userId)) return Forbid();
            var pc = new System.Globalization.PersianCalendar();
            string ToJ(DateTime d) => $"{pc.GetYear(d)}/{pc.GetMonth(d):D2}/{pc.GetDayOfMonth(d):D2}";
            var contracts = await _db.ClientContracts
                .Include(cc => cc.ClientProject)
                .Include(cc => cc.Items).ThenInclude(ci => ci.TaskType)
                .Where(cc => cc.ClientProject!.ProjectId == projectId && cc.IsActive)
                .OrderByDescending(cc => cc.StartDate)
                .ToListAsync();
            var result = contracts.Select(cc => new {
                id = cc.Id,
                clientProjectId = cc.ClientProjectId,
                clientProjectName = cc.ClientProject!.Name,
                clientProjectColor = cc.ClientProject.Color,
                startDateJ = ToJ(cc.StartDate),
                endDateJ = ToJ(cc.EndDate),
                description = cc.Description ?? "",
                items = cc.Items.Select(ci => new {
                    id = ci.Id,
                    taskTypeId = ci.TaskTypeId,
                    taskTypeName = ci.TaskType!.Name,
                    countPerMonth = ci.CountPerMonth,
                    note = ci.Note ?? ""
                })
            });
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetContractReport(int contractId)
        {
            var userId = _userManager.GetUserId(User)!;
            var contract = await _db.ClientContracts
                .Include(cc => cc.ClientProject)
                .Include(cc => cc.Items).ThenInclude(ci => ci.TaskType)
                .FirstOrDefaultAsync(cc => cc.Id == contractId);
            if (contract == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(contract.ClientProject!.ProjectId, userId)) return Forbid();

            var pc = new System.Globalization.PersianCalendar();
            string ToJ(DateTime d) => $"{pc.GetYear(d)}/{pc.GetMonth(d):D2}/{pc.GetDayOfMonth(d):D2}";

            // مدت قرارداد به ماه
            var start = contract.StartDate;
            var end = contract.EndDate;
            var now = DateTime.UtcNow;
            int totalMonths = ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;
            int elapsedMonths = now > end ? totalMonths
                : now < start ? 0
                : ((now.Year - start.Year) * 12) + now.Month - start.Month + 1;

            // کارت‌های این ClientProject که آپلود شدن
            var cards = await _db.Cards
                .Include(c => c.TaskType)
                .Where(c => c.ClientProjectId == contract.ClientProjectId)
                .ToListAsync();
            var cardIds = cards.Select(c => c.Id).ToList();
            var uploads = await _db.VideoUploads
                .Where(v => cardIds.Contains(v.CardId) && v.IsUploaded)
                .OrderByDescending(v => v.UpdatedAt)
                .ToListAsync();

            // گزارش به تفکیک هر آیتم
            var itemReports = contract.Items.Select(ci => {
                int totalContractAllowed = ci.CountPerMonth * totalMonths;   // کل قرارداد (برای اطلاع)
                int expectedSoFar = ci.CountPerMonth * elapsedMonths;        // تا آخر ماه جاری
                // کارت‌های آپلودشده که tasktype مطابق باشه
                var matchedUploads = uploads
                    .Where(u => cards.FirstOrDefault(c => c.Id == u.CardId)?.TaskTypeId == ci.TaskTypeId)
                    .ToList();
                int uploadedCount = matchedUploads.Count;
                int remaining = expectedSoFar - uploadedCount;               // باقیمانده تا آخر ماه جاری
                int behindSchedule = remaining;                              // همان چیز — عقب‌افتادگی
                return new {
                    taskTypeId = ci.TaskTypeId,
                    taskTypeName = ci.TaskType!.Name,
                    note = ci.Note ?? "",
                    countPerMonth = ci.CountPerMonth,
                    totalMonths,
                    totalAllowed = totalContractAllowed,
                    uploadedCount,
                    remaining,
                    expectedSoFar,
                    behindSchedule,
                    uploads = matchedUploads.Select(u => new {
                        cardId = u.CardId,
                        title = cards.FirstOrDefault(c => c.Id == u.CardId)?.Title ?? "",
                        uploadedAtJ = u.UpdatedAt != default ? ToJ(u.UpdatedAt.ToLocalTime()) : ""
                    })
                };
            });

            return Json(new {
                contractId = contract.Id,
                clientProjectName = contract.ClientProject!.Name,
                clientProjectColor = contract.ClientProject.Color,
                startDateJ = ToJ(start),
                endDateJ = ToJ(end),
                nowJ = ToJ(now.ToLocalTime()),
                totalMonths,
                elapsedMonths,
                description = contract.Description ?? "",
                items = itemReports
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveContract([FromBody] SaveContractDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            // projectId رو از ClientProject بگیر
            var cp = await _db.ClientProjects.FindAsync(dto.ClientProjectId);
            if (cp == null) return NotFound();
            if (!await _proj.IsScrumMasterAsync(cp.ProjectId, userId)) return Forbid();

            FamoBoard.Models.ClientContract contract;
            if (dto.Id > 0)
            {
                contract = await _db.ClientContracts
                    .Include(cc => cc.Items)
                    .FirstAsync(cc => cc.Id == dto.Id);
                contract.StartDate = dto.StartDate;
                contract.EndDate = dto.EndDate;
                contract.Description = dto.Description;
                // آیتم‌های قبلی حذف و دوباره ذخیره
                _db.ContractItems.RemoveRange(contract.Items);
            }
            else
            {
                contract = new FamoBoard.Models.ClientContract {
                    ClientProjectId = dto.ClientProjectId,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    Description = dto.Description
                };
                _db.ClientContracts.Add(contract);
            }
            await _db.SaveChangesAsync();
            foreach (var item in dto.Items)
            {
                _db.ContractItems.Add(new FamoBoard.Models.ContractItem {
                    ContractId = contract.Id,
                    TaskTypeId = item.TaskTypeId,
                    CountPerMonth = item.CountPerMonth,
                    Note = item.Note
                });
            }
            await _db.SaveChangesAsync();
            return Json(new { success = true, id = contract.Id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteContract([FromBody] IdDto dto)
        {
            var contract = await _db.ClientContracts.FindAsync(dto.ItemId);
            if (contract == null) return NotFound();
            var cp = await _db.ClientProjects.FindAsync(contract.ClientProjectId);
            if (cp == null) return NotFound();
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(cp.ProjectId, userId)) return Forbid();
            contract.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── TASK TYPES ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> SaveTaskType([FromBody] SaveTaskTypeDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            if (!await _proj.IsScrumMasterAsync(dto.ProjectId, userId)) return Forbid();

            FamoBoard.Models.TaskType tt;
            if (dto.Id > 0)
            {
                tt = await _db.TaskTypes.FindAsync(dto.Id) ?? new FamoBoard.Models.TaskType();
                tt.Name = dto.Name; tt.Rate = dto.Rate;
            }
            else
            {
                tt = new FamoBoard.Models.TaskType { ProjectId = dto.ProjectId, Name = dto.Name, Rate = dto.Rate };
                _db.TaskTypes.Add(tt);
            }
            await _db.SaveChangesAsync();
            return Json(new { success = true, id = tt.Id, name = tt.Name, rate = tt.Rate });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTaskType([FromBody] IdDto dto)
        {
            var tt = await _db.TaskTypes.FindAsync(dto.ItemId);
            if (tt == null) return NotFound();
            tt.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── SET CARD TASKTYPE ─────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> SetCardTaskType([FromBody] SetCardTaskTypeDto dto)
        {
            var userId = _userManager.GetUserId(User)!;
            var card = await _db.Cards.FindAsync(dto.CardId);
            if (card == null) return NotFound();
            if (!await _proj.IsProjectMemberAsync(card.ProjectId, userId)) return Forbid();
            card.TaskTypeId = dto.TaskTypeId > 0 ? dto.TaskTypeId : null;
            card.StartedAt = dto.StartedAt;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    // ── REQUEST DTOs (typed, no JsonElement issues) ───────
    public class UpdateCardDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Deadline { get; set; }
        public string? StartedAt { get; set; }
    }

    public class CreateCardDto
    {
        public int ListId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Color { get; set; }
        public int? TaskTypeId { get; set; }
        public int? ClientProjectId { get; set; }
    }

    public class SetMemberRateDto
    {
        public string MemberId { get; set; } = "";
        public decimal? HourlyRate { get; set; }
        public decimal? TaskRate { get; set; }
    }

    public class CardUserDto
    {
        public int CardId { get; set; }
        public string UserId { get; set; } = "";
    }

    public class ReactDto
    {
        public int CommentId { get; set; }
        public string Emoji { get; set; } = "";
    }

    public class AddCheckListDto
    {
        public int CardId { get; set; }
        public string Name { get; set; } = "";
    }

    public class AddCheckListItemDto
    {
        public int CheckListId { get; set; }
        public string Title { get; set; } = "";
    }

    public class UpdateCommentDto
    {
        public int CommentId { get; set; }
        public string Content { get; set; } = "";
    }

    public class MoveCardDto
    {
        public int CardId { get; set; }
        public int ToListId { get; set; }
    }

    // Universal single-ID DTO — each action reads whichever field it needs
    public class IdDto
    {
        public int CardId { get; set; }
        public int CommentId { get; set; }
        public int CheckListId { get; set; }
        public int ItemId { get; set; }
    }
}

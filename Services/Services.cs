using FamoBoard.Data;
using FamoBoard.Models;
using Microsoft.EntityFrameworkCore;

namespace FamoBoard.Services
{
    // ── PROJECT ──────────────────────────────────────────────
    public interface IProjectService
    {
        Task<Project?> GetProjectAsync(int id, string userId);
        Task<Project> CreateProjectAsync(string name, string description, string scrumMasterId);
        Task<bool> IsProjectMemberAsync(int projectId, string userId);
        Task<bool> IsScrumMasterAsync(int projectId, string userId);
        Task<string> GenerateInviteTokenAsync(int projectId);
        Task<bool> JoinProjectAsync(string token, string userId);
        Task<bool> RemoveMemberAsync(int projectId, string memberId, string requesterId);
    }

    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _db;
        public ProjectService(ApplicationDbContext db) => _db = db;

        public async Task<Project?> GetProjectAsync(int id, string userId)
        {
            var p = await _db.Projects
                .Include(x => x.Members).ThenInclude(m => m.User)
                .Include(x => x.Lists.OrderBy(l => l.Order)).ThenInclude(l => l.Cards.Where(c => !c.IsArchived))
                    .ThenInclude(c => c.Assignees).ThenInclude(a => a.User)
                .Include(x => x.ScrumMaster)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null || !await IsProjectMemberAsync(id, userId)) return null;
            return p;
        }

        public async Task<Project> CreateProjectAsync(string name, string description, string scrumMasterId)
        {
            var p = new Project { Name = name, Description = description, ScrumMasterId = scrumMasterId, InviteToken = Guid.NewGuid().ToString("N") };
            _db.Projects.Add(p);
            await _db.SaveChangesAsync();
            _db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = scrumMasterId });
            var names = new[] { "To Do", "In Progress", "Testing", "Done" };
            for (int i = 0; i < names.Length; i++)
                _db.BoardLists.Add(new BoardList { Name = names[i], ProjectId = p.Id, Order = i });
            await _db.SaveChangesAsync();
            return p;
        }

        public Task<bool> IsProjectMemberAsync(int projectId, string userId) =>
            _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        public Task<bool> IsScrumMasterAsync(int projectId, string userId) =>
            _db.Projects.AnyAsync(p => p.Id == projectId && p.ScrumMasterId == userId);

        public async Task<string> GenerateInviteTokenAsync(int projectId)
        {
            var p = await _db.Projects.FindAsync(projectId);
            if (p == null) return "";
            p.InviteToken = Guid.NewGuid().ToString("N");
            await _db.SaveChangesAsync();
            return p.InviteToken;
        }

        public async Task<bool> JoinProjectAsync(string token, string userId)
        {
            var p = await _db.Projects.FirstOrDefaultAsync(x => x.InviteToken == token);
            if (p == null) return false;
            if (await IsProjectMemberAsync(p.Id, userId)) return true;
            _db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = userId });
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveMemberAsync(int projectId, string memberId, string requesterId)
        {
            if (!await IsScrumMasterAsync(projectId, requesterId)) return false;
            var m = await _db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == memberId);
            if (m == null) return false;
            _db.ProjectMembers.Remove(m);
            await _db.SaveChangesAsync();
            return true;
        }
    }

    // ── NOTIFICATIONS ────────────────────────────────────────
    public interface INotificationService
    {
        Task CreateAsync(string userId, string message, NotificationType type, int? cardId = null, int? projectId = null);
        Task CreateForUsersAsync(IEnumerable<string> userIds, string message, NotificationType type, int? cardId = null, int? projectId = null);
        Task<List<Notification>> GetUnreadAsync(string userId);
        Task MarkAllReadAsync(string userId);
        Task MarkReadAsync(int id, string userId);
        Task CheckDeadlinesAsync();
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        public NotificationService(ApplicationDbContext db) => _db = db;

        public async Task CreateAsync(string userId, string message, NotificationType type, int? cardId = null, int? projectId = null)
        {
            _db.Notifications.Add(new Notification { UserId = userId, Message = message, Type = type, CardId = cardId, ProjectId = projectId });
            await _db.SaveChangesAsync();
        }

        public async Task CreateForUsersAsync(IEnumerable<string> userIds, string message, NotificationType type, int? cardId = null, int? projectId = null)
        {
            foreach (var u in userIds) await CreateAsync(u, message, type, cardId, projectId);
        }

        public Task<List<Notification>> GetUnreadAsync(string userId) =>
            _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).OrderByDescending(n => n.CreatedAt).Take(30).ToListAsync();

        public async Task MarkAllReadAsync(string userId)
        {
            var items = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var i in items) i.IsRead = true;
            await _db.SaveChangesAsync();
        }

        public async Task MarkReadAsync(int id, string userId)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); }
        }

        public async Task CheckDeadlinesAsync()
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            var cards = await _db.Cards.Include(c => c.Assignees).Include(c => c.Project)
                .Where(c => c.Deadline.HasValue && c.Deadline.Value.Date == tomorrow && !c.DeadlineNotified).ToListAsync();
            foreach (var c in cards)
            {
                foreach (var a in c.Assignees)
                    await CreateAsync(a.UserId, $"⏰ ددلاین کارت «{c.Title}» فردا است!", NotificationType.Deadline, c.Id, c.ProjectId);
                c.DeadlineNotified = true;
            }
            await _db.SaveChangesAsync();
        }
    }

    // ── CARD ─────────────────────────────────────────────────
    public interface ICardService
    {
        Task<Card?> GetCardAsync(int id, string userId);
        Task<Card> CreateCardAsync(int listId, string title, string description, string color, string userId);
        Task<bool> UpdateCardAsync(int id, string title, string description, string color, DateTime? deadline, DateTime? startedAt, string userId);
        Task<bool> MoveCardAsync(int cardId, int toListId, string userId);
        Task<bool> AssignUserAsync(int cardId, string targetUserId, string requesterId);
        Task<bool> UnassignUserAsync(int cardId, string targetUserId);
    }

    public class CardService : ICardService
    {
        private readonly ApplicationDbContext _db;
        private readonly IProjectService _proj;
        private readonly INotificationService _notif;
        public CardService(ApplicationDbContext db, IProjectService proj, INotificationService notif) { _db = db; _proj = proj; _notif = notif; }

        public async Task<Card?> GetCardAsync(int id, string userId)
        {
            var c = await _db.Cards
                .Include(x => x.Assignees).ThenInclude(a => a.User)
                .Include(x => x.Comments).ThenInclude(cm => cm.Author)
                .Include(x => x.Comments).ThenInclude(cm => cm.Attachments)
                .Include(x => x.Comments).ThenInclude(cm => cm.Reactions)
                .Include(x => x.Comments).ThenInclude(cm => cm.Replies).ThenInclude(r => r.Author)
                .Include(x => x.Comments).ThenInclude(cm => cm.Replies).ThenInclude(r => r.Reactions)
                .Include(x => x.CheckLists).ThenInclude(cl => cl.Items)
                .Include(x => x.TimeLogs).ThenInclude(t => t.User)
                .Include(x => x.CreatedBy)
                .Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || !await _proj.IsProjectMemberAsync(c.ProjectId, userId)) return null;
            return c;
        }

        public async Task<Card> CreateCardAsync(int listId, string title, string description, string color, string userId)
        {
            var list = await _db.BoardLists.FindAsync(listId) ?? throw new Exception("List not found");
            var c = new Card { Title = title, Description = description, Color = color, BoardListId = listId, ProjectId = list.ProjectId, CreatedById = userId };
            _db.Cards.Add(c);
            await _db.SaveChangesAsync();
            return c;
        }

        public async Task<bool> UpdateCardAsync(int id, string title, string description, string color, DateTime? deadline, DateTime? startedAt, string userId)
        {
            var c = await _db.Cards.FindAsync(id);
            if (c == null) return false;
            var sm = await _proj.IsScrumMasterAsync(c.ProjectId, userId);
            if (!sm && c.CreatedById != userId) return false;
            c.Title = title; c.Description = description; c.Color = color;
            if (deadline != c.Deadline) c.DeadlineNotified = false;
            c.Deadline = deadline;
            c.StartedAt = startedAt;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MoveCardAsync(int cardId, int toListId, string userId)
        {
            var c = await _db.Cards.FindAsync(cardId);
            if (c == null || !await _proj.IsProjectMemberAsync(c.ProjectId, userId)) return false;
            c.BoardListId = toListId;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AssignUserAsync(int cardId, string targetUserId, string requesterId)
        {
            var c = await _db.Cards.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == cardId);
            if (c == null || !await _proj.IsProjectMemberAsync(c.ProjectId, targetUserId)) return false;
            if (!await _db.CardAssignees.AnyAsync(a => a.CardId == cardId && a.UserId == targetUserId))
            {
                _db.CardAssignees.Add(new CardAssignee { CardId = cardId, UserId = targetUserId });
                await _db.SaveChangesAsync();
                if (targetUserId != requesterId)
                    await _notif.CreateAsync(targetUserId, $"📌 کارت «{c.Title}» به شما اختصاص داده شد", NotificationType.Assignment, c.Id, c.ProjectId);
            }
            return true;
        }

        public async Task<bool> UnassignUserAsync(int cardId, string targetUserId)
        {
            var a = await _db.CardAssignees.FirstOrDefaultAsync(x => x.CardId == cardId && x.UserId == targetUserId);
            if (a == null) return false;
            _db.CardAssignees.Remove(a);
            await _db.SaveChangesAsync();
            return true;
        }
    }

    // ── TIMER ────────────────────────────────────────────────
    public interface ITimeLogService
    {
        Task<TimeLog> StartTimerAsync(int cardId, string userId);
        Task<TimeLog?> StopTimerAsync(int cardId, string userId);
        Task<TimeLog?> GetActiveTimerAsync(int cardId, string userId);
    }

    public class TimeLogService : ITimeLogService
    {
        private readonly ApplicationDbContext _db;
        public TimeLogService(ApplicationDbContext db) => _db = db;

        public async Task<TimeLog> StartTimerAsync(int cardId, string userId)
        {
            await StopTimerAsync(cardId, userId);
            var log = new TimeLog { CardId = cardId, UserId = userId, StartTime = DateTime.UtcNow, IsRunning = true };
            _db.TimeLogs.Add(log);
            await _db.SaveChangesAsync();
            return log;
        }

        public async Task<TimeLog?> StopTimerAsync(int cardId, string userId)
        {
            var log = await _db.TimeLogs.FirstOrDefaultAsync(t => t.CardId == cardId && t.UserId == userId && t.IsRunning);
            if (log == null) return null;
            log.EndTime = DateTime.UtcNow;
            log.DurationSeconds = (long)(log.EndTime.Value - log.StartTime).TotalSeconds;
            log.IsRunning = false;
            await _db.SaveChangesAsync();
            return log;
        }

        public Task<TimeLog?> GetActiveTimerAsync(int cardId, string userId) =>
            _db.TimeLogs.FirstOrDefaultAsync(t => t.CardId == cardId && t.UserId == userId && t.IsRunning);
    }

    // ── REPORTS ──────────────────────────────────────────────
    public class DailyWorkEntry { public DateTime Date { get; set; } public long TotalSeconds { get; set; } }

    public class UserTimeReport
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public long TotalSeconds { get; set; }
        public List<CardTimeDetail> Cards { get; set; } = new();
        public List<DailyWorkEntry> DailyBreakdown { get; set; } = new();
    }

    public class CardTimeDetail { public int CardId { get; set; } public string CardTitle { get; set; } = ""; public string CardDescription { get; set; } = ""; public long TotalSeconds { get; set; } }

    public class CardTimeReport
    {
        public int CardId { get; set; }
        public string CardTitle { get; set; } = "";
        public string CardDescription { get; set; } = "";
        public string ListName { get; set; } = "";
        public long TotalSeconds { get; set; }
        public List<UserTimeDetail> Users { get; set; } = new();
    }

    public class UserTimeDetail { public string UserId { get; set; } = ""; public string UserName { get; set; } = ""; public long TotalSeconds { get; set; } }

    public class OverdueCardDetail
    {
        public int CardId { get; set; }
        public string CardTitle { get; set; } = "";
        public DateTime Deadline { get; set; }
        public int LateDays { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class UserSalaryReport
    {
        public string UserId { get; set; } = "";
        public int ProjectMemberId { get; set; }
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public long TotalSeconds { get; set; }
        public decimal TotalHours { get; set; }
        public int TotalCards { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? TaskRate { get; set; }
        public decimal SalaryFromHours { get; set; }
        public decimal SalaryFromTasks { get; set; }
        public decimal GrossSalary { get; set; }
        public int OverdueCardCount { get; set; }
        public int TotalLateDays { get; set; }
        public decimal PenaltyPercent { get; set; }
        public decimal PenaltyAmount { get; set; }
        public decimal TotalSalary { get; set; }
        public decimal PendingWageAdjustment { get; set; }  // مبلغ اضافه‌شده توسط اسکرام مستر
        public decimal FinalPending { get; set; }            // TotalSalary + PendingWageAdjustment
        public List<OverdueCardDetail> OverdueCards { get; set; } = new();
        public DateTime? LastSettlementDate { get; set; }
        public List<SettlementInfo> Settlements { get; set; } = new();
        public List<CardSalaryDetail> CardDetails { get; set; } = new();
        public List<WageAdjustmentInfo> WageAdjustments { get; set; } = new();
    }

    public class WageAdjustmentInfo
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class SettlementInfo
    {
        public decimal Amount { get; set; }
        public DateTime SettledAt { get; set; }
        public string Note { get; set; } = "";
    }

    public class CardSalaryDetail
    {
        public int CardId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string TaskTypeName { get; set; } = "";
        public string ClientProjectName { get; set; } = "";
        public decimal Rate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? Deadline { get; set; }
        public DateTime? StartedAt { get; set; }
    }

    public class ProjectSummaryReport
    {
        public long TotalSeconds { get; set; }
        public int TotalCards { get; set; }
        public int ActiveMembers { get; set; }
        public List<DailyWorkEntry> DailyTrend { get; set; } = new();
    }

    public interface IReportService
    {
        Task<List<UserTimeReport>> GetProjectTimeReportAsync(int projectId, DateTime? from = null, DateTime? to = null);
        Task<List<CardTimeReport>> GetCardTimeReportAsync(int projectId, DateTime? from = null, DateTime? to = null);
        Task<List<UserSalaryReport>> GetSalaryReportAsync(int projectId, DateTime? from = null, DateTime? to = null, bool unsettledOnly = true);
        Task<ProjectSummaryReport> GetProjectSummaryAsync(int projectId, DateTime? from = null, DateTime? to = null);
        Task<List<UserTimeReport>> GetMyReportAsync(string userId, int projectId, DateTime? from = null, DateTime? to = null);
    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _db;
        public ReportService(ApplicationDbContext db) => _db = db;

        private async Task<List<TimeLog>> GetLogsAsync(int projectId, DateTime? from, DateTime? to)
        {
            var q = _db.TimeLogs.Include(t => t.User).Include(t => t.Card).ThenInclude(c => c!.BoardList)
                .Where(t => t.Card != null && t.Card!.ProjectId == projectId && !t.IsRunning);
            if (from.HasValue) q = q.Where(t => t.StartTime >= from.Value);
            if (to.HasValue) q = q.Where(t => t.StartTime < to.Value.AddDays(1));
            return await q.ToListAsync();
        }

        public async Task<List<UserTimeReport>> GetProjectTimeReportAsync(int projectId, DateTime? from = null, DateTime? to = null)
        {
            var logs = await GetLogsAsync(projectId, from, to);
            return logs.GroupBy(t => t.UserId).Select(g => new UserTimeReport
            {
                UserId = g.Key,
                UserName = g.First().User?.Name ?? "",
                UserEmail = g.First().User?.Email ?? "",
                TotalSeconds = g.Sum(t => t.DurationSeconds),
                Cards = g.GroupBy(t => t.CardId).Select(cg => new CardTimeDetail { CardId = cg.Key, CardTitle = cg.First().Card?.Title ?? "", CardDescription = cg.First().Card?.Description ?? "", TotalSeconds = cg.Sum(t => t.DurationSeconds) }).OrderByDescending(c => c.TotalSeconds).ToList(),
                DailyBreakdown = g.GroupBy(t => t.StartTime.Date).Select(dg => new DailyWorkEntry { Date = dg.Key, TotalSeconds = dg.Sum(t => t.DurationSeconds) }).OrderBy(d => d.Date).ToList()
            }).OrderByDescending(u => u.TotalSeconds).ToList();
        }

        public async Task<List<CardTimeReport>> GetCardTimeReportAsync(int projectId, DateTime? from = null, DateTime? to = null)
        {
            var logs = await GetLogsAsync(projectId, from, to);
            return logs.GroupBy(t => t.CardId).Select(g => new CardTimeReport
            {
                CardId = g.Key,
                CardTitle = g.First().Card?.Title ?? "",
                CardDescription = g.First().Card?.Description ?? "",
                ListName = g.First().Card?.BoardList?.Name ?? "",
                TotalSeconds = g.Sum(t => t.DurationSeconds),
                Users = g.GroupBy(t => t.UserId).Select(ug => new UserTimeDetail { UserId = ug.Key, UserName = ug.First().User?.Name ?? "", TotalSeconds = ug.Sum(t => t.DurationSeconds) }).OrderByDescending(u => u.TotalSeconds).ToList()
            }).OrderByDescending(c => c.TotalSeconds).ToList();
        }

        // ── Salary with TaskType rates, 5%/day penalty, settlement support ──
        public async Task<List<UserSalaryReport>> GetSalaryReportAsync(int projectId, DateTime? from = null, DateTime? to = null, bool unsettledOnly = true)
        {
            var members = await _db.ProjectMembers
                .Include(m => m.User)
                .Include(m => m.Settlements)
                .Where(m => m.ProjectId == projectId)
                .ToListAsync();

            // Load pending wage adjustments for all members in this project
            var memberIds = members.Select(m => m.Id).ToList();
            var allAdjustments = await _db.PendingWageAdjustments
                .Where(a => memberIds.Contains(a.ProjectMemberId))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var userReports = await GetProjectTimeReportAsync(projectId, from, to);

            // Cards with deadline for penalty calc
            var cardsWithDeadline = await _db.Cards
                .Include(c => c.Assignees)
                .Include(c => c.BoardList)
                .Where(c => c.ProjectId == projectId && c.Deadline != null)
                .ToListAsync();

            // All assigned cards with TaskType for rate calculation
            var assignedCards = await _db.Cards
                .Include(c => c.Assignees)
                .Include(c => c.TaskType)
                .Include(c => c.ClientProject)
                .Where(c => c.ProjectId == projectId)
                .ToListAsync();

            var lastLogPerCard = await _db.TimeLogs
                .Where(t => t.Card != null && t.Card!.ProjectId == projectId && !t.IsRunning)
                .GroupBy(t => t.CardId)
                .Select(g => new { CardId = g.Key, LastEnd = g.Max(t => t.EndTime) })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var result = new List<UserSalaryReport>();

            foreach (var m in members)
            {
                var rep = userReports.FirstOrDefault(r => r.UserId == m.UserId);
                var totalSecs = rep?.TotalSeconds ?? 0;
                var totalHours = totalSecs / 3600.0m;

                // Cards assigned to this member in date range
                var myCards = assignedCards
                    .Where(c => c.Assignees.Any(a => a.UserId == m.UserId))
                    .Where(c => !from.HasValue || c.CreatedAt >= from.Value)
                    .Where(c => !to.HasValue   || c.CreatedAt <= to.Value.AddDays(1))
                    .ToList();

                // If unsettledOnly, exclude cards before last settlement
                DateTime? lastSettlementDate = null;
                if (unsettledOnly && m.Settlements.Any())
                {
                    lastSettlementDate = m.Settlements.Max(s => s.SettledAt);
                    myCards = myCards.Where(c => c.CreatedAt > lastSettlementDate).ToList();
                }

                // Salary from hours
                var salaryHours = m.HourlyRate.HasValue ? totalHours * m.HourlyRate.Value : 0;

                // Salary from tasks — use TaskType.Rate if set, else member.TaskRate
                var salaryTasks = myCards.Sum(c =>
                {
                    var rate = c.TaskType?.Rate ?? m.TaskRate ?? 0;
                    return rate;
                });

                var gross = salaryHours + salaryTasks;

                // Deadline penalty
                var myOverdue = cardsWithDeadline
                    .Where(c => c.Assignees.Any(a => a.UserId == m.UserId))
                    .Select(c =>
                    {
                        var listName = (c.BoardList?.Name ?? "").Trim().ToLower();
                        var isDone = listName is "done" or "تمام شده" or "انجام شده" or "تکمیل شده";
                        var lastEnd = lastLogPerCard.FirstOrDefault(x => x.CardId == c.Id)?.LastEnd;
                        var reference = isDone && lastEnd.HasValue ? lastEnd.Value : now;
                        var lateDays = (int)Math.Floor((reference.Date - c.Deadline!.Value.Date).TotalDays);
                        return new OverdueCardDetail { CardId = c.Id, CardTitle = c.Title, Deadline = c.Deadline!.Value, LateDays = Math.Max(0, lateDays), IsCompleted = isDone };
                    })
                    .Where(d => d.LateDays > 0)
                    .OrderByDescending(d => d.LateDays)
                    .ToList();

                var totalLateDays = myOverdue.Sum(d => d.LateDays);
                var penaltyPercent = Math.Min(100m, totalLateDays * 5m);
                var penaltyAmount = Math.Round(gross * penaltyPercent / 100m, 0);
                var net = Math.Round(gross - penaltyAmount, 0);

                // Previous settlements for display
                var settlements = m.Settlements
                    .OrderByDescending(s => s.SettledAt)
                    .Select(s => new SettlementInfo { Amount = s.Amount, SettledAt = s.SettledAt, Note = s.Note ?? "" })
                    .ToList();

                // Pending wage adjustments (added by Scrum Master)
                var memberAdj = allAdjustments
                    .Where(a => a.ProjectMemberId == m.Id)
                    .Select(a => new WageAdjustmentInfo { Id = a.Id, Amount = a.Amount, Note = a.Note ?? "", CreatedAt = a.CreatedAt })
                    .ToList();
                var totalAdj = memberAdj.Sum(a => a.Amount);
                var finalPending = net + totalAdj;

                // Card breakdown for report table (with description)
                var cardDetails = myCards.Select(c => new CardSalaryDetail
                {
                    CardId = c.Id,
                    Title = c.Title,
                    Description = c.Description ?? "",
                    TaskTypeName = c.TaskType?.Name ?? "پیش‌فرض",
                    ClientProjectName = c.ClientProject?.Name ?? "",
                    Rate = c.TaskType?.Rate ?? m.TaskRate ?? 0,
                    CreatedAt = c.CreatedAt,
                    Deadline = c.Deadline,
                    StartedAt = c.StartedAt
                }).OrderByDescending(c => c.CreatedAt).ToList();

                result.Add(new UserSalaryReport
                {
                    UserId = m.UserId,
                    ProjectMemberId = m.Id,
                    UserName = m.User?.Name ?? "",
                    UserEmail = m.User?.Email ?? "",
                    TotalSeconds = totalSecs,
                    TotalHours = Math.Round(totalHours, 2),
                    TotalCards = myCards.Count,
                    HourlyRate = m.HourlyRate,
                    TaskRate = m.TaskRate,
                    SalaryFromHours = Math.Round(salaryHours, 0),
                    SalaryFromTasks = Math.Round(salaryTasks, 0),
                    GrossSalary = Math.Round(gross, 0),
                    OverdueCardCount = myOverdue.Count,
                    TotalLateDays = totalLateDays,
                    PenaltyPercent = penaltyPercent,
                    PenaltyAmount = penaltyAmount,
                    TotalSalary = net,
                    PendingWageAdjustment = Math.Round(totalAdj, 0),
                    FinalPending = Math.Round(finalPending, 0),
                    OverdueCards = myOverdue,
                    LastSettlementDate = lastSettlementDate,
                    Settlements = settlements,
                    CardDetails = cardDetails,
                    WageAdjustments = memberAdj
                });
            }
            return result.OrderByDescending(r => r.TotalSalary).ToList();
        }

        public async Task<ProjectSummaryReport> GetProjectSummaryAsync(int projectId, DateTime? from = null, DateTime? to = null)
        {
            var logs = await GetLogsAsync(projectId, from, to);
            var totalCards = await _db.Cards.CountAsync(c => c.ProjectId == projectId);
            return new ProjectSummaryReport
            {
                TotalSeconds = logs.Sum(t => t.DurationSeconds),
                TotalCards = totalCards,
                ActiveMembers = logs.Select(t => t.UserId).Distinct().Count(),
                DailyTrend = logs.GroupBy(t => t.StartTime.Date).Select(g => new DailyWorkEntry { Date = g.Key, TotalSeconds = g.Sum(t => t.DurationSeconds) }).OrderBy(d => d.Date).ToList()
            };
        }

        public async Task<List<UserTimeReport>> GetMyReportAsync(string userId, int projectId, DateTime? from = null, DateTime? to = null)
        {
            var all = await GetProjectTimeReportAsync(projectId, from, to);
            return all.Where(r => r.UserId == userId).ToList();
        }
    }
}

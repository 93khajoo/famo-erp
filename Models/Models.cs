using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace FamoBoard.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Name => DisplayName ?? Email ?? UserName ?? "?";
    }

    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ScrumMasterId { get; set; } = "";
        [ForeignKey("ScrumMasterId")] public ApplicationUser? ScrumMaster { get; set; }
        public string? InviteToken { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<BoardList> Lists { get; set; } = new List<BoardList>();
        // Task types defined per project
        public ICollection<TaskType> TaskTypes { get; set; } = new List<TaskType>();
    }

    // پروژه‌های مشتری — هر کارت می‌تواند به یک پروژه مشتری تعلق داشته باشد
    public class ClientProject
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project? Project { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#4DB6AC";
        public bool IsActive { get; set; } = true;
    }

    // مدل‌های تسک با نرخ‌های مختلف (مثلاً مدل A=100، مدل B=50)
    public class TaskType
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project? Project { get; set; }
        public string Name { get; set; } = "";       // "مدل ۱"، "مدل ۲" و...
        public decimal Rate { get; set; } = 0;        // ارزش ریالی هر تسک از این نوع
        public bool IsActive { get; set; } = true;
    }

    public class ProjectMember
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project? Project { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? TaskRate { get; set; }        // نرخ پیش‌فرض (اگر card.TaskTypeId نداشت)
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
    }

    // تصفیه حساب در تاریخ مشخص
    public class Settlement
    {
        public int Id { get; set; }
        public int ProjectMemberId { get; set; }
        public ProjectMember? ProjectMember { get; set; }
        public DateTime SettledAt { get; set; }       // تاریخ تصفیه
        public decimal Amount { get; set; }            // مبلغ تصفیه‌شده
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // گزارش تصفیه حساب — کارتابل عضو
    public class SettlementReport
    {
        public int Id { get; set; }
        public int ProjectMemberId { get; set; }
        public ProjectMember? ProjectMember { get; set; }
        public int SettlementId { get; set; }
        public Settlement? Settlement { get; set; }
        public DateTime PeriodFrom { get; set; }      // شروع دوره (DateTime.MinValue = ابتدای پروژه)
        public DateTime PeriodTo { get; set; }        // پایان دوره = تاریخ تصفیه
        public decimal Amount { get; set; }
        public string ReportJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BoardList
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int ProjectId { get; set; }
        public Project? Project { get; set; }
        public int Order { get; set; }
        public ICollection<Card> Cards { get; set; } = new List<Card>();
    }

    public class Card
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "#4DB6AC";
        public int BoardListId { get; set; }
        public BoardList? BoardList { get; set; }
        public int ProjectId { get; set; }
        public Project? Project { get; set; }
        public string CreatedById { get; set; } = "";
        [ForeignKey("CreatedById")] public ApplicationUser? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        public DateTime? StartedAt { get; set; }      // تاریخ شروع کار
        public bool DeadlineNotified { get; set; } = false;
        // نوع تسک (اختیاری — اگر نداشت از نرخ پیش‌فرض عضو استفاده می‌شود)
        public int? TaskTypeId { get; set; }
        public TaskType? TaskType { get; set; }
        // پروژه مشتری (اختیاری)
        public int? ClientProjectId { get; set; }
        public ClientProject? ClientProject { get; set; }
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public ICollection<CardAssignee> Assignees { get; set; } = new List<CardAssignee>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<CheckList> CheckLists { get; set; } = new List<CheckList>();
        public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    }

    public class CardAssignee
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public Card? Card { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
    }

    public class Comment
    {
        public int Id { get; set; }
        public string Content { get; set; } = "";
        public int CardId { get; set; }
        public Card? Card { get; set; }
        public string AuthorId { get; set; } = "";
        public ApplicationUser? Author { get; set; }
        public int? ParentCommentId { get; set; }
        [ForeignKey("ParentCommentId")] public Comment? ParentComment { get; set; }
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<CommentAttachment> Attachments { get; set; } = new List<CommentAttachment>();
        public ICollection<CommentReaction> Reactions { get; set; } = new List<CommentReaction>();
    }

    public class CommentAttachment
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public Comment? Comment { get; set; }
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileType { get; set; } = "";
        public string? LinkUrl { get; set; }
    }

    public class CommentReaction
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public Comment? Comment { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public string Emoji { get; set; } = "";
    }

    public class CheckList
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CardId { get; set; }
        public Card? Card { get; set; }
        public ICollection<CheckListItem> Items { get; set; } = new List<CheckListItem>();
    }

    public class CheckListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int CheckListId { get; set; }
        public CheckList? CheckList { get; set; }
        public bool IsCompleted { get; set; } = false;
    }

    public class TimeLog
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public Card? Card { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long DurationSeconds { get; set; } = 0;
        public bool IsRunning { get; set; } = false;
    }

    /// <summary>
    /// مبلغ اضافه‌شده توسط اسکرام مستر به مانده تصفیه‌نشده یک عضو
    /// مثلاً «دستمزد دریافت‌نشده»، «پاداش» و...
    /// </summary>
    public class PendingWageAdjustment
    {
        public int Id { get; set; }
        public int ProjectMemberId { get; set; }
        public ProjectMember? ProjectMember { get; set; }
        public decimal Amount { get; set; }          // مثبت = اضافه، منفی = کسر
        public string? Note { get; set; }             // توضیح (مثلاً «دستمزد دریافت‌نشده»)
        public string CreatedById { get; set; } = ""; // اسکرام مستر
        [ForeignKey("CreatedById")] public ApplicationUser? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class VideoUpload
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public Card? Card { get; set; }
        public bool IsUploaded { get; set; } = false;
        public DateTime? ScheduledUploadAt { get; set; }  // تاریخ و ساعت احتمالی آپلود
        public string? ProjectName { get; set; }          // نام پروژه/پلتفرم (اینستاگرام، یوتیوب و...)
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public long? Shares { get; set; }
        public long? Reposts { get; set; }
        public decimal? SkipRate { get; set; }            // درصد — 0 تا 100
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── قراردادهای مشتری ──────────────────────────────────────
    public class ClientContract
    {
        public int Id { get; set; }
        public int ClientProjectId { get; set; }
        public ClientProject? ClientProject { get; set; }
        public DateTime StartDate { get; set; }   // UTC
        public DateTime EndDate { get; set; }     // UTC
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ContractItem> Items { get; set; } = new List<ContractItem>();
    }

    // هر آیتم = یه نوع تسک با تعداد مشخص در هر ماه
    public class ContractItem
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public ClientContract? Contract { get; set; }
        public int TaskTypeId { get; set; }
        public TaskType? TaskType { get; set; }
        public int CountPerMonth { get; set; } = 0;   // تعداد در ماه
        public string? Note { get; set; }              // توضیح (مثلاً "زیر ۱ دقیقه")
    }

    public enum NotificationType { Info, Deadline, Assignment, Comment, Invite }

    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; } = NotificationType.Info;
        public bool IsRead { get; set; } = false;
        public int? CardId { get; set; }
        public int? ProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

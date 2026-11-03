using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FamoBoard.Models;

namespace FamoBoard.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
        public DbSet<TaskType> TaskTypes => Set<TaskType>();
        public DbSet<Settlement> Settlements => Set<Settlement>();
        public DbSet<BoardList> BoardLists => Set<BoardList>();
        public DbSet<Card> Cards => Set<Card>();
        public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<CommentAttachment> CommentAttachments => Set<CommentAttachment>();
        public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
        public DbSet<CheckList> CheckLists => Set<CheckList>();
        public DbSet<CheckListItem> CheckListItems => Set<CheckListItem>();
        public DbSet<TimeLog> TimeLogs => Set<TimeLog>();
        public DbSet<PendingWageAdjustment> PendingWageAdjustments => Set<PendingWageAdjustment>();
        public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<SettlementReport> SettlementReports => Set<SettlementReport>();
        public DbSet<ClientProject> ClientProjects => Set<ClientProject>();
        public DbSet<ClientContract> ClientContracts => Set<ClientContract>();
        public DbSet<ContractItem> ContractItems => Set<ContractItem>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            b.Entity<Project>().HasOne(p => p.ScrumMaster).WithMany().HasForeignKey(p => p.ScrumMasterId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Card>().HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Card>().HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Comment>().HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ProjectMember>().HasIndex(pm => new { pm.ProjectId, pm.UserId }).IsUnique();
            b.Entity<PendingWageAdjustment>().HasOne(p => p.ProjectMember).WithMany().HasForeignKey(p => p.ProjectMemberId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<PendingWageAdjustment>().HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
            b.Entity<CardAssignee>().HasIndex(ca => new { ca.CardId, ca.UserId }).IsUnique();
            b.Entity<CommentReaction>().HasIndex(cr => new { cr.CommentId, cr.UserId, cr.Emoji }).IsUnique();
            b.Entity<VideoUpload>().HasIndex(v => v.CardId).IsUnique();
            b.Entity<SettlementReport>().HasOne(r => r.ProjectMember).WithMany().HasForeignKey(r => r.ProjectMemberId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<SettlementReport>().HasOne(r => r.Settlement).WithMany().HasForeignKey(r => r.SettlementId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ClientProject>().HasOne(cp => cp.Project).WithMany().HasForeignKey(cp => cp.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<Card>().HasOne(c => c.ClientProject).WithMany().HasForeignKey(c => c.ClientProjectId).OnDelete(DeleteBehavior.SetNull);
            b.Entity<ClientContract>().HasOne(cc => cc.ClientProject).WithMany().HasForeignKey(cc => cc.ClientProjectId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<ContractItem>().HasOne(ci => ci.Contract).WithMany(cc => cc.Items).HasForeignKey(ci => ci.ContractId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<ContractItem>().HasOne(ci => ci.TaskType).WithMany().HasForeignKey(ci => ci.TaskTypeId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}

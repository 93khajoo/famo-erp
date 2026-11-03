using FamoBoard.Data;
using FamoBoard.Models;
using FamoBoard.Services;
using FamoBoard.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite("Data Source=famoboard.db"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.SignIn.RequireConfirmedAccount = false;
    o.Password.RequireDigit = false;
    o.Password.RequireUppercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGoogle(o =>
    {
        o.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "YOUR_GOOGLE_CLIENT_ID";
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "YOUR_GOOGLE_CLIENT_SECRET";
        o.SaveTokens = true;
    });

builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ITimeLogService, TimeLogService>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddSession();

var app = builder.Build();

app.UseStaticFiles();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);
Directory.CreateDirectory(Path.Combine(uploadsPath, "avatars"));
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapHub<BoardHub>("/boardHub");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // Add PendingWageAdjustments table if missing (schema evolution without migrations)
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS PendingWageAdjustments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectMemberId INTEGER NOT NULL REFERENCES ProjectMembers(Id),
                Amount DECIMAL NOT NULL,
                Note TEXT,
                CreatedById TEXT NOT NULL REFERENCES AspNetUsers(Id),
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ");
    }
    catch { /* already exists */ }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS VideoUploads (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CardId INTEGER NOT NULL UNIQUE REFERENCES Cards(Id) ON DELETE CASCADE,
                IsUploaded INTEGER NOT NULL DEFAULT 0,
                ScheduledUploadAt TEXT,
                ProjectName TEXT,
                Views INTEGER,
                Likes INTEGER,
                Shares INTEGER,
                Reposts INTEGER,
                SkipRate REAL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ");
    }
    catch { /* already exists */ }

    // v9.1 — add ProjectName column to existing VideoUploads tables
    try { db.Database.ExecuteSqlRaw("ALTER TABLE VideoUploads ADD COLUMN ProjectName TEXT;"); } catch { }

    // v10 — SettlementReports table
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS SettlementReports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectMemberId INTEGER NOT NULL REFERENCES ProjectMembers(Id) ON DELETE CASCADE,
                SettlementId INTEGER NOT NULL REFERENCES Settlements(Id),
                PeriodFrom TEXT NOT NULL,
                PeriodTo TEXT NOT NULL,
                Amount DECIMAL NOT NULL,
                ReportJson TEXT NOT NULL DEFAULT '{}',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ");
    }
    catch { }

    // v11 — ClientProjects table + ClientProjectId column on Cards
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ClientProjects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
                Name TEXT NOT NULL DEFAULT '',
                Color TEXT NOT NULL DEFAULT '#4DB6AC',
                IsActive INTEGER NOT NULL DEFAULT 1
            );
        ");
    }
    catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Cards ADD COLUMN ClientProjectId INTEGER REFERENCES ClientProjects(Id) ON DELETE SET NULL;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE ClientProjects ADD COLUMN Color TEXT NOT NULL DEFAULT '#4DB6AC';"); } catch { }
    // v12 — ClientContracts + ContractItems
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ClientContracts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientProjectId INTEGER NOT NULL REFERENCES ClientProjects(Id) ON DELETE CASCADE,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Description TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ContractItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ContractId INTEGER NOT NULL REFERENCES ClientContracts(Id) ON DELETE CASCADE,
                TaskTypeId INTEGER NOT NULL REFERENCES TaskTypes(Id) ON DELETE RESTRICT,
                CountPerMonth INTEGER NOT NULL DEFAULT 0,
                Note TEXT
            );
        ");
    }
    catch { }

    // v13 — Card archive columns
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Cards ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Cards ADD COLUMN ArchivedAt TEXT;"); } catch { }
}

app.Run();

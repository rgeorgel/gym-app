using GymApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Infra.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<ClassType> ClassTypes => Set<ClassType>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageItem> PackageItems => Set<PackageItem>();
    public DbSet<WaitingListEntry> WaitingList => Set<WaitingListEntry>();
    public DbSet<PackageTemplate> PackageTemplates => Set<PackageTemplate>();
    public DbSet<PackageTemplateItem> PackageTemplateItems => Set<PackageTemplateItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ProfessionalAvailability> ProfessionalAvailability => Set<ProfessionalAvailability>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<TimeBlock> TimeBlocks => Set<TimeBlock>();
    public DbSet<InstructorService> InstructorServices => Set<InstructorService>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.Property(x => x.PrimaryColor).HasMaxLength(20);
            e.Property(x => x.SecondaryColor).HasMaxLength(20);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.EfiPayeeCode).HasMaxLength(100);
            e.Property(x => x.AbacatePayCustomerId).HasMaxLength(100);
            e.Property(x => x.AbacatePayBillingId).HasMaxLength(100);
            e.Property(x => x.AbacatePayBillingUrl).HasMaxLength(500);
            e.Property(x => x.ReferredByCode).HasMaxLength(100);
            e.Ignore(x => x.HasStudentAccess);
            e.Ignore(x => x.IsInTrial);
            e.Ignore(x => x.TrialDaysRemaining);
            e.HasOne(x => x.DefaultPackageTemplate)
                .WithMany()
                .HasForeignKey(x => x.DefaultPackageTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // Instructor
        modelBuilder.Entity<Instructor>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany(t => t.Instructors).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ServiceCategory
        modelBuilder.Entity<ServiceCategory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // ClassType
        modelBuilder.Entity<ClassType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasOne(x => x.Tenant).WithMany(t => t.ClassTypes).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category).WithMany(c => c.Services).HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // ProfessionalAvailability
        modelBuilder.Entity<ProfessionalAvailability>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.SetNull);
        });

        // Schedule
        modelBuilder.Entity<Schedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany(t => t.Schedules).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ClassType).WithMany(ct => ct.Schedules).HasForeignKey(x => x.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Instructor).WithMany(i => i.Schedules).HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.SetNull);
        });

        // Session
        modelBuilder.Entity<Session>(e =>
        {
            e.HasKey(x => x.Id);
            // Filtered unique index — only enforce uniqueness for gym sessions (with a schedule)
            e.HasIndex(x => new { x.ScheduleId, x.Date })
                .IsUnique()
                .HasFilter("\"ScheduleId\" IS NOT NULL");
            // Index for efficient querying of salon sessions by tenant+date
            e.HasIndex(x => new { x.TenantId, x.Date });
            e.HasOne(x => x.Schedule).WithMany(s => s.Sessions).HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired(false);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ClassType).WithMany().HasForeignKey(x => x.ClassTypeId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        // Booking
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SessionId, x.StudentId });
            e.HasOne(x => x.Session).WithMany(s => s.Bookings).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany(u => u.Bookings).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PackageItem).WithMany(pi => pi.Bookings).HasForeignKey(x => x.PackageItemId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        // Package
        modelBuilder.Entity<Package>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany(t => t.Packages).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany(u => u.Packages).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        // PackageItem
        modelBuilder.Entity<PackageItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PricePerCredit).HasPrecision(10, 2);
            e.HasOne(x => x.Package).WithMany(p => p.Items).HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ClassType).WithMany(ct => ct.PackageItems).HasForeignKey(x => x.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.RemainingCredits);
        });

        // PackageTemplate
        modelBuilder.Entity<PackageTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany(t => t.PackageTemplates).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PackageTemplateItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PricePerCredit).HasPrecision(10, 2);
            e.HasOne(x => x.Template).WithMany(t => t.Items).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ClassType).WithMany().HasForeignKey(x => x.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        // WaitingListEntry
        modelBuilder.Entity<WaitingListEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Session).WithMany(s => s.WaitingList).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        // AiConversation
        modelBuilder.Entity<AiConversation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // AiMessage
        modelBuilder.Entity<AiMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasIndex(x => x.AbacatePayBillingId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PackageTemplate).WithMany().HasForeignKey(x => x.PackageTemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedPackage).WithMany().HasForeignKey(x => x.AssignedPackageId).OnDelete(DeleteBehavior.SetNull);
        });

        // Location
        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.HasOne(x => x.Tenant).WithMany(t => t.Locations).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // Update Schedule to include Location
        modelBuilder.Entity<Schedule>(e =>
        {
            e.HasOne(x => x.Location).WithMany(l => l.Schedules).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        // Update Session to include Location
        modelBuilder.Entity<Session>(e =>
        {
            e.HasOne(x => x.Location).WithMany(l => l.Sessions).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        // Update Instructor to include Location
        modelBuilder.Entity<Instructor>(e =>
        {
            e.HasOne(x => x.PrimaryLocation).WithMany(l => l.Instructors).HasForeignKey(x => x.PrimaryLocationId).OnDelete(DeleteBehavior.SetNull);
        });

        // Session → Instructor (salon: assigned professional)
        modelBuilder.Entity<Session>(e =>
        {
            e.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // InstructorService
        modelBuilder.Entity<InstructorService>(e =>
        {
            e.HasKey(x => new { x.InstructorId, x.ClassTypeId });
            e.HasOne(x => x.Instructor).WithMany(i => i.Services).HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ClassType).WithMany().HasForeignKey(x => x.ClassTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        // TimeBlock
        modelBuilder.Entity<TimeBlock>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Date });
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }
}

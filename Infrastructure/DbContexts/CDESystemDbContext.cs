using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class CDESystemDbContext : DbContext
    {
        public CDESystemDbContext() { }

        public CDESystemDbContext(DbContextOptions<CDESystemDbContext> options) : base(options) { }

        // --- Đã có sẵn ---
        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Department> Departments { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Notification> Notifications { get; set; }
        public virtual DbSet<Document> Documents { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

        // --- Module A: Định danh & Tổ chức ---
        public virtual DbSet<OrganizationType> OrganizationTypes { get; set; }
        public virtual DbSet<Organization> Organizations { get; set; }
        public virtual DbSet<Group> Groups { get; set; }
        public virtual DbSet<GroupMember> GroupMembers { get; set; }

        // --- Module B: Dự án ---
        public virtual DbSet<ProjectParticipant> ProjectParticipants { get; set; }
        public virtual DbSet<ProjectInvitation> ProjectInvitations { get; set; }
        public virtual DbSet<ContractPackage> ContractPackages { get; set; }
        public virtual DbSet<PackageAssignment> PackageAssignments { get; set; }

        // --- Module C: Kho tài liệu CDE ---
        public virtual DbSet<Folder> Folders { get; set; }
        public virtual DbSet<FolderPermission> FolderPermissions { get; set; }
        public virtual DbSet<FileItem> FileItems { get; set; }
        public virtual DbSet<FileVersion> FileVersions { get; set; }
        public virtual DbSet<FileNote> FileNotes { get; set; }
        public virtual DbSet<FolderTemplate> FolderTemplates { get; set; }

        // --- Module D: Phiếu yêu cầu ---
        public virtual DbSet<Submittal> Submittals { get; set; }
        public virtual DbSet<SubmittalStep> SubmittalSteps { get; set; }
        public virtual DbSet<SubmittalAttachment> SubmittalAttachments { get; set; }
        public virtual DbSet<SubmittalCitedFolder> SubmittalCitedFolders { get; set; }

        // --- Module E: Thảo luận ---
        public virtual DbSet<Discussion> Discussions { get; set; }
        public virtual DbSet<DiscussionCitedFolder> DiscussionCitedFolders { get; set; }
        public virtual DbSet<DiscussionMessage> DiscussionMessages { get; set; }
        public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }
        public virtual DbSet<MessageMention> MessageMentions { get; set; }

        // --- Module F: Issues / RFI ---
        public virtual DbSet<Issue> Issues { get; set; }
        public virtual DbSet<IssueComment> IssueComments { get; set; }
        public virtual DbSet<IssueAttachment> IssueAttachments { get; set; }
        public virtual DbSet<IssueCitedFolder> IssueCitedFolders { get; set; }
        public virtual DbSet<IssueMention> IssueMentions { get; set; }

        // --- Module H: Nhật ký / RAG ---
        public virtual DbSet<AuditLog> AuditLogs { get; set; }
        public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

        // --- Module I: Tiến độ sản lượng ---
        public virtual DbSet<Schedule> Schedules { get; set; }
        public virtual DbSet<WorkTask> WorkTasks { get; set; }
        public virtual DbSet<WorkTaskDependency> WorkTaskDependencies { get; set; }
        public virtual DbSet<WorkTaskModelLink> WorkTaskModelLinks { get; set; }
        public virtual DbSet<WorkTaskPermission> WorkTaskPermissions { get; set; }
        public virtual DbSet<ProgressReport> ProgressReports { get; set; }
        public virtual DbSet<ProgressReportItem> ProgressReportItems { get; set; }

        // --- Module J: Hợp đồng / Bill thầu ---
        public virtual DbSet<Contract> Contracts { get; set; }
        public virtual DbSet<ContractAppendix> ContractAppendices { get; set; }
        public virtual DbSet<BillItem> BillItems { get; set; }

        // --- Module K: Mô hình BIM ---
        public virtual DbSet<ProjectModel> ProjectModels { get; set; }
        public virtual DbSet<ModelFile> ModelFiles { get; set; }
        public virtual DbSet<ModelObject> ModelObjects { get; set; }

        // --- Module L: Giải phóng mặt bằng / Công trường số ---
        public virtual DbSet<ProjectLocation> ProjectLocations { get; set; }
        public virtual DbSet<DigitalSite> DigitalSites { get; set; }
        public virtual DbSet<CaptureStage> CaptureStages { get; set; }
        public virtual DbSet<Panorama360> Panorama360s { get; set; }
        public virtual DbSet<SiteImage> SiteImages { get; set; }
        public virtual DbSet<SiteAnnotation> SiteAnnotations { get; set; }

        // --- Module M: Điều phối GPMB ---
        public virtual DbSet<LandParcel> LandParcels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // OrganizationType: Code unique + seed 8 loại theo ISO 19650 / TCVN 14177
            modelBuilder.Entity<OrganizationType>()
                .HasIndex(t => t.Code)
                .IsUnique();

            modelBuilder.Entity<OrganizationType>().HasData(
                new OrganizationType { Id = new Guid("11111111-1111-1111-1111-111111111111"), Code = "Client",                Name = "Chủ đầu tư",            IsActive = true },
                new OrganizationType { Id = new Guid("22222222-2222-2222-2222-222222222222"), Code = "ProjectManagementUnit", Name = "Ban quản lý dự án",     IsActive = true },
                new OrganizationType { Id = new Guid("33333333-3333-3333-3333-333333333333"), Code = "Surveyor",              Name = "Tư vấn giám sát",       IsActive = true },
                new OrganizationType { Id = new Guid("44444444-4444-4444-4444-444444444444"), Code = "Consultant",            Name = "Tư vấn (thiết kế/BIM)", IsActive = true },
                new OrganizationType { Id = new Guid("55555555-5555-5555-5555-555555555555"), Code = "MainContractor",        Name = "Nhà thầu chính",        IsActive = true },
                new OrganizationType { Id = new Guid("66666666-6666-6666-6666-666666666666"), Code = "Subcontractor",         Name = "Nhà thầu phụ",          IsActive = true },
                new OrganizationType { Id = new Guid("77777777-7777-7777-7777-777777777777"), Code = "Supplier",              Name = "Nhà cung cấp",          IsActive = true },
                new OrganizationType { Id = new Guid("88888888-8888-8888-8888-888888888888"), Code = "FacilityManagement",    Name = "Đơn vị vận hành",       IsActive = true }
            );

            // Cascade Restrict cho các cây tự tham chiếu — tránh "multiple cascade paths"
            // và bảo vệ dữ liệu khỏi xóa lan khi xóa node cha.
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.ChildFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submittal>()
                .HasOne(s => s.ParentSubmittal)
                .WithMany(s => s.ChildSubmittals)
                .HasForeignKey(s => s.ParentSubmittalId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkTask>()
                .HasOne(w => w.ParentWorkTask)
                .WithMany(w => w.ChildWorkTasks)
                .HasForeignKey(w => w.ParentWorkTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BillItem>()
                .HasOne(b => b.ParentBillItem)
                .WithMany(b => b.ChildBillItems)
                .HasForeignKey(b => b.ParentBillItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DiscussionMessage>()
                .HasOne(m => m.ReplyToMessage)
                .WithMany(m => m.Replies)
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

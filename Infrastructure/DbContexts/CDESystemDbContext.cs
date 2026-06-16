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


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // OrganizationType: Code unique + seed 8 loại theo ISO 19650 / TCVN 14177
            modelBuilder.Entity<OrganizationType>()
                .HasIndex(t => t.Code)
                .IsUnique();

            // Seed 8 loại theo ISO 19650 / TCVN 14177.
            // GUID generate thật 1 lần (Guid.NewGuid() lúc design) rồi hardcode làm constant —
            // EF Core HasData yêu cầu constant để migration reproducible giữa các môi trường.
            modelBuilder.Entity<OrganizationType>().HasData(
                new OrganizationType { Id = new Guid("7f947ce1-e7c6-49b2-aa41-f9b30292917a"), Code = "Client",                Name = "Chủ đầu tư",            IsActive = true },
                new OrganizationType { Id = new Guid("ad5b98c7-b28f-4c40-861a-5a363b84eb00"), Code = "ProjectManagementUnit", Name = "Ban quản lý dự án",     IsActive = true },
                new OrganizationType { Id = new Guid("ad4c917e-b170-4ff8-bca3-10764641c8d9"), Code = "Surveyor",              Name = "Tư vấn giám sát",       IsActive = true },
                new OrganizationType { Id = new Guid("d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5"), Code = "Consultant",            Name = "Tư vấn (thiết kế/BIM)", IsActive = true },
                new OrganizationType { Id = new Guid("ae2fd257-cca8-4bb4-8f90-c0c45100702b"), Code = "MainContractor",        Name = "Nhà thầu chính",        IsActive = true },
                new OrganizationType { Id = new Guid("8c0dcb7d-87fe-413e-b8d6-83eb91171cbe"), Code = "Subcontractor",         Name = "Nhà thầu phụ",          IsActive = true },
                new OrganizationType { Id = new Guid("3fe93ed9-2e6a-47a6-90cf-6e5aac24c645"), Code = "Supplier",              Name = "Nhà cung cấp",          IsActive = true },
                new OrganizationType { Id = new Guid("e48c6618-c877-46bf-9d6d-7d9fb92a50e9"), Code = "FacilityManagement",    Name = "Đơn vị vận hành",       IsActive = true }
            );

            // Cascade Restrict cho các cây tự tham chiếu — tránh "multiple cascade paths"
            // và bảo vệ dữ liệu khỏi xóa lan khi xóa node cha.
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.ChildFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Nhóm sở hữu "ô" thư mục (WIP/Shared/... của 1 bên tham gia).
            // Restrict: xóa Group không kéo theo xóa cây thư mục của bên đó.
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.OwnerGroup)
                .WithMany()
                .HasForeignKey(f => f.OwnerGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // ACL thư mục: xóa Folder -> xóa các dòng phân quyền của nó.
            // Group/Organization được tham chiếu Restrict để tránh nhiều đường cascade.
            modelBuilder.Entity<FolderPermission>()
                .HasOne(p => p.Folder)
                .WithMany(f => f.Permissions)
                .HasForeignKey(p => p.FolderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FolderPermission>()
                .HasOne(p => p.Group)
                .WithMany()
                .HasForeignKey(p => p.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FolderPermission>()
                .HasOne(p => p.Organization)
                .WithMany()
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submittal>()
                .HasOne(s => s.ParentSubmittal)
                .WithMany(s => s.ChildSubmittals)
                .HasForeignKey(s => s.ParentSubmittalId)
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
            modelBuilder.Entity<FileVersion>()
                .HasOne(v => v.SourceVersion)
                .WithMany()
                .HasForeignKey(v => v.SourceFileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

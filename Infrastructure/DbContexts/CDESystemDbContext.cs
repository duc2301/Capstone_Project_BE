using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.DbContexts
{
    public class CDESystemDbContext : DbContext
    {
        private readonly IConfiguration _configuration;        

        public CDESystemDbContext(DbContextOptions<CDESystemDbContext> options, IConfiguration configuration) : base(options) 
        {
            _configuration = configuration;
        }

        protected CDESystemDbContext()
        {
        }

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
        public virtual DbSet<MarkupSet> MarkupSets { get; set; }
        public virtual DbSet<FileNote> FileNotes { get; set; }
        public virtual DbSet<FilePermission> FilePermissions { get; set; }
        public virtual DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public virtual DbSet<ApprovalRequestSigner> ApprovalRequestSigners { get; set; }
        public virtual DbSet<ApprovalSignatureTransaction> ApprovalSignatureTransactions { get; set; }
        public virtual DbSet<ZoneReturnRequest> ZoneReturnRequests { get; set; }
        public virtual DbSet<FileSignaturePosition> FileSignaturePositions { get; set; }


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
        public virtual DbSet<DocumentChildChunk> DocumentChunks { get; set; }
        public virtual DbSet<DocumentParentChunk> DocumentParentChunks { get; set; }

        // --- Module J: Hợp đồng / Bill thầu ---
        public virtual DbSet<Contract> Contracts { get; set; }
        public virtual DbSet<ContractAppendix> ContractAppendices { get; set; }
        public virtual DbSet<BillItem> BillItems { get; set; }

        // --- Module L: Giải phóng mặt bằng / Công trường số ---
        public virtual DbSet<ProjectLocation> ProjectLocations { get; set; }

        // --- Naming Convention Module ---
        public virtual DbSet<NamingConvention> NamingConventions { get; set; }
        public virtual DbSet<NamingConventionField> NamingConventionFields { get; set; }
        public virtual DbSet<NamingConventionFieldValue> NamingConventionFieldValues { get; set; }
        public virtual DbSet<NamingConventionLockedValue> NamingConventionLockedValues { get; set; }
        public virtual DbSet<FileNamingMetadata> FileNamingMetadata { get; set; }

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
                new OrganizationType { Id = new Guid("7f947ce1-e7c6-49b2-aa41-f9b30292917a"), Code = "Client", Name = "Chủ đầu tư", IsActive = true },
                new OrganizationType { Id = new Guid("ad5b98c7-b28f-4c40-861a-5a363b84eb00"), Code = "ProjectManagementUnit", Name = "Ban quản lý dự án", IsActive = true },
                new OrganizationType { Id = new Guid("ad4c917e-b170-4ff8-bca3-10764641c8d9"), Code = "Surveyor", Name = "Tư vấn giám sát", IsActive = true },
                new OrganizationType { Id = new Guid("d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5"), Code = "Consultant", Name = "Tư vấn (thiết kế/BIM)", IsActive = true },
                new OrganizationType { Id = new Guid("ae2fd257-cca8-4bb4-8f90-c0c45100702b"), Code = "MainContractor", Name = "Nhà thầu chính", IsActive = true },
                new OrganizationType { Id = new Guid("8c0dcb7d-87fe-413e-b8d6-83eb91171cbe"), Code = "Subcontractor", Name = "Nhà thầu phụ", IsActive = true },
                new OrganizationType { Id = new Guid("3fe93ed9-2e6a-47a6-90cf-6e5aac24c645"), Code = "Supplier", Name = "Nhà cung cấp", IsActive = true },
                new OrganizationType { Id = new Guid("e48c6618-c877-46bf-9d6d-7d9fb92a50e9"), Code = "FacilityManagement", Name = "Đơn vị vận hành", IsActive = true }
            );

            // Cascade Restrict cho các cây tự tham chiếu — tránh "multiple cascade paths"
            // và bảo vệ dữ liệu khỏi xóa lan khi xóa node cha.
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.ChildFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);


            // ACL thư mục: xóa Folder -> xóa các dòng phân quyền của nó.
            // Group được tham chiếu Restrict để tránh nhiều đường cascade.
            modelBuilder.Entity<FolderPermission>()
                .HasOne(p => p.Folder)
                .WithMany(f => f.Permissions)
                .HasForeignKey(p => p.FolderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FolderPermission>()
                .HasOne(fp => fp.ProjectParticipant)
                .WithMany()
                .HasForeignKey(fp => fp.ProjectParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FolderPermission>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.FolderId,
                    x.ProjectParticipantId
                })
                .IsUnique();
            });

            // ACL thư mục: xóa File -> xóa các dòng phân quyền của nó.
            // Group được tham chiếu Restrict để tránh nhiều đường cascade.
            modelBuilder.Entity<FilePermission>()
                .HasOne(fp => fp.FileItem)
                .WithMany(f => f.Permissions)
                .HasForeignKey(fp => fp.FileItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FilePermission>()
                .HasOne(fp => fp.ProjectParticipant)
                .WithMany()
                .HasForeignKey(fp => fp.ProjectParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FilePermission>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.FileItemId,
                    x.ProjectParticipantId
                })
                .IsUnique();
            });

            

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

            modelBuilder.Entity<ApprovalRequest>()
                .HasOne(a => a.FileItem)
                .WithMany()
                .HasForeignKey(a => a.FileItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalRequest>()
                .HasOne(a => a.Requester)
                .WithMany()
                .HasForeignKey(a => a.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalRequest>()
                .HasOne(a => a.Approver)
                .WithMany()
                .HasForeignKey(a => a.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalRequestSigner>()
                .HasOne(s => s.ApprovalRequest)
                .WithMany(a => a.Signers)
                .HasForeignKey(s => s.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApprovalRequestSigner>()
                .HasOne(s => s.SignerAccount)
                .WithMany()
                .HasForeignKey(s => s.SignerAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalRequestSigner>()
                .HasOne(s => s.SignerGroup)
                .WithMany()
                .HasForeignKey(s => s.SignerGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalSignatureTransaction>()
                .HasOne(t => t.ApprovalRequest)
                .WithMany()
                .HasForeignKey(t => t.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalSignatureTransaction>()
                .HasOne(t => t.FileItem)
                .WithMany()
                .HasForeignKey(t => t.FileItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalSignatureTransaction>()
                .HasOne(t => t.SignedByAccount)
                .WithMany()
                .HasForeignKey(t => t.SignedBy)
                .OnDelete(DeleteBehavior.Restrict);


            // NamingConvention → Folder (1:1)
            modelBuilder.Entity<NamingConvention>()
                .HasOne(nc => nc.Folder)
                .WithOne(f => f.NamingConvention)           // Assume Folder has NamingConvention navigation
                .HasForeignKey<NamingConvention>(nc => nc.FolderId)
                .OnDelete(DeleteBehavior.Cascade);         // Deleting folder deletes its convention

            // NamingConvention → NamingConventionField (1:N)
            modelBuilder.Entity<NamingConventionField>()
                .HasOne(nf => nf.NamingConvention)
                .WithMany(nc => nc.Fields)                  // Add ICollection<NamingConventionField> Fields to NamingConvention if missing
                .HasForeignKey(nf => nf.NamingConventionId)
                .OnDelete(DeleteBehavior.Cascade);

            // NamingConventionField → NamingConventionFieldValue (1:N)
            modelBuilder.Entity<NamingConventionFieldValue>()
                .HasOne(nfv => nfv.Field)
                .WithMany(nf => nf.AllowedValues)
                .HasForeignKey(nfv => nfv.NamingConventionFieldId)
                .OnDelete(DeleteBehavior.Cascade);

            // NamingConventionField → NamingConventionLockedValue (1:0..1)
            modelBuilder.Entity<NamingConventionField>()
                .HasOne(nf => nf.LockedValue)
                .WithOne(lv => lv.Field)
                .HasForeignKey<NamingConventionLockedValue>(lv => lv.NamingConventionFieldId)
                .OnDelete(DeleteBehavior.Restrict);         // Protect locked value

            // NamingConventionLockedValue → NamingConventionFieldValue (1:1)
            modelBuilder.Entity<NamingConventionLockedValue>()
                .HasOne(lv => lv.Value)
                .WithOne(nfv => nfv.LockedValue)
                .HasForeignKey<NamingConventionLockedValue>(lv => lv.NamingConventionFieldValueId)
                .OnDelete(DeleteBehavior.Restrict);

            // FileItem → FileNamingMetadata (1:N)
            modelBuilder.Entity<FileNamingMetadata>()
                .HasOne(m => m.FileItem)
                .WithMany(f => f.NamingMetadata)            // Add this collection to FileItem
                .HasForeignKey(m => m.FileItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // FileNamingMetadata → NamingConventionField (N:1)
            modelBuilder.Entity<FileNamingMetadata>()
                .HasOne(m => m.Field)
                .WithMany()
                .HasForeignKey(m => m.NamingConventionFieldId)
                .OnDelete(DeleteBehavior.Restrict);         // Don't delete field if metadata exists

            // FileNamingMetadata → NamingConventionFieldValue (N:0..1)
            modelBuilder.Entity<FileNamingMetadata>()
                .HasOne(m => m.SelectedValue)
                .WithMany()
                .HasForeignKey(m => m.SelectedValueId)
                .OnDelete(DeleteBehavior.Restrict);

            // INDEXES & CONSTRAINTS

            // Unique constraints
            modelBuilder.Entity<NamingConventionField>()
                .HasIndex(nf => new { nf.NamingConventionId, nf.Code })
                .IsUnique();

            modelBuilder.Entity<NamingConventionFieldValue>()
                .HasIndex(nfv => new { nfv.NamingConventionFieldId, nfv.Code })
                .IsUnique();

            modelBuilder.Entity<FileNamingMetadata>()
                .HasIndex(m => new { m.FileItemId, m.NamingConventionFieldId })
                .IsUnique();   // One metadata per field per file

            modelBuilder.Entity<FileNamingMetadata>()
                .HasIndex(m => new { m.FileItemId, m.SelectedValueId })
                .IsUnique();

            //// Performance indexes
            //modelBuilder.Entity<NamingConventionField>()
            //    .HasIndex(nf => new { nf.NamingConventionId, nf.OrderIndex });

            //modelBuilder.Entity<NamingConventionFieldValue>()
            //    .HasIndex(nfv => new { nfv.NamingConventionFieldId, nfv.OrderIndex });

            //modelBuilder.Entity<NamingConventionLockedValue>()
            //    .HasIndex(lv => lv.NamingConventionFieldId)
            //    .IsUnique();   // One lock per field

            // --- RAG: Document / DocumentChunk (pgvector) ---
            modelBuilder.HasPostgresExtension("vector");


            var embeddingDimension = _configuration?.GetValue<int>("Ollama:EmbeddingDimension") ?? 1024;
            if (embeddingDimension <= 0)
                embeddingDimension = 1024;

            modelBuilder.Entity<Document>(b =>
            {
                b.HasIndex(d => d.ProjectId);
                b.HasIndex(d => d.FileItemId);
                b.HasIndex(d => d.SourceFileVersionId);
                b.HasIndex(d => new { d.ProjectId, d.UpdateAt });   // hard-filter bản mới nhất
            });

            modelBuilder.Entity<DocumentParentChunk>(b =>
            {
                b.HasOne(p => p.Document)
                    .WithMany(d => d.Chunks)
                    .HasForeignKey(p => p.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(p => p.ProjectId);
                b.HasIndex(p => p.DocumentId);
            });
            
            modelBuilder.Entity<DocumentChildChunk>(b =>
            {
                b.Property(c => c.Embedding)
                    .HasColumnType($"vector({embeddingDimension})");

                b.HasOne(c => c.ParentChunk)
                    .WithMany(p => p.ChildChunks)
                    .HasForeignKey(c => c.ParentChunkId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(c => c.ProjectId);      
                b.HasIndex(c => c.DocumentId);
                b.HasIndex(c => c.ParentChunkId);
            });
            modelBuilder.Entity<ZoneReturnRequest>()
                .HasOne(r => r.FileItem)
                .WithMany()
                .HasForeignKey(r => r.FileItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ZoneReturnRequest>()
                .HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ZoneReturnRequest>()
                .HasOne(r => r.Approver)
                .WithMany()
                .HasForeignKey(r => r.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Moi FileItem chi giu 1 vi tri chu ky hien hanh.
            modelBuilder.Entity<FileSignaturePosition>()
                .HasIndex(p => p.FileItemId)
                .IsUnique();

            modelBuilder.Entity<FileSignaturePosition>()
                .HasOne(p => p.FileItem)
                .WithMany()
                .HasForeignKey(p => p.FileItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MarkupSet>()
                .HasOne(m => m.FileItem)
                .WithMany()
                .HasForeignKey(m => m.FileItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarkupSet>()
                .HasOne(m => m.FileVersion)
                .WithMany()
                .HasForeignKey(m => m.FileVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarkupSet>(b =>
            {
                b.HasIndex(m => m.FileItemId);
                b.HasIndex(m => m.FileVersionId);
                b.HasIndex(m => m.IssueId);
            });

            modelBuilder.Entity<FileNote>()
                .HasOne(n => n.MarkupSet)
                .WithMany(m => m.Notes)
                .HasForeignKey(n => n.MarkupSetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FileNote>()
                .HasOne(n => n.FileVersion)
                .WithMany()
                .HasForeignKey(n => n.FileVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FileNote>()
                .HasIndex(n => n.MarkupSetId);
        }
    }
}

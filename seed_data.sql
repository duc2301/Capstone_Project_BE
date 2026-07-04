-- ============================================================================
--  CDE System — SEED DATA (PostgreSQL / Npgsql, EF Core schema)
-- ----------------------------------------------------------------------------
--  Mục đích : tạo dữ liệu test mạch lạc để dễ test API & dựng UI.
--  Sinh theo : Infrastructure/Migrations/CDESystemDbContextModelSnapshot.cs
--              + Domain/Enum/* (mọi enum lưu DƯỚI DẠNG SỐ — integer).
--
--  CÁCH CHẠY :
--    psql -h localhost -U <user> -d <database> -f seed_data.sql
--    (hoặc dán toàn bộ vào pgAdmin / DBeaver Query Tool rồi Execute)
--
--  ĐẶC ĐIỂM :
--    • Idempotent: TRUNCATE các bảng seed rồi INSERT lại với UUID CỐ ĐỊNH
--      ⇒ chạy lại nhiều lần luôn ra cùng kết quả.
--      ("OrganizationTypes" do migration seed sẵn 8 dòng — KHÔNG truncate,
--       chỉ INSERT ... ON CONFLICT DO NOTHING để phòng DB bị xoá tay.)
--    • Bọc trong 1 transaction (BEGIN/COMMIT) — lỗi giữa chừng sẽ rollback hết.
--    • ⚠️ CẢNH BÁO: sẽ XÓA toàn bộ dữ liệu test cũ trong các bảng liệt kê ở
--      lệnh TRUNCATE bên dưới. KHÔNG chạy trên DB có dữ liệu thật cần giữ.
--
--  TÀI KHOẢN :
--    Mật khẩu CHUNG cho mọi account: "password"
--    (BCrypt $2a$11$... — sinh & verify bằng đúng BCrypt.Net-Next của BE)
--      admin@cde.vn          — Admin hệ thống (Role=Admin)
--      hoa.pm@cde.vn         — Project Manager (quản lý các dự án)
--      nam.design@cde.vn     — Tư vấn thiết kế (Leader nhóm BIM)
--      lan.design@cde.vn     — Tư vấn thiết kế (Member)
--      binh.contractor@cde.vn— Nhà thầu thi công (Leader)
--      cuong.super@cde.vn    — Tư vấn giám sát (Leader)
--      duong.client@cde.vn   — Chủ đầu tư (Leader)
--      em.verify@cde.vn      — Tư vấn thẩm tra (Member)
--      phong.viewer@cde.vn   — Tài khoản INACTIVE (để test trạng thái khoá)
--
--  Quy ước UUID (segment đầu = "mã bảng" cho dễ lần theo quan hệ):
--    a*=Accounts  b*=Organizations  c0*=Groups  c1*=GroupMembers
--    d0*=Projects d1*=Locations d2*=ProjectModels d3*=Participants d4*=Invitations
--    e0*=ContractPackages e1*=PackageAssignments
--    f0*=Folders f1*=FolderPermissions f2*=FileItems f3*=FileVersions
--    f4*=FilePermissions f5*=FileNotes f6*=ApprovalRequests
--    f7*=ApprovalSignatureTransactions f8*=ZoneReturnRequests f9*=FileSignaturePositions
--    fa*=NamingConventions fb*=NamingConventionFields fc*=NamingConventionFieldValues
--    fd*=NamingConventionLockedValues fe*=FileNamingMetadata
--    10*=Submittals 11*=SubmittalSteps 12*=SubmittalAttachments 13*=SubmittalCitedFolders
--    20*=Discussions 21*=Messages 22*=Mentions 23*=MsgAttachments 24*=DiscCitedFolders
--    30*=Issues 31*=IssueComments 32*=IssueMentions 33*=IssueAttachments 34*=IssueCitedFolders
--    40*=Contracts 41*=ContractAppendices 42*=BillItems
--    50*=ModelFiles 51*=ModelObjects  60*=Notifications
--    7a*=Documents 7b*=DocumentParentChunks 7c*=DocumentChunks(child)
--    80*=RefreshTokens 90*=AuditLogs
-- ============================================================================

BEGIN;

-- --- Dọn dữ liệu cũ (KHÔNG đụng "OrganizationTypes" do migration seed) -------
TRUNCATE TABLE
    "Notifications",
    "ModelObjects", "ModelFiles",
    "BillItems", "ContractAppendices", "Contracts",
    "IssueCitedFolders", "IssueAttachments", "IssueMentions", "IssueComments", "Issues",
    "DiscussionCitedFolders", "MessageAttachments", "MessageMentions", "DiscussionMessages", "Discussions",
    "SubmittalCitedFolders", "SubmittalAttachments", "SubmittalSteps", "Submittals",
    "ApprovalSignatureTransactions", "ApprovalRequests", "ZoneReturnRequests", "FileSignaturePositions",
    "FileNamingMetadata", "NamingConventionLockedValues", "NamingConventionFieldValues",
    "NamingConventionFields", "NamingConventions",
    "FileNotes", "FilePermissions", "FileVersions", "FileItems", "FolderPermissions", "Folders",
    "PackageAssignments", "ContractPackages",
    "ProjectInvitations", "ProjectParticipants", "ProjectModels", "ProjectLocations", "Projects",
    "GroupMembers", "Groups", "Organizations",
    "RefreshTokens", "AuditLogs",
    "DocumentChunks", "DocumentParentChunks", "Documents",
    "Accounts"
    RESTART IDENTITY CASCADE;

-- ============================================================================
-- 0) ORGANIZATION TYPES  (migration đã seed 8 dòng — chèn lại phòng khi bị xoá)
-- ============================================================================
INSERT INTO "OrganizationTypes" ("Id","Code","Name","Description","IsActive") VALUES
('7f947ce1-e7c6-49b2-aa41-f9b30292917a','Client','Chủ đầu tư',NULL,true),
('ad5b98c7-b28f-4c40-861a-5a363b84eb00','ProjectManagementUnit','Ban quản lý dự án',NULL,true),
('ad4c917e-b170-4ff8-bca3-10764641c8d9','Surveyor','Tư vấn giám sát',NULL,true),
('d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5','Consultant','Tư vấn (thiết kế/BIM)',NULL,true),
('ae2fd257-cca8-4bb4-8f90-c0c45100702b','MainContractor','Nhà thầu chính',NULL,true),
('8c0dcb7d-87fe-413e-b8d6-83eb91171cbe','Subcontractor','Nhà thầu phụ',NULL,true),
('3fe93ed9-2e6a-47a6-90cf-6e5aac24c645','Supplier','Nhà cung cấp',NULL,true),
('e48c6618-c877-46bf-9d6d-7d9fb92a50e9','FacilityManagement','Đơn vị vận hành',NULL,true)
ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- 1) ACCOUNTS   Role: Admin=0, User=1 | Status: Active=0, Inactive=1, Suspended=2
-- ============================================================================
INSERT INTO "Accounts" ("Id","UserName","Email","PasswordHash","Role","Status","ResetPasswordToken","ResetPasswordTokenExpiresAt","CreatedAt","UpdatedAt") VALUES
('a0000000-0000-0000-0000-000000000001','Nguyễn Văn Admin','admin@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',0,0,NULL,NULL,'2026-01-02 08:00:00+07','2026-01-02 08:00:00+07'),
('a0000000-0000-0000-0000-000000000002','Trần Thị Hoa','hoa.pm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-03 08:00:00+07','2026-01-03 08:00:00+07'),
('a0000000-0000-0000-0000-000000000003','Lê Hoàng Nam','nam.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-04 08:00:00+07','2026-01-04 08:00:00+07'),
('a0000000-0000-0000-0000-000000000004','Phạm Thị Lan','lan.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-04 09:00:00+07','2026-01-04 09:00:00+07'),
('a0000000-0000-0000-0000-000000000005','Vũ Văn Bình','binh.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-05 08:00:00+07','2026-01-05 08:00:00+07'),
('a0000000-0000-0000-0000-000000000006','Đỗ Mạnh Cường','cuong.super@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-06 08:00:00+07','2026-01-06 08:00:00+07'),
('a0000000-0000-0000-0000-000000000007','Ngô Thị Dương','duong.client@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-06 09:00:00+07','2026-01-06 09:00:00+07'),
('a0000000-0000-0000-0000-000000000008','Bùi Văn Em','em.verify@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-01-07 08:00:00+07','2026-01-07 08:00:00+07'),
('a0000000-0000-0000-0000-000000000009','Đặng Quốc Phong','phong.viewer@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,1,NULL,NULL,'2026-01-08 08:00:00+07','2026-01-08 08:00:00+07');

-- ============================================================================
-- 2) ORGANIZATIONS  (OrganizationTypeId trỏ tới 8 dòng migration seed sẵn)
-- ============================================================================
INSERT INTO "Organizations" ("Id","LegalName","DisplayName","TaxCode","Address","Email","Phone","OrganizationTypeId","CreatedAt","UpdatedAt") VALUES
('b0000000-0000-0000-0000-000000000001','Công ty CP Tập đoàn Đầu tư ABC','Tập đoàn ABC','0301234567','123 Nguyễn Hữu Cảnh, Bình Thạnh, TP.HCM','contact@abc.vn','02838001122','7f947ce1-e7c6-49b2-aa41-f9b30292917a','2026-01-02 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000002','Ban QLDA Đầu tư Xây dựng số 1','Ban QLDA số 1','0302345678','45 Lê Duẩn, Quận 1, TP.HCM','pmu1@abc.vn','02838003344','ad5b98c7-b28f-4c40-861a-5a363b84eb00','2026-01-02 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000003','Công ty TNHH Tư vấn Thiết kế BIM Việt','TV BIM Việt','0303456789','78 Cách Mạng Tháng 8, Quận 3, TP.HCM','info@bimviet.vn','02838005566','d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5','2026-01-03 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000004','Tổng Công ty Xây dựng Trường Sơn','XD Trường Sơn','0304567890','12 Trường Chinh, Tân Bình, TP.HCM','contact@truongson.vn','02838007788','ae2fd257-cca8-4bb4-8f90-c0c45100702b','2026-01-03 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000005','Công ty CP Tư vấn Giám sát Thăng Long','TVGS Thăng Long','0305678901','9 Phạm Văn Đồng, Thủ Đức, TP.HCM','tvgs@thanglong.vn','02838009900','ad4c917e-b170-4ff8-bca3-10764641c8d9','2026-01-04 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000006','Công ty TNHH Vật liệu Xây dựng Hòa Phát Nam','VLXD Hòa Phát Nam','0306789012','KCN Tân Tạo, Bình Tân, TP.HCM','sales@hpnam.vn','02838001199','3fe93ed9-2e6a-47a6-90cf-6e5aac24c645','2026-01-04 08:00:00+07',NULL);

-- ============================================================================
-- 3) GROUPS  (OrganizationId nullable)
-- ============================================================================
INSERT INTO "Groups" ("Id","Name","Description","OrganizationId","CreatedAt") VALUES
('c0000000-0000-0000-0000-000000000001','Chủ đầu tư','Nhóm đại diện chủ đầu tư','b0000000-0000-0000-0000-000000000001','2026-01-05 08:00:00+07'),
('c0000000-0000-0000-0000-000000000002','Ban quản lý dự án','Ban QLDA điều phối chung','b0000000-0000-0000-0000-000000000002','2026-01-05 08:00:00+07'),
('c0000000-0000-0000-0000-000000000003','Tư vấn thiết kế / BIM','Nhóm thiết kế và dựng mô hình BIM','b0000000-0000-0000-0000-000000000003','2026-01-05 08:00:00+07'),
('c0000000-0000-0000-0000-000000000004','Nhà thầu thi công','Nhóm nhà thầu chính','b0000000-0000-0000-0000-000000000004','2026-01-05 08:00:00+07'),
('c0000000-0000-0000-0000-000000000005','Tư vấn giám sát','Nhóm giám sát & thẩm tra','b0000000-0000-0000-0000-000000000005','2026-01-05 08:00:00+07'),
('c0000000-0000-0000-0000-000000000006','Nhà cung cấp','Nhóm cung cấp vật tư','b0000000-0000-0000-0000-000000000006','2026-01-05 08:00:00+07');

-- ============================================================================
-- 4) GROUP MEMBERS  Role: Member=0, Leader=1 | Status: Active=0, Left=1
-- ============================================================================
INSERT INTO "GroupMembers" ("Id","GroupId","AccountId","Role","Status","JoinedAt") VALUES
('c1000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000007',1,0,'2026-01-10 08:00:00+07'),
('c1000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002',1,0,'2026-01-10 08:00:00+07'),
('c1000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000001',0,0,'2026-01-10 08:30:00+07'),
('c1000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003',1,0,'2026-01-11 08:00:00+07'),
('c1000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004',0,0,'2026-01-11 08:30:00+07'),
('c1000000-0000-0000-0000-000000000006','c0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005',1,0,'2026-01-12 08:00:00+07'),
('c1000000-0000-0000-0000-000000000007','c0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000006',1,0,'2026-01-12 08:30:00+07'),
('c1000000-0000-0000-0000-000000000008','c0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000008',0,0,'2026-01-12 09:00:00+07'),
('c1000000-0000-0000-0000-000000000009','c0000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000009',0,1,'2026-01-13 08:00:00+07');

-- ============================================================================
-- 5) PROJECTS  Status: Planning=0,Active=1,OnHold=2,Completed=3,Closed=4
--              Phase : Concept=0,Design=1,Construction=2,Handover=3,Operation=4
-- ============================================================================
INSERT INTO "Projects" ("Id","ProjectName","ProjectDescription","Status","Phase","ManagerAccountId") VALUES
('d0000000-0000-0000-0000-000000000001','Khu phức hợp căn hộ Riverside Tower','Tổ hợp căn hộ cao cấp 3 tháp, 35 tầng ven sông Sài Gòn.',1,2,'a0000000-0000-0000-0000-000000000002'),
('d0000000-0000-0000-0000-000000000002','Cầu vượt nút giao Cát Lái','Cầu vượt thép giảm ùn tắc nút giao Cát Lái, TP. Thủ Đức.',0,1,'a0000000-0000-0000-0000-000000000002'),
('d0000000-0000-0000-0000-000000000003','Nhà máy xử lý nước thải Bình Hưng','Nhà máy xử lý nước thải công suất 469.000 m3/ngày đêm.',2,0,NULL),
('d0000000-0000-0000-0000-000000000004','Trung tâm thương mại Sài Gòn Center','TTTM kết hợp văn phòng cho thuê khu trung tâm Quận 1.',3,3,'a0000000-0000-0000-0000-000000000002');

-- ============================================================================
-- 6) PROJECT LOCATIONS  (IsDefault bool)
-- ============================================================================
INSERT INTO "ProjectLocations" ("Id","ProjectId","Address","Latitude","Longitude","IsDefault","CreatedAt") VALUES
('d1000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','123 Nguyễn Hữu Cảnh, P.22, Bình Thạnh, TP.HCM',10.7980,106.7220,true,'2026-01-15 08:00:00+07'),
('d1000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000002','Nút giao Cát Lái, P. Thạnh Mỹ Lợi, TP. Thủ Đức, TP.HCM',10.7769,106.7600,true,'2026-01-16 08:00:00+07'),
('d1000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000003','Khu xử lý Bình Hưng, H. Bình Chánh, TP.HCM',10.6900,106.6300,true,'2026-01-17 08:00:00+07'),
('d1000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000004','456 Lê Lợi, P. Bến Thành, Quận 1, TP.HCM',10.7720,106.6980,true,'2026-01-18 08:00:00+07');

-- ============================================================================
-- 7) PROJECT MODELS
-- ============================================================================
INSERT INTO "ProjectModels" ("Id","ProjectId","Name","Description","CreatedAt") VALUES
('d2000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','Mô hình Kiến trúc - Riverside Tower','Bộ mô hình kiến trúc tổng thể 3 tháp.','2026-02-01 08:00:00+07'),
('d2000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','Mô hình Kết cấu - Riverside Tower','Bộ mô hình kết cấu phần thân.','2026-02-01 08:30:00+07'),
('d2000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000002','Mô hình Cầu - Cát Lái','Mô hình kết cấu nhịp cầu thép.','2026-02-02 08:00:00+07');

-- ============================================================================
-- 8) PROJECT PARTICIPANTS  Role: ProjectAdmin=0, Member=1 | Status: Active=0, Inactive=1
-- ============================================================================
INSERT INTO "ProjectParticipants" ("Id","ProjectId","GroupId","Role","Status","JoinedAt") VALUES
('d3000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000001',0,0,'2026-01-20 08:00:00+07'),
('d3000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000002',1,0,'2026-01-20 08:10:00+07'),
('d3000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000003',1,0,'2026-01-20 08:20:00+07'),
('d3000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000004',1,0,'2026-01-20 08:30:00+07'),
('d3000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000005',1,0,'2026-01-20 08:40:00+07'),
('d3000000-0000-0000-0000-000000000006','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000001',0,0,'2026-01-21 08:00:00+07'),
('d3000000-0000-0000-0000-000000000007','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000003',1,0,'2026-01-21 08:10:00+07'),
('d3000000-0000-0000-0000-000000000008','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000006',1,1,'2026-01-21 08:20:00+07');

-- ============================================================================
-- 9) PROJECT INVITATIONS  Status: Pending=0,Accepted=1,Rejected=2,Expired=3
--                         Role(GroupMemberRole): Member=0, Leader=1
-- ============================================================================
INSERT INTO "ProjectInvitations" ("Id","ProjectId","InvitedAccountId","InvitedByAccountId","InvitedGroupId","Role","Status","Token","Note","CreatedAt","ExpiresAt","RespondedAt") VALUES
('d4000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000009','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000006',0,0,'inv-token-0001-pending','Mời tham gia nhóm Nhà cung cấp dự án Riverside Tower.','2026-06-14 09:00:00+07','2026-07-14 09:00:00+07',NULL),
('d4000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000005',0,1,'inv-token-0002-accepted','Mời tham gia nhóm Tư vấn giám sát.','2026-05-20 09:00:00+07','2026-06-20 09:00:00+07','2026-05-21 10:15:00+07'),
('d4000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000003',0,0,'inv-token-0003-pending','Mời tham gia thiết kế dự án Cầu Cát Lái.','2026-06-15 09:00:00+07','2026-07-15 09:00:00+07',NULL),
('d4000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000002',NULL,'a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000004',1,0,'inv-token-0004-leader','Mời làm Leader nhóm Nhà thầu thi công.','2026-06-16 09:00:00+07','2026-07-16 09:00:00+07',NULL),
('d4000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000004',0,3,'inv-token-0005-expired','Lời mời đã hết hạn.','2026-04-01 09:00:00+07','2026-05-01 09:00:00+07',NULL);

-- ============================================================================
-- 10) CONTRACT PACKAGES  Status: Draft=0,Active=1,Completed=2,Suspended=3
-- ============================================================================
INSERT INTO "ContractPackages" ("Id","ProjectId","Code","Name","Description","Status","IsDefault","ContractValue","StartDate","EndDate","CreatedAt") VALUES
('e0000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','GT-00','Gói thầu mặc định','Gói mặc định chứa hồ sơ chung của dự án.',1,true,NULL,NULL,NULL,'2026-01-25 08:00:00+07'),
('e0000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','GT-01','Gói thầu thi công phần thân','Thi công kết cấu phần thân 3 tháp.',1,false,185000000000,'2026-02-01 00:00:00+07','2027-06-30 00:00:00+07','2026-01-25 08:10:00+07'),
('e0000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','GT-02','Gói thầu cơ điện (M&E)','Cung cấp & lắp đặt hệ thống cơ điện.',0,false,72000000000,NULL,NULL,'2026-01-25 08:20:00+07'),
('e0000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000002','GT-00','Gói thầu mặc định','Gói mặc định dự án Cầu Cát Lái.',1,true,NULL,NULL,NULL,'2026-01-26 08:00:00+07');

-- ============================================================================
-- 11) PACKAGE ASSIGNMENTS  Role: MainContractor=0,Subcontractor=1,
--                          SupervisionConsultant=2,DesignConsultant=3,Supplier=4
-- ============================================================================
INSERT INTO "PackageAssignments" ("Id","ContractPackageId","OrganizationId","Role","ContractNumber","Position","VatCode","RepresentativeAccountId","CreatedAt") VALUES
('e1000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000004',0,'HD-2026-001','Nhà thầu chính','0304567890','a0000000-0000-0000-0000-000000000005','2026-02-01 08:00:00+07'),
('e1000000-0000-0000-0000-000000000002','e0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000005',2,'HD-2026-001-GS','Tư vấn giám sát','0305678901','a0000000-0000-0000-0000-000000000006','2026-02-01 08:10:00+07'),
('e1000000-0000-0000-0000-000000000003','e0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003',3,'HD-2026-001-TK','Tư vấn thiết kế','0303456789','a0000000-0000-0000-0000-000000000003','2026-02-01 08:20:00+07'),
('e1000000-0000-0000-0000-000000000004','e0000000-0000-0000-0000-000000000003','b0000000-0000-0000-0000-000000000006',4,'HD-2026-002','Nhà cung cấp',NULL,NULL,'2026-02-02 08:00:00+07');

-- ============================================================================
-- 12) FOLDERS  Area(CdeArea): Wip=0, Shared=1, Published=2, Archived=3
--     (cây tự tham chiếu — chèn folder gốc trước, folder con sau)
--     NamingConventionId: cột scalar, KHÔNG có FK — trỏ tới mục 13 an toàn.
-- ============================================================================
INSERT INTO "Folders" ("Id","ProjectId","Name","Area","ParentFolderId","IsTemplate","NamingConventionId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('f0000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','01-WIP',0,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-01 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','02-Shared',1,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-01 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','03-Published',2,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-01 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000001','04-Archived',3,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-01 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000001','Kiến trúc',0,'f0000000-0000-0000-0000-000000000001',false,'fa000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000003','2026-02-02 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000006','d0000000-0000-0000-0000-000000000001','Kết cấu',0,'f0000000-0000-0000-0000-000000000001',false,NULL,'a0000000-0000-0000-0000-000000000004','2026-02-02 08:10:00+07',NULL),
('f0000000-0000-0000-0000-000000000007','d0000000-0000-0000-0000-000000000001','MEP',0,'f0000000-0000-0000-0000-000000000001',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:20:00+07',NULL),
('f0000000-0000-0000-0000-000000000008','d0000000-0000-0000-0000-000000000001','Bản vẽ phối hợp',1,'f0000000-0000-0000-0000-000000000002',false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-03 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000009','d0000000-0000-0000-0000-000000000001','Hồ sơ phát hành',2,'f0000000-0000-0000-0000-000000000003',false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-03 08:10:00+07',NULL),
('f0000000-0000-0000-0000-000000000010','d0000000-0000-0000-0000-000000000002','01-WIP',0,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-04 08:00:00+07',NULL);

-- --- Dữ liệu bổ sung cho FOLDER TREE (FolderTreeService.GetTreeAsync) ---------
--   • Cây sâu 4 cấp: 01-WIP > Kiến trúc > Tháp A > Tầng điển hình
--   • 'tháp C' viết thường: test sort OrdinalIgnoreCase (Tháp A < Tháp B < tháp C)
--   • IsTemplate=true (f0..0022/0023): service phải LOẠI khỏi cây
--   • f0..0024 Area=Shared nhưng cha là folder Wip: khi lọc theo area, cha bị ẩn
--     ⇒ node này phải nổi lên thành root (edge case ghép cây theo visibleIds)
--   • f0..0025/0026: cây của dự án 2 — test tách cây theo ProjectId
INSERT INTO "Folders" ("Id","ProjectId","Name","Area","ParentFolderId","IsTemplate","NamingConventionId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('f0000000-0000-0000-0000-000000000011','d0000000-0000-0000-0000-000000000001','Tháp A',0,'f0000000-0000-0000-0000-000000000005',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000012','d0000000-0000-0000-0000-000000000001','Tháp B',0,'f0000000-0000-0000-0000-000000000005',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:05:00+07',NULL),
('f0000000-0000-0000-0000-000000000013','d0000000-0000-0000-0000-000000000001','tháp C',0,'f0000000-0000-0000-0000-000000000005',false,NULL,'a0000000-0000-0000-0000-000000000004','2026-02-10 08:10:00+07',NULL),
('f0000000-0000-0000-0000-000000000014','d0000000-0000-0000-0000-000000000001','Tầng điển hình',0,'f0000000-0000-0000-0000-000000000011',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:15:00+07',NULL),
('f0000000-0000-0000-0000-000000000015','d0000000-0000-0000-0000-000000000001','Tầng hầm B1',0,'f0000000-0000-0000-0000-000000000011',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:20:00+07',NULL),
('f0000000-0000-0000-0000-000000000016','d0000000-0000-0000-0000-000000000001','Móng cọc',0,'f0000000-0000-0000-0000-000000000006',false,NULL,'a0000000-0000-0000-0000-000000000004','2026-02-10 08:25:00+07',NULL),
('f0000000-0000-0000-0000-000000000017','d0000000-0000-0000-0000-000000000001','Điện',0,'f0000000-0000-0000-0000-000000000007',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:30:00+07',NULL),
('f0000000-0000-0000-0000-000000000018','d0000000-0000-0000-0000-000000000001','Cấp thoát nước',0,'f0000000-0000-0000-0000-000000000007',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-10 08:35:00+07',NULL),
('f0000000-0000-0000-0000-000000000019','d0000000-0000-0000-0000-000000000001','Báo cáo va chạm',1,'f0000000-0000-0000-0000-000000000008',false,NULL,'a0000000-0000-0000-0000-000000000006','2026-02-11 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000020','d0000000-0000-0000-0000-000000000001','Đợt phát hành 01',2,'f0000000-0000-0000-0000-000000000009',false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-11 08:10:00+07',NULL),
('f0000000-0000-0000-0000-000000000021','d0000000-0000-0000-0000-000000000001','Lưu trữ 2025',3,'f0000000-0000-0000-0000-000000000004',false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-11 08:20:00+07',NULL),
('f0000000-0000-0000-0000-000000000022','d0000000-0000-0000-0000-000000000001','Template CDE chuẩn',0,NULL,true,NULL,'a0000000-0000-0000-0000-000000000001','2026-01-30 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000023','d0000000-0000-0000-0000-000000000001','01-WIP',0,'f0000000-0000-0000-0000-000000000022',true,NULL,'a0000000-0000-0000-0000-000000000001','2026-01-30 08:05:00+07',NULL),
('f0000000-0000-0000-0000-000000000024','d0000000-0000-0000-0000-000000000001','Hồ sơ chia sẻ Tháp A',1,'f0000000-0000-0000-0000-000000000011',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-12 08:00:00+07',NULL),
('f0000000-0000-0000-0000-000000000025','d0000000-0000-0000-0000-000000000002','02-Shared',1,NULL,false,NULL,'a0000000-0000-0000-0000-000000000002','2026-02-04 08:10:00+07',NULL),
('f0000000-0000-0000-0000-000000000026','d0000000-0000-0000-0000-000000000002','Kết cấu nhịp thép',0,'f0000000-0000-0000-0000-000000000010',false,NULL,'a0000000-0000-0000-0000-000000000003','2026-02-04 08:20:00+07',NULL);

-- ============================================================================
-- 13) NAMING CONVENTIONS (1:1 với Folder — quy tắc đặt tên của folder "Kiến trúc")
-- ============================================================================
INSERT INTO "NamingConventions" ("Id","FolderId","Delimiter","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fa000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000005','-',true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL);

-- ============================================================================
-- 14) NAMING CONVENTION FIELDS  FieldType: IsoStandard=0, Custom=1
--     (unique: NamingConventionId + Code)
-- ============================================================================
INSERT INTO "NamingConventionFields" ("Id","NamingConventionId","Code","DisplayName","Description","OrderIndex","IsRequired","IsLocked","MinLength","MaxLength","FieldType","CreatedById","CreatedAt","UpdatedAt") VALUES
('fb000000-0000-0000-0000-000000000001','fa000000-0000-0000-0000-000000000001','PRJ','Mã dự án','Mã dự án theo ISO 19650, khóa cứng giá trị RIV.',1,true,true,2,5,0,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('fb000000-0000-0000-0000-000000000002','fa000000-0000-0000-0000-000000000001','DIS','Bộ môn','Bộ môn thiết kế (chọn từ danh sách giá trị).',2,true,false,3,3,0,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('fb000000-0000-0000-0000-000000000003','fa000000-0000-0000-0000-000000000001','NUM','Số thứ tự','Số thứ tự bản vẽ, nhập tự do 3 ký tự.',3,true,false,3,3,1,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL);

-- ============================================================================
-- 15) NAMING CONVENTION FIELD VALUES  (unique: FieldId + Code)
-- ============================================================================
INSERT INTO "NamingConventionFieldValues" ("Id","NamingConventionFieldId","Code","DisplayName","Description","OrderIndex","IsLocked","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fc000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000001','RIV','Riverside Tower','Mã cố định của dự án Riverside Tower.',1,true,true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('fc000000-0000-0000-0000-000000000002','fb000000-0000-0000-0000-000000000002','ARC','Architecture','Bộ môn kiến trúc.',1,false,true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('fc000000-0000-0000-0000-000000000003','fb000000-0000-0000-0000-000000000002','STR','Structural','Bộ môn kết cấu.',2,false,true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('fc000000-0000-0000-0000-000000000004','fb000000-0000-0000-0000-000000000002','MEP','Mechanical-Electrical-Plumbing','Bộ môn cơ điện.',3,false,true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL);

-- ============================================================================
-- 16) NAMING CONVENTION LOCKED VALUES  (field PRJ bị khóa cứng vào giá trị RIV)
-- ============================================================================
INSERT INTO "NamingConventionLockedValues" ("Id","NamingConventionFieldId","NamingConventionFieldValueId","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fd000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000001','fc000000-0000-0000-0000-000000000001',true,'a0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL);

-- ============================================================================
-- 17) FOLDER PERMISSIONS  (6 cờ bool + Status: Active=0, Inactive=1)
--     (unique: FolderId + ProjectParticipantId)
-- ============================================================================
INSERT INTO "FolderPermissions" ("Id","FolderId","ProjectParticipantId","CanView","CanEdit","CanUpdate","CanDownload","CanVerify","CanApprove","Status") VALUES
('f1000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000005','d3000000-0000-0000-0000-000000000003',true,true,true,true,false,false,0),
('f1000000-0000-0000-0000-000000000002','f0000000-0000-0000-0000-000000000008','d3000000-0000-0000-0000-000000000005',true,false,false,true,true,false,0),
('f1000000-0000-0000-0000-000000000003','f0000000-0000-0000-0000-000000000009','d3000000-0000-0000-0000-000000000001',true,false,false,true,false,true,0),
('f1000000-0000-0000-0000-000000000004','f0000000-0000-0000-0000-000000000002','d3000000-0000-0000-0000-000000000002',true,false,true,true,false,false,1);

-- --- ACL bổ sung trên các folder con của cây (test suy quyền theo nhánh) ------
INSERT INTO "FolderPermissions" ("Id","FolderId","ProjectParticipantId","CanView","CanEdit","CanUpdate","CanDownload","CanVerify","CanApprove","Status") VALUES
('f1000000-0000-0000-0000-000000000005','f0000000-0000-0000-0000-000000000011','d3000000-0000-0000-0000-000000000004',true,false,false,true,false,false,0),
('f1000000-0000-0000-0000-000000000006','f0000000-0000-0000-0000-000000000019','d3000000-0000-0000-0000-000000000003',true,false,false,true,false,false,0),
('f1000000-0000-0000-0000-000000000007','f0000000-0000-0000-0000-000000000020','d3000000-0000-0000-0000-000000000005',true,false,false,true,true,false,0);

-- ============================================================================
-- 18) FILE ITEMS  FileType: Pdf=0,Ifc=1,Image=2,Cad=3,Office=4,Other=5
--                 Status(FileItemStatus): Draft=0,PendingApproval=1,Approved=2,Rejected=3
--     (CurrentVersionId/SignedVersionId là cột scalar — không có FK ràng buộc,
--      trỏ tới FileVersion sẽ chèn ở mục 19)
-- ============================================================================
INSERT INTO "FileItems" ("Id","FolderId","Name","FileType","Status","RequiresSignature","IsSigned","CurrentVersionId","SignedVersionId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('f2000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000005','RIV-ARC-001.pdf',0,1,false,false,'f3000000-0000-0000-0000-000000000001',NULL,'a0000000-0000-0000-0000-000000000003','2026-02-05 08:00:00+07',NULL),
('f2000000-0000-0000-0000-000000000002','f0000000-0000-0000-0000-000000000005','RIV-ARC-002.ifc',1,0,false,false,'f3000000-0000-0000-0000-000000000003',NULL,'a0000000-0000-0000-0000-000000000003','2026-02-05 08:10:00+07','2026-02-20 10:00:00+07'),
('f2000000-0000-0000-0000-000000000003','f0000000-0000-0000-0000-000000000006','ST-Calc.xlsx',4,3,false,false,'f3000000-0000-0000-0000-000000000004',NULL,'a0000000-0000-0000-0000-000000000004','2026-02-06 08:00:00+07',NULL),
('f2000000-0000-0000-0000-000000000004','f0000000-0000-0000-0000-000000000008','Coord-Drawing.dwg',3,0,false,false,'f3000000-0000-0000-0000-000000000005',NULL,'a0000000-0000-0000-0000-000000000003','2026-02-07 08:00:00+07',NULL),
('f2000000-0000-0000-0000-000000000005','f0000000-0000-0000-0000-000000000009','Published-Set.pdf',0,2,true,true,'f3000000-0000-0000-0000-000000000006','f3000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000002','2026-02-08 08:00:00+07','2026-02-09 10:00:00+07'),
('f2000000-0000-0000-0000-000000000006','f0000000-0000-0000-0000-000000000014','RIV-ARC-003.pdf',0,0,false,false,'f3000000-0000-0000-0000-000000000007',NULL,'a0000000-0000-0000-0000-000000000003','2026-02-12 08:00:00+07',NULL),
('f2000000-0000-0000-0000-000000000007','f0000000-0000-0000-0000-000000000019','Clash-Report-01.pdf',0,0,false,false,'f3000000-0000-0000-0000-000000000008',NULL,'a0000000-0000-0000-0000-000000000006','2026-02-13 08:00:00+07',NULL);

-- ============================================================================
-- 19) FILE VERSIONS
--     ViewerStatus(ModelViewerStatus): None=0,Pending=1,Processing=2,Ready=3,Failed=4
--     IsSigned/SignedAt/SignedBy/CertificateSerial: chữ ký số VNPT SmartCA
-- ============================================================================
INSERT INTO "FileVersions" ("Id","FileItemId","VersionNumber","StoragePath","Format","FileSizeBytes","Checksum","IsHidden","UploadedByAccountId","UploadedAt","ViewerUrn","PreviewStoragePath","ViewerStatus","ViewerProgress","ViewerError","IsSigned","SignedAt","SignedBy","CertificateSerial") VALUES
('f3000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001',1,'projects/d01/wip/kien-truc/riv-arc-001-v1.pdf','pdf',1048576,'sha256:aa01',false,'a0000000-0000-0000-0000-000000000003','2026-02-05 08:00:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000002',1,'projects/d01/wip/kien-truc/riv-arc-002-v1.ifc','ifc',5242880,'sha256:bb01',true,'a0000000-0000-0000-0000-000000000003','2026-02-05 08:10:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000002',2,'projects/d01/wip/kien-truc/riv-arc-002-v2.ifc','ifc',6291456,'sha256:bb02',false,'a0000000-0000-0000-0000-000000000003','2026-02-20 10:00:00+07','urn:adsk.objects:os.object:cde-bucket/riv-arc-002-v2.ifc',NULL,3,'100% complete',NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000004','f2000000-0000-0000-0000-000000000003',1,'projects/d01/wip/ket-cau/st-calc-v1.xlsx','xlsx',262144,'sha256:cc01',false,'a0000000-0000-0000-0000-000000000004','2026-02-06 08:00:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000005','f2000000-0000-0000-0000-000000000004',1,'projects/d01/shared/coord-drawing-v1.dwg','dwg',2097152,'sha256:dd01',false,'a0000000-0000-0000-0000-000000000003','2026-02-07 08:00:00+07',NULL,NULL,1,NULL,NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000006','f2000000-0000-0000-0000-000000000005',1,'projects/d01/published/published-set-v1.pdf','pdf',3145728,'sha256:ee01',false,'a0000000-0000-0000-0000-000000000002','2026-02-08 08:00:00+07',NULL,'projects/d01/published/preview/published-set-v1.pdf',0,NULL,NULL,true,'2026-02-09 10:00:00+07','a0000000-0000-0000-0000-000000000007','5404fefff36a01'),
('f3000000-0000-0000-0000-000000000007','f2000000-0000-0000-0000-000000000006',1,'projects/d01/wip/kien-truc/thap-a/tang-dien-hinh/riv-arc-003-v1.pdf','pdf',1572864,'sha256:ff01',false,'a0000000-0000-0000-0000-000000000003','2026-02-12 08:00:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('f3000000-0000-0000-0000-000000000008','f2000000-0000-0000-0000-000000000007',1,'projects/d01/shared/bao-cao-va-cham/clash-report-01-v1.pdf','pdf',786432,'sha256:ff02',false,'a0000000-0000-0000-0000-000000000006','2026-02-13 08:00:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL);

-- ============================================================================
-- 20) FILE PERMISSIONS  (6 cờ bool + Status: Active=0, Inactive=1)
--     (unique: FileItemId + ProjectParticipantId)
-- ============================================================================
INSERT INTO "FilePermissions" ("Id","FileItemId","ProjectParticipantId","CanView","CanEdit","CanUpdate","CanDownload","CanVerify","CanApprove","Status") VALUES
('f4000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000005','d3000000-0000-0000-0000-000000000001',true,false,false,true,false,true,0),
('f4000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000001','d3000000-0000-0000-0000-000000000005',true,false,false,true,true,false,0);

-- ============================================================================
-- 21) FILE NOTES (markup/ghi chú trên 1 phiên bản file)
-- ============================================================================
INSERT INTO "FileNotes" ("Id","FileVersionId","AuthorAccountId","Content","PageNumber","CoordinateJson","CreatedAt") VALUES
('f5000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000006','Cần kiểm tra cao độ sàn tầng 1 so với hồ sơ kết cấu.',1,'{"x":120,"y":340}','2026-02-10 09:00:00+07');

-- ============================================================================
-- 22) FILE NAMING METADATA  (giá trị từng field đặt tên của 1 file)
--     (unique: FileItemId+FieldId và FileItemId+SelectedValueId)
-- ============================================================================
INSERT INTO "FileNamingMetadata" ("Id","FileItemId","NamingConventionFieldId","SelectedValueId","Value","DisplayValue","CreatedAt","UpdatedAt") VALUES
('fe000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000001','fc000000-0000-0000-0000-000000000001','RIV','Riverside Tower','2026-02-05 08:00:00+07',NULL),
('fe000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000002','fc000000-0000-0000-0000-000000000002','ARC','Architecture','2026-02-05 08:00:00+07',NULL),
('fe000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000003',NULL,'001','001','2026-02-05 08:00:00+07',NULL),
('fe000000-0000-0000-0000-000000000004','f2000000-0000-0000-0000-000000000002','fb000000-0000-0000-0000-000000000001','fc000000-0000-0000-0000-000000000001','RIV','Riverside Tower','2026-02-05 08:10:00+07',NULL),
('fe000000-0000-0000-0000-000000000005','f2000000-0000-0000-0000-000000000002','fb000000-0000-0000-0000-000000000002','fc000000-0000-0000-0000-000000000002','ARC','Architecture','2026-02-05 08:10:00+07',NULL),
('fe000000-0000-0000-0000-000000000006','f2000000-0000-0000-0000-000000000002','fb000000-0000-0000-0000-000000000003',NULL,'002','002','2026-02-05 08:10:00+07',NULL);

-- ============================================================================
-- 23) APPROVAL REQUESTS  Status: Pending=0, Approved=1, Rejected=2
-- ============================================================================
INSERT INTO "ApprovalRequests" ("Id","FileItemId","RequestedBy","ApproverId","Status","RejectReason","CreatedAt","ApprovedAt") VALUES
('f6000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000007',1,NULL,'2026-02-08 09:00:00+07','2026-02-09 10:00:00+07'),
('f6000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000003',NULL,0,NULL,'2026-06-20 09:00:00+07',NULL),
('f6000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000003',2,'Thiếu bảng tính tải trọng gió, đề nghị bổ sung.','2026-02-15 09:00:00+07','2026-02-16 10:00:00+07');

-- ============================================================================
-- 24) APPROVAL SIGNATURE TRANSACTIONS  (VNPT SmartCA)
--     Status: Created=0, WaitingConfirm=1, Signed=2, Failed=3, Expired=4
-- ============================================================================
INSERT INTO "ApprovalSignatureTransactions" ("Id","ApprovalRequestId","FileItemId","TransactionId","CertificateSerial","Sad","SignedBy","SignedAt","Status","RawRequest","RawResponse","CreatedAt","UpdatedAt") VALUES
('f7000000-0000-0000-0000-000000000001','f6000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000005','vnpt-smartca-tx-0001','5404fefff36a01','sad-token-0001','a0000000-0000-0000-0000-000000000007','2026-02-09 10:00:00+07',2,'{"docId":"published-set-v1","hash":"sha256:ee01"}','{"status":"SIGNED","transactionId":"vnpt-smartca-tx-0001"}','2026-02-09 09:50:00+07','2026-02-09 10:00:00+07'),
('f7000000-0000-0000-0000-000000000002','f6000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000001','vnpt-smartca-tx-0002',NULL,NULL,NULL,NULL,4,'{"docId":"riv-arc-001-v1","hash":"sha256:aa01"}','{"status":"EXPIRED","transactionId":"vnpt-smartca-tx-0002"}','2026-06-20 09:10:00+07','2026-06-20 09:40:00+07');

-- ============================================================================
-- 25) ZONE RETURN REQUESTS  (trả file về khu vực CDE trước đó)
--     FromZone/TargetZone(CdeArea): Wip=0,Shared=1,Published=2,Archived=3
--     Status: Pending=0, Approved=1, Rejected=2
-- ============================================================================
INSERT INTO "ZoneReturnRequests" ("Id","FileItemId","FromZone","TargetZone","RequestedBy","ApprovedBy","Status","Reason","RejectReason","CreatedAt","DecidedAt") VALUES
('f8000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000005',2,1,'a0000000-0000-0000-0000-000000000005',NULL,0,'Phát hiện sai khác chi tiết mặt đứng, xin trả về Shared để rà soát.',NULL,'2026-06-25 09:00:00+07',NULL),
('f8000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000004',1,0,'a0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000002',1,'Bản vẽ phối hợp cần cập nhật lại theo model MEP mới.',NULL,'2026-06-18 09:00:00+07','2026-06-19 10:00:00+07');

-- ============================================================================
-- 26) FILE SIGNATURE POSITIONS  (vị trí đặt chữ ký trên PDF — unique theo FileItemId)
-- ============================================================================
INSERT INTO "FileSignaturePositions" ("Id","FileItemId","PageNumber","X","Y","Width","Height","CreatedBy","CreatedAt","UpdatedAt") VALUES
('f9000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000005',1,420.5,60.0,160.0,60.0,'a0000000-0000-0000-0000-000000000002','2026-02-08 09:30:00+07',NULL),
('f9000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000001',1,380.0,72.5,150.0,55.0,'a0000000-0000-0000-0000-000000000003','2026-06-20 08:30:00+07',NULL);

-- ============================================================================
-- 27) SUBMITTALS  Status: Draft=0,Submitted=1,UnderReview=2,Verified=3,
--                 Approved=4,Rejected=5,Returned=6
--                 WorkflowType: OneStep=0, TwoStep=1
-- ============================================================================
INSERT INTO "Submittals" ("Id","ProjectId","ContractPackageId","ParentSubmittalId","Title","Description","Status","WorkflowType","SubmittedByOrganizationId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('10000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000002',NULL,'Trình duyệt biện pháp thi công phần thân','Biện pháp tổ chức thi công kết cấu phần thân tháp A.',1,1,'b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-01 08:00:00+07',NULL),
('10000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000002',NULL,'Hồ sơ nghiệm thu cọc khoan nhồi','Hồ sơ nghiệm thu giai đoạn cọc khoan nhồi tháp A.',4,0,'b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-05 08:00:00+07','2026-03-12 16:00:00+07'),
('10000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001',NULL,'10000000-0000-0000-0000-000000000001','Trình mẫu vật liệu hoàn thiện','Trình mẫu gạch ốp, sơn, thiết bị vệ sinh (phiếu con của biện pháp thi công).',2,1,'b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-08 08:00:00+07',NULL);

-- ============================================================================
-- 28) SUBMITTAL STEPS  StepType: Submit=0,Verify=1,Approve=2
--                      Action  : Pending=0,Approved=1,Rejected=2,Returned=3
-- ============================================================================
INSERT INTO "SubmittalSteps" ("Id","SubmittalId","StepOrder","StepType","Action","AssignedAccountId","AssignedOrganizationId","ActedByAccountId","ActedAt","Comment") VALUES
('11000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',1,0,1,'a0000000-0000-0000-0000-000000000005','b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-01 08:00:00+07','Đã trình nộp đầy đủ hồ sơ.'),
('11000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001',2,1,0,'a0000000-0000-0000-0000-000000000006','b0000000-0000-0000-0000-000000000005',NULL,NULL,NULL),
('11000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000001',3,2,0,'a0000000-0000-0000-0000-000000000007','b0000000-0000-0000-0000-000000000001',NULL,NULL,NULL),
('11000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000002',1,0,1,'a0000000-0000-0000-0000-000000000005','b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-05 08:00:00+07','Trình hồ sơ nghiệm thu.'),
('11000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000002',2,2,1,'a0000000-0000-0000-0000-000000000007','b0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000007','2026-03-12 16:00:00+07','Đồng ý nghiệm thu.'),
('11000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000003',1,0,1,'a0000000-0000-0000-0000-000000000005','b0000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000005','2026-03-08 08:00:00+07','Trình mẫu vật liệu.'),
('11000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000003',2,1,0,'a0000000-0000-0000-0000-000000000008','b0000000-0000-0000-0000-000000000005',NULL,NULL,NULL);

-- ============================================================================
-- 29) SUBMITTAL ATTACHMENTS / CITED FOLDERS
-- ============================================================================
INSERT INTO "SubmittalAttachments" ("Id","SubmittalId","FileVersionId","AttachedByAccountId","AttachedAt") VALUES
('12000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000005','2026-03-01 08:05:00+07'),
('12000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','f3000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000005','2026-03-05 08:05:00+07');

INSERT INTO "SubmittalCitedFolders" ("Id","SubmittalId","FolderId") VALUES
('13000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000008'),
('13000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','f0000000-0000-0000-0000-000000000009');

-- ============================================================================
-- 30) DISCUSSIONS  ScopeType: Standalone=0,File=1,Note=2,Submittal=3,Issue=4
--                  Status   : Open=0, Resolved=1, Closed=2
-- ============================================================================
INSERT INTO "Discussions" ("Id","ProjectId","Title","ScopeType","ScopeId","Status","CreatedByAccountId","CreatedAt") VALUES
('20000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','Phối hợp cao độ dầm tầng 3',0,NULL,0,'a0000000-0000-0000-0000-000000000003','2026-03-10 09:00:00+07'),
('20000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','Thảo luận về file RIV-ARC-002.ifc',1,'f2000000-0000-0000-0000-000000000002',1,'a0000000-0000-0000-0000-000000000004','2026-03-11 09:00:00+07'),
('20000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000002','Phương án móng cầu',0,NULL,0,'a0000000-0000-0000-0000-000000000003','2026-03-12 09:00:00+07');

-- ============================================================================
-- 31) DISCUSSION MESSAGES  (ReplyToMessageId tự tham chiếu — chèn tin gốc trước)
-- ============================================================================
INSERT INTO "DiscussionMessages" ("Id","DiscussionId","AuthorAccountId","Content","IsSolution","ReplyToMessageId","RecalledAt","CreatedAt") VALUES
('21000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000003','Cao độ dầm tầng 3 đang lệch 50mm giữa mô hình KT và KC, cần thống nhất.',false,NULL,NULL,'2026-03-10 09:05:00+07'),
('21000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000004','Đề xuất lấy theo bản vẽ kết cấu và cập nhật lại mô hình kiến trúc.',false,'21000000-0000-0000-0000-000000000001',NULL,'2026-03-10 09:20:00+07'),
('21000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000002','Thống nhất theo kết cấu. Chốt phương án này, nhóm KT cập nhật model.',true,'21000000-0000-0000-0000-000000000002',NULL,'2026-03-10 10:00:00+07'),
('21000000-0000-0000-0000-000000000004','20000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000004','Model IFC v2 đã cập nhật, xử lý va chạm với hệ MEP.',false,NULL,NULL,'2026-03-11 09:10:00+07'),
('21000000-0000-0000-0000-000000000005','20000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000006','Đã kiểm tra lại, không còn va chạm. Đóng thảo luận.',true,NULL,NULL,'2026-03-11 14:00:00+07'),
('21000000-0000-0000-0000-000000000006','20000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003','Đề xuất phương án móng cọc khoan nhồi D1000.',false,NULL,NULL,'2026-03-12 09:05:00+07');

-- ============================================================================
-- 32) MESSAGE MENTIONS / ATTACHMENTS / DISCUSSION CITED FOLDERS
--     MessageAttachmentType: File=0, Image=1, Link=2, CitedFolder=3
-- ============================================================================
INSERT INTO "MessageMentions" ("Id","DiscussionMessageId","MentionedAccountId") VALUES
('22000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000004'),
('22000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003'),
('22000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004');

INSERT INTO "MessageAttachments" ("Id","DiscussionMessageId","Type","FileVersionId","FolderId","Url") VALUES
('23000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000002',0,'f3000000-0000-0000-0000-000000000003',NULL,NULL),
('23000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000004',2,NULL,NULL,'https://bim.example.vn/model/ar-v2'),
('23000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000006',3,NULL,'f0000000-0000-0000-0000-000000000010',NULL);

INSERT INTO "DiscussionCitedFolders" ("Id","DiscussionId","FolderId") VALUES
('24000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000005'),
('24000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002','f0000000-0000-0000-0000-000000000005');

-- ============================================================================
-- 33) ISSUES  Type: Issue=0, Rfi=1 | Priority: Low=0,Medium=1,High=2,Critical=3
--             Status: Open=0, InProgress=1, Answered=2, Closed=3
-- ============================================================================
INSERT INTO "Issues" ("Id","ProjectId","Title","Description","Type","Priority","Status","RaisedByAccountId","AssignedToAccountId","AssignedToOrganizationId","DueDate","LinkedFolderId","LinkedFileItemId","ModelLocationJson","CreatedAt","UpdatedAt") VALUES
('30000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','Va chạm ống gió và dầm tại trục C-3','Ống gió HVAC va chạm dầm bê tông tại trục C-3, tầng 3.',0,2,0,'a0000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000003','b0000000-0000-0000-0000-000000000003','2026-06-25 17:00:00+07','f0000000-0000-0000-0000-000000000005','f2000000-0000-0000-0000-000000000002','{"dbId":12345,"position":{"x":1.2,"y":3.4,"z":2.1}}','2026-06-10 09:00:00+07',NULL),
('30000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','RFI: Xác nhận mác bê tông cột tầng hầm','Đề nghị xác nhận mác bê tông cột tầng hầm B1-B3.',1,1,2,'a0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000003','b0000000-0000-0000-0000-000000000003',NULL,NULL,NULL,NULL,'2026-06-11 09:00:00+07','2026-06-12 10:00:00+07'),
('30000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','Thiếu chi tiết liên kết thép tại nút khung','Bản vẽ thiếu chi tiết liên kết thép tại nút khung trục B-2.',0,3,1,'a0000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000004','b0000000-0000-0000-0000-000000000003','2026-06-20 17:00:00+07','f0000000-0000-0000-0000-000000000006',NULL,NULL,'2026-06-12 09:00:00+07',NULL),
('30000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000002','RFI: Cao độ thiết kế mặt cầu','Xác nhận cao độ hoàn thiện mặt cầu so với mốc chuẩn.',1,0,0,'a0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000003',NULL,NULL,NULL,NULL,NULL,'2026-06-13 09:00:00+07',NULL);

-- ============================================================================
-- 34) ISSUE COMMENTS / MENTIONS / ATTACHMENTS / CITED FOLDERS
-- ============================================================================
INSERT INTO "IssueComments" ("Id","IssueId","AuthorAccountId","Content","CreatedAt") VALUES
('31000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000003','Đã nhận, sẽ điều chỉnh tuyến ống gió trong mô hình MEP.','2026-06-10 10:00:00+07'),
('31000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000003','Mác bê tông cột tầng hầm là B30 (tương đương M400).','2026-06-12 09:30:00+07'),
('31000000-0000-0000-0000-000000000003','30000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000005','Đã rõ, cảm ơn tư vấn thiết kế.','2026-06-12 10:00:00+07'),
('31000000-0000-0000-0000-000000000004','30000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004','Đang bổ sung chi tiết liên kết, dự kiến hoàn thành trong 2 ngày.','2026-06-13 08:00:00+07');

INSERT INTO "IssueMentions" ("Id","IssueId","MentionedAccountId") VALUES
('32000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000003'),
('32000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004');

INSERT INTO "IssueAttachments" ("Id","IssueId","FileVersionId","Url") VALUES
('33000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000000003',NULL),
('33000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002',NULL,'https://bim.example.vn/rfi/bt-cot.pdf');

INSERT INTO "IssueCitedFolders" ("Id","IssueId","FolderId") VALUES
('34000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000005');

-- ============================================================================
-- 35) CONTRACTS  Status: Draft=0, Active=1, Superseded=2, Closed=3
-- ============================================================================
INSERT INTO "Contracts" ("Id","ContractPackageId","Code","Name","Status","ContractorOrganizationId","SignedDate","SourceFileVersionId","CreatedAt","UpdatedAt") VALUES
('40000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000002','HD-2026-001','Hợp đồng thi công phần thân',1,'b0000000-0000-0000-0000-000000000004','2026-01-15 00:00:00+07',NULL,'2026-01-15 08:00:00+07',NULL),
('40000000-0000-0000-0000-000000000002','e0000000-0000-0000-0000-000000000003','HD-2026-002','Hợp đồng cung cấp vật liệu M&E',0,'b0000000-0000-0000-0000-000000000006',NULL,NULL,'2026-02-02 08:00:00+07',NULL);

-- ============================================================================
-- 36) CONTRACT APPENDICES
-- ============================================================================
INSERT INTO "ContractAppendices" ("Id","ContractId","AppendixNo","Note","SignedDate","SourceFileVersionId","CreatedAt") VALUES
('41000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',1,'Phụ lục điều chỉnh khối lượng đợt 1.','2026-03-01 00:00:00+07',NULL,'2026-03-01 08:00:00+07');

-- ============================================================================
-- 37) BILL ITEMS  Sheet(BillSheet): InContract=0, OutOfContract=1
--     (Level + cây cha-con: chèn dòng cha trước, dòng con sau)
-- ============================================================================
INSERT INTO "BillItems" ("Id","ContractId","ContractAppendixId","ParentBillItemId","Code","Name","Unit","Level","Sheet","ContractQuantity","ContractUnitPrice","ContractAmount","AdjustedQuantity","AdjustedUnitPrice","AdjustedAmount") VALUES
('42000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',NULL,NULL,'A','Phần thân',NULL,0,0,NULL,NULL,1275000000,NULL,NULL,NULL),
('42000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000001',NULL,'42000000-0000-0000-0000-000000000001','A.1','Bê tông cột','m3',1,0,250,1500000,375000000,NULL,NULL,NULL),
('42000000-0000-0000-0000-000000000003','40000000-0000-0000-0000-000000000001',NULL,'42000000-0000-0000-0000-000000000001','A.2','Cốt thép','kg',1,0,50000,18000,900000000,NULL,NULL,NULL),
('42000000-0000-0000-0000-000000000004','40000000-0000-0000-0000-000000000001','41000000-0000-0000-0000-000000000001',NULL,'X.1','Phát sinh chống thấm','m2',0,1,300,250000,75000000,320,250000,80000000);

-- ============================================================================
-- 38) MODEL FILES  Status(ModelFileStatus): Uploaded=0,Processing=1,Ready=2,Failed=3
-- ============================================================================
INSERT INTO "ModelFiles" ("Id","ProjectId","ProjectModelId","Name","Status","OffsetX","OffsetY","OffsetZ","RotationJson","SourceFileVersionId","CreatedAt") VALUES
('50000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','d2000000-0000-0000-0000-000000000001','RIV-ARC-002.ifc',2,0,0,0,'{"x":0,"y":0,"z":0}','f3000000-0000-0000-0000-000000000003','2026-02-21 08:00:00+07'),
('50000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','d2000000-0000-0000-0000-000000000002','ST-Model.ifc',1,0,0,0,NULL,NULL,'2026-02-22 08:00:00+07'),
('50000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000002','d2000000-0000-0000-0000-000000000003','Bridge-Model.ifc',0,0,0,0,NULL,NULL,'2026-02-23 08:00:00+07');

-- ============================================================================
-- 39) MODEL OBJECTS
-- ============================================================================
INSERT INTO "ModelObjects" ("Id","ModelFileId","ObjectGuid","Name") VALUES
('51000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','3kf9d0a1-ar-0001','Tường trục A'),
('51000000-0000-0000-0000-000000000002','50000000-0000-0000-0000-000000000001','3kf9d0a1-ar-0002','Dầm tầng 3'),
('51000000-0000-0000-0000-000000000003','50000000-0000-0000-0000-000000000001','3kf9d0a1-ar-0003','Cột C-3');

-- ============================================================================
-- 40) NOTIFICATIONS  (LinkId là text; LinkType phân loại: Invitation/Submittal/
--                     Discussion/Issue...)  IsRead bool
-- ============================================================================
INSERT INTO "Notifications" ("Id","AccountId","Message","SenderName","IsRead","LinkId","LinkType","SendAt") VALUES
('60000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000009','Bạn được mời tham gia dự án Khu phức hợp Riverside Tower.','Trần Thị Hoa',false,'d4000000-0000-0000-0000-000000000001','Invitation','2026-06-14 09:00:00+07'),
('60000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000008','Bạn được phân công thẩm tra submittal "Trình mẫu vật liệu hoàn thiện".','Vũ Văn Bình',false,'10000000-0000-0000-0000-000000000003','Submittal','2026-06-14 10:00:00+07'),
('60000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003','Bạn được nhắc trong thảo luận "Phối hợp cao độ dầm tầng 3".','Trần Thị Hoa',true,'20000000-0000-0000-0000-000000000001','Discussion','2026-06-10 10:05:00+07'),
('60000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000003','Issue mới được gán cho bạn: Va chạm ống gió và dầm tại trục C-3.','Đỗ Mạnh Cường',false,'30000000-0000-0000-0000-000000000001','Issue','2026-06-10 09:05:00+07'),
('60000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000004','Issue ưu tiên cao cần xử lý: Thiếu chi tiết liên kết thép tại nút khung.','Bùi Văn Em',false,'30000000-0000-0000-0000-000000000003','Issue','2026-06-12 09:05:00+07'),
('60000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000002','Submittal "Hồ sơ nghiệm thu cọc khoan nhồi" đã được duyệt.','Ngô Thị Dương',true,'10000000-0000-0000-0000-000000000002','Submittal','2026-03-12 16:05:00+07'),
('60000000-0000-0000-0000-000000000007','a0000000-0000-0000-0000-000000000007','Có yêu cầu phê duyệt mới cho file RIV-ARC-001.pdf.','Lê Hoàng Nam',false,'f6000000-0000-0000-0000-000000000002','ApprovalRequest','2026-06-20 09:01:00+07');

-- ============================================================================
-- 41) DOCUMENTS (RAG)  Area(CdeArea): Wip=0,Shared=1,Published=2,Archived=3
--     Status(DocumentIngestStatus): Pending=0, Embedded=1, Failed=2
-- ============================================================================
INSERT INTO "Documents" ("Id","SourceFileVersionId","FileItemId","ProjectId","Area","Discipline","FileName","Format","Revision","UpdateAt","ContentHash","Status","IngestedAt","ChunkCount") VALUES
('7a000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001',0,'Architecture','RIV-ARC-001.pdf','pdf','P01','2026-03-01 08:00:00+07','sha256:aa01',1,'2026-03-01 08:30:00+07',3),
('7a000000-0000-0000-0000-000000000002','f3000000-0000-0000-0000-000000000006','f2000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000001',2,NULL,'Published-Set.pdf','pdf','C01','2026-03-02 08:00:00+07','sha256:ee01',0,NULL,0);

-- ============================================================================
-- 42) DOCUMENT PARENT CHUNKS  (chunk cha — ngữ cảnh rộng cho contextual retrieval)
-- ============================================================================
INSERT INTO "DocumentParentChunks" ("Id","DocumentId","ProjectId","ChunkIndex","Content","SectionTitle","PageNumber","CreatedAt") VALUES
('7b000000-0000-0000-0000-000000000001','7a000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001',0,'Chương 1: Quy định chung về hồ sơ thiết kế kiến trúc tầng 1, bao gồm cao độ hoàn thiện, vật liệu sàn và yêu cầu chống thấm khu vệ sinh.','Quy định chung',1,'2026-03-01 08:30:00+07'),
('7b000000-0000-0000-0000-000000000002','7a000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001',1,'Chương 2: Chi tiết mặt bằng tầng 1 — bố trí căn hộ, hành lang, lõi thang và các trục định vị chính A-E.','Mặt bằng tầng 1',2,'2026-03-01 08:30:00+07');

-- ============================================================================
-- 43) DOCUMENT CHUNKS (child — đơn vị truy vấn vector)
--     Embedding vector(1024): để NULL — pipeline ingest của BE sẽ sinh embedding
--     thật bằng Ollama; không seed tay để tránh lệch số chiều/model.
-- ============================================================================
INSERT INTO "DocumentChunks" ("Id","DocumentId","ProjectId","ParentChunkId","ChunkIndex","Content","Embedding","CreatedAt") VALUES
('7c000000-0000-0000-0000-000000000001','7a000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','7b000000-0000-0000-0000-000000000001',0,'Cao độ hoàn thiện sàn tầng 1 là +0.450 so với cốt 0.00 của công trình.',NULL,'2026-03-01 08:30:00+07'),
('7c000000-0000-0000-0000-000000000002','7a000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','7b000000-0000-0000-0000-000000000001',1,'Khu vệ sinh dùng lớp chống thấm gốc xi măng 2 thành phần, thi công 2 lớp.',NULL,'2026-03-01 08:30:00+07'),
('7c000000-0000-0000-0000-000000000003','7a000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','7b000000-0000-0000-0000-000000000002',2,'Trục định vị chính A-E cách đều 8.4m; lõi thang bố trí giữa trục C và D.',NULL,'2026-03-01 08:30:00+07');

-- ============================================================================
-- 44) REFRESH TOKENS  (AccountId không có FK ràng buộc; CreatedAt/ExpiresAt NOT NULL)
-- ============================================================================
INSERT INTO "RefreshTokens" ("Id","AccountId","Token","CreatedAt","ExpiresAt","RevokedAt","ReplacedByToken") VALUES
('80000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','refresh-admin-active-0001','2026-06-15 08:00:00+07','2026-07-15 08:00:00+07',NULL,NULL),
('80000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002','refresh-hoa-active-0002','2026-06-16 08:00:00+07','2026-07-16 08:00:00+07',NULL,NULL),
('80000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000002','refresh-hoa-old-0003','2026-05-16 08:00:00+07','2026-06-16 08:00:00+07','2026-06-16 08:00:00+07','refresh-hoa-active-0002');

-- ============================================================================
-- 45) AUDIT LOGS  Action(AuditAction): Create=0,Update=1,Delete=2,Move=3,Submit=4,
--                 Verify=5,Approve=6,Reject=7,Download=8,PermissionChange=9
--                 (EntityId là TEXT; ActorAccountId/ProjectId không có FK ràng buộc)
-- ============================================================================
INSERT INTO "AuditLogs" ("Id","Action","ActorAccountId","EntityType","EntityId","ProjectId","DetailJson","CreatedAt") VALUES
('90000000-0000-0000-0000-000000000001',0,'a0000000-0000-0000-0000-000000000002','Project','d0000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','{"projectName":"Khu phức hợp căn hộ Riverside Tower"}','2026-01-15 08:00:00+07'),
('90000000-0000-0000-0000-000000000002',0,'a0000000-0000-0000-0000-000000000003','FileItem','f2000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','{"name":"RIV-ARC-001.pdf","folder":"Kiến trúc"}','2026-02-05 08:00:00+07'),
('90000000-0000-0000-0000-000000000003',4,'a0000000-0000-0000-0000-000000000005','Submittal','10000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','{"title":"Trình duyệt biện pháp thi công phần thân"}','2026-03-01 08:00:00+07'),
('90000000-0000-0000-0000-000000000004',6,'a0000000-0000-0000-0000-000000000007','ApprovalRequest','f6000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','{"file":"Published-Set.pdf","result":"Approved","signed":true}','2026-02-09 10:00:00+07'),
('90000000-0000-0000-0000-000000000005',9,'a0000000-0000-0000-0000-000000000002','Folder','f0000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000001','{"group":"Tư vấn thiết kế / BIM","granted":["View","Edit","Update","Download"]}','2026-02-10 09:00:00+07'),
('90000000-0000-0000-0000-000000000006',8,'a0000000-0000-0000-0000-000000000006','FileVersion','f3000000-0000-0000-0000-000000000006','d0000000-0000-0000-0000-000000000001',NULL,'2026-03-13 10:00:00+07');

COMMIT;

-- ============================================================================
--  KẾT THÚC. Số dòng mỗi bảng (tham khảo):
--    Accounts 9 · Organizations 6 · Groups 6 · GroupMembers 9
--    Projects 4 · ProjectLocations 4 · ProjectModels 3 · ProjectParticipants 8
--    ProjectInvitations 5 · ContractPackages 4 · PackageAssignments 4
--    Folders 26 (2 template, cây sâu 4 cấp) · FolderPermissions 7
--    FileItems 7 · FileVersions 8 · FilePermissions 2 · FileNotes 1
--    NamingConventions 1 · NamingConventionFields 3 · NamingConventionFieldValues 4
--    NamingConventionLockedValues 1 · FileNamingMetadata 6
--    ApprovalRequests 3 · ApprovalSignatureTransactions 2
--    ZoneReturnRequests 2 · FileSignaturePositions 2
--    Submittals 3 · SubmittalSteps 7 · SubmittalAttachments 2 · SubmittalCitedFolders 2
--    Discussions 3 · DiscussionMessages 6 · MessageMentions 3 · MessageAttachments 3
--    DiscussionCitedFolders 2
--    Issues 4 · IssueComments 4 · IssueMentions 2 · IssueAttachments 2 · IssueCitedFolders 1
--    Contracts 2 · ContractAppendices 1 · BillItems 4
--    ModelFiles 3 · ModelObjects 3 · Notifications 7
--    Documents 2 · DocumentParentChunks 2 · DocumentChunks 3
--    RefreshTokens 3 · AuditLogs 6
--    OrganizationTypes 8 (migration seed — chèn lại ON CONFLICT DO NOTHING)
--
--  ⇒ Phủ TOÀN BỘ 52 bảng trong CDESystemDbContext, không chừa bảng nào.
-- ============================================================================

-- ============================================================================
--  CDE System — SEED DATA "CHƯA CÓ DỰ ÁN" (PostgreSQL / Npgsql, EF Core schema)
-- ----------------------------------------------------------------------------
--  Mục đích : dữ liệu test ở trạng thái BAN ĐẦU — đã có tài khoản, tổ chức,
--             nhóm và thành viên nhóm, nhưng CHƯA có bất kỳ Project, Folder,
--             File hay dữ liệu phái sinh nào (permissions, naming convention,
--             approval, discussion, issue, contract, notification...).
--             Dùng để test luồng TẠO MỚI dự án / thư mục / upload file từ đầu.
--  Sinh theo : seed_data_new.sql (giữ nguyên UUID + mật khẩu các bảng gốc)
--              + Infrastructure/Migrations/20260711144613_InitialCreate.cs
--              + Domain/Enum/* (mọi enum lưu DƯỚI DẠNG SỐ — integer).
--
--  CÁCH CHẠY :
--    psql -h localhost -U <user> -d CapstoneProjectDb -f seed_data_no_projects.sql
--    (hoặc dán toàn bộ vào pgAdmin / DBeaver Query Tool rồi Execute)
--
--  ĐẶC ĐIỂM :
--    • Idempotent: TRUNCATE toàn bộ bảng nghiệp vụ (TRỪ OrganizationTypes —
--      migration đã seed 8 dòng) rồi INSERT lại phần nền tảng với UUID CỐ ĐỊNH.
--    • Các bảng thuộc Project/Folder/File chỉ bị TRUNCATE — KHÔNG insert gì,
--      nên DB kết thúc ở trạng thái "sạch dự án".
--    • Bọc trong 1 transaction (BEGIN/COMMIT) — lỗi giữa chừng rollback hết.
--    • ⚠️ CẢNH BÁO: XÓA toàn bộ dữ liệu cũ trong các bảng ở lệnh TRUNCATE.
--      KHÔNG chạy trên DB có dữ liệu thật cần giữ.
--
--  TÀI KHOẢN : mật khẩu CHUNG = "password"
--      admin@cde.vn (Admin) · hoa.pm@cde.vn (PM) · nam.design / lan.design
--      binh.contractor · cuong.super · duong.client · em.verify
--      phong.viewer@cde.vn = INACTIVE + CHƯA verify email (test verify-otp)
--
--  Quy ước UUID (segment đầu = "mã bảng" cho dễ lần theo quan hệ):
--    a0*=Accounts  b0*=Organizations  c0*=Groups  c1*=GroupMembers
--    80*=RefreshTokens
-- ============================================================================

BEGIN;

-- --- Dọn dữ liệu cũ (KHÔNG đụng "OrganizationTypes" do migration seed) -------
TRUNCATE TABLE
    "ApprovalSignatureTransactions", "ApprovalRequestSigners", "ApprovalRequests",
    "ZoneReturnRequests", "Notifications",
    "BillItems", "ContractAppendices", "Contracts",
    "IssueCitedFolders", "IssueAttachments", "IssueMentions", "IssueComments", "Issues",
    "DiscussionCitedFolders", "MessageAttachments", "MessageMentions", "DiscussionMessages", "Discussions",
    "FileNotes", "MarkupSets", "FileSignaturePositions",
    "FileNamingMetadata", "NamingConventionLockedValues", "NamingConventionFieldValues",
    "NamingConventionFields", "NamingConventions",
    "FileLinks", "FilePermissions", "FileVersions", "FileItems", "FolderPermissions", "Folders",
    "PackageAssignments", "ContractPackages",
    "ProjectInvitations", "ProjectParticipants", "ProjectLocations", "Projects",
    "GroupMembers", "Groups", "Organizations",
    "RefreshTokens", "AuditLogs",
    "DocumentChunks", "DocumentParentChunks", "Documents",
    "Accounts"
    RESTART IDENTITY CASCADE;

-- ============================================================================
-- 1) ACCOUNTS  Role: Admin=0, User=1 | Status: Active=0, Inactive=1, Suspended=2
--    IsEmailVerified NOT NULL — a9 phong.viewer: CHƯA xác thực, còn OTP chờ nhập
--    (test luồng verify-otp / resend-otp).
-- ============================================================================
INSERT INTO "Accounts" ("Id","UserName","Email","PasswordHash","Role","Status","ResetPasswordToken","ResetPasswordTokenExpiresAt","IsEmailVerified","EmailOtp","EmailOtpExpiresAt","CreatedAt","UpdatedAt") VALUES
('a0000000-0000-0000-0000-000000000001','Nguyễn Văn Admin','admin@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',0,0,NULL,NULL,true,NULL,NULL,'2026-01-02 08:00:00+07','2026-01-02 08:00:00+07'),
('a0000000-0000-0000-0000-000000000002','Trần Thị Hoa','hoa.pm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-03 08:00:00+07','2026-01-03 08:00:00+07'),
('a0000000-0000-0000-0000-000000000003','Lê Hoàng Nam','nam.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-04 08:00:00+07','2026-01-04 08:00:00+07'),
('a0000000-0000-0000-0000-000000000004','Phạm Thị Lan','lan.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'reset-token-lan-demo-0001','2026-07-20 09:00:00+07',true,NULL,NULL,'2026-01-04 09:00:00+07','2026-01-04 09:00:00+07'),
('a0000000-0000-0000-0000-000000000005','Vũ Văn Bình','binh.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-05 08:00:00+07','2026-01-05 08:00:00+07'),
('a0000000-0000-0000-0000-000000000006','Đỗ Mạnh Cường','cuong.super@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-06 08:00:00+07','2026-01-06 08:00:00+07'),
('a0000000-0000-0000-0000-000000000007','Ngô Thị Dương','duong.client@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-06 09:00:00+07','2026-01-06 09:00:00+07'),
('a0000000-0000-0000-0000-000000000008','Bùi Văn Em','em.verify@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,true,NULL,NULL,'2026-01-07 08:00:00+07','2026-01-07 08:00:00+07'),
('a0000000-0000-0000-0000-000000000009','Đặng Quốc Phong','phong.viewer@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,1,NULL,NULL,false,'482913','2026-07-12 09:00:00+07','2026-01-08 08:00:00+07','2026-01-08 08:00:00+07');

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
-- 5) REFRESH TOKENS  (chỉ phụ thuộc Accounts — giữ lại để test luồng refresh)
-- ============================================================================
INSERT INTO "RefreshTokens" ("Id","AccountId","Token","CreatedAt","ExpiresAt","RevokedAt","ReplacedByToken") VALUES
('80000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','refresh-admin-active-0001','2026-07-01 08:00:00+07','2026-08-01 08:00:00+07',NULL,NULL),
('80000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002','refresh-hoa-active-0002','2026-07-02 08:00:00+07','2026-08-02 08:00:00+07',NULL,NULL),
('80000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000002','refresh-hoa-old-0003','2026-06-02 08:00:00+07','2026-07-02 08:00:00+07','2026-07-02 08:00:00+07','refresh-hoa-active-0002');

COMMIT;

-- ============================================================================
--  KẾT THÚC. Số dòng mỗi bảng (tham khảo):
--    Accounts 9 · Organizations 6 · Groups 6 · GroupMembers 9 · RefreshTokens 3
--    OrganizationTypes 8 (do MIGRATION seed sẵn — KHÔNG truncate/insert ở đây)
--  Các bảng còn lại (Projects, Folders, FileItems, permissions, naming
--  convention, approval, discussion, issue, contract, notification, document,
--  audit log...) đều TRỐNG — sẵn sàng test luồng tạo dự án từ đầu.
-- ============================================================================

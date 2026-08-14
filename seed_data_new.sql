-- ============================================================================
--  CDE System — SEED DATA DEMO (PostgreSQL / Npgsql, EF Core schema)
-- ----------------------------------------------------------------------------
--  Muc dich : bo du lieu mau "nhu that" de trinh dien he thong —
--             5 du an day du chuoi nghiep vu, 28 tai khoan, 15 to chuc
--             (chu dau tu / ban QLDA / tu van / nha thau / thau phu /
--              giam sat / cung cap / van hanh / lien danh).
--
--  Doi chieu schema : sinh truc tiep tu information_schema cua DB da migrate
--                     (khong phai tu InitialCreate). Moi enum luu DUOI DANG SO.
--
--  CACH CHAY :
--    psql -h localhost -U postgres -d CapstoneProjectDb -f seed_data_new.sql
--
--  DAC DIEM :
--    - Idempotent: TRUNCATE cac bang seed roi INSERT lai voi UUID CO DINH.
--    - Bot trong 1 transaction (BEGIN/COMMIT) — loi giua chung rollback het.
--    - KHONG dung "OrganizationTypes" (migration da seed 8 dong) va KHONG dung
--      nhom bang "Loi*" (da co seed_loi_rules.sql — 656 cau kien, 158 tham so).
--      Projects."LoiRuleSetId" de NULL => du an dung bo luat MAC DINH,
--      nho vay file nay khong phu thuoc vao thu tu chay cua seed_loi_rules.sql.
--    - CANH BAO: XOA toan bo du lieu cu trong cac bang o lenh TRUNCATE.
--
--  TAI KHOAN : mat khau CHUNG = "password"
--      admin@cde.vn        Admin, Ban QLDA Thu Duc
--      hoa.pm@cde.vn       PM cua ca 5 du an
--      nam.design@cde.vn   Leader Tu van thiet ke
--      binh.contractor@cde.vn  Leader Nha thau chinh
--      cuong.super@cde.vn  Leader Tu van giam sat
--      phong.viewer@cde.vn TAI KHOAN KHOA (Inactive, chua xac thuc email — con OTP)
--      ... 28 tai khoan, xem muc 1.
--
--  Quy uoc UUID (segment dau = "ma bang" cho de lan theo quan he):
--    a0*=Accounts  b0*=Organizations b1*=JointVentureMembers
--    c0*=Groups  c1*=GroupMembers
--    d0*=Projects d1*=Locations d3*=Participants d4*=Invitations d5*=Documents
--    e0*=ContractPackages e1*=PackageAssignments e2*=Contracts
--    f0*=Folders f1*=FolderPermissions f2*=FileItems f3*=FileVersionStates
--    f4*=FilePermissions f5*=FileNotes f6*=MarkupSets f7*=FolderNamingFields
--    f8*=FileViewGrants f9*=FileSignaturePositions ff*=FileLinks
--    fa*=NamingConventions fb*=Fields fc*=FieldValues fd*=LockedValues
--    fe*=FileNamingMetadata
--    aa*=ApprovalRequests ab*=SignatureTransactions ac*=ZoneReturnRequests
--    ad*=ApprovalRequestSigners
--    20*=Discussions 21*=Messages 22*=Mentions 23*=MsgAttachments
--    30*=Issues 32*=IssueMentions 33*=IssueAttachments 34*=IssueFileViewGrants
--    60*=Notifications 80*=RefreshTokens 90*=AuditLogs
-- ============================================================================

BEGIN;

-- --- Don du lieu cu ---------------------------------------------------------
-- KHONG dung: "OrganizationTypes" (migration seed) va "Loi*" (seed_loi_rules.sql)
TRUNCATE TABLE
    "ApprovalSignatureTransactions", "ApprovalRequestSigners", "ApprovalRequests",
    "ZoneReturnRequests", "Notifications",
    "IssueFileViewGrants", "IssueAttachments", "IssueMentions", "Issues",
    "MessageAttachments", "MessageMentions", "DiscussionMessages", "Discussions",
    "FileNotes", "MarkupSets", "FileSignaturePositions", "FileViewGrants",
    "FileNamingMetadata", "NamingConventionLockedValues", "NamingConventionFieldValues",
    "NamingConventionFields", "NamingConventions", "FolderNamingFields",
    "FileLinks", "FilePermissions", "FileVersionStates", "FileItems",
    "FolderPermissions", "Folders",
    "Contracts", "PackageAssignments", "ContractPackages",
    "ProjectInvitations", "ProjectParticipants", "ProjectLocations", "Projects",
    "GroupMembers", "Groups", "JointVentureMembers", "Organizations",
    "RefreshTokens", "AuditLogs",
    "DocumentChunks", "DocumentParentChunks", "Documents",
    "Accounts"
    RESTART IDENTITY CASCADE;

-- ============================================================================
-- 1) ORGANIZATIONS — 14 phap nhan + 1 lien danh
--    OrganizationTypeId tro toi 8 dong migration da seed san:
--      Client                7f947ce1-e7c6-49b2-aa41-f9b30292917a
--      ProjectManagementUnit ad5b98c7-b28f-4c40-861a-5a363b84eb00
--      Consultant            d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5
--      MainContractor        ae2fd257-cca8-4bb4-8f90-c0c45100702b
--      Subcontractor         8c0dcb7d-87fe-413e-b8d6-83eb91171cbe
--      Surveyor              ad4c917e-b170-4ff8-bca3-10764641c8d9
--      Supplier              3fe93ed9-2e6a-47a6-90cf-6e5aac24c645
--      FacilityManagement    e48c6618-c877-46bf-9d6d-7d9fb92a50e9
--    b0..15 = LIEN DANH (IsJointVenture=true) — thanh vien o "JointVentureMembers".
-- ============================================================================
INSERT INTO "Organizations" ("Id","TaxCode","LegalName","DisplayName","InternationalName","OrganizationTypeId","Address","Phone","Email","IsJointVenture","RepresentativeOrganizationId","CreatedAt","UpdatedAt") VALUES
('b0000000-0000-0000-0000-000000000001','0312845771','Công ty Cổ phần Đầu tư Bất động sản Minh Khang','Minh Khang Invest','Minh Khang Real Estate Investment JSC','7f947ce1-e7c6-49b2-aa41-f9b30292917a','Tầng 18, Tòa nhà Sunrise, 27 Nguyễn Hữu Cảnh, P.22, Bình Thạnh, TP.HCM','02836221188','lienhe@minhkhang-invest.vn',false,NULL,'2026-01-02 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000002','0106427318','Tổng Công ty Phát triển Hạ tầng Đông Dương','Hạ tầng Đông Dương','Indochina Infrastructure Development Corporation','7f947ce1-e7c6-49b2-aa41-f9b30292917a','Số 8 Phạm Hùng, P. Mỹ Đình 2, Q. Nam Từ Liêm, Hà Nội','02437854420','info@donduonginfra.vn',false,NULL,'2026-01-02 08:10:00+07',NULL),
('b0000000-0000-0000-0000-000000000003','0313996402','Ban Quản lý Dự án Đầu tư Xây dựng Khu vực Thủ Đức','Ban QLDA Thủ Đức','Thu Duc Construction Investment Project Management Unit','ad5b98c7-b28f-4c40-861a-5a363b84eb00','12 Đường số 5, P. Hiệp Bình Chánh, TP. Thủ Đức, TP.HCM','02837224466','banqlda@thuduc.gov.vn',false,NULL,'2026-01-02 08:20:00+07',NULL),
('b0000000-0000-0000-0000-000000000004','0108553219','Ban Quản lý Dự án Giao thông Đô thị Hà Thành','Ban QLDA Hà Thành','Ha Thanh Urban Transport Project Management Unit','ad5b98c7-b28f-4c40-861a-5a363b84eb00','215 Trần Duy Hưng, P. Trung Hòa, Q. Cầu Giấy, Hà Nội','02436330077','qlda@hathanh-transport.vn',false,NULL,'2026-01-02 08:30:00+07',NULL),
('b0000000-0000-0000-0000-000000000005','0311674205','Công ty Cổ phần Tư vấn Thiết kế Xây dựng Phương Nam','TVTK Phương Nam','Phuong Nam Construction Design Consultant JSC','d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5','145 Cách Mạng Tháng Tám, P.5, Quận 3, TP.HCM','02839305511','thietke@phuongnam-tk.vn',false,NULL,'2026-01-03 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000006','0315228740','Công ty TNHH Giải pháp BIM Tiên Phong','BIM Tiên Phong','Tien Phong BIM Solutions Co., Ltd','d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5','Lầu 6, Tòa nhà Vietbuild, 31 Đinh Bộ Lĩnh, Bình Thạnh, TP.HCM','02835127799','contact@bimtienphong.vn',false,NULL,'2026-01-03 08:10:00+07',NULL),
('b0000000-0000-0000-0000-000000000007','0100238914','Tổng Công ty Cổ phần Xây dựng Hà Thành','XD Hà Thành','Ha Thanh Construction Corporation','ae2fd257-cca8-4bb4-8f90-c0c45100702b','Số 62 Nguyễn Chí Thanh, P. Láng Thượng, Q. Đống Đa, Hà Nội','02437761234','vanphong@xdhathanh.vn',false,NULL,'2026-01-03 08:20:00+07',NULL),
('b0000000-0000-0000-0000-000000000008','0314087563','Công ty Cổ phần Xây lắp Trường Thịnh','Xây lắp Trường Thịnh','Truong Thinh Construction & Installation JSC','ae2fd257-cca8-4bb4-8f90-c0c45100702b','88 Quốc lộ 13, P. Hiệp Bình Phước, TP. Thủ Đức, TP.HCM','02837269900','info@truongthinh-xl.vn',false,NULL,'2026-01-03 08:30:00+07',NULL),
('b0000000-0000-0000-0000-000000000009','0309771468','Công ty TNHH Cơ điện Việt Long','Cơ điện Việt Long','Viet Long M&E Engineering Co., Ltd','8c0dcb7d-87fe-413e-b8d6-83eb91171cbe','Lô C12, KCN Tân Bình, Q. Tân Phú, TP.HCM','02838165544','mep@vietlong-me.vn',false,NULL,'2026-01-04 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000010','0316552087','Công ty Cổ phần Nội thất và Hoàn thiện An Gia','Nội thất An Gia','An Gia Interior & Finishing JSC','8c0dcb7d-87fe-413e-b8d6-83eb91171cbe','204 Phan Văn Trị, P.11, Q. Bình Thạnh, TP.HCM','02835889977','angia@noithatangia.vn',false,NULL,'2026-01-04 08:10:00+07',NULL),
('b0000000-0000-0000-0000-000000000011','0105338712','Công ty Cổ phần Tư vấn Giám sát Chất lượng Bắc Hà','TVGS Bắc Hà','Bac Ha Quality Supervision Consultant JSC','ad4c917e-b170-4ff8-bca3-10764641c8d9','Số 19 Trần Thái Tông, P. Dịch Vọng Hậu, Q. Cầu Giấy, Hà Nội','02432115566','giamsat@bacha-tvgs.vn',false,NULL,'2026-01-04 08:20:00+07',NULL),
('b0000000-0000-0000-0000-000000000012','0310884236','Công ty TNHH Vật liệu Xây dựng Nam Tiến','VLXD Nam Tiến','Nam Tien Building Materials Co., Ltd','3fe93ed9-2e6a-47a6-90cf-6e5aac24c645','Lô 24, KCN Tân Tạo, Q. Bình Tân, TP.HCM','02837507788','banhang@namtien-vlxd.vn',false,NULL,'2026-01-04 08:30:00+07',NULL),
('b0000000-0000-0000-0000-000000000013','0107912350','Công ty Cổ phần Thiết bị Cơ điện Đại Phát','Cơ điện Đại Phát','Dai Phat M&E Equipment JSC','3fe93ed9-2e6a-47a6-90cf-6e5aac24c645','Km 12 Đại lộ Thăng Long, P. Đại Mỗ, Q. Nam Từ Liêm, Hà Nội','02433559911','sales@daiphat-me.vn',false,NULL,'2026-01-05 08:00:00+07',NULL),
('b0000000-0000-0000-0000-000000000014','0317204558','Công ty TNHH Quản lý Vận hành Tòa nhà An Bình','Vận hành An Bình','An Binh Building Management Co., Ltd','e48c6618-c877-46bf-9d6d-7d9fb92a50e9','Tầng 3, Tòa nhà Bitexco, 2 Hải Triều, P. Bến Nghé, Quận 1, TP.HCM','02839142200','vanhanh@anbinh-bm.vn',false,NULL,'2026-01-05 08:10:00+07',NULL),
('b0000000-0000-0000-0000-000000000015','0100238914001','Liên danh Xây dựng Hà Thành – Trường Thịnh','Liên danh Hà Thành – Trường Thịnh','Ha Thanh – Truong Thinh Joint Venture','ae2fd257-cca8-4bb4-8f90-c0c45100702b','Số 62 Nguyễn Chí Thanh, P. Láng Thượng, Q. Đống Đa, Hà Nội','02437761234','liendanh@xdhathanh.vn',true,'b0000000-0000-0000-0000-000000000007','2026-01-08 08:00:00+07',NULL);

-- ============================================================================
-- 2) JOINT VENTURE MEMBERS — thanh vien cua lien danh b0..15
--    (UUID phai la HEX — dung tien to b1*, khong dung "bj*")
-- ============================================================================
INSERT INTO "JointVentureMembers" ("Id","JointVentureId","MemberOrganizationId") VALUES
('b1000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000015','b0000000-0000-0000-0000-000000000007'),
('b1000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000015','b0000000-0000-0000-0000-000000000008');

-- ============================================================================
-- 3) ACCOUNTS — 28 tai khoan, mat khau CHUNG = "password"
--    Role: Admin=0, User=1 | Status: Active=0, Inactive=1, Suspended=2
--    OrganizationId = CONG TY CHU QUAN (quan he 1-1). Lam viec voi nhieu doi tac
--    thi di qua Groups."OrganizationId", KHONG phai qua bang nhieu-nhieu.
--    a0..25 phong.viewer: Inactive + CHUA xac thuc email (con OTP cho nhap)
--    => du de test luong verify-otp / resend-otp / dang nhap bi khoa.
-- ============================================================================
INSERT INTO "Accounts" ("Id","UserName","Email","PasswordHash","Role","Status","OrganizationId","ResetPasswordToken","ResetPasswordTokenExpiresAt","IsEmailVerified","EmailOtp","EmailOtpExpiresAt","IsOnboardingEmailPending","AvatarStoragePath","CreatedAt","UpdatedAt") VALUES
('a0000000-0000-0000-0000-000000000001','Nguyễn Văn Sơn','admin@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',0,0,'b0000000-0000-0000-0000-000000000003',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-02 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000002','Trần Thị Hoa','hoa.pm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000003',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-02 08:30:00+07',NULL),
('a0000000-0000-0000-0000-000000000003','Ngô Thị Dương','duong.client@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000001',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-03 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000004','Lý Thanh Tùng','tung.client@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000001',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-03 08:10:00+07',NULL),
('a0000000-0000-0000-0000-000000000005','Hoàng Minh Đức','duc.client@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000002',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-03 08:20:00+07',NULL),
('a0000000-0000-0000-0000-000000000006','Phan Thị Mai','mai.pmu@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000003',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-03 08:30:00+07',NULL),
('a0000000-0000-0000-0000-000000000007','Trịnh Văn Khoa','khoa.pmu@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000004',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-03 08:40:00+07',NULL),
('a0000000-0000-0000-0000-000000000008','Lê Hoàng Nam','nam.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000005',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-04 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000009','Phạm Thị Lan','lan.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000005','reset-token-lan-demo-0001','2026-08-20 09:00:00+07',true,NULL,NULL,false,NULL,'2026-01-04 08:10:00+07',NULL),
('a0000000-0000-0000-0000-000000000010','Vũ Đình Hải','hai.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000005',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-04 08:20:00+07',NULL),
('a0000000-0000-0000-0000-000000000011','Đinh Thị Thu','thu.design@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000005',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-04 08:30:00+07',NULL),
('a0000000-0000-0000-0000-000000000012','Nguyễn Tuấn Kiệt','kiet.bim@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000006',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-04 08:40:00+07',NULL),
('a0000000-0000-0000-0000-000000000013','Bùi Thanh Trúc','truc.bim@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000006',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-04 08:50:00+07',NULL),
('a0000000-0000-0000-0000-000000000014','Vũ Văn Bình','binh.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000007',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-05 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000015','Trần Quốc Toản','toan.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000007',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-05 08:10:00+07',NULL),
('a0000000-0000-0000-0000-000000000016','Nguyễn Thị Quỳnh','quynh.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000007',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-05 08:20:00+07',NULL),
('a0000000-0000-0000-0000-000000000017','Đặng Hữu Phước','phuoc.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000008',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-05 08:30:00+07',NULL),
('a0000000-0000-0000-0000-000000000018','Hồ Văn Lộc','loc.contractor@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000008',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-05 08:40:00+07',NULL),
('a0000000-0000-0000-0000-000000000019','Mai Xuân Trường','truong.mep@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000009',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000020','Lâm Chí Kiên','kien.mep@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000009',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:10:00+07',NULL),
('a0000000-0000-0000-0000-000000000021','Tạ Thị Hồng','hong.finish@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000010',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:20:00+07',NULL),
('a0000000-0000-0000-0000-000000000022','Đỗ Mạnh Cường','cuong.super@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000011',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:30:00+07',NULL),
('a0000000-0000-0000-0000-000000000023','Bùi Văn Em','em.verify@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000011',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:40:00+07',NULL),
('a0000000-0000-0000-0000-000000000024','Nguyễn Hải Yến','yen.super@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000011',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-06 08:50:00+07',NULL),
('a0000000-0000-0000-0000-000000000025','Đặng Quốc Phong','phong.viewer@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,1,'b0000000-0000-0000-0000-000000000012',NULL,NULL,false,'482913','2026-09-12 09:00:00+07',true,NULL,'2026-01-07 08:00:00+07',NULL),
('a0000000-0000-0000-0000-000000000026','Chu Thị Ngọc','ngoc.supply@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000012',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-07 08:10:00+07',NULL),
('a0000000-0000-0000-0000-000000000027','Vương Đình Sang','sang.supply@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000013',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-07 08:20:00+07',NULL),
('a0000000-0000-0000-0000-000000000028','Nguyễn Thị Bích','bich.fm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,'b0000000-0000-0000-0000-000000000014',NULL,NULL,true,NULL,NULL,false,NULL,'2026-01-07 08:30:00+07',NULL);

-- ============================================================================
-- 4) GROUPS — moi du an 5 nhom mac dinh (dung luong tao du an cua he thong).
--    Quy uoc Id: c0000000-...-0000000P0GG  (P = so du an, GG = so nhom)
--    Group."OrganizationId" = don vi DAM NHAN vai tro do trong du an.
-- ============================================================================
INSERT INTO "Groups" ("Id","Name","Description","OrganizationId","CreatedAt","UpdatedAt") VALUES
-- Du an 1 — Riverside Tower
('c0000000-0000-0000-0000-000000000101','Chủ đầu tư','Đại diện chủ đầu tư Minh Khang Invest','b0000000-0000-0000-0000-000000000001','2026-01-15 08:00:00+07',NULL),
('c0000000-0000-0000-0000-000000000102','Ban quản lý dự án','Ban QLDA Thủ Đức điều phối chung','b0000000-0000-0000-0000-000000000003','2026-01-15 08:05:00+07',NULL),
('c0000000-0000-0000-0000-000000000103','Tư vấn thiết kế','Thiết kế kiến trúc – kết cấu – MEP','b0000000-0000-0000-0000-000000000005','2026-01-15 08:10:00+07',NULL),
('c0000000-0000-0000-0000-000000000104','Nhà thầu thi công','Nhà thầu chính phần thân','b0000000-0000-0000-0000-000000000007','2026-01-15 08:15:00+07',NULL),
('c0000000-0000-0000-0000-000000000105','Tư vấn giám sát','Giám sát chất lượng hiện trường','b0000000-0000-0000-0000-000000000011','2026-01-15 08:20:00+07',NULL),
-- Du an 2 — Cau vuot Cat Lai
('c0000000-0000-0000-0000-000000000201','Chủ đầu tư','Đại diện Hạ tầng Đông Dương','b0000000-0000-0000-0000-000000000002','2026-02-02 08:00:00+07',NULL),
('c0000000-0000-0000-0000-000000000202','Ban quản lý dự án','Ban QLDA Thủ Đức','b0000000-0000-0000-0000-000000000003','2026-02-02 08:05:00+07',NULL),
('c0000000-0000-0000-0000-000000000203','Tư vấn thiết kế','Thiết kế cầu và đường dẫn','b0000000-0000-0000-0000-000000000005','2026-02-02 08:10:00+07',NULL),
('c0000000-0000-0000-0000-000000000204','Nhà thầu thi công','Thi công kết cấu nhịp thép','b0000000-0000-0000-0000-000000000008','2026-02-02 08:15:00+07',NULL),
('c0000000-0000-0000-0000-000000000205','Tư vấn giám sát','Giám sát thi công cầu','b0000000-0000-0000-0000-000000000011','2026-02-02 08:20:00+07',NULL),
-- Du an 3 — Nha may XLNT Binh Hung
('c0000000-0000-0000-0000-000000000301','Chủ đầu tư','Đại diện Hạ tầng Đông Dương','b0000000-0000-0000-0000-000000000002','2026-03-04 08:00:00+07',NULL),
('c0000000-0000-0000-0000-000000000302','Ban quản lý dự án','Ban QLDA Thủ Đức','b0000000-0000-0000-0000-000000000003','2026-03-04 08:05:00+07',NULL),
('c0000000-0000-0000-0000-000000000303','Tư vấn thiết kế','Mô hình BIM công nghệ xử lý','b0000000-0000-0000-0000-000000000006','2026-03-04 08:10:00+07',NULL),
('c0000000-0000-0000-0000-000000000304','Nhà thầu thi công','Liên danh thi công hạng mục chính','b0000000-0000-0000-0000-000000000015','2026-03-04 08:15:00+07',NULL),
('c0000000-0000-0000-0000-000000000305','Nhà thầu cơ điện','Lắp đặt thiết bị công nghệ và cơ điện','b0000000-0000-0000-0000-000000000009','2026-03-04 08:20:00+07',NULL),
-- Du an 4 — Sai Gon Center (DA HOAN THANH)
('c0000000-0000-0000-0000-000000000401','Chủ đầu tư','Đại diện Minh Khang Invest','b0000000-0000-0000-0000-000000000001','2025-06-10 08:00:00+07',NULL),
('c0000000-0000-0000-0000-000000000402','Ban quản lý dự án','Ban QLDA Thủ Đức','b0000000-0000-0000-0000-000000000003','2025-06-10 08:05:00+07',NULL),
('c0000000-0000-0000-0000-000000000403','Tư vấn thiết kế','Thiết kế TTTM và văn phòng','b0000000-0000-0000-0000-000000000005','2025-06-10 08:10:00+07',NULL),
('c0000000-0000-0000-0000-000000000404','Nhà thầu thi công','Tổng thầu xây lắp','b0000000-0000-0000-0000-000000000007','2025-06-10 08:15:00+07',NULL),
('c0000000-0000-0000-0000-000000000405','Đơn vị vận hành','Tiếp nhận hồ sơ hoàn công, vận hành tòa nhà','b0000000-0000-0000-0000-000000000014','2025-06-10 08:20:00+07',NULL),
-- Du an 5 — Benh vien Hoa An
('c0000000-0000-0000-0000-000000000501','Chủ đầu tư','Đại diện Minh Khang Invest','b0000000-0000-0000-0000-000000000001','2026-05-06 08:00:00+07',NULL),
('c0000000-0000-0000-0000-000000000502','Ban quản lý dự án','Ban QLDA Hà Thành','b0000000-0000-0000-0000-000000000004','2026-05-06 08:05:00+07',NULL),
('c0000000-0000-0000-0000-000000000503','Tư vấn thiết kế','Thiết kế khối khám và khối nội trú','b0000000-0000-0000-0000-000000000005','2026-05-06 08:10:00+07',NULL),
('c0000000-0000-0000-0000-000000000504','Nhà thầu thi công','Thi công phần ngầm và phần thân','b0000000-0000-0000-0000-000000000008','2026-05-06 08:15:00+07',NULL),
('c0000000-0000-0000-0000-000000000505','Nhà thầu cơ điện','Cơ điện và khí y tế','b0000000-0000-0000-0000-000000000009','2026-05-06 08:20:00+07',NULL);

-- ============================================================================
-- 5) GROUP MEMBERS  Role: Member=0, Leader=1 | Status: Active=0, Left=1
--    Moi nhom co dung 1 Leader (nguoi duyet ho so cua nhom do).
--    c1..9901 = ban ghi Status=Left (test loc thanh vien da roi nhom).
-- ============================================================================
INSERT INTO "GroupMembers" ("Id","GroupId","AccountId","Role","Status","JoinedAt") VALUES
-- Du an 1
('c1000000-0000-0000-0000-000000010101','c0000000-0000-0000-0000-000000000101','a0000000-0000-0000-0000-000000000003',1,0,'2026-01-16 08:00:00+07'),
('c1000000-0000-0000-0000-000000010102','c0000000-0000-0000-0000-000000000101','a0000000-0000-0000-0000-000000000004',0,0,'2026-01-16 08:10:00+07'),
('c1000000-0000-0000-0000-000000010201','c0000000-0000-0000-0000-000000000102','a0000000-0000-0000-0000-000000000002',1,0,'2026-01-16 08:20:00+07'),
('c1000000-0000-0000-0000-000000010202','c0000000-0000-0000-0000-000000000102','a0000000-0000-0000-0000-000000000006',0,0,'2026-01-16 08:30:00+07'),
('c1000000-0000-0000-0000-000000010301','c0000000-0000-0000-0000-000000000103','a0000000-0000-0000-0000-000000000008',1,0,'2026-01-16 08:40:00+07'),
('c1000000-0000-0000-0000-000000010302','c0000000-0000-0000-0000-000000000103','a0000000-0000-0000-0000-000000000009',0,0,'2026-01-16 08:50:00+07'),
('c1000000-0000-0000-0000-000000010303','c0000000-0000-0000-0000-000000000103','a0000000-0000-0000-0000-000000000010',0,0,'2026-01-16 09:00:00+07'),
('c1000000-0000-0000-0000-000000010401','c0000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000014',1,0,'2026-01-16 09:10:00+07'),
('c1000000-0000-0000-0000-000000010402','c0000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000015',0,0,'2026-01-16 09:20:00+07'),
('c1000000-0000-0000-0000-000000010403','c0000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000016',0,0,'2026-01-16 09:30:00+07'),
('c1000000-0000-0000-0000-000000010501','c0000000-0000-0000-0000-000000000105','a0000000-0000-0000-0000-000000000022',1,0,'2026-01-16 09:40:00+07'),
('c1000000-0000-0000-0000-000000010502','c0000000-0000-0000-0000-000000000105','a0000000-0000-0000-0000-000000000023',0,0,'2026-01-16 09:50:00+07'),
-- Du an 2
('c1000000-0000-0000-0000-000000020101','c0000000-0000-0000-0000-000000000201','a0000000-0000-0000-0000-000000000005',1,0,'2026-02-03 08:00:00+07'),
('c1000000-0000-0000-0000-000000020201','c0000000-0000-0000-0000-000000000202','a0000000-0000-0000-0000-000000000002',1,0,'2026-02-03 08:10:00+07'),
('c1000000-0000-0000-0000-000000020202','c0000000-0000-0000-0000-000000000202','a0000000-0000-0000-0000-000000000007',0,0,'2026-02-03 08:20:00+07'),
('c1000000-0000-0000-0000-000000020301','c0000000-0000-0000-0000-000000000203','a0000000-0000-0000-0000-000000000010',1,0,'2026-02-03 08:30:00+07'),
('c1000000-0000-0000-0000-000000020302','c0000000-0000-0000-0000-000000000203','a0000000-0000-0000-0000-000000000011',0,0,'2026-02-03 08:40:00+07'),
('c1000000-0000-0000-0000-000000020401','c0000000-0000-0000-0000-000000000204','a0000000-0000-0000-0000-000000000017',1,0,'2026-02-03 08:50:00+07'),
('c1000000-0000-0000-0000-000000020402','c0000000-0000-0000-0000-000000000204','a0000000-0000-0000-0000-000000000018',0,0,'2026-02-03 09:00:00+07'),
('c1000000-0000-0000-0000-000000020501','c0000000-0000-0000-0000-000000000205','a0000000-0000-0000-0000-000000000024',1,0,'2026-02-03 09:10:00+07'),
-- Du an 3
('c1000000-0000-0000-0000-000000030101','c0000000-0000-0000-0000-000000000301','a0000000-0000-0000-0000-000000000005',1,0,'2026-03-05 08:00:00+07'),
('c1000000-0000-0000-0000-000000030201','c0000000-0000-0000-0000-000000000302','a0000000-0000-0000-0000-000000000002',1,0,'2026-03-05 08:10:00+07'),
('c1000000-0000-0000-0000-000000030301','c0000000-0000-0000-0000-000000000303','a0000000-0000-0000-0000-000000000012',1,0,'2026-03-05 08:20:00+07'),
('c1000000-0000-0000-0000-000000030302','c0000000-0000-0000-0000-000000000303','a0000000-0000-0000-0000-000000000013',0,0,'2026-03-05 08:30:00+07'),
('c1000000-0000-0000-0000-000000030401','c0000000-0000-0000-0000-000000000304','a0000000-0000-0000-0000-000000000014',1,0,'2026-03-05 08:40:00+07'),
('c1000000-0000-0000-0000-000000030402','c0000000-0000-0000-0000-000000000304','a0000000-0000-0000-0000-000000000017',0,0,'2026-03-05 08:50:00+07'),
('c1000000-0000-0000-0000-000000030501','c0000000-0000-0000-0000-000000000305','a0000000-0000-0000-0000-000000000019',1,0,'2026-03-05 09:00:00+07'),
('c1000000-0000-0000-0000-000000030502','c0000000-0000-0000-0000-000000000305','a0000000-0000-0000-0000-000000000020',0,0,'2026-03-05 09:10:00+07'),
-- Du an 4 (da hoan thanh)
('c1000000-0000-0000-0000-000000040101','c0000000-0000-0000-0000-000000000401','a0000000-0000-0000-0000-000000000003',1,0,'2025-06-11 08:00:00+07'),
('c1000000-0000-0000-0000-000000040201','c0000000-0000-0000-0000-000000000402','a0000000-0000-0000-0000-000000000002',1,0,'2025-06-11 08:10:00+07'),
('c1000000-0000-0000-0000-000000040301','c0000000-0000-0000-0000-000000000403','a0000000-0000-0000-0000-000000000008',1,0,'2025-06-11 08:20:00+07'),
('c1000000-0000-0000-0000-000000040401','c0000000-0000-0000-0000-000000000404','a0000000-0000-0000-0000-000000000014',1,0,'2025-06-11 08:30:00+07'),
('c1000000-0000-0000-0000-000000040501','c0000000-0000-0000-0000-000000000405','a0000000-0000-0000-0000-000000000028',1,0,'2025-06-11 08:40:00+07'),
-- Du an 5
('c1000000-0000-0000-0000-000000050101','c0000000-0000-0000-0000-000000000501','a0000000-0000-0000-0000-000000000004',1,0,'2026-05-07 08:00:00+07'),
('c1000000-0000-0000-0000-000000050201','c0000000-0000-0000-0000-000000000502','a0000000-0000-0000-0000-000000000007',1,0,'2026-05-07 08:10:00+07'),
('c1000000-0000-0000-0000-000000050202','c0000000-0000-0000-0000-000000000502','a0000000-0000-0000-0000-000000000002',0,0,'2026-05-07 08:20:00+07'),
('c1000000-0000-0000-0000-000000050301','c0000000-0000-0000-0000-000000000503','a0000000-0000-0000-0000-000000000008',1,0,'2026-05-07 08:30:00+07'),
('c1000000-0000-0000-0000-000000050302','c0000000-0000-0000-0000-000000000503','a0000000-0000-0000-0000-000000000011',0,0,'2026-05-07 08:40:00+07'),
('c1000000-0000-0000-0000-000000050401','c0000000-0000-0000-0000-000000000504','a0000000-0000-0000-0000-000000000017',1,0,'2026-05-07 08:50:00+07'),
('c1000000-0000-0000-0000-000000050402','c0000000-0000-0000-0000-000000000504','a0000000-0000-0000-0000-000000000021',0,0,'2026-05-07 09:00:00+07'),
('c1000000-0000-0000-0000-000000050501','c0000000-0000-0000-0000-000000000505','a0000000-0000-0000-0000-000000000019',1,0,'2026-05-07 09:10:00+07'),
-- Thanh vien DA ROI NHOM (Status=Left) — test loc GroupMemberStatus.Active
('c1000000-0000-0000-0000-000000009901','c0000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000018',0,1,'2026-01-16 09:35:00+07');

-- ============================================================================
-- 6) PROJECTS  Status: Active=0, Completed=1
--    OwnerOrganizationId = CHU DAU TU. ContactAddress tach khoi dia chi cong
--    trinh (nam o "ProjectLocations"). LoiRuleSetId = NULL => dung bo luat
--    LOI MAC DINH cua he thong (khong phu thuoc seed_loi_rules.sql).
-- ============================================================================
INSERT INTO "Projects" ("Id","ProjectName","ProjectCode","ProjectDescription","Status","ManagerAccountId","OwnerOrganizationId","ContactAddress","ProjectImageUrl","ProjectImageStoragePath","LoiRuleSetId","CreatedAt","UpdatedAt") VALUES
('d0000000-0000-0000-0000-000000000001','Khu phức hợp căn hộ Riverside Tower','RIV','Tổ hợp 3 tháp căn hộ cao cấp 35 tầng và 2 tầng hầm ven sông Sài Gòn, tổng diện tích sàn 186.000 m2, quy mô 1.240 căn hộ kèm khối đế thương mại 3 tầng.',0,'a0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000001','Tầng 18, Tòa nhà Sunrise, 27 Nguyễn Hữu Cảnh, P.22, Bình Thạnh, TP.HCM',NULL,NULL,NULL,'2026-01-15 08:00:00+07','2026-06-20 14:20:00+07'),
('d0000000-0000-0000-0000-000000000002','Cầu vượt nút giao Cát Lái','CAT','Cầu vượt kết cấu thép 4 làn xe dài 486 m vượt nút giao Cát Lái, giải quyết ùn tắc trục Mai Chí Thọ – Đồng Văn Cống, TP. Thủ Đức.',0,'a0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000002','Số 8 Phạm Hùng, P. Mỹ Đình 2, Q. Nam Từ Liêm, Hà Nội',NULL,NULL,NULL,'2026-02-02 08:00:00+07','2026-07-11 10:05:00+07'),
('d0000000-0000-0000-0000-000000000003','Nhà máy xử lý nước thải Bình Hưng giai đoạn 2','BHU','Nâng công suất xử lý từ 141.000 lên 469.000 m3/ngày đêm, bổ sung 4 bể sinh học, nhà điều hành trung tâm và hệ thống SCADA.',0,'a0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000002','Số 8 Phạm Hùng, P. Mỹ Đình 2, Q. Nam Từ Liêm, Hà Nội',NULL,NULL,NULL,'2026-03-04 08:00:00+07',NULL),
('d0000000-0000-0000-0000-000000000004','Trung tâm thương mại Sài Gòn Center','SGC','Trung tâm thương mại 5 tầng kết hợp 12 tầng văn phòng cho thuê tại khu lõi Quận 1. Dự án đã nghiệm thu, bàn giao đơn vị vận hành.',1,'a0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000001','Tầng 18, Tòa nhà Sunrise, 27 Nguyễn Hữu Cảnh, P.22, Bình Thạnh, TP.HCM',NULL,NULL,NULL,'2025-06-10 08:00:00+07','2026-04-28 16:40:00+07'),
('d0000000-0000-0000-0000-000000000005','Bệnh viện Đa khoa Hòa An','HOA','Bệnh viện đa khoa 500 giường gồm khối khám ngoại trú 5 tầng và khối nội trú 9 tầng, có khu xạ trị và hệ thống khí y tế trung tâm.',0,'a0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000001','Tầng 18, Tòa nhà Sunrise, 27 Nguyễn Hữu Cảnh, P.22, Bình Thạnh, TP.HCM',NULL,NULL,NULL,'2026-05-06 08:00:00+07',NULL);

-- ============================================================================
-- 7) PROJECT LOCATIONS — dia chi CONG TRINH (toa do that de ban do hien dung)
-- ============================================================================
INSERT INTO "ProjectLocations" ("Id","ProjectId","Address","Latitude","Longitude","IsDefault","CreatedAt") VALUES
('d1000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','208 Nguyễn Hữu Cảnh, P.22, Q. Bình Thạnh, TP.HCM',10.7935,106.7215,true,'2026-01-15 08:00:00+07'),
('d1000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000002','Nút giao Cát Lái, P. Thạnh Mỹ Lợi, TP. Thủ Đức, TP.HCM',10.7784,106.7602,true,'2026-02-02 08:00:00+07'),
('d1000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000003','Khu xử lý nước thải Bình Hưng, xã Bình Hưng, H. Bình Chánh, TP.HCM',10.6912,106.6338,true,'2026-03-04 08:00:00+07'),
('d1000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000004','92-94 Lê Lợi, P. Bến Thành, Quận 1, TP.HCM',10.7726,106.6989,true,'2025-06-10 08:00:00+07'),
('d1000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000005','Lô CC-05, Khu đô thị Tây Mỗ, P. Tây Mỗ, Q. Nam Từ Liêm, Hà Nội',21.0132,105.7449,true,'2026-05-06 08:00:00+07');

-- ============================================================================
-- 8) PROJECT PARTICIPANTS  Role: ProjectAdmin=0, Member=1 | Status: Active=0, Inactive=1
--    Nhom "Ban quan ly du an" giu vai tro ProjectAdmin o moi du an.
--    d3..9901 = participant Inactive (test loc ProjectParticipantStatus.Active).
-- ============================================================================
INSERT INTO "ProjectParticipants" ("Id","ProjectId","GroupId","Role","Status","JoinedAt") VALUES
('d3000000-0000-0000-0000-000000000101','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000101',1,0,'2026-01-17 08:00:00+07'),
('d3000000-0000-0000-0000-000000000102','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000102',0,0,'2026-01-17 08:05:00+07'),
('d3000000-0000-0000-0000-000000000103','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000103',1,0,'2026-01-17 08:10:00+07'),
('d3000000-0000-0000-0000-000000000104','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000104',1,0,'2026-01-17 08:15:00+07'),
('d3000000-0000-0000-0000-000000000105','d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000105',1,0,'2026-01-17 08:20:00+07'),
('d3000000-0000-0000-0000-000000000201','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000201',1,0,'2026-02-04 08:00:00+07'),
('d3000000-0000-0000-0000-000000000202','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000202',0,0,'2026-02-04 08:05:00+07'),
('d3000000-0000-0000-0000-000000000203','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000203',1,0,'2026-02-04 08:10:00+07'),
('d3000000-0000-0000-0000-000000000204','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000204',1,0,'2026-02-04 08:15:00+07'),
('d3000000-0000-0000-0000-000000000205','d0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000205',1,0,'2026-02-04 08:20:00+07'),
('d3000000-0000-0000-0000-000000000301','d0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000301',1,0,'2026-03-06 08:00:00+07'),
('d3000000-0000-0000-0000-000000000302','d0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000302',0,0,'2026-03-06 08:05:00+07'),
('d3000000-0000-0000-0000-000000000303','d0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000303',1,0,'2026-03-06 08:10:00+07'),
('d3000000-0000-0000-0000-000000000304','d0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000304',1,0,'2026-03-06 08:15:00+07'),
('d3000000-0000-0000-0000-000000000305','d0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000305',1,0,'2026-03-06 08:20:00+07'),
('d3000000-0000-0000-0000-000000000401','d0000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000401',1,0,'2025-06-12 08:00:00+07'),
('d3000000-0000-0000-0000-000000000402','d0000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000402',0,0,'2025-06-12 08:05:00+07'),
('d3000000-0000-0000-0000-000000000403','d0000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000403',1,0,'2025-06-12 08:10:00+07'),
('d3000000-0000-0000-0000-000000000404','d0000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000404',1,0,'2025-06-12 08:15:00+07'),
('d3000000-0000-0000-0000-000000000405','d0000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000405',1,0,'2025-06-12 08:20:00+07'),
('d3000000-0000-0000-0000-000000000501','d0000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000501',1,0,'2026-05-08 08:00:00+07'),
('d3000000-0000-0000-0000-000000000502','d0000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000502',0,0,'2026-05-08 08:05:00+07'),
('d3000000-0000-0000-0000-000000000503','d0000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000503',1,0,'2026-05-08 08:10:00+07'),
('d3000000-0000-0000-0000-000000000504','d0000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000504',1,0,'2026-05-08 08:15:00+07'),
('d3000000-0000-0000-0000-000000000505','d0000000-0000-0000-0000-000000000005','c0000000-0000-0000-0000-000000000505',1,0,'2026-05-08 08:20:00+07');

-- ============================================================================
-- 9) PROJECT INVITATIONS  Status: Pending=0, Accepted=1, Rejected=2, Expired=3
--    Role(GroupMemberRole): Member=0, Leader=1
--    Phu du 4 trang thai de demo chuong thong bao + trang loi moi.
-- ============================================================================
INSERT INTO "ProjectInvitations" ("Id","ProjectId","InvitedAccountId","InvitedByAccountId","InvitedGroupId","Role","Status","Token","Note","CreatedAt","ExpiresAt","RespondedAt") VALUES
('d4000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000026','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000104',0,0,'inv-riv-supply-pending-0001','Mời tham gia nhóm Nhà thầu thi công dự án Riverside Tower để phối hợp cung ứng vật tư hoàn thiện.','2026-08-01 09:00:00+07','2026-09-01 09:00:00+07',NULL),
('d4000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000023','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000105',0,1,'inv-riv-tvgs-accepted-0002','Mời tham gia tổ giám sát chất lượng phần thân.','2026-01-18 09:00:00+07','2026-02-18 09:00:00+07','2026-01-19 10:15:00+07'),
('d4000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000013','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000503',0,0,'inv-hoa-bim-pending-0003','Mời tham gia dựng mô hình BIM khối nội trú Bệnh viện Hòa An.','2026-08-05 09:00:00+07','2026-09-05 09:00:00+07',NULL),
('d4000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000027','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000204',1,2,'inv-cat-leader-rejected-0004','Mời làm Trưởng nhóm Nhà thầu thi công dự án Cầu Cát Lái.','2026-06-10 09:00:00+07','2026-07-10 09:00:00+07','2026-06-12 08:30:00+07'),
('d4000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000021','a0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000305',0,3,'inv-bhu-expired-0005','Lời mời đã quá hạn phản hồi.','2026-04-01 09:00:00+07','2026-05-01 09:00:00+07',NULL);

-- ============================================================================
-- 10) CONTRACT PACKAGES
--     Status: Draft=0, Pending=1, Active=2, Completed=3, Suspended=4, Reviewing=5
--     (LUU Y: seed cu chu thich SAI la Active=1 — enum that Active=2.)
--     WorkTypes luu MA loai cong viec, ma dau tien dung sinh ma goi thau.
--     Moi du an co 1 goi MAC DINH (IsDefault=true) chua ho so chung.
-- ============================================================================
INSERT INTO "ContractPackages" ("Id","ProjectId","Code","Name","Description","ScopeDescription","Status","IsDefault","ContractValue","Currency","TaxRate","WorkTypes","Notes","StartDate","EndDate","CreatedAt","UpdatedAt") VALUES
('e0000000-0000-0000-0000-000000000101','d0000000-0000-0000-0000-000000000001','RIV-2026-GEN-001','Gói thầu mặc định','Hồ sơ chung toàn dự án, không thuộc gói thầu cụ thể.',NULL,2,true,NULL,'VND',NULL,'GEN',NULL,NULL,NULL,'2026-01-17 08:00:00+07',NULL),
('e0000000-0000-0000-0000-000000000102','d0000000-0000-0000-0000-000000000001','RIV-2026-STR-002','Thi công kết cấu phần thân 3 tháp','Thi công bê tông cốt thép phần thân từ tầng 1 đến tầng mái 3 tháp A, B, C.','Bao gồm cốp pha, cốt thép, bê tông thương phẩm, sàn ứng lực trước và kết cấu thép mái.',2,false,185400000000,'VND',8.00,'STR','Tiến độ bám mốc cất nóc tháp A quý IV/2026.','2026-02-15 00:00:00+07','2027-08-30 00:00:00+07','2026-01-17 08:10:00+07','2026-06-20 14:20:00+07'),
('e0000000-0000-0000-0000-000000000103','d0000000-0000-0000-0000-000000000001','RIV-2026-MEP-003','Cung cấp và lắp đặt hệ thống cơ điện','Hệ thống điện, cấp thoát nước, điều hòa thông gió và PCCC toàn khối căn hộ.','Bao gồm thiết kế bản vẽ thi công, cung cấp thiết bị, lắp đặt, thử nghiệm và nghiệm thu.',5,false,72850000000,'VND',8.00,'MEP','Đang soát xét hồ sơ dự thầu, dự kiến trao thầu tháng 10/2026.',NULL,NULL,'2026-01-17 08:20:00+07',NULL),
('e0000000-0000-0000-0000-000000000201','d0000000-0000-0000-0000-000000000002','CAT-2026-GEN-001','Gói thầu mặc định','Hồ sơ chung dự án Cầu vượt Cát Lái.',NULL,2,true,NULL,'VND',NULL,'GEN',NULL,NULL,NULL,'2026-02-04 08:00:00+07',NULL),
('e0000000-0000-0000-0000-000000000202','d0000000-0000-0000-0000-000000000002','CAT-2026-STR-002','Thi công kết cấu nhịp thép và mố trụ','Chế tạo, vận chuyển và lắp dựng 8 nhịp dầm thép cùng hệ mố trụ bê tông.','Kèm công tác thử tải, sơn bảo vệ và hoàn thiện mặt cầu.',2,false,247600000000,'VND',8.00,'STR','Lắp dựng nhịp ban đêm để hạn chế chặn đường.','2026-03-01 00:00:00+07','2027-04-30 00:00:00+07','2026-02-04 08:10:00+07',NULL),
('e0000000-0000-0000-0000-000000000301','d0000000-0000-0000-0000-000000000003','BHU-2026-GEN-001','Gói thầu mặc định','Hồ sơ chung dự án Nhà máy XLNT Bình Hưng.',NULL,2,true,NULL,'VND',NULL,'GEN',NULL,NULL,NULL,'2026-03-06 08:00:00+07',NULL),
('e0000000-0000-0000-0000-000000000302','d0000000-0000-0000-0000-000000000003','BHU-2026-STR-002','Xây dựng bể sinh học và nhà điều hành','Thi công 4 bể sinh học, bể lắng thứ cấp và nhà điều hành trung tâm 3 tầng.','Kèm hệ thống chống thấm, đường ống công nghệ và hạ tầng kỹ thuật nội bộ.',2,false,412300000000,'VND',8.00,'STR','Gói thầu do liên danh Hà Thành – Trường Thịnh thực hiện.','2026-04-01 00:00:00+07','2028-03-31 00:00:00+07','2026-03-06 08:10:00+07',NULL),
('e0000000-0000-0000-0000-000000000303','d0000000-0000-0000-0000-000000000003','BHU-2026-MEP-003','Lắp đặt thiết bị công nghệ và SCADA','Cung cấp, lắp đặt máy thổi khí, bơm, thiết bị tách rác và hệ thống điều khiển SCADA.','Bao gồm đào tạo vận hành và bảo hành 24 tháng.',1,false,158900000000,'VND',8.00,'MEP','Chờ bàn giao mặt bằng bể sinh học mới khởi công.','2026-11-01 00:00:00+07','2028-06-30 00:00:00+07','2026-03-06 08:20:00+07',NULL),
('e0000000-0000-0000-0000-000000000401','d0000000-0000-0000-0000-000000000004','SGC-2025-GEN-001','Gói thầu mặc định','Hồ sơ chung dự án Sài Gòn Center.',NULL,3,true,NULL,'VND',NULL,'GEN',NULL,NULL,NULL,'2025-06-12 08:00:00+07',NULL),
('e0000000-0000-0000-0000-000000000402','d0000000-0000-0000-0000-000000000004','SGC-2025-STR-002','Tổng thầu xây lắp Sài Gòn Center','Thi công trọn gói phần ngầm, phần thân và hoàn thiện mặt ngoài.','Đã nghiệm thu hoàn thành và bàn giao đơn vị vận hành ngày 28/04/2026.',3,false,318500000000,'VND',8.00,'STR','Đang trong thời gian bảo hành 24 tháng.','2025-07-01 00:00:00+07','2026-04-28 00:00:00+07','2025-06-12 08:10:00+07','2026-04-28 16:40:00+07'),
('e0000000-0000-0000-0000-000000000501','d0000000-0000-0000-0000-000000000005','HOA-2026-GEN-001','Gói thầu mặc định','Hồ sơ chung dự án Bệnh viện Hòa An.',NULL,2,true,NULL,'VND',NULL,'GEN',NULL,NULL,NULL,'2026-05-08 08:00:00+07',NULL),
('e0000000-0000-0000-0000-000000000502','d0000000-0000-0000-0000-000000000005','HOA-2026-STR-002','Thi công phần ngầm và phần thân','Thi công 2 tầng hầm, khối khám 5 tầng và khối nội trú 9 tầng.','Bao gồm tường vây, cọc khoan nhồi và kết cấu bê tông cốt thép toàn khối.',2,false,289700000000,'VND',8.00,'STR','Yêu cầu kiểm soát rung chấn do gần khu dân cư.','2026-06-01 00:00:00+07','2028-05-31 00:00:00+07','2026-05-08 08:10:00+07',NULL),
('e0000000-0000-0000-0000-000000000503','d0000000-0000-0000-0000-000000000005','HOA-2026-MEP-003','Cơ điện và hệ thống khí y tế','Hệ thống điện nhẹ, HVAC phòng sạch, khí y tế trung tâm và PCCC.','Tuân thủ tiêu chuẩn phòng mổ và khu xạ trị.',0,false,134600000000,'VND',8.00,'MEP','Đang lập hồ sơ mời thầu.',NULL,NULL,'2026-05-08 08:20:00+07',NULL);

-- ============================================================================
-- 11) PACKAGE ASSIGNMENTS
--     Role: MainContractor=0, Subcontractor=1, SupervisionConsultant=2,
--           DesignConsultant=3, Supplier=4
-- ============================================================================
INSERT INTO "PackageAssignments" ("Id","ContractPackageId","OrganizationId","Role","ContractNumber","Position","VatCode","RepresentativeAccountId","ContractSignDate","CreatedAt") VALUES
('e1000000-0000-0000-0000-000000000101','e0000000-0000-0000-0000-000000000102','b0000000-0000-0000-0000-000000000007',0,'HĐ-RIV-2026/01','Tổng Giám đốc','0100238914','a0000000-0000-0000-0000-000000000014','2026-02-10 00:00:00+07','2026-02-10 08:00:00+07'),
('e1000000-0000-0000-0000-000000000102','e0000000-0000-0000-0000-000000000102','b0000000-0000-0000-0000-000000000011',2,'HĐ-RIV-2026/01-GS','Giám đốc Tư vấn giám sát','0105338712','a0000000-0000-0000-0000-000000000022','2026-02-12 00:00:00+07','2026-02-12 08:00:00+07'),
('e1000000-0000-0000-0000-000000000103','e0000000-0000-0000-0000-000000000102','b0000000-0000-0000-0000-000000000005',3,'HĐ-RIV-2026/01-TK','Chủ nhiệm thiết kế','0311674205','a0000000-0000-0000-0000-000000000008','2026-01-28 00:00:00+07','2026-01-28 08:00:00+07'),
('e1000000-0000-0000-0000-000000000104','e0000000-0000-0000-0000-000000000102','b0000000-0000-0000-0000-000000000012',4,'HĐ-RIV-2026/01-VT','Trưởng phòng Kinh doanh','0310884236','a0000000-0000-0000-0000-000000000026','2026-03-05 00:00:00+07','2026-03-05 08:00:00+07'),
('e1000000-0000-0000-0000-000000000201','e0000000-0000-0000-0000-000000000202','b0000000-0000-0000-0000-000000000008',0,'HĐ-CAT-2026/02','Giám đốc','0314087563','a0000000-0000-0000-0000-000000000017','2026-02-25 00:00:00+07','2026-02-25 08:00:00+07'),
('e1000000-0000-0000-0000-000000000202','e0000000-0000-0000-0000-000000000202','b0000000-0000-0000-0000-000000000011',2,'HĐ-CAT-2026/02-GS','Giám sát trưởng','0105338712','a0000000-0000-0000-0000-000000000024','2026-02-26 00:00:00+07','2026-02-26 08:00:00+07'),
('e1000000-0000-0000-0000-000000000301','e0000000-0000-0000-0000-000000000302','b0000000-0000-0000-0000-000000000015',0,'HĐ-BHU-2026/03','Đại diện liên danh','0100238914001','a0000000-0000-0000-0000-000000000014','2026-03-25 00:00:00+07','2026-03-25 08:00:00+07'),
('e1000000-0000-0000-0000-000000000302','e0000000-0000-0000-0000-000000000303','b0000000-0000-0000-0000-000000000009',1,'HĐ-BHU-2026/04','Giám đốc Dự án','0309771468','a0000000-0000-0000-0000-000000000019','2026-04-02 00:00:00+07','2026-04-02 08:00:00+07'),
('e1000000-0000-0000-0000-000000000303','e0000000-0000-0000-0000-000000000303','b0000000-0000-0000-0000-000000000013',4,'HĐ-BHU-2026/04-TB','Giám đốc Kinh doanh','0107912350','a0000000-0000-0000-0000-000000000027','2026-04-08 00:00:00+07','2026-04-08 08:00:00+07'),
('e1000000-0000-0000-0000-000000000401','e0000000-0000-0000-0000-000000000402','b0000000-0000-0000-0000-000000000007',0,'HĐ-SGC-2025/01','Tổng Giám đốc','0100238914','a0000000-0000-0000-0000-000000000014','2025-06-25 00:00:00+07','2025-06-25 08:00:00+07'),
('e1000000-0000-0000-0000-000000000402','e0000000-0000-0000-0000-000000000402','b0000000-0000-0000-0000-000000000010',1,'HĐ-SGC-2025/01-HT','Giám đốc Điều hành','0316552087','a0000000-0000-0000-0000-000000000021','2025-09-15 00:00:00+07','2025-09-15 08:00:00+07'),
('e1000000-0000-0000-0000-000000000501','e0000000-0000-0000-0000-000000000502','b0000000-0000-0000-0000-000000000008',0,'HĐ-HOA-2026/01','Giám đốc','0314087563','a0000000-0000-0000-0000-000000000017','2026-05-25 00:00:00+07','2026-05-25 08:00:00+07'),
('e1000000-0000-0000-0000-000000000502','e0000000-0000-0000-0000-000000000502','b0000000-0000-0000-0000-000000000005',3,'HĐ-HOA-2026/01-TK','Chủ nhiệm thiết kế','0311674205','a0000000-0000-0000-0000-000000000008','2026-05-20 00:00:00+07','2026-05-20 08:00:00+07');

-- ============================================================================
-- 12) CONTRACTS  Status: 0=Draft, 1=Active, 2=Completed (Domain/Enum/Contract)
--     SourceFileVersionId de NULL — hop dong nhap tay, chua dinh kem ban scan.
-- ============================================================================
INSERT INTO "Contracts" ("Id","ContractPackageId","Code","Name","ContractorOrganizationId","SourceFileVersionId","SignedDate","Status","CreatedAt","UpdatedAt") VALUES
('e2000000-0000-0000-0000-000000000101','e0000000-0000-0000-0000-000000000102','HĐ-RIV-2026/01','Hợp đồng thi công kết cấu phần thân Riverside Tower','b0000000-0000-0000-0000-000000000007',NULL,'2026-02-10 00:00:00+07',1,'2026-02-10 08:30:00+07',NULL),
('e2000000-0000-0000-0000-000000000201','e0000000-0000-0000-0000-000000000202','HĐ-CAT-2026/02','Hợp đồng thi công nhịp thép cầu vượt Cát Lái','b0000000-0000-0000-0000-000000000008',NULL,'2026-02-25 00:00:00+07',1,'2026-02-25 08:30:00+07',NULL),
('e2000000-0000-0000-0000-000000000301','e0000000-0000-0000-0000-000000000302','HĐ-BHU-2026/03','Hợp đồng xây dựng bể sinh học và nhà điều hành','b0000000-0000-0000-0000-000000000015',NULL,'2026-03-25 00:00:00+07',1,'2026-03-25 08:30:00+07',NULL),
('e2000000-0000-0000-0000-000000000401','e0000000-0000-0000-0000-000000000402','HĐ-SGC-2025/01','Hợp đồng tổng thầu xây lắp Sài Gòn Center','b0000000-0000-0000-0000-000000000007',NULL,'2025-06-25 00:00:00+07',2,'2025-06-25 08:30:00+07','2026-04-28 16:40:00+07'),
('e2000000-0000-0000-0000-000000000501','e0000000-0000-0000-0000-000000000502','HĐ-HOA-2026/01','Hợp đồng thi công phần ngầm và phần thân Bệnh viện Hòa An','b0000000-0000-0000-0000-000000000008',NULL,'2026-05-25 00:00:00+07',1,'2026-05-25 08:30:00+07',NULL);

-- ============================================================================
-- 13) NAMING CONVENTIONS — quy uoc dat ten tep muc DU AN
--     FieldType: IsoStandard=0, Custom=1
--     fa..01: ISO 19650 day du 7 truong (Riverside, delimiter '-')
--     fa..02: rut gon 4 truong (Cau Cat Lai, delimiter '_')
--     fa..03: quy uoc CU da tat IsActive=false (test loc convention dang dung)
-- ============================================================================
INSERT INTO "NamingConventions" ("Id","ProjectId","Name","Delimiter","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fa000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','Quy ước ISO 19650 — Riverside Tower','-',true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:00:00+07','2026-02-15 09:00:00+07'),
('fa000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000002','Quy ước rút gọn — Cầu Cát Lái','_',true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:00:00+07',NULL),
('fa000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','Quy ước nội bộ 2025 (ngừng dùng)','-',false,'a0000000-0000-0000-0000-000000000002','2026-01-16 09:00:00+07','2026-01-20 09:00:00+07'),
('fa000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000005','Quy ước ISO 19650 — Bệnh viện Hòa An','-',true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:00:00+07',NULL);

-- ---- Fields: PRJ-ORG-ZON-LVL-TYP-DIS-NUM ------------------------------------
INSERT INTO "NamingConventionFields" ("Id","NamingConventionId","Code","DisplayName","Description","OrderIndex","IsRequired","IsLocked","MinLength","MaxLength","FieldType","CreatedById","CreatedAt","UpdatedAt") VALUES
('fb000000-0000-0000-0000-000000000101','fa000000-0000-0000-0000-000000000001','PRJ','Mã dự án','Mã viết tắt của dự án, khóa cứng theo dự án.',1,true,true,3,3,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:00:00+07',NULL),
('fb000000-0000-0000-0000-000000000102','fa000000-0000-0000-0000-000000000001','ORG','Đơn vị phát hành','Đơn vị lập hồ sơ.',2,true,false,2,4,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:01:00+07',NULL),
('fb000000-0000-0000-0000-000000000103','fa000000-0000-0000-0000-000000000001','ZON','Phân khu','Tháp hoặc phân khu công trình.',3,true,false,2,3,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:02:00+07',NULL),
('fb000000-0000-0000-0000-000000000104','fa000000-0000-0000-0000-000000000001','LVL','Cao độ / tầng','Tầng áp dụng, ZZ nếu áp dụng toàn bộ.',4,true,false,2,2,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:03:00+07',NULL),
('fb000000-0000-0000-0000-000000000105','fa000000-0000-0000-0000-000000000001','TYP','Loại tài liệu','Bản vẽ, mô hình, thuyết minh...',5,true,false,2,2,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:04:00+07',NULL),
('fb000000-0000-0000-0000-000000000106','fa000000-0000-0000-0000-000000000001','DIS','Bộ môn','Bộ môn kỹ thuật.',6,true,false,3,3,0,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:05:00+07',NULL),
('fb000000-0000-0000-0000-000000000107','fa000000-0000-0000-0000-000000000001','NUM','Số thứ tự','Số hiệu tài liệu trong bộ.',7,true,false,3,3,1,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:06:00+07',NULL),
('fb000000-0000-0000-0000-000000000201','fa000000-0000-0000-0000-000000000002','PRJ','Mã dự án','Mã viết tắt dự án.',1,true,true,3,3,0,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:00:00+07',NULL),
('fb000000-0000-0000-0000-000000000202','fa000000-0000-0000-0000-000000000002','HM','Hạng mục','Hạng mục công trình.',2,true,false,2,4,1,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:01:00+07',NULL),
('fb000000-0000-0000-0000-000000000203','fa000000-0000-0000-0000-000000000002','DIS','Bộ môn','Bộ môn kỹ thuật.',3,true,false,3,3,0,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:02:00+07',NULL),
('fb000000-0000-0000-0000-000000000204','fa000000-0000-0000-0000-000000000002','NUM','Số thứ tự','Số hiệu tài liệu.',4,true,false,3,3,1,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:03:00+07',NULL),
('fb000000-0000-0000-0000-000000000501','fa000000-0000-0000-0000-000000000005','PRJ','Mã dự án','Mã viết tắt dự án.',1,true,true,3,3,0,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:00:00+07',NULL),
('fb000000-0000-0000-0000-000000000502','fa000000-0000-0000-0000-000000000005','ZON','Khối công trình','Khối khám hoặc khối nội trú.',2,true,false,2,3,0,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:01:00+07',NULL),
('fb000000-0000-0000-0000-000000000503','fa000000-0000-0000-0000-000000000005','DIS','Bộ môn','Bộ môn kỹ thuật.',3,true,false,3,3,0,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:02:00+07',NULL),
('fb000000-0000-0000-0000-000000000504','fa000000-0000-0000-0000-000000000005','NUM','Số thứ tự','Số hiệu tài liệu.',4,true,false,3,3,1,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:03:00+07',NULL);

-- ---- Field values -----------------------------------------------------------
INSERT INTO "NamingConventionFieldValues" ("Id","NamingConventionFieldId","Code","DisplayName","Description","OrderIndex","IsLocked","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fc000000-0000-0000-0000-000000010101','fb000000-0000-0000-0000-000000000101','RIV','Riverside Tower','Mã dự án cố định.',1,true,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:10:00+07',NULL),
('fc000000-0000-0000-0000-000000010201','fb000000-0000-0000-0000-000000000102','PNA','TVTK Phương Nam',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:11:00+07',NULL),
('fc000000-0000-0000-0000-000000010202','fb000000-0000-0000-0000-000000000102','HTH','XD Hà Thành',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:12:00+07',NULL),
('fc000000-0000-0000-0000-000000010203','fb000000-0000-0000-0000-000000000102','BHA','TVGS Bắc Hà',NULL,3,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:13:00+07',NULL),
('fc000000-0000-0000-0000-000000010301','fb000000-0000-0000-0000-000000000103','TA','Tháp A',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:14:00+07',NULL),
('fc000000-0000-0000-0000-000000010302','fb000000-0000-0000-0000-000000000103','TB','Tháp B',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:15:00+07',NULL),
('fc000000-0000-0000-0000-000000010303','fb000000-0000-0000-0000-000000000103','TC','Tháp C',NULL,3,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:16:00+07',NULL),
('fc000000-0000-0000-0000-000000010304','fb000000-0000-0000-0000-000000000103','XX','Toàn dự án',NULL,4,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:17:00+07',NULL),
('fc000000-0000-0000-0000-000000010401','fb000000-0000-0000-0000-000000000104','ZZ','Áp dụng mọi tầng',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:18:00+07',NULL),
('fc000000-0000-0000-0000-000000010402','fb000000-0000-0000-0000-000000000104','B1','Tầng hầm B1',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:19:00+07',NULL),
('fc000000-0000-0000-0000-000000010403','fb000000-0000-0000-0000-000000000104','01','Tầng 1',NULL,3,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:20:00+07',NULL),
('fc000000-0000-0000-0000-000000010404','fb000000-0000-0000-0000-000000000104','05','Tầng 5',NULL,4,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:21:00+07',NULL),
('fc000000-0000-0000-0000-000000010501','fb000000-0000-0000-0000-000000000105','M3','Mô hình 3D',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:22:00+07',NULL),
('fc000000-0000-0000-0000-000000010502','fb000000-0000-0000-0000-000000000105','DR','Bản vẽ',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:23:00+07',NULL),
('fc000000-0000-0000-0000-000000010503','fb000000-0000-0000-0000-000000000105','CA','Bảng tính / thống kê',NULL,3,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:24:00+07',NULL),
('fc000000-0000-0000-0000-000000010504','fb000000-0000-0000-0000-000000000105','SP','Thuyết minh / chỉ dẫn',NULL,4,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:25:00+07',NULL),
('fc000000-0000-0000-0000-000000010601','fb000000-0000-0000-0000-000000000106','ARC','Kiến trúc',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:26:00+07',NULL),
('fc000000-0000-0000-0000-000000010602','fb000000-0000-0000-0000-000000000106','STR','Kết cấu',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:27:00+07',NULL),
('fc000000-0000-0000-0000-000000010603','fb000000-0000-0000-0000-000000000106','MEP','Cơ điện',NULL,3,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:28:00+07',NULL),
('fc000000-0000-0000-0000-000000010604','fb000000-0000-0000-0000-000000000106','GEN','Hồ sơ chung',NULL,4,false,true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:29:00+07',NULL),
('fc000000-0000-0000-0000-000000020101','fb000000-0000-0000-0000-000000000201','CAT','Cầu vượt Cát Lái','Mã dự án cố định.',1,true,true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:10:00+07',NULL),
('fc000000-0000-0000-0000-000000020301','fb000000-0000-0000-0000-000000000203','STR','Kết cấu',NULL,1,false,true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:11:00+07',NULL),
('fc000000-0000-0000-0000-000000020302','fb000000-0000-0000-0000-000000000203','GEO','Địa kỹ thuật',NULL,2,false,true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:12:00+07',NULL),
('fc000000-0000-0000-0000-000000020303','fb000000-0000-0000-0000-000000000203','ROA','Đường và tổ chức giao thông',NULL,3,false,true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:13:00+07',NULL),
('fc000000-0000-0000-0000-000000050101','fb000000-0000-0000-0000-000000000501','HOA','Bệnh viện Hòa An','Mã dự án cố định.',1,true,true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:10:00+07',NULL),
('fc000000-0000-0000-0000-000000050201','fb000000-0000-0000-0000-000000000502','KK','Khối khám ngoại trú',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:11:00+07',NULL),
('fc000000-0000-0000-0000-000000050202','fb000000-0000-0000-0000-000000000502','KN','Khối nội trú',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:12:00+07',NULL),
('fc000000-0000-0000-0000-000000050301','fb000000-0000-0000-0000-000000000503','ARC','Kiến trúc',NULL,1,false,true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:13:00+07',NULL),
('fc000000-0000-0000-0000-000000050302','fb000000-0000-0000-0000-000000000503','MEP','Cơ điện và khí y tế',NULL,2,false,true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:14:00+07',NULL);

-- ---- Locked values: PRJ bi khoa cung theo tung du an ------------------------
INSERT INTO "NamingConventionLockedValues" ("Id","NamingConventionFieldId","NamingConventionFieldValueId","IsActive","CreatedById","CreatedAt","UpdatedAt") VALUES
('fd000000-0000-0000-0000-000000000001','fb000000-0000-0000-0000-000000000101','fc000000-0000-0000-0000-000000010101',true,'a0000000-0000-0000-0000-000000000008','2026-01-20 09:30:00+07',NULL),
('fd000000-0000-0000-0000-000000000002','fb000000-0000-0000-0000-000000000201','fc000000-0000-0000-0000-000000020101',true,'a0000000-0000-0000-0000-000000000002','2026-02-06 09:20:00+07',NULL),
('fd000000-0000-0000-0000-000000000003','fb000000-0000-0000-0000-000000000501','fc000000-0000-0000-0000-000000050101',true,'a0000000-0000-0000-0000-000000000008','2026-05-10 09:20:00+07',NULL);

-- ============================================================================
-- 14) FOLDERS — khung CDE 4 vung theo ISO 19650 cho ca 5 du an.
--     Area: Wip=0, Shared=1, Published=2, Archived=3
--     Cach dung: 4 thu muc GOC khai tuong minh, con "o" cua tung nhom va ban
--     chieu sang Shared duoc SINH tu "ProjectParticipants" nen luon khop so nhom.
--     UUID goc: f0000000-...-00000000{P}{Z}00  (P=du an 1..5, Z=vung 1..4)
-- ============================================================================
INSERT INTO "Folders" ("Id","ProjectId","ParentFolderId","Name","Area","IsTemplate","NamingConventionId","MirrorSourceFolderId","CreatedByAccountId","CreatedAt","UpdatedAt")
SELECT
    ('f0000000-0000-0000-0000-00000000' || p.n || z.z || '00')::uuid,
    p.id, NULL, z.nm, z.area, false,
    CASE WHEN z.area = 0 THEN nc.id ELSE NULL END,
    NULL, 'a0000000-0000-0000-0000-000000000001', p.created, NULL
FROM (VALUES
        ('1','d0000000-0000-0000-0000-000000000001'::uuid,'2026-01-17 08:00:00+07'::timestamptz),
        ('2','d0000000-0000-0000-0000-000000000002'::uuid,'2026-02-04 08:00:00+07'::timestamptz),
        ('3','d0000000-0000-0000-0000-000000000003'::uuid,'2026-03-06 08:00:00+07'::timestamptz),
        ('4','d0000000-0000-0000-0000-000000000004'::uuid,'2025-06-12 08:00:00+07'::timestamptz),
        ('5','d0000000-0000-0000-0000-000000000005'::uuid,'2026-05-08 08:00:00+07'::timestamptz)
     ) AS p(n,id,created)
CROSS JOIN (VALUES ('1','01-WIP',0),('2','02-Shared',1),('3','03-Published',2),('4','04-Archived',3)) AS z(z,nm,area)
LEFT JOIN (VALUES
        ('d0000000-0000-0000-0000-000000000001'::uuid,'fa000000-0000-0000-0000-000000000001'::uuid),
        ('d0000000-0000-0000-0000-000000000002'::uuid,'fa000000-0000-0000-0000-000000000002'::uuid),
        ('d0000000-0000-0000-0000-000000000005'::uuid,'fa000000-0000-0000-0000-000000000005'::uuid)
     ) AS nc(pid,id) ON nc.pid = p.id;

-- ---- "O" rieng cua tung nhom trong vung WIP ---------------------------------
INSERT INTO "Folders" ("Id","ProjectId","ParentFolderId","Name","Area","IsTemplate","NamingConventionId","MirrorSourceFolderId","CreatedByAccountId","CreatedAt","UpdatedAt")
SELECT md5('wip' || pp."Id"::text)::uuid, pp."ProjectId", wip."Id", g."Name", 0, false,
       wip."NamingConventionId", NULL, 'a0000000-0000-0000-0000-000000000001', pp."JoinedAt", NULL
FROM "ProjectParticipants" pp
JOIN "Groups" g ON g."Id" = pp."GroupId"
JOIN "Folders" wip ON wip."ProjectId" = pp."ProjectId" AND wip."Area" = 0 AND wip."ParentFolderId" IS NULL;

-- ---- Ban chieu cua moi "o" WIP sang vung Shared -----------------------------
INSERT INTO "Folders" ("Id","ProjectId","ParentFolderId","Name","Area","IsTemplate","NamingConventionId","MirrorSourceFolderId","CreatedByAccountId","CreatedAt","UpdatedAt")
SELECT md5('shared' || pp."Id"::text)::uuid, pp."ProjectId", sh."Id", g."Name", 1, false,
       NULL, md5('wip' || pp."Id"::text)::uuid, 'a0000000-0000-0000-0000-000000000001', pp."JoinedAt", NULL
FROM "ProjectParticipants" pp
JOIN "Groups" g ON g."Id" = pp."GroupId"
JOIN "Folders" sh ON sh."ProjectId" = pp."ProjectId" AND sh."Area" = 1 AND sh."ParentFolderId" IS NULL;

-- ---- 2 thu muc HE THONG trong vung Published (khop CdeFolderNames) ----------
INSERT INTO "Folders" ("Id","ProjectId","ParentFolderId","Name","Area","IsTemplate","NamingConventionId","MirrorSourceFolderId","CreatedByAccountId","CreatedAt","UpdatedAt")
SELECT md5('pkgroot' || pub."ProjectId"::text)::uuid, pub."ProjectId", pub."Id", 'Các gói thầu', 2, false, NULL::uuid, NULL::uuid,
       'a0000000-0000-0000-0000-000000000001'::uuid, pub."CreatedAt", NULL::timestamptz
FROM "Folders" pub WHERE pub."Area" = 2 AND pub."ParentFolderId" IS NULL
UNION ALL
SELECT md5('legal' || pub."ProjectId"::text)::uuid, pub."ProjectId", pub."Id", 'Hồ sơ pháp lý', 2, false, NULL::uuid, NULL::uuid,
       'a0000000-0000-0000-0000-000000000001'::uuid, pub."CreatedAt", NULL::timestamptz
FROM "Folders" pub WHERE pub."Area" = 2 AND pub."ParentFolderId" IS NULL;

-- ---- Thu muc rieng cho tung goi thau (nam trong "Cac goi thau") -------------
INSERT INTO "Folders" ("Id","ProjectId","ParentFolderId","Name","Area","IsTemplate","NamingConventionId","MirrorSourceFolderId","CreatedByAccountId","CreatedAt","UpdatedAt")
SELECT md5('pkg' || cp."Id"::text)::uuid, cp."ProjectId", md5('pkgroot' || cp."ProjectId"::text)::uuid, cp."Name", 2, false, NULL, NULL,
       'a0000000-0000-0000-0000-000000000001', cp."CreatedAt", NULL
FROM "ContractPackages" cp WHERE cp."IsDefault" = false;

UPDATE "ContractPackages" cp SET "DocumentFolderId" = md5('pkg' || cp."Id"::text)::uuid WHERE cp."IsDefault" = false;

-- ============================================================================
-- 15) FOLDER PERMISSIONS — sinh tu ma tran (thu muc x nhom tham gia)
--     Luat: MOI nhom trong du an deu THAY (CanView) cac o; chi nhom SO HUU o
--     WIP moi duoc GHI (CanEdit). Vung Shared/Published/Archived khong ai ghi
--     truc tiep — tep di vao do qua luong phe duyet.
--     CanApprove BE gan cung true (dinh tuyen nhom phu trach, khong phai quyen).
--     Status: 0 = Active.
-- ============================================================================
INSERT INTO "FolderPermissions" ("Id","FolderId","ProjectParticipantId","CanView","CanEdit","CanApprove","Status")
SELECT md5('fp' || f."Id"::text || pp."Id"::text)::uuid, f."Id", pp."Id",
       true,
       (f."Area" = 0 AND f."Id" = md5('wip' || pp."Id"::text)::uuid),
       true, 0
FROM "Folders" f
JOIN "ProjectParticipants" pp ON pp."ProjectId" = f."ProjectId"
WHERE f."ParentFolderId" IS NOT NULL;

-- ---- Gan truong dat ten cho tung thu muc WIP (FolderNamingFields) -----------
INSERT INTO "FolderNamingFields" ("Id","FolderId","NamingConventionFieldId","CreatedById","CreatedAt")
SELECT md5('fnf' || f."Id"::text || fld."Id"::text)::uuid, f."Id", fld."Id",
       'a0000000-0000-0000-0000-000000000008', f."CreatedAt"
FROM "Folders" f
JOIN "NamingConventions" nc ON nc."ProjectId" = f."ProjectId" AND nc."IsActive" = true
JOIN "NamingConventionFields" fld ON fld."NamingConventionId" = nc."Id"
WHERE f."Area" = 0 AND f."ParentFolderId" IS NOT NULL;

-- ============================================================================
-- 16) FILE ITEMS
--     FileType: Pdf=0, Ifc=1, Image=2, Cad=3, Office=4, Other=5
--     Status  : Draft=0, PendingApproval=1, Approved=2, Rejected=3
--     "CurrentVersionId" duoc gan o buoc 18 (khoa vong: FileItem <-> Version).
--     FolderId tinh tu ProjectParticipant => luon tro dung "o" cua nhom so huu.
-- ============================================================================
INSERT INTO "FileItems" ("Id","FolderId","Name","FileType","Status","RequiresSignature","IsSigned","CurrentVersionId","SignedVersionId","CreatedByAccountId","CreatedAt","UpdatedAt","SourceFileItemId")
SELECT v.id::uuid, md5(v.zone || v.pp)::uuid, v.nm, v.ftype, v.status, v.reqsig, v.issig,
       NULL::uuid, NULL::uuid, v.author::uuid, v.created::timestamptz, NULL::timestamptz, NULL::uuid
FROM (VALUES
 -- Du an 1 — Riverside Tower (o Tu van thiet ke: d3..0103, Nha thau: d3..0104, TVGS: d3..0105)
 ('f2000000-0000-0000-0000-000000000101','wip','d3000000-0000-0000-0000-000000000103','RIV-PNA-TA-ZZ-M3-ARC-001.ifc',1,0,false,false,'a0000000-0000-0000-0000-000000000009','2026-03-02 09:00:00+07'),
 ('f2000000-0000-0000-0000-000000000102','wip','d3000000-0000-0000-0000-000000000103','RIV-PNA-TA-01-DR-ARC-014.pdf',0,1,true,false,'a0000000-0000-0000-0000-000000000009','2026-03-10 10:00:00+07'),
 ('f2000000-0000-0000-0000-000000000103','wip','d3000000-0000-0000-0000-000000000103','RIV-PNA-TB-ZZ-M3-STR-002.ifc',1,0,false,false,'a0000000-0000-0000-0000-000000000010','2026-03-14 08:30:00+07'),
 ('f2000000-0000-0000-0000-000000000104','wip','d3000000-0000-0000-0000-000000000104','RIV-HTH-TA-B1-DR-STR-021.pdf',0,2,true,true,'a0000000-0000-0000-0000-000000000015','2026-03-20 14:00:00+07'),
 ('f2000000-0000-0000-0000-000000000105','wip','d3000000-0000-0000-0000-000000000104','RIV-HTH-XX-ZZ-CA-STR-003.xlsx',4,0,false,false,'a0000000-0000-0000-0000-000000000016','2026-04-02 09:15:00+07'),
 ('f2000000-0000-0000-0000-000000000106','wip','d3000000-0000-0000-0000-000000000105','RIV-BHA-TA-05-DR-ARC-047.pdf',0,3,false,false,'a0000000-0000-0000-0000-000000000023','2026-04-18 16:20:00+07'),
 ('f2000000-0000-0000-0000-000000000107','wip','d3000000-0000-0000-0000-000000000103','RIV-PNA-XX-ZZ-SP-GEN-001.docx',4,0,false,false,'a0000000-0000-0000-0000-000000000011','2026-05-06 11:00:00+07'),
 -- Du an 2 — Cau Cat Lai
 ('f2000000-0000-0000-0000-000000000201','wip','d3000000-0000-0000-0000-000000000203','CAT_NHIP_STR_001.dwg',3,0,false,false,'a0000000-0000-0000-0000-000000000010','2026-03-12 09:00:00+07'),
 ('f2000000-0000-0000-0000-000000000202','wip','d3000000-0000-0000-0000-000000000203','CAT_MOTRU_GEO_002.pdf',0,2,true,true,'a0000000-0000-0000-0000-000000000011','2026-03-25 10:30:00+07'),
 ('f2000000-0000-0000-0000-000000000203','wip','d3000000-0000-0000-0000-000000000204','CAT_NHIP_STR_004.ifc',1,1,false,false,'a0000000-0000-0000-0000-000000000018','2026-04-09 08:45:00+07'),
 -- Du an 3 — Nha may XLNT Binh Hung
 ('f2000000-0000-0000-0000-000000000301','wip','d3000000-0000-0000-0000-000000000303','BHU-BIM-BE-SINH-HOC-M3-001.ifc',1,0,false,false,'a0000000-0000-0000-0000-000000000013','2026-04-15 09:00:00+07'),
 ('f2000000-0000-0000-0000-000000000302','wip','d3000000-0000-0000-0000-000000000304','BHU-LD-NHA-DIEU-HANH-DR-002.pdf',0,1,true,false,'a0000000-0000-0000-0000-000000000017','2026-05-02 13:30:00+07'),
 ('f2000000-0000-0000-0000-000000000303','wip','d3000000-0000-0000-0000-000000000305','BHU-VL-SCADA-SP-003.pdf',0,0,false,false,'a0000000-0000-0000-0000-000000000020','2026-05-20 15:00:00+07'),
 -- Du an 4 — Sai Gon Center (da hoan thanh, ho so nam o Published/Archived)
 ('f2000000-0000-0000-0000-000000000401','wip','d3000000-0000-0000-0000-000000000403','SGC-PNA-XX-ZZ-DR-ARC-101.pdf',0,2,true,true,'a0000000-0000-0000-0000-000000000008','2025-08-14 09:00:00+07'),
 ('f2000000-0000-0000-0000-000000000402','wip','d3000000-0000-0000-0000-000000000404','SGC-HTH-XX-ZZ-CA-STR-088.xlsx',4,2,false,false,'a0000000-0000-0000-0000-000000000014','2025-11-20 10:00:00+07'),
 -- Du an 5 — Benh vien Hoa An
 ('f2000000-0000-0000-0000-000000000501','wip','d3000000-0000-0000-0000-000000000503','HOA-KK-ARC-001.ifc',1,0,false,false,'a0000000-0000-0000-0000-000000000011','2026-06-10 09:00:00+07'),
 ('f2000000-0000-0000-0000-000000000502','wip','d3000000-0000-0000-0000-000000000503','HOA-KN-ARC-002.pdf',0,1,true,false,'a0000000-0000-0000-0000-000000000008','2026-06-24 14:10:00+07'),
 ('f2000000-0000-0000-0000-000000000503','wip','d3000000-0000-0000-0000-000000000505','HOA-KN-MEP-011.dwg',3,0,false,false,'a0000000-0000-0000-0000-000000000019','2026-07-08 08:20:00+07')
) AS v(id,zone,pp,nm,ftype,status,reqsig,issig,author,created);

-- ============================================================================
-- 17) FILE VERSION STATES
--     Stage: Working=0 (hien P{rev}.{ver}) · Published=1 (C{pubRev}) · Archived=2
--     ViewerStatus: None=0, Pending=1, Processing=2, Ready=3, Failed=4
--     StoragePath tro len S3 theo dung quy uoc BE sinh khi upload.
--     f3..0101v2 va f3..0104v2 la ban HIEN HANH cua tep co 2 phien ban.
-- ============================================================================
INSERT INTO "FileVersionStates" ("Id","FileItemId","Stage","WorkingRevision","WorkingVersion","PublishedRevision","DisplayVersion","IsCurrent","IsHidden","FileName","StoragePath","PreviewStoragePath","Format","FileSizeBytes","Checksum","ViewerStatus","ViewerUrn","ViewerProgress","ViewerError","IsSigned","SignedAt","SignedBy","CertificateSerial","Description","Warnning","WarnningMessage","UploadedByAccountId","UploadedAt","CreatedAt","UpdatedAt") VALUES
('f3000000-0000-0000-0000-000000010101','f2000000-0000-0000-0000-000000000101',0,1,1,0,'P01.01',false,false,'RIV-PNA-TA-ZZ-M3-ARC-001.ifc','projects/riv/wip/tvtk/riv-pna-ta-zz-m3-arc-001-v1.ifc',NULL,'ifc',18452330,'sha256:1a4f9c22e8b7d5a3',0,NULL,0,NULL,false,NULL,NULL,NULL,'Mô hình kiến trúc tháp A — bản dựng lần đầu, chưa gồm khu kỹ thuật mái.',false,NULL,'a0000000-0000-0000-0000-000000000009','2026-03-02 09:00:00+07','2026-03-02 09:00:00+07',NULL),
('f3000000-0000-0000-0000-000000010102','f2000000-0000-0000-0000-000000000101',0,1,2,0,'P01.02',true,false,'RIV-PNA-TA-ZZ-M3-ARC-001.ifc','projects/riv/wip/tvtk/riv-pna-ta-zz-m3-arc-001-v2.ifc',NULL,'ifc',19883104,'sha256:7c2e0b91f4a6d833',3,'urn:adsk.objects:os.object:cde-riv/riv-arc-001-v2.ifc',100,NULL,false,NULL,NULL,NULL,'Bổ sung khu kỹ thuật mái và lõi thang, cập nhật cao độ tầng điển hình.',false,NULL,'a0000000-0000-0000-0000-000000000009','2026-04-06 10:20:00+07','2026-04-06 10:20:00+07',NULL),
('f3000000-0000-0000-0000-000000010201','f2000000-0000-0000-0000-000000000102',0,1,1,0,'P01.01',true,false,'RIV-PNA-TA-01-DR-ARC-014.pdf','projects/riv/wip/tvtk/riv-pna-ta-01-dr-arc-014-v1.pdf','projects/riv/preview/riv-pna-ta-01-dr-arc-014-v1.png','pdf',3241880,'sha256:be31a7d09c45e612',0,NULL,0,NULL,false,NULL,NULL,NULL,'Mặt bằng tầng 1 tháp A — trình duyệt sang vùng Shared.',false,NULL,'a0000000-0000-0000-0000-000000000009','2026-03-10 10:00:00+07','2026-03-10 10:00:00+07',NULL),
('f3000000-0000-0000-0000-000000010301','f2000000-0000-0000-0000-000000000103',0,1,1,0,'P01.01',true,false,'RIV-PNA-TB-ZZ-M3-STR-002.ifc','projects/riv/wip/tvtk/riv-pna-tb-zz-m3-str-002-v1.ifc',NULL,'ifc',24117640,'sha256:0d9b4e77c1a2f508',4,NULL,0,'Bản dịch thất bại: tệp IFC thiếu định nghĩa IFCPROJECT hợp lệ.',false,NULL,NULL,NULL,'Mô hình kết cấu tháp B — đang chờ xuất lại từ Revit.',true,'Tên tệp khai bộ môn STR nhưng nội dung mô hình chủ yếu là cấu kiện kiến trúc.','a0000000-0000-0000-0000-000000000010','2026-03-14 08:30:00+07','2026-03-14 08:30:00+07',NULL),
('f3000000-0000-0000-0000-000000010401','f2000000-0000-0000-0000-000000000104',0,1,1,0,'P01.01',false,false,'RIV-HTH-TA-B1-DR-STR-021.pdf','projects/riv/wip/nhathau/riv-hth-ta-b1-dr-str-021-v1.pdf','projects/riv/preview/riv-hth-ta-b1-dr-str-021-v1.png','pdf',5120440,'sha256:44c8e1b0a97d3f26',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bản vẽ biện pháp thi công tầng hầm B1 — bản trình lần 1.',false,NULL,'a0000000-0000-0000-0000-000000000015','2026-03-20 14:00:00+07','2026-03-20 14:00:00+07',NULL),
('f3000000-0000-0000-0000-000000010402','f2000000-0000-0000-0000-000000000104',1,1,2,1,'C01',true,false,'RIV-HTH-TA-B1-DR-STR-021.pdf','projects/riv/published/nhathau/riv-hth-ta-b1-dr-str-021-c01.pdf','projects/riv/preview/riv-hth-ta-b1-dr-str-021-c01.png','pdf',5233918,'sha256:9f70dd2a5c81b4e0',0,NULL,0,NULL,true,'2026-04-24 15:10:00+07','a0000000-0000-0000-0000-000000000022','VNPT-CA-2026-0447281','Bản phát hành chính thức, đã ký số bởi Tư vấn giám sát.',false,NULL,'a0000000-0000-0000-0000-000000000015','2026-04-22 09:00:00+07','2026-04-22 09:00:00+07','2026-04-24 15:10:00+07'),
('f3000000-0000-0000-0000-000000010501','f2000000-0000-0000-0000-000000000105',0,1,1,0,'P01.01',true,false,'RIV-HTH-XX-ZZ-CA-STR-003.xlsx','projects/riv/wip/nhathau/riv-hth-xx-zz-ca-str-003-v1.xlsx','projects/riv/preview/riv-hth-xx-zz-ca-str-003-v1.pdf','xlsx',884120,'sha256:c05a3e9147b6820d',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bảng thống kê khối lượng cốt thép phần thân tháp A.',false,NULL,'a0000000-0000-0000-0000-000000000016','2026-04-02 09:15:00+07','2026-04-02 09:15:00+07',NULL),
('f3000000-0000-0000-0000-000000010601','f2000000-0000-0000-0000-000000000106',0,1,1,0,'P01.01',true,false,'RIV-BHA-TA-05-DR-ARC-047.pdf','projects/riv/wip/tvgs/riv-bha-ta-05-dr-arc-047-v1.pdf','projects/riv/preview/riv-bha-ta-05-dr-arc-047-v1.png','pdf',2884006,'sha256:31eb95c7402ad168',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bản vẽ hoàn công tầng 5 — BỊ TỪ CHỐI, thiếu chữ ký chủ trì.',false,NULL,'a0000000-0000-0000-0000-000000000023','2026-04-18 16:20:00+07','2026-04-18 16:20:00+07',NULL),
('f3000000-0000-0000-0000-000000010701','f2000000-0000-0000-0000-000000000107',0,1,1,0,'P01.01',true,false,'RIV-PNA-XX-ZZ-SP-GEN-001.docx','projects/riv/wip/tvtk/riv-pna-xx-zz-sp-gen-001-v1.docx','projects/riv/preview/riv-pna-xx-zz-sp-gen-001-v1.pdf','docx',1442300,'sha256:6a8f20d13cb7e945',0,NULL,0,NULL,false,NULL,NULL,NULL,'Thuyết minh chỉ dẫn kỹ thuật phần kiến trúc.',false,NULL,'a0000000-0000-0000-0000-000000000011','2026-05-06 11:00:00+07','2026-05-06 11:00:00+07',NULL),
('f3000000-0000-0000-0000-000000020101','f2000000-0000-0000-0000-000000000201',0,1,1,0,'P01.01',true,false,'CAT_NHIP_STR_001.dwg','projects/cat/wip/tvtk/cat-nhip-str-001-v1.dwg','projects/cat/preview/cat-nhip-str-001-v1.pdf','dwg',7740221,'sha256:2b6c47f8e0193da5',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bản vẽ chi tiết nhịp dầm thép N1–N4.',false,NULL,'a0000000-0000-0000-0000-000000000010','2026-03-12 09:00:00+07','2026-03-12 09:00:00+07',NULL),
('f3000000-0000-0000-0000-000000020201','f2000000-0000-0000-0000-000000000202',1,1,1,1,'C01',true,false,'CAT_MOTRU_GEO_002.pdf','projects/cat/published/tvtk/cat-motru-geo-002-c01.pdf','projects/cat/preview/cat-motru-geo-002-c01.png','pdf',4102778,'sha256:df31820ae954c6b7',0,NULL,0,NULL,true,'2026-05-14 10:40:00+07','a0000000-0000-0000-0000-000000000024','VNPT-CA-2026-0518902','Báo cáo khảo sát địa chất mố trụ — đã phát hành và ký số.',false,NULL,'a0000000-0000-0000-0000-000000000011','2026-05-10 08:00:00+07','2026-05-10 08:00:00+07','2026-05-14 10:40:00+07'),
('f3000000-0000-0000-0000-000000020301','f2000000-0000-0000-0000-000000000203',0,1,1,0,'P01.01',true,false,'CAT_NHIP_STR_004.ifc','projects/cat/wip/nhathau/cat-nhip-str-004-v1.ifc',NULL,'ifc',15220890,'sha256:8e1d0c73ba492f16',2,'urn:adsk.objects:os.object:cde-cat/cat-nhip-str-004-v1.ifc',62,NULL,false,NULL,NULL,NULL,'Mô hình lắp dựng nhịp thép — đang chờ duyệt sang Shared.',false,NULL,'a0000000-0000-0000-0000-000000000018','2026-04-09 08:45:00+07','2026-04-09 08:45:00+07',NULL),
('f3000000-0000-0000-0000-000000030101','f2000000-0000-0000-0000-000000000301',0,1,1,0,'P01.01',true,false,'BHU-BIM-BE-SINH-HOC-M3-001.ifc','projects/bhu/wip/tvtk/bhu-bim-be-sinh-hoc-m3-001-v1.ifc',NULL,'ifc',33940112,'sha256:5c7920ae4b1d38f0',3,'urn:adsk.objects:os.object:cde-bhu/bhu-be-sinh-hoc-v1.ifc',100,NULL,false,NULL,NULL,NULL,'Mô hình 4 bể sinh học và hệ đường ống công nghệ.',false,NULL,'a0000000-0000-0000-0000-000000000013','2026-04-15 09:00:00+07','2026-04-15 09:00:00+07',NULL),
('f3000000-0000-0000-0000-000000030201','f2000000-0000-0000-0000-000000000302',0,1,1,0,'P01.01',true,false,'BHU-LD-NHA-DIEU-HANH-DR-002.pdf','projects/bhu/wip/nhathau/bhu-ld-nha-dieu-hanh-dr-002-v1.pdf','projects/bhu/preview/bhu-ld-nha-dieu-hanh-dr-002-v1.png','pdf',6019442,'sha256:a2f4b8016e5c37d9',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bản vẽ thi công nhà điều hành trung tâm — chờ duyệt.',false,NULL,'a0000000-0000-0000-0000-000000000017','2026-05-02 13:30:00+07','2026-05-02 13:30:00+07',NULL),
('f3000000-0000-0000-0000-000000030301','f2000000-0000-0000-0000-000000000303',0,1,1,0,'P01.01',true,false,'BHU-VL-SCADA-SP-003.pdf','projects/bhu/wip/codien/bhu-vl-scada-sp-003-v1.pdf','projects/bhu/preview/bhu-vl-scada-sp-003-v1.png','pdf',2210778,'sha256:7d3e01c9f2ba4685',0,NULL,0,NULL,false,NULL,NULL,NULL,'Thuyết minh kiến trúc hệ thống SCADA.',false,NULL,'a0000000-0000-0000-0000-000000000020','2026-05-20 15:00:00+07','2026-05-20 15:00:00+07',NULL),
('f3000000-0000-0000-0000-000000040101','f2000000-0000-0000-0000-000000000401',2,1,3,2,'C02',true,false,'SGC-PNA-XX-ZZ-DR-ARC-101.pdf','projects/sgc/archived/tvtk/sgc-pna-xx-zz-dr-arc-101-c02.pdf','projects/sgc/preview/sgc-pna-xx-zz-dr-arc-101-c02.png','pdf',8813204,'sha256:e40a7c19d5382bf6',0,NULL,0,NULL,true,'2026-04-20 09:30:00+07','a0000000-0000-0000-0000-000000000022','VNPT-CA-2026-0490117','Bản vẽ hoàn công kiến trúc — đã niêm phong lưu trữ khi kết thúc dự án.',false,NULL,'a0000000-0000-0000-0000-000000000008','2026-04-18 08:00:00+07','2026-04-18 08:00:00+07','2026-04-28 16:40:00+07'),
('f3000000-0000-0000-0000-000000040201','f2000000-0000-0000-0000-000000000402',2,1,2,1,'C01',true,false,'SGC-HTH-XX-ZZ-CA-STR-088.xlsx','projects/sgc/archived/nhathau/sgc-hth-xx-zz-ca-str-088-c01.xlsx','projects/sgc/preview/sgc-hth-xx-zz-ca-str-088-c01.pdf','xlsx',1902330,'sha256:36bd8a04ce971f52',0,NULL,0,NULL,false,NULL,NULL,NULL,'Bảng khối lượng quyết toán phần kết cấu.',false,NULL,'a0000000-0000-0000-0000-000000000014','2026-04-18 09:00:00+07','2026-04-18 09:00:00+07','2026-04-28 16:40:00+07'),
('f3000000-0000-0000-0000-000000050101','f2000000-0000-0000-0000-000000000501',0,1,1,0,'P01.01',true,false,'HOA-KK-ARC-001.ifc','projects/hoa/wip/tvtk/hoa-kk-arc-001-v1.ifc',NULL,'ifc',21044880,'sha256:4901fa7e3c2d85b1',1,NULL,0,NULL,false,NULL,NULL,NULL,'Mô hình kiến trúc khối khám ngoại trú — vừa tải lên, chờ dịch.',false,NULL,'a0000000-0000-0000-0000-000000000011','2026-06-10 09:00:00+07','2026-06-10 09:00:00+07',NULL),
('f3000000-0000-0000-0000-000000050201','f2000000-0000-0000-0000-000000000502',0,1,1,0,'P01.01',true,false,'HOA-KN-ARC-002.pdf','projects/hoa/wip/tvtk/hoa-kn-arc-002-v1.pdf','projects/hoa/preview/hoa-kn-arc-002-v1.png','pdf',4477120,'sha256:b8027e5a1cd94f3a',0,NULL,0,NULL,false,NULL,NULL,NULL,'Mặt bằng khối nội trú tầng điển hình — đang chờ duyệt.',false,NULL,'a0000000-0000-0000-0000-000000000008','2026-06-24 14:10:00+07','2026-06-24 14:10:00+07',NULL),
('f3000000-0000-0000-0000-000000050301','f2000000-0000-0000-0000-000000000503',0,1,1,0,'P01.01',true,false,'HOA-KN-MEP-011.dwg','projects/hoa/wip/codien/hoa-kn-mep-011-v1.dwg','projects/hoa/preview/hoa-kn-mep-011-v1.pdf','dwg',9102554,'sha256:1f6c48b0937ea25d',0,NULL,0,NULL,false,NULL,NULL,NULL,'Sơ đồ nguyên lý khí y tế trung tâm khối nội trú.',false,NULL,'a0000000-0000-0000-0000-000000000019','2026-07-08 08:20:00+07','2026-07-08 08:20:00+07',NULL);

-- ---- Gan ban HIEN HANH cho tung tep (go khoa vong FK) -----------------------
UPDATE "FileItems" fi SET "CurrentVersionId" = v."Id"
FROM "FileVersionStates" v WHERE v."FileItemId" = fi."Id" AND v."IsCurrent" = true;

UPDATE "FileItems" fi SET "SignedVersionId" = v."Id"
FROM "FileVersionStates" v WHERE v."FileItemId" = fi."Id" AND v."IsSigned" = true;

-- ---- FILE PERMISSIONS: sinh theo quyen cua thu muc chua tep -----------------
INSERT INTO "FilePermissions" ("Id","FileItemId","ProjectParticipantId","CanView","CanEdit","CanApprove","Status")
SELECT md5('fperm' || fi."Id"::text || fp."ProjectParticipantId"::text)::uuid,
       fi."Id", fp."ProjectParticipantId", fp."CanView", fp."CanEdit", fp."CanApprove", 0
FROM "FileItems" fi
JOIN "FolderPermissions" fp ON fp."FolderId" = fi."FolderId";

-- ---- FILE LINKS: tep lien quan (ban ve <-> mo hinh) -------------------------
INSERT INTO "FileLinks" ("Id","FileItemId","LinkedFileItemId","CreatedByAccountId","CreatedAt") VALUES
('ff000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000102','f2000000-0000-0000-0000-000000000101','a0000000-0000-0000-0000-000000000009','2026-03-11 09:00:00+07'),
('ff000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000104','f2000000-0000-0000-0000-000000000103','a0000000-0000-0000-0000-000000000015','2026-03-21 09:00:00+07'),
('ff000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000105','f2000000-0000-0000-0000-000000000103','a0000000-0000-0000-0000-000000000016','2026-04-03 09:00:00+07'),
('ff000000-0000-0000-0000-000000000004','f2000000-0000-0000-0000-000000000203','f2000000-0000-0000-0000-000000000201','a0000000-0000-0000-0000-000000000018','2026-04-10 09:00:00+07');

-- ---- FILE NAMING METADATA: bam vet tung truong cua ten tep ------------------
INSERT INTO "FileNamingMetadata" ("Id","FileItemId","NamingConventionFieldId","SelectedValueId","Value","DisplayValue","CreatedAt","UpdatedAt") VALUES
('fe000000-0000-0000-0000-000000010101','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000101','fc000000-0000-0000-0000-000000010101','RIV','Riverside Tower','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010102','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000102','fc000000-0000-0000-0000-000000010201','PNA','TVTK Phương Nam','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010103','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000103','fc000000-0000-0000-0000-000000010301','TA','Tháp A','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010104','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000104','fc000000-0000-0000-0000-000000010401','ZZ','Áp dụng mọi tầng','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010105','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000105','fc000000-0000-0000-0000-000000010501','M3','Mô hình 3D','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010106','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000106','fc000000-0000-0000-0000-000000010601','ARC','Kiến trúc','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010107','f2000000-0000-0000-0000-000000000101','fb000000-0000-0000-0000-000000000107',NULL,'001','001','2026-03-02 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010201','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000101','fc000000-0000-0000-0000-000000010101','RIV','Riverside Tower','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010202','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000102','fc000000-0000-0000-0000-000000010201','PNA','TVTK Phương Nam','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010203','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000103','fc000000-0000-0000-0000-000000010301','TA','Tháp A','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010204','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000104','fc000000-0000-0000-0000-000000010403','01','Tầng 1','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010205','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000105','fc000000-0000-0000-0000-000000010502','DR','Bản vẽ','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010206','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000106','fc000000-0000-0000-0000-000000010601','ARC','Kiến trúc','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000010207','f2000000-0000-0000-0000-000000000102','fb000000-0000-0000-0000-000000000107',NULL,'014','014','2026-03-10 10:00:00+07',NULL),
('fe000000-0000-0000-0000-000000020101','f2000000-0000-0000-0000-000000000201','fb000000-0000-0000-0000-000000000201','fc000000-0000-0000-0000-000000020101','CAT','Cầu vượt Cát Lái','2026-03-12 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000020102','f2000000-0000-0000-0000-000000000201','fb000000-0000-0000-0000-000000000202',NULL,'NHIP','Nhịp dầm','2026-03-12 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000020103','f2000000-0000-0000-0000-000000000201','fb000000-0000-0000-0000-000000000203','fc000000-0000-0000-0000-000000020301','STR','Kết cấu','2026-03-12 09:00:00+07',NULL),
('fe000000-0000-0000-0000-000000020104','f2000000-0000-0000-0000-000000000201','fb000000-0000-0000-0000-000000000204',NULL,'001','001','2026-03-12 09:00:00+07',NULL);

-- ---- Vi tri dau ky tren PDF -------------------------------------------------
INSERT INTO "FileSignaturePositions" ("Id","FileItemId","PageNumber","X","Y","Width","Height","CreatedBy","CreatedAt","UpdatedAt") VALUES
('f9000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000104',1,0.7200,0.8400,0.2000,0.0900,'a0000000-0000-0000-0000-000000000022','2026-04-22 09:10:00+07',NULL),
('f9000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000202',1,0.6800,0.8200,0.2200,0.1000,'a0000000-0000-0000-0000-000000000024','2026-05-12 08:30:00+07',NULL),
('f9000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000401',1,0.7000,0.8600,0.2000,0.0900,'a0000000-0000-0000-0000-000000000022','2026-04-18 08:30:00+07',NULL);

-- ============================================================================
-- 18) APPROVAL REQUESTS  Status: Pending=0, Approved=1, Rejected=2
--     FromZone/TargetZone dung CdeArea (Wip=0, Shared=1, Published=2, Archived=3)
-- ============================================================================
INSERT INTO "ApprovalRequests" ("Id","FileItemId","RequestedBy","ApproverId","FromZone","TargetZone","RequiresSignature","Status","RejectReason","CreatedAt","ApprovedAt") VALUES
('aa000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000102','a0000000-0000-0000-0000-000000000009','a0000000-0000-0000-0000-000000000008',0,1,false,0,NULL,'2026-03-12 09:00:00+07',NULL),
('aa000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000015','a0000000-0000-0000-0000-000000000022',1,2,true,1,NULL,'2026-04-22 09:00:00+07','2026-04-24 15:10:00+07'),
('aa000000-0000-0000-0000-000000000003','f2000000-0000-0000-0000-000000000106','a0000000-0000-0000-0000-000000000023','a0000000-0000-0000-0000-000000000022',0,1,false,2,'Bản vẽ hoàn công thiếu chữ ký chủ trì bộ môn và chưa cập nhật cao độ thực tế tầng 5. Đề nghị bổ sung rồi trình lại.','2026-04-19 09:00:00+07','2026-04-21 11:20:00+07'),
('aa000000-0000-0000-0000-000000000004','f2000000-0000-0000-0000-000000000203','a0000000-0000-0000-0000-000000000018','a0000000-0000-0000-0000-000000000017',0,1,false,0,NULL,'2026-04-11 08:00:00+07',NULL),
('aa000000-0000-0000-0000-000000000005','f2000000-0000-0000-0000-000000000202','a0000000-0000-0000-0000-000000000011','a0000000-0000-0000-0000-000000000024',1,2,true,1,NULL,'2026-05-12 08:00:00+07','2026-05-14 10:40:00+07'),
('aa000000-0000-0000-0000-000000000006','f2000000-0000-0000-0000-000000000302','a0000000-0000-0000-0000-000000000017','a0000000-0000-0000-0000-000000000012',0,1,false,0,NULL,'2026-05-04 09:00:00+07',NULL),
('aa000000-0000-0000-0000-000000000007','f2000000-0000-0000-0000-000000000502','a0000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000007',0,1,false,0,NULL,'2026-06-26 09:00:00+07',NULL),
('aa000000-0000-0000-0000-000000000008','f2000000-0000-0000-0000-000000000401','a0000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000022',1,2,true,1,NULL,'2026-04-18 08:00:00+07','2026-04-20 09:30:00+07');

INSERT INTO "ApprovalRequestSigners" ("Id","ApprovalRequestId","SignerAccountId","SignerGroupId","Status","SignedAt","CertificateSerial") VALUES
('ad000000-0000-0000-0000-000000000001','aa000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000022','c0000000-0000-0000-0000-000000000105',1,'2026-04-24 15:10:00+07','VNPT-CA-2026-0447281'),
('ad000000-0000-0000-0000-000000000002','aa000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000024','c0000000-0000-0000-0000-000000000205',1,'2026-05-14 10:40:00+07','VNPT-CA-2026-0518902'),
('ad000000-0000-0000-0000-000000000003','aa000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000022','c0000000-0000-0000-0000-000000000405',1,'2026-04-20 09:30:00+07','VNPT-CA-2026-0490117'),
('ad000000-0000-0000-0000-000000000004','aa000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000008','c0000000-0000-0000-0000-000000000103',0,NULL,NULL);

INSERT INTO "ApprovalSignatureTransactions" ("Id","ApprovalRequestId","FileItemId","TransactionId","CertificateSerial","SignedBy","SignedAt","Status","HashAlgorithm","PreparedPdfStoragePath","CreatedAt","UpdatedAt") VALUES
('ab000000-0000-0000-0000-000000000001','aa000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000104','SMARTCA-TXN-20260424-100472','VNPT-CA-2026-0447281','a0000000-0000-0000-0000-000000000022','2026-04-24 15:10:00+07',1,'SHA-256','projects/riv/prepared/riv-hth-ta-b1-dr-str-021-prepared.pdf','2026-04-24 15:02:00+07','2026-04-24 15:10:00+07'),
('ab000000-0000-0000-0000-000000000002','aa000000-0000-0000-0000-000000000005','f2000000-0000-0000-0000-000000000202','SMARTCA-TXN-20260514-100518','VNPT-CA-2026-0518902','a0000000-0000-0000-0000-000000000024','2026-05-14 10:40:00+07',1,'SHA-256','projects/cat/prepared/cat-motru-geo-002-prepared.pdf','2026-05-14 10:33:00+07','2026-05-14 10:40:00+07');

-- ---- Yeu cau tra tep ve WIP -------------------------------------------------
INSERT INTO "ZoneReturnRequests" ("Id","FileItemId","IssueId","FromZone","TargetZone","RequestedBy","ApprovedBy","Status","Reason","RejectReason","CreatedAt","DecidedAt") VALUES
('ac000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000104',NULL,2,0,'a0000000-0000-0000-0000-000000000015','a0000000-0000-0000-0000-000000000002',0,'Phát hiện sai cao độ đáy đài móng M12 so với hồ sơ khảo sát, cần chỉnh lại bản vẽ biện pháp.',NULL,'2026-06-18 09:00:00+07',NULL),
('ac000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000202',NULL,2,0,'a0000000-0000-0000-0000-000000000011','a0000000-0000-0000-0000-000000000002',2,'Đề nghị mở lại để bổ sung phụ lục hố khoan HK-07.','Hồ sơ đã phát hành và ký số; phụ lục bổ sung lập thành tài liệu mới, không mở lại bản đã ký.','2026-06-02 10:00:00+07','2026-06-04 08:30:00+07');

-- ============================================================================
-- 19) MARKUP — ghi chu tren tep (toa do CHUAN HOA 0..1)
--     Status: 0=Open, 1=Resolved (Domain/Enum/Markup)
-- ============================================================================
INSERT INTO "MarkupSets" ("Id","FileItemId","FileVersionId","Title","Status","IssueId","SnapshotStoragePath","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('f6000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000102','f3000000-0000-0000-0000-000000010201','Góp ý mặt bằng tầng 1 tháp A',0,NULL,NULL,'a0000000-0000-0000-0000-000000000008','2026-03-13 10:00:00+07',NULL),
('f6000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000106','f3000000-0000-0000-0000-000000010601','Điểm cần sửa trước khi trình lại',0,NULL,NULL,'a0000000-0000-0000-0000-000000000022','2026-04-20 14:00:00+07',NULL);

INSERT INTO "FileNotes" ("Id","MarkupSetId","FileVersionId","PageNumber","MarkupType","CoordinateJson","StyleJson","Content","ViewpointStateJson","MarkupSvg","ThumbnailDataUrl","Status","AuthorAccountId","CreatedAt","UpdatedAt") VALUES
('f5000000-0000-0000-0000-000000000001','f6000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000010201',1,0,'{"x":0.312,"y":0.448}','{"color":"#d94b3a","width":2}','Cửa thoát hiểm trục C-4 mở sai chiều, phải mở theo hướng thoát nạn.',NULL,NULL,NULL,0,'a0000000-0000-0000-0000-000000000008','2026-03-13 10:05:00+07',NULL),
('f5000000-0000-0000-0000-000000000002','f6000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000010201',1,1,'{"x1":0.520,"y1":0.300,"x2":0.680,"y2":0.412}','{"color":"#e0a83b","width":2}','Khu vực sảnh thang máy chưa khớp mô hình kiến trúc bản P01.02.',NULL,NULL,NULL,0,'a0000000-0000-0000-0000-000000000012','2026-03-13 10:12:00+07',NULL),
('f5000000-0000-0000-0000-000000000003','f6000000-0000-0000-0000-000000000002','f3000000-0000-0000-0000-000000010601',1,0,'{"x":0.664,"y":0.802}','{"color":"#d94b3a","width":2}','Thiếu ô chữ ký chủ trì bộ môn kiến trúc ở khung tên.',NULL,NULL,NULL,0,'a0000000-0000-0000-0000-000000000022','2026-04-20 14:05:00+07',NULL);

-- ============================================================================
-- 20) DISCUSSIONS  ScopeType: Standalone=0, File=1, Note=2, Submittal=3, Issue=4
--                  Status: Open=0, Resolved=1, Closed=2
-- ============================================================================
INSERT INTO "Discussions" ("Id","ProjectId","Title","ScopeType","ScopeId","Status","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
('20000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','Thống nhất cao độ sàn tầng điển hình tháp A',1,'f2000000-0000-0000-0000-000000000101',0,'a0000000-0000-0000-0000-000000000008','2026-03-16 09:00:00+07',NULL),
('20000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001','Biện pháp thi công tầng hầm B1 trong mùa mưa',0,NULL,1,'a0000000-0000-0000-0000-000000000015','2026-04-05 08:00:00+07','2026-04-30 17:00:00+07'),
('20000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000002','Trình tự lắp dựng nhịp thép ban đêm',0,NULL,0,'a0000000-0000-0000-0000-000000000017','2026-04-20 09:00:00+07',NULL);

INSERT INTO "DiscussionMessages" ("Id","DiscussionId","Content","AuthorAccountId","IsSolution","ReplyToMessageId","CreatedAt","RecalledAt") VALUES
('21000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','Mô hình bản P01.02 đang lấy cao độ sàn hoàn thiện +3.600 cho tầng điển hình, nhưng bản vẽ kết cấu ghi +3.550. Nhờ bên kết cấu xác nhận lại.','a0000000-0000-0000-0000-000000000008',false,NULL,'2026-03-16 09:05:00+07',NULL),
('21000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000001','Cao độ +3.550 là cao độ mặt sàn kết cấu, chưa gồm 50mm lớp hoàn thiện. Hai con số không mâu thuẫn.','a0000000-0000-0000-0000-000000000010',true,'21000000-0000-0000-0000-000000000001','2026-03-16 10:30:00+07',NULL),
('21000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000001','Vậy tôi sẽ ghi chú rõ hai mốc cao độ trong khung tên bản vẽ để tránh hiểu nhầm ở các bộ môn khác.','a0000000-0000-0000-0000-000000000009',false,'21000000-0000-0000-0000-000000000002','2026-03-16 11:00:00+07',NULL),
('21000000-0000-0000-0000-000000000004','20000000-0000-0000-0000-000000000002','Đề nghị bổ sung hệ bơm hạ mực nước ngầm dự phòng, tháng 6–9 lượng mưa khu vực này rất lớn.','a0000000-0000-0000-0000-000000000022',false,NULL,'2026-04-05 08:20:00+07',NULL),
('21000000-0000-0000-0000-000000000005','20000000-0000-0000-0000-000000000002','Đã bổ sung 2 bơm dự phòng công suất 45m3/h và rãnh thu nước quanh hố móng vào biện pháp thi công bản P01.02.','a0000000-0000-0000-0000-000000000015',true,'21000000-0000-0000-0000-000000000004','2026-04-28 16:00:00+07',NULL),
('21000000-0000-0000-0000-000000000006','20000000-0000-0000-0000-000000000003','Sở GTVT chỉ cấp phép chặn làn từ 22h đến 4h. Cần chia thành 3 đêm lắp dựng cho 8 nhịp.','a0000000-0000-0000-0000-000000000017',false,NULL,'2026-04-20 09:10:00+07',NULL),
('21000000-0000-0000-0000-000000000007','20000000-0000-0000-0000-000000000003','Tin nhắn đã được thu hồi.','a0000000-0000-0000-0000-000000000018',false,NULL,'2026-04-20 09:30:00+07','2026-04-20 09:32:00+07');

INSERT INTO "MessageMentions" ("Id","DiscussionMessageId","MentionedAccountId") VALUES
('22000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000010'),
('22000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000015');

INSERT INTO "MessageAttachments" ("Id","DiscussionMessageId","Type","FileVersionId","FolderId","Url") VALUES
('23000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000002',0,'f3000000-0000-0000-0000-000000010301',NULL,NULL);

-- ============================================================================
-- 21) ISSUES  Type: Issue=0, Rfi=1 | Status: Open=0, InProgress=1, Answered=2, Closed=3
--             Priority: Low=0, Medium=1, High=2, Critical=3
-- ============================================================================
INSERT INTO "Issues" ("Id","ProjectId","Type","Title","Description","Status","Priority","RaisedByAccountId","AssignedToAccountId","AssignedToGroupId","AssignedToOrganizationId","DueDate","LinkedFileItemId","ModelLocationJson","CreatedAt","UpdatedAt") VALUES
('30000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001',0,'Va chạm ống gió D800 với dầm chính trục B-5','Ống gió chính D800 tầng 5 cắt qua dầm bê tông cao 700mm tại trục B-5. Cần điều chỉnh cao độ ống hoặc bố trí lỗ mở có gia cường.',1,3,'a0000000-0000-0000-0000-000000000012','a0000000-0000-0000-0000-000000000010','c0000000-0000-0000-0000-000000000103',NULL,'2026-08-30 00:00:00+07','f2000000-0000-0000-0000-000000000101','{"urn":"urn:adsk.objects:os.object:cde-riv/riv-arc-001-v2.ifc","dbId":184203,"position":{"x":42.18,"y":-11.66,"z":18.40}}','2026-06-12 09:00:00+07','2026-07-02 15:20:00+07'),
('30000000-0000-0000-0000-000000000002','d0000000-0000-0000-0000-000000000001',1,'Đề nghị làm rõ chủng loại kính mặt đứng tháp A','Hồ sơ thiết kế ghi kính hộp Low-E 8+12A+8 nhưng chỉ dẫn kỹ thuật ghi 6+12A+6. Đề nghị chủ đầu tư xác nhận chủng loại áp dụng.',2,2,'a0000000-0000-0000-0000-000000000015','a0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000101',NULL,'2026-07-31 00:00:00+07',NULL,NULL,'2026-06-20 10:30:00+07','2026-07-10 09:00:00+07'),
('30000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001',0,'Bản vẽ hoàn công tầng 5 thiếu chữ ký chủ trì','Hồ sơ trình duyệt bị trả lại do khung tên chưa có chữ ký chủ trì bộ môn kiến trúc.',0,1,'a0000000-0000-0000-0000-000000000022','a0000000-0000-0000-0000-000000000023','c0000000-0000-0000-0000-000000000105',NULL,'2026-08-15 00:00:00+07','f2000000-0000-0000-0000-000000000106',NULL,'2026-04-21 11:30:00+07',NULL),
('30000000-0000-0000-0000-000000000004','d0000000-0000-0000-0000-000000000002',0,'Sai lệch cao độ đỉnh mố M1 so với hồ sơ khảo sát','Kết quả trắc đạc thực tế đỉnh mố M1 chênh +38mm so với bản vẽ. Cần đánh giá ảnh hưởng tới cao độ gối cầu.',1,2,'a0000000-0000-0000-0000-000000000024','a0000000-0000-0000-0000-000000000018','c0000000-0000-0000-0000-000000000204',NULL,'2026-08-20 00:00:00+07','f2000000-0000-0000-0000-000000000202',NULL,'2026-06-28 08:00:00+07','2026-07-05 10:00:00+07'),
('30000000-0000-0000-0000-000000000005','d0000000-0000-0000-0000-000000000003',1,'Xác nhận công suất máy thổi khí bể sinh học số 3','Đề nghị làm rõ công suất thiết kế máy thổi khí bể số 3 để đặt hàng thiết bị đúng thông số.',0,1,'a0000000-0000-0000-0000-000000000020','a0000000-0000-0000-0000-000000000012','c0000000-0000-0000-0000-000000000303',NULL,'2026-09-10 00:00:00+07',NULL,NULL,'2026-07-15 09:00:00+07',NULL),
('30000000-0000-0000-0000-000000000006','d0000000-0000-0000-0000-000000000004',0,'Thấm trần tầng hầm B1 khu vực trục D-7','Xuất hiện vệt thấm trần hầm B1 sau mưa lớn. Đã xử lý bơm keo và nghiệm thu lại.',3,2,'a0000000-0000-0000-0000-000000000028','a0000000-0000-0000-0000-000000000014','c0000000-0000-0000-0000-000000000404',NULL,'2026-03-31 00:00:00+07',NULL,NULL,'2026-02-10 14:00:00+07','2026-03-28 16:00:00+07'),
('30000000-0000-0000-0000-000000000007','d0000000-0000-0000-0000-000000000005',0,'Khoảng thông thủy hành lang khối nội trú chưa đạt','Hành lang khối nội trú tầng 4 rộng 2.100mm, chưa đạt yêu cầu tối thiểu 2.400mm cho giường bệnh di chuyển.',0,3,'a0000000-0000-0000-0000-000000000007','a0000000-0000-0000-0000-000000000008','c0000000-0000-0000-0000-000000000503',NULL,'2026-08-25 00:00:00+07','f2000000-0000-0000-0000-000000000502',NULL,'2026-07-20 10:00:00+07',NULL);

INSERT INTO "IssueMentions" ("Id","IssueId","MentionedAccountId") VALUES
('32000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000019'),
('32000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000008'),
('32000000-0000-0000-0000-000000000003','30000000-0000-0000-0000-000000000007','a0000000-0000-0000-0000-000000000011');

INSERT INTO "IssueAttachments" ("Id","IssueId","FileVersionId","Url") VALUES
('33000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','f3000000-0000-0000-0000-000000010102',NULL),
('33000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000003','f3000000-0000-0000-0000-000000010601',NULL);

-- ---- Cap quyen xem tep tam thoi cho nguoi ngoai nhom (theo issue / phe duyet)
INSERT INTO "IssueFileViewGrants" ("Id","IssueId","FileItemId","AccountId","Status","CreatedAt") VALUES
('34000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000101','a0000000-0000-0000-0000-000000000019',0,'2026-06-12 09:05:00+07');

INSERT INTO "FileViewGrants" ("Id","FileItemId","AccountId","SourceApprovalRequestId","Status","CreatedAt") VALUES
('f8000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000104','a0000000-0000-0000-0000-000000000022','aa000000-0000-0000-0000-000000000002',0,'2026-04-22 09:01:00+07'),
('f8000000-0000-0000-0000-000000000002','f2000000-0000-0000-0000-000000000202','a0000000-0000-0000-0000-000000000024','aa000000-0000-0000-0000-000000000005',0,'2026-05-12 08:01:00+07');

-- ============================================================================
-- 22) NOTIFICATIONS — hop chuong thong bao
-- ============================================================================
INSERT INTO "Notifications" ("Id","AccountId","Message","SenderName","IsRead","IsEmailSent","LinkType","LinkId","SendAt") VALUES
('60000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000008','Phạm Thị Lan đã trình duyệt tệp RIV-PNA-TA-01-DR-ARC-014.pdf sang vùng Shared.','Phạm Thị Lan',false,true,'Approval','aa000000-0000-0000-0000-000000000001','2026-03-12 09:01:00+07'),
('60000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000015','Đỗ Mạnh Cường đã phê duyệt và ký số tệp RIV-HTH-TA-B1-DR-STR-021.pdf.','Đỗ Mạnh Cường',true,true,'Approval','aa000000-0000-0000-0000-000000000002','2026-04-24 15:11:00+07'),
('60000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000023','Hồ sơ RIV-BHA-TA-05-DR-ARC-047.pdf bị trả lại: thiếu chữ ký chủ trì bộ môn.','Đỗ Mạnh Cường',false,true,'Approval','aa000000-0000-0000-0000-000000000003','2026-04-21 11:21:00+07'),
('60000000-0000-0000-0000-000000000004','a0000000-0000-0000-0000-000000000010','Nguyễn Tuấn Kiệt đã giao cho bạn vấn đề "Va chạm ống gió D800 với dầm chính trục B-5".','Nguyễn Tuấn Kiệt',false,false,'Issue','30000000-0000-0000-0000-000000000001','2026-06-12 09:02:00+07'),
('60000000-0000-0000-0000-000000000005','a0000000-0000-0000-0000-000000000026','Bạn được mời tham gia nhóm Nhà thầu thi công dự án Khu phức hợp căn hộ Riverside Tower.','Trần Thị Hoa',false,true,'Invitation','d4000000-0000-0000-0000-000000000001','2026-08-01 09:01:00+07'),
('60000000-0000-0000-0000-000000000006','a0000000-0000-0000-0000-000000000013','Bạn được mời tham gia nhóm Tư vấn thiết kế dự án Bệnh viện Đa khoa Hòa An.','Trần Thị Hoa',false,true,'Invitation','d4000000-0000-0000-0000-000000000003','2026-08-05 09:01:00+07'),
('60000000-0000-0000-0000-000000000007','a0000000-0000-0000-0000-000000000002','Vũ Văn Bình yêu cầu trả tệp RIV-HTH-TA-B1-DR-STR-021.pdf về vùng WIP.','Vũ Văn Bình',false,true,'ZoneReturn','ac000000-0000-0000-0000-000000000001','2026-06-18 09:01:00+07'),
('60000000-0000-0000-0000-000000000008','a0000000-0000-0000-0000-000000000008','Lê Hoàng Nam đã trả lời trong thảo luận "Thống nhất cao độ sàn tầng điển hình tháp A".','Vũ Đình Hải',true,false,'Discussion','20000000-0000-0000-0000-000000000001','2026-03-16 10:31:00+07');

-- ============================================================================
-- 23) REFRESH TOKENS — 1 con hieu luc, 1 da thu hoi (test luong refresh)
-- ============================================================================
INSERT INTO "RefreshTokens" ("Id","AccountId","Token","CreatedAt","ExpiresAt","RevokedAt","ReplacedByToken") VALUES
('80000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000002','rt-demo-hoa-active-4f19c07a','2026-08-10 08:00:00+07','2026-09-09 08:00:00+07',NULL,NULL),
('80000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002','rt-demo-hoa-revoked-91ba3c2d','2026-07-05 08:00:00+07','2026-08-04 08:00:00+07','2026-08-10 08:00:00+07','rt-demo-hoa-active-4f19c07a');

-- ============================================================================
-- 24) AUDIT LOGS
--     Action : Create=0 Update=1 Delete=2 Move=3 Submit=4 Verify=5 Approve=6
--              Reject=7 Download=8 PermissionChange=9 Upload=10 NewVersion=11
--              Sign=12 ZoneTransfer=13 ReturnRequest=14 Invite=15 AcceptInvite=16
--              RejectInvite=17 Assign=18 StatusChange=19 Archive=20
--              (CHI duoc THEM vao CUOI enum — da luu dang so trong DB.)
--     Scope  : System=0, Project=1, Group=2
-- ============================================================================
INSERT INTO "AuditLogs" ("Id","Scope","ProjectId","GroupId","FolderId","ActorAccountId","Action","EntityType","EntityId","Detail","CreatedAt") VALUES
('90000000-0000-0000-0000-000000000001',0,NULL,NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Account','a0000000-0000-0000-0000-000000000026','Tạo tài khoản Chu Thị Ngọc (ngoc.supply@cde.vn) thuộc VLXD Nam Tiến.','2026-01-07 08:10:00+07'),
('90000000-0000-0000-0000-000000000002',0,NULL,NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Organization','b0000000-0000-0000-0000-000000000015','Tạo liên danh Hà Thành – Trường Thịnh gồm 2 thành viên.','2026-01-08 08:05:00+07'),
('90000000-0000-0000-0000-000000000003',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Project','d0000000-0000-0000-0000-000000000001','Tạo dự án Khu phức hợp căn hộ Riverside Tower kèm 5 nhóm mặc định.','2026-01-15 08:00:00+07'),
('90000000-0000-0000-0000-000000000004',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000001',18,'Project','d0000000-0000-0000-0000-000000000001','Gán Trần Thị Hoa làm quản lý dự án.','2026-01-15 08:30:00+07'),
('90000000-0000-0000-0000-000000000005',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000009',10,'FileItem','f2000000-0000-0000-0000-000000000101','Tải lên tệp RIV-PNA-TA-ZZ-M3-ARC-001.ifc vào ô Tư vấn thiết kế (WIP).','2026-03-02 09:00:00+07'),
('90000000-0000-0000-0000-000000000006',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000009',11,'FileItem','f2000000-0000-0000-0000-000000000101','Tạo phiên bản P01.02 cho RIV-PNA-TA-ZZ-M3-ARC-001.ifc.','2026-04-06 10:20:00+07'),
('90000000-0000-0000-0000-000000000007',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000009',4,'ApprovalRequest','aa000000-0000-0000-0000-000000000001','Trình duyệt RIV-PNA-TA-01-DR-ARC-014.pdf từ WIP sang Shared.','2026-03-12 09:00:00+07'),
('90000000-0000-0000-0000-000000000008',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000022',6,'ApprovalRequest','aa000000-0000-0000-0000-000000000002','Phê duyệt RIV-HTH-TA-B1-DR-STR-021.pdf sang vùng Published.','2026-04-24 15:05:00+07'),
('90000000-0000-0000-0000-000000000009',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000022',12,'FileItem','f2000000-0000-0000-0000-000000000104','Ký số bản C01 bằng chứng thư VNPT-CA-2026-0447281.','2026-04-24 15:10:00+07'),
('90000000-0000-0000-0000-000000000010',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000022',7,'ApprovalRequest','aa000000-0000-0000-0000-000000000003','Từ chối RIV-BHA-TA-05-DR-ARC-047.pdf: thiếu chữ ký chủ trì bộ môn.','2026-04-21 11:20:00+07'),
('90000000-0000-0000-0000-000000000011',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000015',14,'ZoneReturnRequest','ac000000-0000-0000-0000-000000000001','Yêu cầu trả RIV-HTH-TA-B1-DR-STR-021.pdf về WIP để chỉnh cao độ đài móng.','2026-06-18 09:00:00+07'),
('90000000-0000-0000-0000-000000000012',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000002',15,'ProjectInvitation','d4000000-0000-0000-0000-000000000001','Mời Chu Thị Ngọc vào nhóm Nhà thầu thi công.','2026-08-01 09:00:00+07'),
('90000000-0000-0000-0000-000000000013',1,'d0000000-0000-0000-0000-000000000001',NULL,NULL,'a0000000-0000-0000-0000-000000000023',16,'ProjectInvitation','d4000000-0000-0000-0000-000000000002','Chấp nhận lời mời vào nhóm Tư vấn giám sát.','2026-01-19 10:15:00+07'),
('90000000-0000-0000-0000-000000000014',2,'d0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000104',NULL,'a0000000-0000-0000-0000-000000000014',2,'GroupMember','c1000000-0000-0000-0000-000000009901','Xóa Hồ Văn Lộc khỏi nhóm Nhà thầu thi công.','2026-05-14 10:00:00+07'),
('90000000-0000-0000-0000-000000000015',1,'d0000000-0000-0000-0000-000000000002',NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Project','d0000000-0000-0000-0000-000000000002','Tạo dự án Cầu vượt nút giao Cát Lái.','2026-02-02 08:00:00+07'),
('90000000-0000-0000-0000-000000000016',1,'d0000000-0000-0000-0000-000000000002',NULL,NULL,'a0000000-0000-0000-0000-000000000024',12,'FileItem','f2000000-0000-0000-0000-000000000202','Ký số bản C01 báo cáo khảo sát địa chất mố trụ.','2026-05-14 10:40:00+07'),
('90000000-0000-0000-0000-000000000017',1,'d0000000-0000-0000-0000-000000000003',NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Project','d0000000-0000-0000-0000-000000000003','Tạo dự án Nhà máy xử lý nước thải Bình Hưng giai đoạn 2.','2026-03-04 08:00:00+07'),
('90000000-0000-0000-0000-000000000018',1,'d0000000-0000-0000-0000-000000000004',NULL,NULL,'a0000000-0000-0000-0000-000000000002',20,'FileItem','f2000000-0000-0000-0000-000000000401','Niêm phong lưu trữ bản vẽ hoàn công kiến trúc khi kết thúc dự án.','2026-04-28 16:40:00+07'),
('90000000-0000-0000-0000-000000000019',1,'d0000000-0000-0000-0000-000000000004',NULL,NULL,'a0000000-0000-0000-0000-000000000002',19,'Project','d0000000-0000-0000-0000-000000000004','Chuyển trạng thái dự án Sài Gòn Center sang Hoàn thành.','2026-04-28 16:45:00+07'),
('90000000-0000-0000-0000-000000000020',1,'d0000000-0000-0000-0000-000000000005',NULL,NULL,'a0000000-0000-0000-0000-000000000001',0,'Project','d0000000-0000-0000-0000-000000000005','Tạo dự án Bệnh viện Đa khoa Hòa An.','2026-05-06 08:00:00+07'),
('90000000-0000-0000-0000-000000000021',1,'d0000000-0000-0000-0000-000000000005',NULL,NULL,'a0000000-0000-0000-0000-000000000019',10,'FileItem','f2000000-0000-0000-0000-000000000503','Tải lên sơ đồ nguyên lý khí y tế khối nội trú.','2026-07-08 08:20:00+07');

COMMIT;

-- ============================================================================
--  KIEM TRA NHANH SAU KHI CHAY
-- ----------------------------------------------------------------------------
--  SELECT 'Accounts', count(*) FROM "Accounts"
--  UNION ALL SELECT 'Organizations', count(*) FROM "Organizations"
--  UNION ALL SELECT 'Projects', count(*) FROM "Projects"
--  UNION ALL SELECT 'Folders', count(*) FROM "Folders"
--  UNION ALL SELECT 'FileItems', count(*) FROM "FileItems"
--  UNION ALL SELECT 'Issues', count(*) FROM "Issues";
--
--  Dang nhap thu : admin@cde.vn / password  (Admin — thay toan bo 5 du an)
--                  hoa.pm@cde.vn / password (PM ca 5 du an)
--                  nam.design@cde.vn / password (Leader Tu van thiet ke)
--                  binh.contractor@cde.vn / password (Leader Nha thau chinh)
--                  phong.viewer@cde.vn      (BI KHOA — de test man hinh chan)
-- ============================================================================

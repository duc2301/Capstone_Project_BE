-- ============================================================================
--  CDE System — SEED PERMISSION FOLDER (PostgreSQL / Npgsql, EF Core schema)
-- ----------------------------------------------------------------------------
--  Mục đích : dữ liệu test DÀNH RIÊNG cho flow api/folder-tree
--             (FolderTreeController / FolderTreeService / FolderTreeRepository).
--
--  CÁCH CHẠY :
--    psql -h localhost -U <user> -d <database> -f seed_permission_folder.sql
--    (hoặc dán toàn bộ vào pgAdmin / DBeaver Query Tool rồi Execute)
--
--  ĐẶC ĐIỂM :
--    • Standalone: KHÔNG phụ thuộc seed_data.sql, không TRUNCATE bảng nào.
--      Chỉ DELETE đúng các dòng của chính nó (UUID cố định) rồi INSERT lại
--      ⇒ idempotent, chạy lại nhiều lần luôn ra cùng kết quả.
--    • Chuỗi phân quyền: Account → GroupMember(Active) → Group
--      → ProjectParticipant(Active) → FolderPermission/FilePermission(Active).
--      Quyền của từng user cá nhân LUÔN suy từ ProjectParticipant của group.
--
--  TÀI KHOẢN (mật khẩu chung: "password"):
--    tree.pm@cde.vn      — Leader nhóm Ban QLDA (participant Role=ProjectAdmin)
--                          ⇒ bypass: thấy TOÀN BỘ cây + mọi file.
--    tree.user@cde.vn    — Member nhóm Kỹ sư (participant Role=Member)
--                          ⇒ user chính để test 6 case bên dưới.
--    tree.noperm@cde.vn  — Member nhóm Không-quyền (participant Active nhưng
--                          KHÔNG có dòng FolderPermission nào) + Member nhóm
--                          Đã-rời-dự-án (participant INACTIVE nhưng có CanView)
--                          ⇒ cây rỗng, mọi folder trả 403 (quyền của participant
--                          Inactive không được tính).
--    tree.left@cde.vn    — GroupMember Status=Left của nhóm Kỹ sư
--                          ⇒ cây rỗng, 403 (member đã rời không hưởng quyền nhóm).
--
--  DỰ ÁN : "Dự án Test Phân Quyền CDE"  Id = dd100000-...-0001
--
--  CÂY FOLDER & QUYỀN CỦA NHÓM KỸ SƯ (participant P1 — tree.user):
--    01-WIP        (F1, Wip)        CanView=true            → THẤY
--    ├─ Kiến trúc  (F2)             CanView=true            → THẤY
--    │    KT-01-MatBang.pdf   (A1)  FilePermission CanView=true
--    │    KT-02-MatDung.pdf   (A2)  FilePermission CanView=FALSE
--    │    KT-03-PhoiCanh.pdf  (A3)  KHÔNG có dòng FilePermission
--    ├─ Kết cấu    (F3)             CanView=true            → THẤY
--    │    KC-01-MongCoc.pdf   (B1)
--    └─ MEP        (F4)             KHÔNG có dòng quyền      → ẨN khỏi cây, files trả 403
--         MEP-01-SoDoDien.pdf (C1)  (file có CanView=true nhưng folder không view được)
--    02-Shared     (F5, Shared)     CanView=FALSE (row Active) → ẨN, files trả 403
--    └─ Báo cáo phối hợp (F6)       CanView=true             → THẤY (cha bị ẩn ⇒ nổi lên thành root)
--         PH-01-BaoCaoPhoiHop.pdf (D1)
--    03-Published  (F7, Published)  CanView=true nhưng Status=INACTIVE → ẨN, files trả 403
--         XB-01-HoSoPhatHanh.pdf (E1)
--
--  MAP 6 CASE CẦN TEST (đăng nhập tree.user@cde.vn):
--    1. User của nhóm CÓ quyền view FILE            → A1 (KT-01-MatBang.pdf)
--    2. User của nhóm CÓ quyền view FOLDER          → F1/F2/F3 xuất hiện trong cây,
--                                                     GET folders/F2/files trả 200
--    3. CÓ view folder nhưng KHÔNG view 1 số FILE   → F2 viewable, A2 CanView=false, A3 không có row
--    4. CÓ view folder nhưng KHÔNG view 1 số FOLDER → F1 viewable nhưng con F4 (MEP) bị ẩn
--    5. KHÔNG có quyền view FILE                    → A2 (row CanView=false), E1 (row Inactive)
--    6. KHÔNG có quyền view FOLDER                  → F4 (không row), F5 (CanView=false),
--                                                     F7 (row Inactive) — ẩn khỏi cây + 403
--
--  GỌI API (đăng nhập lấy JWT rồi gắn Bearer):
--    GET /api/folder-tree/tree?projectId=dd100000-0000-0000-0000-000000000001
--    GET /api/folder-tree/tree?projectId=dd100000-0000-0000-0000-000000000001&area=Wip
--    GET /api/folder-tree/folders/{folderId}/files
--
--  KẾT QUẢ MONG ĐỢI:
--    tree.user   : cây = [01-WIP > (Kiến trúc, Kết cấu)] + [Báo cáo phối hợp (root mồ côi)]
--                  files F2 → 200 (A1,A2,A3)   files F4/F5/F7 → 403
--    tree.pm     : cây đầy đủ 7 folder, mọi folder files → 200
--    tree.noperm : cây = []   mọi folder files → 403
--    tree.left   : cây = []   mọi folder files → 403
--    (Account hệ thống Role=Admin bất kỳ cũng bypass giống tree.pm.)
--
--  ⚠️ LƯU Ý: endpoint GET folders/{id}/files hiện chỉ check quyền View ở CẤP FOLDER;
--    dữ liệu FilePermissions (case 1/3/5) được seed sẵn để test khi bổ sung
--    lọc theo quyền file — hiện tại folder view được thì trả đủ A1,A2,A3.
--
--  Quy ước UUID (segment đầu — riêng cho file này, không đụng seed_data.sql):
--    aa1*=Accounts  bb1*=Organizations  cc1*=Groups  cc2*=GroupMembers
--    dd1*=Projects  dd3*=ProjectParticipants
--    ff1*=Folders   ff2*=FolderPermissions
--    ff3*=FileItems ff4*=FileVersions ff5*=FilePermissions
-- ============================================================================

BEGIN;

-- ============================================================================
-- 0) DỌN DỮ LIỆU CŨ CỦA CHÍNH SCRIPT NÀY (theo UUID cố định — không đụng data khác)
-- ============================================================================
DELETE FROM "FilePermissions"
 WHERE "FileItemId" IN (SELECT fi."Id" FROM "FileItems" fi
                        JOIN "Folders" f ON fi."FolderId" = f."Id"
                        WHERE f."ProjectId" = 'dd100000-0000-0000-0000-000000000001');
DELETE FROM "FileVersions"
 WHERE "FileItemId" IN (SELECT fi."Id" FROM "FileItems" fi
                        JOIN "Folders" f ON fi."FolderId" = f."Id"
                        WHERE f."ProjectId" = 'dd100000-0000-0000-0000-000000000001');
DELETE FROM "FileItems"
 WHERE "FolderId" IN (SELECT "Id" FROM "Folders"
                      WHERE "ProjectId" = 'dd100000-0000-0000-0000-000000000001');
DELETE FROM "FolderPermissions"
 WHERE "FolderId" IN (SELECT "Id" FROM "Folders"
                      WHERE "ProjectId" = 'dd100000-0000-0000-0000-000000000001');
-- Folder tự tham chiếu: xóa con trước, gốc sau
DELETE FROM "Folders"
 WHERE "ProjectId" = 'dd100000-0000-0000-0000-000000000001' AND "ParentFolderId" IS NOT NULL;
DELETE FROM "Folders"
 WHERE "ProjectId" = 'dd100000-0000-0000-0000-000000000001';
DELETE FROM "ProjectParticipants"
 WHERE "ProjectId" = 'dd100000-0000-0000-0000-000000000001';
DELETE FROM "Projects"
 WHERE "Id" = 'dd100000-0000-0000-0000-000000000001';
DELETE FROM "GroupMembers"
 WHERE "GroupId" IN ('cc100000-0000-0000-0000-000000000001',
                     'cc100000-0000-0000-0000-000000000002',
                     'cc100000-0000-0000-0000-000000000003',
                     'cc100000-0000-0000-0000-000000000004');
DELETE FROM "Groups"
 WHERE "Id" IN ('cc100000-0000-0000-0000-000000000001',
                'cc100000-0000-0000-0000-000000000002',
                'cc100000-0000-0000-0000-000000000003',
                'cc100000-0000-0000-0000-000000000004');
DELETE FROM "Organizations"
 WHERE "Id" = 'bb100000-0000-0000-0000-000000000001';
-- Dọn dấu vết đăng nhập/thông báo của các account test (nếu có sau các lần test trước)
DELETE FROM "RefreshTokens"
 WHERE "AccountId" IN ('aa100000-0000-0000-0000-000000000001',
                       'aa100000-0000-0000-0000-000000000002',
                       'aa100000-0000-0000-0000-000000000003',
                       'aa100000-0000-0000-0000-000000000004');
DELETE FROM "Notifications"
 WHERE "AccountId" IN ('aa100000-0000-0000-0000-000000000001',
                       'aa100000-0000-0000-0000-000000000002',
                       'aa100000-0000-0000-0000-000000000003',
                       'aa100000-0000-0000-0000-000000000004');
DELETE FROM "Accounts"
 WHERE "Id" IN ('aa100000-0000-0000-0000-000000000001',
                'aa100000-0000-0000-0000-000000000002',
                'aa100000-0000-0000-0000-000000000003',
                'aa100000-0000-0000-0000-000000000004');

-- ============================================================================
-- 1) ACCOUNTS   Role: Admin=0, User=1 | Status: Active=0
--    Mật khẩu chung "password" (BCrypt — cùng hash với seed_data.sql)
-- ============================================================================
INSERT INTO "Accounts" ("Id","UserName","Email","PasswordHash","Role","Status","ResetPasswordToken","ResetPasswordTokenExpiresAt","CreatedAt","UpdatedAt") VALUES
('aa100000-0000-0000-0000-000000000001','Trịnh Quang PM','tree.pm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-03-01 08:00:00+07',NULL),
('aa100000-0000-0000-0000-000000000002','Lý Văn User','tree.user@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-03-01 08:05:00+07',NULL),
('aa100000-0000-0000-0000-000000000003','Hồ Thị Không Quyền','tree.noperm@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-03-01 08:10:00+07',NULL),
('aa100000-0000-0000-0000-000000000004','Trương Văn Đã Rời','tree.left@cde.vn','$2a$11$7EPcFyGnHfBCSULBHTvop.rOh9nMvhLacXUe2lmAw5RTP36Ek11ke',1,0,NULL,NULL,'2026-03-01 08:15:00+07',NULL);

-- ============================================================================
-- 2) ORGANIZATION  (OrganizationTypeId = Consultant — migration đã seed sẵn)
-- ============================================================================
INSERT INTO "Organizations" ("Id","LegalName","DisplayName","TaxCode","Address","Email","Phone","OrganizationTypeId","CreatedAt","UpdatedAt") VALUES
('bb100000-0000-0000-0000-000000000001','Công ty TNHH Test Phân Quyền CDE','Test Phân Quyền CDE','0309998888','99 Đường Test, Quận 1, TP.HCM','tree.test@cde.vn','02838990099','d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5','2026-03-01 08:00:00+07',NULL);

-- ============================================================================
-- 3) GROUPS
-- ============================================================================
INSERT INTO "Groups" ("Id","Name","Description","OrganizationId","CreatedAt") VALUES
('cc100000-0000-0000-0000-000000000001','[Tree] Nhóm Kỹ sư','Nhóm test chính — quyền giới hạn theo FolderPermission','bb100000-0000-0000-0000-000000000001','2026-03-01 09:00:00+07'),
('cc100000-0000-0000-0000-000000000002','[Tree] Ban QLDA','Nhóm ProjectAdmin — bypass, thấy toàn bộ cây','bb100000-0000-0000-0000-000000000001','2026-03-01 09:00:00+07'),
('cc100000-0000-0000-0000-000000000003','[Tree] Nhóm Không Quyền','Participant Active nhưng không có dòng quyền nào','bb100000-0000-0000-0000-000000000001','2026-03-01 09:00:00+07'),
('cc100000-0000-0000-0000-000000000004','[Tree] Nhóm Đã Rời Dự Án','Participant INACTIVE — có CanView nhưng không được tính','bb100000-0000-0000-0000-000000000001','2026-03-01 09:00:00+07');

-- ============================================================================
-- 4) GROUP MEMBERS  Role: Member=0, Leader=1 | Status: Active=0, Left=1
-- ============================================================================
INSERT INTO "GroupMembers" ("Id","GroupId","AccountId","Role","Status","JoinedAt") VALUES
-- Ban QLDA: tree.pm (Leader, Active)
('cc200000-0000-0000-0000-000000000001','cc100000-0000-0000-0000-000000000002','aa100000-0000-0000-0000-000000000001',1,0,'2026-03-02 08:00:00+07'),
-- Nhóm Kỹ sư: tree.user (Member, Active)
('cc200000-0000-0000-0000-000000000002','cc100000-0000-0000-0000-000000000001','aa100000-0000-0000-0000-000000000002',0,0,'2026-03-02 08:05:00+07'),
-- Nhóm Kỹ sư: tree.left (Member, ĐÃ RỜI — không được hưởng quyền của nhóm)
('cc200000-0000-0000-0000-000000000003','cc100000-0000-0000-0000-000000000001','aa100000-0000-0000-0000-000000000004',0,1,'2026-03-02 08:10:00+07'),
-- Nhóm Không Quyền: tree.noperm (Member, Active)
('cc200000-0000-0000-0000-000000000004','cc100000-0000-0000-0000-000000000003','aa100000-0000-0000-0000-000000000003',0,0,'2026-03-02 08:15:00+07'),
-- Nhóm Đã Rời Dự Án: tree.noperm (Member, Active — nhưng participant Inactive)
('cc200000-0000-0000-0000-000000000005','cc100000-0000-0000-0000-000000000004','aa100000-0000-0000-0000-000000000003',0,0,'2026-03-02 08:20:00+07');

-- ============================================================================
-- 5) PROJECT  Status: Active=1 | Phase: Construction=2
-- ============================================================================
INSERT INTO "Projects" ("Id","ProjectName","ProjectDescription","Status","Phase","ManagerAccountId") VALUES
('dd100000-0000-0000-0000-000000000001','Dự án Test Phân Quyền CDE','Dự án riêng để test api/folder-tree: quyền View trên cây folder và files.',1,2,'aa100000-0000-0000-0000-000000000001');

-- ============================================================================
-- 6) PROJECT PARTICIPANTS  Role: ProjectAdmin=0, Member=1 | Status: Active=0, Inactive=1
--    P2 = Ban QLDA (ProjectAdmin) | P1 = Kỹ sư (nhóm test chính)
--    P3 = Không Quyền (Active, 0 dòng quyền) | P4 = Đã Rời (INACTIVE, có quyền nhưng vô hiệu)
-- ============================================================================
INSERT INTO "ProjectParticipants" ("Id","ProjectId","GroupId","Role","Status","JoinedAt") VALUES
('dd300000-0000-0000-0000-000000000001','dd100000-0000-0000-0000-000000000001','cc100000-0000-0000-0000-000000000002',0,0,'2026-03-03 08:00:00+07'),
('dd300000-0000-0000-0000-000000000002','dd100000-0000-0000-0000-000000000001','cc100000-0000-0000-0000-000000000001',1,0,'2026-03-03 08:05:00+07'),
('dd300000-0000-0000-0000-000000000003','dd100000-0000-0000-0000-000000000001','cc100000-0000-0000-0000-000000000003',1,0,'2026-03-03 08:10:00+07'),
('dd300000-0000-0000-0000-000000000004','dd100000-0000-0000-0000-000000000001','cc100000-0000-0000-0000-000000000004',1,1,'2026-03-03 08:15:00+07');

-- ============================================================================
-- 7) FOLDERS  Area(CdeArea): Wip=0, Shared=1, Published=2, Archived=3
--    (cây tự tham chiếu — chèn folder gốc trước, folder con sau)
-- ============================================================================
INSERT INTO "Folders" ("Id","ProjectId","Name","Area","ParentFolderId","IsTemplate","NamingConventionId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
-- F1: 01-WIP (root) — P1 CÓ view
('ff100000-0000-0000-0000-000000000001','dd100000-0000-0000-0000-000000000001','01-WIP',0,NULL,false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:00:00+07',NULL),
-- F5: 02-Shared (root) — P1 bị TỪ CHỐI view (row CanView=false)
('ff100000-0000-0000-0000-000000000005','dd100000-0000-0000-0000-000000000001','02-Shared',1,NULL,false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:00:00+07',NULL),
-- F7: 03-Published (root) — P1 có row CanView=true nhưng Status=Inactive ⇒ không tính
('ff100000-0000-0000-0000-000000000007','dd100000-0000-0000-0000-000000000001','03-Published',2,NULL,false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:00:00+07',NULL),
-- F2: Kiến trúc (con F1) — P1 CÓ view; chứa 3 file test quyền file
('ff100000-0000-0000-0000-000000000002','dd100000-0000-0000-0000-000000000001','Kiến trúc',0,'ff100000-0000-0000-0000-000000000001',false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:10:00+07',NULL),
-- F3: Kết cấu (con F1) — P1 CÓ view
('ff100000-0000-0000-0000-000000000003','dd100000-0000-0000-0000-000000000001','Kết cấu',0,'ff100000-0000-0000-0000-000000000001',false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:15:00+07',NULL),
-- F4: MEP (con F1) — P1 KHÔNG có dòng quyền ⇒ ẩn khỏi cây, files trả 403
('ff100000-0000-0000-0000-000000000004','dd100000-0000-0000-0000-000000000001','MEP',0,'ff100000-0000-0000-0000-000000000001',false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:20:00+07',NULL),
-- F6: Báo cáo phối hợp (con F5) — P1 CÓ view nhưng cha F5 bị ẩn ⇒ nổi lên thành root
('ff100000-0000-0000-0000-000000000006','dd100000-0000-0000-0000-000000000001','Báo cáo phối hợp',1,'ff100000-0000-0000-0000-000000000005',false,NULL,'aa100000-0000-0000-0000-000000000001','2026-03-04 08:25:00+07',NULL);

-- ============================================================================
-- 8) FOLDER PERMISSIONS  (6 cờ bool + Status: Active=0, Inactive=1)
--    Toàn bộ gán cho P1 (nhóm Kỹ sư — dd300000-...-0002), trừ dòng cuối cho P4.
--    P2 (Ban QLDA) KHÔNG cần dòng nào — ProjectAdmin bypass trong service.
--    P3 (Không Quyền) cố tình KHÔNG có dòng nào.
-- ============================================================================
INSERT INTO "FolderPermissions" ("Id","FolderId","ProjectParticipantId","CanView","CanEdit","CanUpdate","CanDownload","CanVerify","CanApprove","Status") VALUES
-- F1 01-WIP: view ✓
('ff200000-0000-0000-0000-000000000001','ff100000-0000-0000-0000-000000000001','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,0),
-- F2 Kiến trúc: view ✓ (case 2 — có quyền view folder)
('ff200000-0000-0000-0000-000000000002','ff100000-0000-0000-0000-000000000002','dd300000-0000-0000-0000-000000000002',true,true,true,true,false,false,0),
-- F3 Kết cấu: view ✓
('ff200000-0000-0000-0000-000000000003','ff100000-0000-0000-0000-000000000003','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,0),
-- (F4 MEP: KHÔNG có dòng — case 6, không quyền view folder)
-- F5 02-Shared: row Active nhưng CanView=false — case 6 (từ chối tường minh)
('ff200000-0000-0000-0000-000000000004','ff100000-0000-0000-0000-000000000005','dd300000-0000-0000-0000-000000000002',false,false,false,false,false,false,0),
-- F6 Báo cáo phối hợp: view ✓ (cha F5 ẩn ⇒ test node mồ côi nổi lên root)
('ff200000-0000-0000-0000-000000000005','ff100000-0000-0000-0000-000000000006','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,0),
-- F7 03-Published: CanView=true nhưng Status=INACTIVE ⇒ không được tính — case 6
('ff200000-0000-0000-0000-000000000006','ff100000-0000-0000-0000-000000000007','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,1),
-- F1 cho P4 (participant INACTIVE): CanView=true nhưng participant đã rời dự án
-- ⇒ tree.noperm vẫn KHÔNG thấy gì (test quyền phụ thuộc trạng thái participant)
('ff200000-0000-0000-0000-000000000007','ff100000-0000-0000-0000-000000000001','dd300000-0000-0000-0000-000000000004',true,true,true,true,true,true,0);

-- ============================================================================
-- 9) FILE ITEMS  FileType: Pdf=0 | Status(FileItemStatus): Draft=0, Approved=2
--    (CurrentVersionId là cột scalar — trỏ tới FileVersion chèn ở mục 10)
-- ============================================================================
INSERT INTO "FileItems" ("Id","FolderId","Name","FileType","Status","RequiresSignature","IsSigned","CurrentVersionId","SignedVersionId","CreatedByAccountId","CreatedAt","UpdatedAt") VALUES
-- A1 (F2 Kiến trúc): FilePermission CanView=true — case 1
('ff300000-0000-0000-0000-000000000001','ff100000-0000-0000-0000-000000000002','KT-01-MatBang.pdf',0,0,false,false,'ff400000-0000-0000-0000-000000000001',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:00:00+07',NULL),
-- A2 (F2 Kiến trúc): FilePermission CanView=FALSE — case 3/5
('ff300000-0000-0000-0000-000000000002','ff100000-0000-0000-0000-000000000002','KT-02-MatDung.pdf',0,0,false,false,'ff400000-0000-0000-0000-000000000002',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:05:00+07',NULL),
-- A3 (F2 Kiến trúc): KHÔNG có dòng FilePermission — case 3
('ff300000-0000-0000-0000-000000000003','ff100000-0000-0000-0000-000000000002','KT-03-PhoiCanh.pdf',0,0,false,false,'ff400000-0000-0000-0000-000000000003',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:10:00+07',NULL),
-- B1 (F3 Kết cấu): folder view được, không có quyền file riêng
('ff300000-0000-0000-0000-000000000004','ff100000-0000-0000-0000-000000000003','KC-01-MongCoc.pdf',0,0,false,false,'ff400000-0000-0000-0000-000000000004',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:15:00+07',NULL),
-- C1 (F4 MEP): file CÓ CanView=true nhưng folder KHÔNG view được ⇒ vẫn 403 khi vào folder
('ff300000-0000-0000-0000-000000000005','ff100000-0000-0000-0000-000000000004','MEP-01-SoDoDien.pdf',0,0,false,false,'ff400000-0000-0000-0000-000000000005',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:20:00+07',NULL),
-- D1 (F6 Báo cáo phối hợp)
('ff300000-0000-0000-0000-000000000006','ff100000-0000-0000-0000-000000000006','PH-01-BaoCaoPhoiHop.pdf',0,2,false,false,'ff400000-0000-0000-0000-000000000006',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:25:00+07',NULL),
-- E1 (F7 03-Published): FilePermission Status=Inactive — case 5
('ff300000-0000-0000-0000-000000000007','ff100000-0000-0000-0000-000000000007','XB-01-HoSoPhatHanh.pdf',0,2,false,false,'ff400000-0000-0000-0000-000000000007',NULL,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:30:00+07',NULL);

-- ============================================================================
-- 10) FILE VERSIONS  (mỗi file 1 version v1 — đủ để CurrentVersionId hợp lệ)
-- ============================================================================
INSERT INTO "FileVersions" ("Id","FileItemId","VersionNumber","StoragePath","Format","FileSizeBytes","Checksum","IsHidden","UploadedByAccountId","UploadedAt","ViewerUrn","PreviewStoragePath","ViewerStatus","ViewerProgress","ViewerError","IsSigned","SignedAt","SignedBy","CertificateSerial") VALUES
('ff400000-0000-0000-0000-000000000001','ff300000-0000-0000-0000-000000000001',1,'projects/tree-test/wip/kien-truc/kt-01-matbang-v1.pdf','pdf',1048576,'sha256:tree01',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:00:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000002','ff300000-0000-0000-0000-000000000002',1,'projects/tree-test/wip/kien-truc/kt-02-matdung-v1.pdf','pdf',1048576,'sha256:tree02',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:05:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000003','ff300000-0000-0000-0000-000000000003',1,'projects/tree-test/wip/kien-truc/kt-03-phoicanh-v1.pdf','pdf',1048576,'sha256:tree03',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:10:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000004','ff300000-0000-0000-0000-000000000004',1,'projects/tree-test/wip/ket-cau/kc-01-mongcoc-v1.pdf','pdf',2097152,'sha256:tree04',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:15:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000005','ff300000-0000-0000-0000-000000000005',1,'projects/tree-test/wip/mep/mep-01-sododien-v1.pdf','pdf',1572864,'sha256:tree05',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:20:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000006','ff300000-0000-0000-0000-000000000006',1,'projects/tree-test/shared/bao-cao-phoi-hop/ph-01-v1.pdf','pdf',786432,'sha256:tree06',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:25:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL),
('ff400000-0000-0000-0000-000000000007','ff300000-0000-0000-0000-000000000007',1,'projects/tree-test/published/xb-01-hosophathanh-v1.pdf','pdf',3145728,'sha256:tree07',false,'aa100000-0000-0000-0000-000000000001','2026-03-05 08:30:00+07',NULL,NULL,0,NULL,NULL,false,NULL,NULL,NULL);

-- ============================================================================
-- 11) FILE PERMISSIONS  (6 cờ bool + Status: Active=0, Inactive=1)
--     Gán cho P1 (nhóm Kỹ sư) — phục vụ case 1/3/5 ở cấp file.
-- ============================================================================
INSERT INTO "FilePermissions" ("Id","FileItemId","ProjectParticipantId","CanView","CanEdit","CanUpdate","CanDownload","CanVerify","CanApprove","Status") VALUES
-- A1: CÓ quyền view file — case 1
('ff500000-0000-0000-0000-000000000001','ff300000-0000-0000-0000-000000000001','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,0),
-- A2: KHÔNG có quyền view file (row Active, CanView=false) — case 3/5
('ff500000-0000-0000-0000-000000000002','ff300000-0000-0000-0000-000000000002','dd300000-0000-0000-0000-000000000002',false,false,false,false,false,false,0),
-- (A3: cố tình KHÔNG có dòng — case 3, file không được cấp quyền riêng)
-- C1: file CanView=true nhưng folder MEP không view được — quyền file không "cứu" được folder
('ff500000-0000-0000-0000-000000000003','ff300000-0000-0000-0000-000000000005','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,0),
-- E1: CanView=true nhưng Status=INACTIVE ⇒ không tính — case 5
('ff500000-0000-0000-0000-000000000004','ff300000-0000-0000-0000-000000000007','dd300000-0000-0000-0000-000000000002',true,false,false,true,false,false,1);

COMMIT;

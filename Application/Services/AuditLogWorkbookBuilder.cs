using Application.DTOs.ResponseDTOs.Audit;
using Domain.Enum.Audit;
using Syncfusion.XlsIO;

namespace Application.Services
{
    public sealed record AuditLogWorkbookRequest(
        string Title,
        string ExportedBy,
        DateTime ExportedAtLocal,
        IReadOnlyList<string> FilterLines,
        bool Truncated,
        int TruncationLimit,
        int LocalOffsetHours,
        IReadOnlyList<AuditLogResponseDTO> Rows);

    public static class AuditLogWorkbookBuilder
    {
        private const string SheetName = "Nhật ký";
        private const string DateFormat = "dd/mm/yyyy";
        private const string ClockFormat = "hh:mm:ss";
        private const int HeaderRow = 6;
        private const int FirstDataRow = HeaderRow + 1;
        private const string NotAvailable = "—";

        private static readonly string[] Columns =
        {
            "Ngày", "Giờ", "Người thao tác", "Hành động", "Phạm vi", "Đối tượng", "Nội dung"
        };

        private static readonly double[] ColumnWidths = { 12, 10, 24, 22, 12, 22, 88 };

        private static readonly Dictionary<AuditAction, string> ActionLabels = new()
        {
            [AuditAction.Create] = "Tạo mới",
            [AuditAction.Update] = "Cập nhật",
            [AuditAction.Delete] = "Xoá",
            [AuditAction.Move] = "Di chuyển",
            [AuditAction.Submit] = "Gửi duyệt",
            [AuditAction.Verify] = "Kiểm tra",
            [AuditAction.Approve] = "Duyệt",
            [AuditAction.Reject] = "Từ chối",
            [AuditAction.Download] = "Tải về",
            [AuditAction.PermissionChange] = "Đổi phân quyền",
            [AuditAction.Upload] = "Tải lên",
            [AuditAction.NewVersion] = "Phiên bản mới",
            [AuditAction.Sign] = "Ký số",
            [AuditAction.ZoneTransfer] = "Chuyển vùng",
            [AuditAction.ReturnRequest] = "Yêu cầu trả về",
            [AuditAction.Invite] = "Mời tham gia",
            [AuditAction.AcceptInvite] = "Chấp nhận lời mời",
            [AuditAction.RejectInvite] = "Từ chối lời mời",
            [AuditAction.Assign] = "Phân công",
            [AuditAction.StatusChange] = "Đổi trạng thái",
            [AuditAction.Archive] = "Niêm phong lưu trữ"
        };

        private static readonly Dictionary<LogScope, string> ScopeLabels = new()
        {
            [LogScope.System] = "Hệ thống",
            [LogScope.Project] = "Dự án",
            [LogScope.Group] = "Nhóm"
        };

        private static readonly Dictionary<string, string> EntityLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FileItem"] = "Tệp",
            ["FileVersionState"] = "Phiên bản tệp",
            ["Folder"] = "Thư mục",
            ["FolderPermission"] = "Quyền thư mục",
            ["FilePermission"] = "Quyền tệp",
            ["Project"] = "Dự án",
            ["ProjectParticipant"] = "Nhóm trong dự án",
            ["ProjectInvitation"] = "Lời mời vào dự án",
            ["Account"] = "Tài khoản",
            ["Password"] = "Mật khẩu",
            ["NewPassword"] = "Mật khẩu mới",
            ["Group"] = "Nhóm",
            ["GroupMember"] = "Thành viên nhóm",
            ["Organization"] = "Đối tác",
            ["ApprovalRequest"] = "Phiếu duyệt",
            ["ZoneReturnRequest"] = "Yêu cầu trả về WIP",
            ["Issue"] = "Vấn đề",
            ["Discussion"] = "Thảo luận",
            ["MarkupSet"] = "Bộ ghi chú",
            ["NamingConvention"] = "Quy ước đặt tên",
            ["ContractPackage"] = "Gói thầu",
            ["Contract"] = "Hợp đồng",
            ["LoiRuleSet"] = "Bộ luật phi hình học",
            ["LoiComponent"] = "Cấu kiện LOI",
            ["LoiParameter"] = "Tham số LOI",
            ["LoiRequirement"] = "Yêu cầu LOI",
            ["LoiFieldAlias"] = "Ánh xạ tham số LOI"
        };

        public static byte[] Build(AuditLogWorkbookRequest request)
        {
            using var engine = new ExcelEngine();
            engine.Excel.DefaultVersion = ExcelVersion.Excel2016;

            var workbook = engine.Excel.Workbooks.Create(1);
            var sheet = workbook.Worksheets[0];
            sheet.Name = SheetName;

            WriteSummary(sheet, request);
            WriteHeader(sheet);
            var lastRow = WriteRows(sheet, request);
            ApplyLayout(sheet, lastRow);

            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        }

        private static void WriteSummary(IWorksheet sheet, AuditLogWorkbookRequest request)
        {
            var lines = new[]
            {
                request.Title,
                $"Người xuất: {request.ExportedBy}    ·    Thời điểm xuất: "
                    + $"{request.ExportedAtLocal:dd/MM/yyyy HH:mm} (UTC+{request.LocalOffsetHours})",
                $"Bộ lọc: {(request.FilterLines.Count == 0 ? "không áp dụng" : string.Join("  ·  ", request.FilterLines))}",
                request.Truncated
                    ? $"Số bản ghi: {request.Rows.Count} (đã cắt ở mức tối đa {request.TruncationLimit} dòng — hãy thu hẹp bộ lọc để xuất đủ)"
                    : $"Số bản ghi: {request.Rows.Count}"
            };

            for (var index = 0; index < lines.Length; index++)
            {
                var row = index + 1;
                var range = sheet.Range[row, 1, row, Columns.Length];
                range.Merge();
                sheet.Range[row, 1].Text = lines[index];
                range.CellStyle.VerticalAlignment = ExcelVAlign.VAlignCenter;
            }

            var titleCell = sheet.Range[1, 1];
            titleCell.CellStyle.Font.Bold = true;
            titleCell.CellStyle.Font.Size = 15;
            titleCell.CellStyle.Font.Color = ExcelKnownColors.Dark_blue;
            sheet.Rows[0].RowHeight = 26;

            for (var row = 2; row <= lines.Length; row++)
                sheet.Range[row, 1].CellStyle.Font.Color = ExcelKnownColors.Grey_50_percent;

            if (request.Truncated)
                sheet.Range[lines.Length, 1].CellStyle.Font.Color = ExcelKnownColors.Dark_red;
        }

        private static void WriteHeader(IWorksheet sheet)
        {
            for (var index = 0; index < Columns.Length; index++)
                sheet.Range[HeaderRow, index + 1].Text = Columns[index];

            var header = sheet.Range[HeaderRow, 1, HeaderRow, Columns.Length];
            header.CellStyle.Font.Bold = true;
            header.CellStyle.Font.Color = ExcelKnownColors.White;
            header.CellStyle.ColorIndex = ExcelKnownColors.Blue_grey;
            header.CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;
            header.CellStyle.VerticalAlignment = ExcelVAlign.VAlignCenter;
            sheet.Rows[HeaderRow - 1].RowHeight = 22;
        }

        private static int WriteRows(IWorksheet sheet, AuditLogWorkbookRequest request)
        {
            var row = FirstDataRow;

            foreach (var log in request.Rows)
            {
                WriteMoment(sheet, row, log.CreatedAt, request.LocalOffsetHours);

                sheet.Range[row, 3].Text = Fallback(log.ActorName);
                sheet.Range[row, 4].Text = ActionLabel(log.Action);
                sheet.Range[row, 5].Text = ScopeLabel(log.Scope);
                sheet.Range[row, 6].Text = EntityLabel(log.EntityType);
                sheet.Range[row, 7].Text = Fallback(log.Detail);

                if ((row - FirstDataRow) % 2 == 1)
                    sheet.Range[row, 1, row, Columns.Length].CellStyle.ColorIndex = ExcelKnownColors.Grey_25_percent;

                row++;
            }

            return row - 1;
        }

        private static void WriteMoment(IWorksheet sheet, int row, DateTime? createdAt, int offsetHours)
        {
            if (createdAt is null)
            {
                sheet.Range[row, 1].Text = NotAvailable;
                sheet.Range[row, 2].Text = NotAvailable;
                return;
            }

            var local = createdAt.Value.AddHours(offsetHours);

            sheet.Range[row, 1].DateTime = local.Date;
            sheet.Range[row, 1].NumberFormat = DateFormat;
            sheet.Range[row, 2].DateTime = local;
            sheet.Range[row, 2].NumberFormat = ClockFormat;
        }

        private static void ApplyLayout(IWorksheet sheet, int lastRow)
        {
            for (var index = 0; index < ColumnWidths.Length; index++)
                sheet.Columns[index].ColumnWidth = ColumnWidths[index];

            if (lastRow < FirstDataRow)
            {
                sheet.Range[FirstDataRow, 1].Text = "Không có bản ghi nào khớp bộ lọc.";
                sheet.Range[FirstDataRow, 1].CellStyle.Font.Color = ExcelKnownColors.Grey_50_percent;
                sheet.Range[FirstDataRow, 1].FreezePanes();
                return;
            }

            var body = sheet.Range[FirstDataRow, 1, lastRow, Columns.Length];
            body.CellStyle.VerticalAlignment = ExcelVAlign.VAlignTop;
            body.CellStyle.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Hair;
            body.CellStyle.Borders[ExcelBordersIndex.EdgeBottom].Color = ExcelKnownColors.Grey_40_percent;

            var detail = sheet.Range[FirstDataRow, Columns.Length, lastRow, Columns.Length];
            detail.CellStyle.WrapText = true;

            sheet.Range[FirstDataRow, 5, lastRow, 5].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;
            sheet.Range[FirstDataRow, 1, lastRow, 2].CellStyle.HorizontalAlignment = ExcelHAlign.HAlignCenter;

            sheet.AutoFilters.FilterRange = sheet.Range[HeaderRow, 1, lastRow, Columns.Length];
            sheet.Range[FirstDataRow, 1].FreezePanes();
            sheet.Range[FirstDataRow, 1, lastRow, Columns.Length].AutofitRows();
        }

        public static string ActionLabel(AuditAction action) =>
            ActionLabels.TryGetValue(action, out var label) ? label : action.ToString();

        public static string ScopeLabel(LogScope scope) =>
            ScopeLabels.TryGetValue(scope, out var label) ? label : scope.ToString();

        public static string EntityLabel(string? entityType) =>
            string.IsNullOrWhiteSpace(entityType)
                ? NotAvailable
                : EntityLabels.TryGetValue(entityType, out var label) ? label : entityType;

        private static string Fallback(string? value) =>
            string.IsNullOrWhiteSpace(value) ? NotAvailable : value;
    }
}

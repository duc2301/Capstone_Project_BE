using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Syncfusion.XlsIO;

namespace Application.Services
{
    public sealed class ProjectImportService : IProjectImportService
    {
        private const int LookupOrganizationColumn = 1;
        private const int LookupAccountColumn = 5;
        private const int LookupWorkTypeColumn = 8;

        private static readonly (string Code, string Label)[] WorkTypeCatalog =
        {
            ("XDT", "Xây dựng tổng thể"),
            ("STR", "Kết cấu"),
            ("ARC", "Kiến trúc"),
            ("MEP", "Cơ điện"),
            ("FIN", "Hoàn thiện"),
            ("RCC", "Bê tông cốt thép"),
            ("INF", "Hạ tầng"),
            ("PCCC", "Phòng cháy chữa cháy")
        };

        private static readonly char[] WorkTypeSeparators = { ',', ';', '|' };

        private readonly IUnitOfWork _unitOfWork;

        public ProjectImportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
        {
            var organizations = (await _unitOfWork.Repository<Organization>().GetAllAsync())
                .OrderBy(o => o.DisplayName ?? o.LegalName, StringComparer.CurrentCulture)
                .ToList();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .OrderBy(a => a.UserName, StringComparer.CurrentCulture)
                .ToList();

            using var engine = new ExcelEngine();
            engine.Excel.DefaultVersion = ExcelVersion.Excel2016;
            var workbook = engine.Excel.Workbooks.Create(5);

            WriteGuideSheet(workbook.Worksheets[0]);
            WriteProjectSheet(workbook.Worksheets[1]);
            WritePackageSheet(workbook.Worksheets[2]);
            WriteGroupSheet(workbook.Worksheets[3]);
            WriteLookupSheet(workbook.Worksheets[4], organizations, accounts);

            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        }

        public async Task<ProjectImportPreviewDTO> ParseAsync(Stream stream, CancellationToken ct = default)
        {
            var organizations = (await _unitOfWork.Repository<Organization>().GetAllAsync()).ToList();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync()).ToList();

            using var engine = new ExcelEngine();
            IWorkbook workbook;
            try
            {
                workbook = engine.Excel.Workbooks.Open(stream, ExcelOpenType.Automatic);
            }
            catch (Exception)
            {
                throw new ApiExceptionResponse(
                    "Không đọc được file. Hãy dùng file .xlsx tải từ nút tải file mẫu.", 400);
            }

            var result = new ProjectImportPreviewDTO();

            var projectSheet = FindSheet(workbook, ProjectImportSheet.Project)
                ?? throw BuildMissingProjectSheetError(workbook);

            ReadProjectSheet(projectSheet, result, organizations, accounts);
            ReadPackageSheet(FindSheet(workbook, ProjectImportSheet.Packages), result, organizations, accounts);
            ReadGroupSheet(FindSheet(workbook, ProjectImportSheet.Groups), result, organizations);

            if (string.IsNullOrWhiteSpace(result.ProjectName) && string.IsNullOrWhiteSpace(result.ProjectCode))
                result.Warnings.Add(
                    $"Sheet \"{ProjectImportSheet.Project}\" chưa có tên và mã dự án — hãy điền trực tiếp ở bước 1.");

            return result;
        }

        private static ApiExceptionResponse BuildMissingProjectSheetError(IWorkbook workbook)
        {
            var looksLikeTemplate =
                FindSheet(workbook, ProjectImportSheet.Guide) is not null
                || FindSheet(workbook, ProjectImportSheet.Packages) is not null
                || FindSheet(workbook, ProjectImportSheet.Groups) is not null;

            return new ApiExceptionResponse(
                looksLikeTemplate
                    ? $"File thiếu sheet \"{ProjectImportSheet.Project}\". Hãy tải file mẫu và điền theo."
                    : "File không đúng mẫu khởi tạo dự án. Hãy bấm \"Tải file Excel mẫu\" rồi điền vào chính file đó.",
                400);
        }

        private static IWorksheet? FindSheet(IWorkbook workbook, string name)
        {
            foreach (IWorksheet sheet in workbook.Worksheets)
                if (string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return sheet;
            return null;
        }

        private static string? CellText(IWorksheet sheet, int row, int column)
        {
            var text = sheet.Range[row, column].DisplayText?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool TryCellDecimal(IWorksheet sheet, int row, int column, out decimal value)
        {
            var range = sheet.Range[row, column];
            if (range.HasNumber)
            {
                value = (decimal)range.Number;
                return true;
            }
            return ProjectImportSheet.TryParseDecimal(range.DisplayText, out value);
        }

        private static string? CellDate(IWorksheet sheet, int row, int column)
        {
            var range = sheet.Range[row, column];
            if (range.HasDateTime)
                return range.DateTime.ToString(ProjectImportSheet.DateFormat);

            return ProjectImportSheet.TryParseDate(range.DisplayText, out var parsed)
                ? parsed.ToString(ProjectImportSheet.DateFormat)
                : null;
        }

        private static Organization? MatchOrganization(string? text, List<Organization> organizations)
        {
            var value = text?.Trim();
            if (string.IsNullOrEmpty(value)) return null;

            var compact = value.Replace(" ", string.Empty);
            var byTaxCode = organizations.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.TaxCode)
                && string.Equals(o.TaxCode.Replace(" ", string.Empty), compact, StringComparison.OrdinalIgnoreCase));
            if (byTaxCode is not null) return byTaxCode;

            var normalized = ProjectImportSheet.Normalize(value);
            if (normalized.Length == 0) return null;

            return organizations.FirstOrDefault(o =>
                ProjectImportSheet.Normalize(o.DisplayName) == normalized
                || ProjectImportSheet.Normalize(o.LegalName) == normalized);
        }

        private static Account? MatchAccount(string? text, List<Account> accounts)
        {
            var value = text?.Trim();
            if (string.IsNullOrEmpty(value)) return null;

            return accounts.FirstOrDefault(a => string.Equals(a.Email, value, StringComparison.OrdinalIgnoreCase));
        }

        private static string OrganizationLabel(Organization organization) =>
            string.IsNullOrWhiteSpace(organization.DisplayName) ? organization.LegalName : organization.DisplayName!;

        private static Dictionary<string, int> MapHeaderColumns(IWorksheet sheet)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var lastColumn = Math.Min(sheet.UsedRange.LastColumn, ProjectImportSheet.MaxColumns);

            for (var column = 1; column <= lastColumn; column++)
            {
                var header = ProjectImportSheet.Normalize(sheet.Range[ProjectImportSheet.HeaderRow, column].DisplayText);
                if (header.Length > 0 && !map.ContainsKey(header)) map[header] = column;
            }

            return map;
        }

        private static string? ColumnText(IWorksheet sheet, Dictionary<string, int> columns, string header, int row) =>
            columns.TryGetValue(ProjectImportSheet.Normalize(header), out var column)
                ? CellText(sheet, row, column)
                : null;

        private static void ReadProjectSheet(
            IWorksheet sheet,
            ProjectImportPreviewDTO result,
            List<Organization> organizations,
            List<Account> accounts)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var lastRow = Math.Min(sheet.UsedRange.LastRow, ProjectImportSheet.MaxRows);

            for (var row = ProjectImportSheet.FirstDataRow; row <= lastRow; row++)
            {
                var label = ProjectImportSheet.Normalize(
                    sheet.Range[row, ProjectImportSheet.LabelColumn].DisplayText);
                if (label.Length == 0) continue;

                var value = sheet.Range[row, ProjectImportSheet.ValueColumn].DisplayText?.Trim();
                if (!string.IsNullOrEmpty(value)) values[label] = value;
            }

            string? Read(string field) =>
                values.TryGetValue(ProjectImportSheet.Normalize(field), out var value) ? value : null;

            result.ProjectName = Read(ProjectImportSheet.FieldProjectName);
            result.ProjectCode = Read(ProjectImportSheet.FieldProjectCode);
            result.ProjectDescription = Read(ProjectImportSheet.FieldProjectDescription);
            result.ContactAddress = Read(ProjectImportSheet.FieldContactAddress);
            result.Address = Read(ProjectImportSheet.FieldAddress);

            if (ProjectImportSheet.TryParseDouble(Read(ProjectImportSheet.FieldLatitude), out var latitude))
                result.Latitude = latitude;
            if (ProjectImportSheet.TryParseDouble(Read(ProjectImportSheet.FieldLongitude), out var longitude))
                result.Longitude = longitude;

            var ownerText = Read(ProjectImportSheet.FieldOwnerTaxCode);
            var owner = MatchOrganization(ownerText, organizations);
            if (owner is not null)
            {
                result.OwnerOrganizationId = owner.Id;
                result.OwnerOrganizationName = OrganizationLabel(owner);
            }
            else if (!string.IsNullOrWhiteSpace(ownerText))
            {
                result.Warnings.Add(
                    $"Không tìm thấy chủ đầu tư \"{ownerText}\" trong danh mục đối tác — hãy chọn lại ở bước 1.");
            }

            var managerEmail = Read(ProjectImportSheet.FieldManagerEmail);
            var manager = MatchAccount(managerEmail, accounts);
            if (manager is not null)
            {
                result.ManagerAccountId = manager.Id;
                result.ManagerAccountName = manager.UserName;
            }
            else if (!string.IsNullOrWhiteSpace(managerEmail))
            {
                result.Warnings.Add(
                    $"Không tìm thấy tài khoản quản lý dự án \"{managerEmail}\" — hãy chọn lại ở bước 4.");
            }
        }

        private static void ReadPackageSheet(
            IWorksheet? sheet,
            ProjectImportPreviewDTO result,
            List<Organization> organizations,
            List<Account> accounts)
        {
            if (sheet is null)
            {
                result.Warnings.Add($"Không thấy sheet \"{ProjectImportSheet.Packages}\" — bỏ qua phần gói thầu.");
                return;
            }

            var columns = MapHeaderColumns(sheet);
            if (!columns.ContainsKey(ProjectImportSheet.Normalize(ProjectImportSheet.ColumnPackageName)))
            {
                result.Warnings.Add(
                    $"Sheet \"{ProjectImportSheet.Packages}\" thiếu cột \"{ProjectImportSheet.ColumnPackageName}\" — bỏ qua phần gói thầu.");
                return;
            }

            var lastRow = Math.Min(sheet.UsedRange.LastRow, ProjectImportSheet.MaxRows);

            for (var row = ProjectImportSheet.FirstDataRow; row <= lastRow; row++)
            {
                var name = ColumnText(sheet, columns, ProjectImportSheet.ColumnPackageName, row);
                if (string.IsNullOrEmpty(name))
                {
                    if (RowHasValue(sheet, columns, row))
                        result.Warnings.Add(
                            $"{ProjectImportSheet.Packages} dòng {row}: thiếu {ProjectImportSheet.ColumnPackageName} — đã bỏ qua.");
                    continue;
                }

                var package = new ProjectImportPackageDTO
                {
                    Code = ColumnText(sheet, columns, ProjectImportSheet.ColumnPackageCode, row),
                    Name = name,
                    Description = ColumnText(sheet, columns, ProjectImportSheet.ColumnPackageDescription, row),
                    WorkTypes = NormalizeWorkTypes(
                        ColumnText(sheet, columns, ProjectImportSheet.ColumnWorkTypes, row)),
                    ScopeDescription = ColumnText(sheet, columns, ProjectImportSheet.ColumnScope, row),
                    Currency = ColumnText(sheet, columns, ProjectImportSheet.ColumnCurrency, row),
                    ContractNumber = ColumnText(sheet, columns, ProjectImportSheet.ColumnContractNumber, row),
                    ContractJobTitle = ColumnText(sheet, columns, ProjectImportSheet.ColumnJobTitle, row),
                    Notes = ColumnText(sheet, columns, ProjectImportSheet.ColumnNotes, row),
                    StartDate = ReadColumnDate(sheet, columns, ProjectImportSheet.ColumnStartDate, row),
                    EndDate = ReadColumnDate(sheet, columns, ProjectImportSheet.ColumnEndDate, row),
                    ContractSignDate = ReadColumnDate(sheet, columns, ProjectImportSheet.ColumnContractSignDate, row)
                };

                if (TryReadColumnDecimal(sheet, columns, ProjectImportSheet.ColumnContractValue, row, out var value))
                    package.ContractValue = value;
                if (TryReadColumnDecimal(sheet, columns, ProjectImportSheet.ColumnTaxRate, row, out var taxRate))
                    package.TaxRate = taxRate;

                var contractorText = ColumnText(sheet, columns, ProjectImportSheet.ColumnContractorTaxCode, row);
                var contractor = MatchOrganization(contractorText, organizations);
                if (contractor is not null)
                {
                    package.ContractorOrganizationId = contractor.Id;
                    package.ContractorOrganizationName = OrganizationLabel(contractor);
                }
                else if (!string.IsNullOrWhiteSpace(contractorText))
                {
                    result.Warnings.Add(
                        $"{ProjectImportSheet.Packages} dòng {row}: không tìm thấy nhà thầu \"{contractorText}\" — hãy chọn lại ở bước 3.");
                }

                var representativeEmail = ColumnText(sheet, columns, ProjectImportSheet.ColumnRepresentativeEmail, row);
                var representative = MatchAccount(representativeEmail, accounts);
                if (representative is not null)
                {
                    package.RepresentativeAccountId = representative.Id;
                    package.RepresentativeAccountName = representative.UserName;
                }
                else if (!string.IsNullOrWhiteSpace(representativeEmail))
                {
                    result.Warnings.Add(
                        $"{ProjectImportSheet.Packages} dòng {row}: không tìm thấy người đại diện \"{representativeEmail}\" — hãy chọn lại ở bước 3.");
                }

                result.Packages.Add(package);
            }
        }

        private static void ReadGroupSheet(
            IWorksheet? sheet,
            ProjectImportPreviewDTO result,
            List<Organization> organizations)
        {
            if (sheet is null)
            {
                result.Warnings.Add($"Không thấy sheet \"{ProjectImportSheet.Groups}\" — dùng bộ nhóm mặc định.");
                return;
            }

            var columns = MapHeaderColumns(sheet);
            if (!columns.ContainsKey(ProjectImportSheet.Normalize(ProjectImportSheet.ColumnGroupName)))
            {
                result.Warnings.Add(
                    $"Sheet \"{ProjectImportSheet.Groups}\" thiếu cột \"{ProjectImportSheet.ColumnGroupName}\" — dùng bộ nhóm mặc định.");
                return;
            }

            var lastRow = Math.Min(sheet.UsedRange.LastRow, ProjectImportSheet.MaxRows);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var row = ProjectImportSheet.FirstDataRow; row <= lastRow; row++)
            {
                var name = ColumnText(sheet, columns, ProjectImportSheet.ColumnGroupName, row);
                if (string.IsNullOrEmpty(name))
                {
                    if (RowHasValue(sheet, columns, row))
                        result.Warnings.Add(
                            $"{ProjectImportSheet.Groups} dòng {row}: thiếu {ProjectImportSheet.ColumnGroupName} — đã bỏ qua.");
                    continue;
                }

                if (!seen.Add(ProjectImportSheet.Normalize(name)))
                {
                    result.Warnings.Add($"{ProjectImportSheet.Groups} dòng {row}: nhóm \"{name}\" bị lặp — đã bỏ qua.");
                    continue;
                }

                var group = new ProjectImportGroupDTO
                {
                    Name = name,
                    Description = ColumnText(sheet, columns, ProjectImportSheet.ColumnGroupDescription, row)
                };

                var partnerText = ColumnText(sheet, columns, ProjectImportSheet.ColumnPartnerTaxCode, row);
                var partner = MatchOrganization(partnerText, organizations);
                if (partner is not null)
                {
                    group.PartnerOrganizationId = partner.Id;
                    group.PartnerOrganizationName = OrganizationLabel(partner);
                }
                else if (!string.IsNullOrWhiteSpace(partnerText))
                {
                    result.Warnings.Add(
                        $"{ProjectImportSheet.Groups} dòng {row}: không tìm thấy đối tác \"{partnerText}\" — hãy chọn lại ở bước 5.");
                }

                result.Groups.Add(group);
            }
        }

        private static bool RowHasValue(IWorksheet sheet, Dictionary<string, int> columns, int row) =>
            columns.Values.Any(column => !string.IsNullOrEmpty(CellText(sheet, row, column)));

        private static string? ReadColumnDate(
            IWorksheet sheet, Dictionary<string, int> columns, string header, int row) =>
            columns.TryGetValue(ProjectImportSheet.Normalize(header), out var column)
                ? CellDate(sheet, row, column)
                : null;

        private static bool TryReadColumnDecimal(
            IWorksheet sheet, Dictionary<string, int> columns, string header, int row, out decimal value)
        {
            value = 0m;
            return columns.TryGetValue(ProjectImportSheet.Normalize(header), out var column)
                && TryCellDecimal(sheet, row, column, out value);
        }

        private static string? NormalizeWorkTypes(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var known = WorkTypeCatalog.ToDictionary(w => w.Code, w => w.Code, StringComparer.OrdinalIgnoreCase);
            var codes = raw
                .Split(WorkTypeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => known.TryGetValue(code, out var match) ? match : null)
                .Where(code => code is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return codes.Count == 0 ? null : string.Join(",", codes);
        }

        private static void WriteGuideSheet(IWorksheet sheet)
        {
            sheet.Name = ProjectImportSheet.Guide;

            var lines = new (string Label, string Value)[]
            {
                ("Mục đích", "Điền sẵn dữ liệu khởi tạo dự án rồi nhập vào hệ thống, đỡ phải gõ tay từng bước."),
                (string.Empty, string.Empty),
                ($"Sheet {ProjectImportSheet.Project}", "Thông tin chung của dự án. Cột A là tên trường (KHÔNG sửa), cột B điền giá trị."),
                ($"Sheet {ProjectImportSheet.Packages}", "Mỗi dòng là một gói thầu. Không có gói thầu thì để trống sheet này."),
                ($"Sheet {ProjectImportSheet.Groups}", "Mỗi dòng là một nhóm tham gia dự án. Để trống thì hệ thống dùng bộ nhóm mặc định."),
                ($"Sheet {ProjectImportSheet.Lookup}", "Danh sách đối tác, tài khoản và mã loại công việc đang có trong hệ thống. Chỉ để tra cứu, không nhập."),
                (string.Empty, string.Empty),
                ("Cách ghi đối tác", $"Điền mã số thuế lấy từ sheet {ProjectImportSheet.Lookup}. Ghi đúng tên đối tác cũng được."),
                ("Cách ghi tài khoản", $"Điền email lấy từ sheet {ProjectImportSheet.Lookup}."),
                ("Không khớp thì sao", "Hệ thống bỏ trống ô đó và báo cảnh báo; bạn chọn lại trực tiếp trên giao diện."),
                (string.Empty, string.Empty),
                ("Định dạng ngày", $"{ProjectImportSheet.DateFormat} (ví dụ 2026-03-15)."),
                ("Định dạng số tiền", "Chỉ ghi số, ví dụ 12500000000. Không ghi ký hiệu tiền tệ."),
                ("Loại công việc", "Ghi mã cách nhau bởi dấu phẩy, ví dụ STR,ARC,MEP."),
                (string.Empty, string.Empty),
                ("KHÔNG điền ở đây", "Ảnh bìa dự án, hồ sơ pháp lý bắt buộc và tệp hợp đồng đính kèm."),
                ("", "Ba loại tệp trên đính kèm trực tiếp trên giao diện sau khi nhập file này.")
            };

            sheet.Range[1, 1].Text = "HƯỚNG DẪN ĐIỀN FILE KHỞI TẠO DỰ ÁN";
            sheet.Range[1, 1].CellStyle.Font.Bold = true;
            sheet.Range[1, 1].CellStyle.Font.Size = 14;

            for (var i = 0; i < lines.Length; i++)
            {
                sheet.Range[i + 3, 1].Text = lines[i].Label;
                sheet.Range[i + 3, 2].Text = lines[i].Value;
                sheet.Range[i + 3, 1].CellStyle.Font.Bold = true;
            }

            sheet.SetColumnWidth(1, 24);
            sheet.SetColumnWidth(2, 110);
        }

        private static void WriteProjectSheet(IWorksheet sheet)
        {
            sheet.Name = ProjectImportSheet.Project;

            sheet.Range[ProjectImportSheet.TitleRow, 1].Text = "THÔNG TIN DỰ ÁN";
            sheet.Range[ProjectImportSheet.TitleRow, 1].CellStyle.Font.Bold = true;

            sheet.Range[ProjectImportSheet.HeaderRow, 1].Text = "Trường";
            sheet.Range[ProjectImportSheet.HeaderRow, 2].Text = "Giá trị";
            sheet.Range[ProjectImportSheet.HeaderRow, 3].Text = "Ghi chú";
            sheet.Range[ProjectImportSheet.HeaderRow, 1, ProjectImportSheet.HeaderRow, 3].CellStyle.Font.Bold = true;

            var hints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProjectImportSheet.FieldProjectName] = "Bắt buộc",
                [ProjectImportSheet.FieldProjectCode] = "Bắt buộc",
                [ProjectImportSheet.FieldOwnerTaxCode] = $"Bắt buộc — lấy từ sheet {ProjectImportSheet.Lookup}",
                [ProjectImportSheet.FieldLatitude] = "Số thập phân, ví dụ 21.028511",
                [ProjectImportSheet.FieldLongitude] = "Số thập phân, ví dụ 105.804817",
                [ProjectImportSheet.FieldManagerEmail] = $"Lấy từ sheet {ProjectImportSheet.Lookup}"
            };

            var row = ProjectImportSheet.FirstDataRow;
            foreach (var field in ProjectImportSheet.ProjectFields)
            {
                sheet.Range[row, 1].Text = field;
                sheet.Range[row, 1].CellStyle.Font.Bold = true;
                if (hints.TryGetValue(field, out var hint)) sheet.Range[row, 3].Text = hint;
                row++;
            }

            sheet.SetColumnWidth(1, 26);
            sheet.SetColumnWidth(2, 52);
            sheet.SetColumnWidth(3, 44);
        }

        private static void WritePackageSheet(IWorksheet sheet)
        {
            sheet.Name = ProjectImportSheet.Packages;

            sheet.Range[ProjectImportSheet.TitleRow, 1].Text = "DANH SÁCH GÓI THẦU — MỖI DÒNG MỘT GÓI";
            sheet.Range[ProjectImportSheet.TitleRow, 1].CellStyle.Font.Bold = true;

            WriteHeaderRow(sheet, ProjectImportSheet.PackageHeaders);

            var widths = new[] { 14, 38, 34, 20, 34, 14, 14, 20, 10, 12, 18, 18, 18, 28, 24, 30 };
            for (var i = 0; i < widths.Length; i++) sheet.SetColumnWidth(i + 1, widths[i]);
        }

        private static void WriteGroupSheet(IWorksheet sheet)
        {
            sheet.Name = ProjectImportSheet.Groups;

            sheet.Range[ProjectImportSheet.TitleRow, 1].Text = "DANH SÁCH NHÓM THAM GIA — MỖI DÒNG MỘT NHÓM";
            sheet.Range[ProjectImportSheet.TitleRow, 1].CellStyle.Font.Bold = true;

            WriteHeaderRow(sheet, ProjectImportSheet.GroupHeaders);

            var samples = new[]
            {
                "Chủ đầu tư", "Tư vấn thiết kế", "Tư vấn thẩm tra", "Nhà thầu thi công", "Tư vấn giám sát"
            };
            for (var i = 0; i < samples.Length; i++)
                sheet.Range[ProjectImportSheet.FirstDataRow + i, 1].Text = samples[i];

            sheet.SetColumnWidth(1, 34);
            sheet.SetColumnWidth(2, 46);
            sheet.SetColumnWidth(3, 20);
        }

        private static void WriteHeaderRow(IWorksheet sheet, string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Range[ProjectImportSheet.HeaderRow, i + 1].Text = headers[i];
                sheet.Range[ProjectImportSheet.HeaderRow, i + 1].CellStyle.Font.Bold = true;
            }
        }

        private static void WriteLookupSheet(
            IWorksheet sheet, List<Organization> organizations, List<Account> accounts)
        {
            sheet.Name = ProjectImportSheet.Lookup;

            sheet.Range[ProjectImportSheet.TitleRow, LookupOrganizationColumn].Text = "DANH MỤC ĐỐI TÁC";
            sheet.Range[ProjectImportSheet.TitleRow, LookupAccountColumn].Text = "DANH MỤC TÀI KHOẢN";
            sheet.Range[ProjectImportSheet.TitleRow, LookupWorkTypeColumn].Text = "MÃ LOẠI CÔNG VIỆC";

            sheet.Range[ProjectImportSheet.HeaderRow, LookupOrganizationColumn].Text = "Mã số thuế";
            sheet.Range[ProjectImportSheet.HeaderRow, LookupOrganizationColumn + 1].Text = "Tên đối tác";
            sheet.Range[ProjectImportSheet.HeaderRow, LookupAccountColumn].Text = "Email";
            sheet.Range[ProjectImportSheet.HeaderRow, LookupAccountColumn + 1].Text = "Tên tài khoản";
            sheet.Range[ProjectImportSheet.HeaderRow, LookupWorkTypeColumn].Text = "Mã";
            sheet.Range[ProjectImportSheet.HeaderRow, LookupWorkTypeColumn + 1].Text = "Diễn giải";

            foreach (var column in new[] { LookupOrganizationColumn, LookupAccountColumn, LookupWorkTypeColumn })
            {
                sheet.Range[ProjectImportSheet.TitleRow, column].CellStyle.Font.Bold = true;
                sheet.Range[ProjectImportSheet.HeaderRow, column].CellStyle.Font.Bold = true;
                sheet.Range[ProjectImportSheet.HeaderRow, column + 1].CellStyle.Font.Bold = true;
            }

            var row = ProjectImportSheet.FirstDataRow;
            foreach (var organization in organizations)
            {
                sheet.Range[row, LookupOrganizationColumn].Text = organization.TaxCode;
                sheet.Range[row, LookupOrganizationColumn + 1].Text = OrganizationLabel(organization);
                row++;
            }

            row = ProjectImportSheet.FirstDataRow;
            foreach (var account in accounts)
            {
                sheet.Range[row, LookupAccountColumn].Text = account.Email;
                sheet.Range[row, LookupAccountColumn + 1].Text = account.UserName;
                row++;
            }

            row = ProjectImportSheet.FirstDataRow;
            foreach (var (code, label) in WorkTypeCatalog)
            {
                sheet.Range[row, LookupWorkTypeColumn].Text = code;
                sheet.Range[row, LookupWorkTypeColumn + 1].Text = label;
                row++;
            }

            sheet.SetColumnWidth(LookupOrganizationColumn, 18);
            sheet.SetColumnWidth(LookupOrganizationColumn + 1, 46);
            sheet.SetColumnWidth(LookupAccountColumn, 34);
            sheet.SetColumnWidth(LookupAccountColumn + 1, 28);
            sheet.SetColumnWidth(LookupWorkTypeColumn, 10);
            sheet.SetColumnWidth(LookupWorkTypeColumn + 1, 30);
        }
    }
}

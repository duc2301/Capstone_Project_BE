using Domain.Enum.File;

namespace Application.Services.Signing
{
    // Phân loại định dạng file theo khả năng ký số trực quan, dùng chung cho ApprovalService,
    // PdfSignatureService, FileSignaturePositionService, VnptSmartCaService - tránh mỗi nơi tự
    // định nghĩa lại danh sách định dạng.
    public static class FileSignatureFormatRules
    {
        public static bool IsWordFormat(string? format) => NormalizeFormat(format) is "doc" or "docx";

        public static bool IsExcelFormat(string? format) => NormalizeFormat(format) is "xls" or "xlsx";

        // Chỉ dwg/dwgx - ConvertAPI (dịch vụ dùng để convert CAD 2D -> PDF) chỉ hỗ trợ 2 định dạng này.
        public static bool IsCad2DFormat(string? format) => NormalizeFormat(format) is "dwg" or "dwgx";

        // File 3D (BIM/mesh: IFC, hoặc CAD không phải dwg/dwgx như rvt/nwc/nwd/dgn) không có đường ký số
        // trực quan -> không bắt buộc ký khi chuyển vùng Shared -> Published.
        public static bool Is3DModelFile(FileType fileType, string? format)
            => fileType == FileType.Ifc || (fileType == FileType.Cad && !IsCad2DFormat(format));

        public static string NormalizeFormat(string? format)
        {
            var normalized = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalized) ? "pdf" : normalized;
        }
    }
}

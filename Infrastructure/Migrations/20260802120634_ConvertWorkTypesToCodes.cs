using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertWorkTypesToCodes : Migration
    {
        // Không đổi schema. Chỉ chuyển dữ liệu cột WorkTypes từ NHÃN tiếng Việt sang MÃ
        // (XDT, STR, ARC, MEP, FIN, RCC, INF, PCCC) để mã gói thầu tự sinh không còn phụ
        // thuộc chuỗi hiển thị. Cột Code của gói thầu cũ giữ nguyên — đó là định danh đã
        // phát hành, không sửa lại.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ContractPackages"
                SET "WorkTypes" = replace(replace(replace(replace(replace(replace(replace(replace(
                    "WorkTypes",
                    'Phòng cháy chữa cháy', 'PCCC'),
                    'Bê tông cốt thép',     'RCC'),
                    'Hạ tầng kỹ thuật',     'INF'),
                    'Xây dựng thô',         'XDT'),
                    'Hoàn thiện',           'FIN'),
                    'Kiến trúc',            'ARC'),
                    'Kết cấu',              'STR'),
                    'Cơ điện',              'MEP')
                WHERE "WorkTypes" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ContractPackages"
                SET "WorkTypes" = replace(replace(replace(replace(replace(replace(replace(replace(
                    "WorkTypes",
                    'PCCC', 'Phòng cháy chữa cháy'),
                    'RCC',  'Bê tông cốt thép'),
                    'INF',  'Hạ tầng kỹ thuật'),
                    'XDT',  'Xây dựng thô'),
                    'FIN',  'Hoàn thiện'),
                    'ARC',  'Kiến trúc'),
                    'STR',  'Kết cấu'),
                    'MEP',  'Cơ điện')
                WHERE "WorkTypes" IS NOT NULL;
                """);
        }
    }
}

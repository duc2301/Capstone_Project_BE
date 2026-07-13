using Domain.Entities;
using Domain.Enum.Loi;
using static Domain.Enum.Loi.LoiParamGroup;

namespace Application.Services.Loi
{
    public static class LoiSeedData
    {
        public static (List<LoiRequirement> requirements, List<LoiFieldAlias> aliases) Build()
        {
            var reqs = new List<LoiRequirement>();

            void Common(string field, LoiParamGroup g, int stage) => reqs.Add(Req(null, null, field, g, stage, true));
            Common("Tên và Số hiệu", DinhDanh, 2);
            Common("Mã số phân loại", DinhDanh, 2);
            Common("Tên phân loại", DinhDanh, 2);
            Common("Hạng mục", DinhDanh, 2);
            Common("Vị trí theo tầng", DinhVi, 2);
            Common("Tên vật liệu", VatLieu, 2);

            void Comp(string code, string name, params (string field, LoiParamGroup g, int stage)[] fields)
            {
                foreach (var f in fields) reqs.Add(Req(code, name, f.field, f.g, f.stage, false));
            }

            Comp("21 01 10 10 10", "Móng tiêu chuẩn (móng băng/đơn)",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Tiết diện", HinhHoc, 2),
                ("Chiều dày", HinhHoc, 2), ("Thể tích", HinhHoc, 2), ("Biện pháp đổ bê tông", QuyCach, 2),
                ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 01 10 20 10", "Cọc ép / Cọc đóng",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Loại cọc", DinhDanh, 2), ("Chiều dài", HinhHoc, 2),
                ("Tiết diện cọc", HinhHoc, 2), ("Đường kính thép chủ", HinhHoc, 2), ("Đường kính thép đai", HinhHoc, 2),
                ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2), ("Biện pháp đổ bê tông", QuyCach, 2),
                ("Điều kiện thi công", QuyCach, 2), ("Sức chịu tải cọc", QuyCach, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2), ("Tiêu chuẩn sản xuất/Thiết kế", QuyCach, 2));

            Comp("21 01 10 20 15", "Cọc khoan nhồi",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Loại cọc", DinhDanh, 2), ("Tiết diện", HinhHoc, 2),
                ("Thể tích", HinhHoc, 2), ("Chiều dài", HinhHoc, 2), ("Tên vật liệu", VatLieu, 2),
                ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2), ("Biện pháp đổ bê tông", QuyCach, 2),
                ("Điều kiện thi công", QuyCach, 2), ("Sức chịu tải cọc", QuyCach, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 01 10 20 70", "Đài móng",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Tiết diện", HinhHoc, 2),
                ("Chiều dày", HinhHoc, 2), ("Thể tích", HinhHoc, 2), ("Biện pháp đổ bê tông", QuyCach, 2),
                ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 01 10 20 80", "Dầm móng",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Hình dáng", HinhHoc, 2),
                ("Tiết diện", HinhHoc, 2), ("Chiều dài", HinhHoc, 2), ("Thể tích", HinhHoc, 2),
                ("Biện pháp đổ bê tông", QuyCach, 2), ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2),
                ("Độ sụt bê tông", VatLieu, 2), ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 01 90 30 30", "Cừ Larsen",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Tiết diện", HinhHoc, 2),
                ("Chiều dài", HinhHoc, 2), ("Khối lượng", HinhHoc, 2), ("Loại cừ", DinhDanh, 2),
                ("Tên vật liệu", VatLieu, 2), ("Mác thép", VatLieu, 2), ("Biện pháp thi công", QuyCach, 2),
                ("Tiêu chuẩn sản xuất/Thiết kế", QuyCach, 2));

            Comp("21 02 10 10 10", "Dầm bê tông (khung sàn)",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Hình dáng", HinhHoc, 2),
                ("Tiết diện", HinhHoc, 2), ("Chiều dài", HinhHoc, 2), ("Thể tích", HinhHoc, 2),
                ("Biện pháp đổ bê tông", QuyCach, 2), ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2),
                ("Độ sụt bê tông", VatLieu, 2), ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 02 10 10 11", "Cột bê tông",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Hình dáng", HinhHoc, 2),
                ("Tiết diện", HinhHoc, 2), ("Chiều dài", HinhHoc, 2), ("Thể tích", HinhHoc, 2),
                ("Biện pháp đổ bê tông", QuyCach, 2), ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2),
                ("Độ sụt bê tông", VatLieu, 2), ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 02 10 10 20", "Sàn bê tông",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Chiều dày", HinhHoc, 2),
                ("Diện tích", HinhHoc, 2), ("Thể tích", HinhHoc, 2), ("Biện pháp đổ bê tông", QuyCach, 2),
                ("Tên vật liệu", VatLieu, 2), ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 02 10 20 10", "Kết cấu khung thép (dầm/cột thép, khung mái)",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Loại thép", DinhDanh, 2),
                ("Tiết diện", HinhHoc, 2), ("Thể tích", HinhHoc, 2), ("Tên vật liệu", VatLieu, 2),
                ("Mác thép", VatLieu, 2), ("Các lớp sơn", QuyCach, 2), ("Xử lý bề mặt hoàn thiện", QuyCach, 2),
                ("Màu sắc", QuyCach, 2), ("Loại liên kết", QuyCach, 3), ("Yêu cầu kỹ thuật thi công", QuyCach, 2),
                ("Tiêu chuẩn thiết kế/sản xuất", QuyCach, 2));

            Comp("21 02 10 80 10", "Thang bê tông",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Chiều dày", HinhHoc, 2),
                ("Thể tích", HinhHoc, 2), ("Biện pháp đổ bê tông", QuyCach, 2), ("Tên vật liệu", VatLieu, 2),
                ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2), ("Phụ gia", VatLieu, 2),
                ("Yêu cầu kỹ thuật thi công", QuyCach, 2));

            Comp("21 02 10 80 50", "Lan can thang",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Vị trí theo tầng", DinhVi, 2), ("Chiều dài", HinhHoc, 2), ("Diện tích", HinhHoc, 2),
                ("Thể tích", HinhHoc, 2), ("Vật liệu làm trụ, thanh ngang, tay vịn", VatLieu, 2),
                ("Màu sắc", QuyCach, 2), ("Yêu cầu kỹ thuật thi công", QuyCach, 2),
                ("Tiêu chuẩn thiết kế/sản xuất", QuyCach, 2));

            Comp("21 02 20 10 20", "Tường bê tông",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Hạng mục", DinhDanh, 2), ("Vị trí theo tầng", DinhVi, 2), ("Chiều dày", HinhHoc, 2),
                ("Thể tích", HinhHoc, 2), ("Biện pháp đổ bê tông", QuyCach, 2), ("Tên vật liệu", VatLieu, 2),
                ("Mác bê tông", VatLieu, 2), ("Độ sụt bê tông", VatLieu, 2), ("Phụ gia", VatLieu, 2),
                ("Yêu cầu kỹ thuật, bảo dưỡng", QuyCach, 2));

            Comp("21 03 10 10 10", "Vách ngăn (bê tông/gạch xây)",
                ("Tên và Số hiệu", DinhDanh, 2), ("Mã số phân loại", DinhDanh, 2), ("Tên phân loại", DinhDanh, 2),
                ("Vị trí theo tầng", DinhVi, 2), ("Chiều dày tường", HinhHoc, 2), ("Chiều dài", HinhHoc, 2),
                ("Thể tích", HinhHoc, 2), ("Tên vật liệu", VatLieu, 2), ("Mác vữa xây", VatLieu, 2),
                ("Mác gạch xây", VatLieu, 2), ("Tiêu chuẩn thiết kế/sản xuất", QuyCach, 2));

            var aliases = new List<LoiFieldAlias>();
            void Alias(string field, params string[] variants)
            {
                var canonical = IfcFieldText.Normalize(field);
                foreach (var v in variants)
                    aliases.Add(new LoiFieldAlias
                    {
                        Id = Guid.NewGuid(),
                        FieldNameNormalized = canonical,
                        AliasNormalized = IfcFieldText.Normalize(v)
                    });
            }

            Alias("Tên và Số hiệu", "Tên cấu kiện", "Mã cấu kiện", "Số hiệu", "Số hiệu cấu kiện", "Tên và số hiệu");
            Alias("Mã số phân loại", "Phân loại cấu kiện", "Mã định danh theo nhóm Omni", "Mã phân loại", "Mã Omni");
            Alias("Tên phân loại", "Tên định danh theo nhóm Omni", "Phân loại cấu kiện");
            Alias("Hạng mục", "Hạng mục công trình");
            Alias("Vị trí theo tầng", "Tên tầng", "Vị trí theo khu vực", "Vị trí theo dự án");
            Alias("Tên vật liệu", "Vật liệu thiết kế", "Vật liệu", "Vật liệu làm trụ, thanh ngang, tay vịn");
            Alias("Tiết diện", "Tiết diện thiết kế", "Kích thước");
            Alias("Chiều dày", "Chiều dày thiết kế");
            Alias("Chiều dài", "Chiều dài thiết kế");
            Alias("Thể tích", "Thể tích thiết kế", "Khối tích");
            Alias("Diện tích", "Diện tích thiết kế");
            Alias("Hình dáng", "Hình dạng thiết kế", "Hình dạng");
            Alias("Mác bê tông", "Cấp bền bê tông", "Mác BT");
            Alias("Yêu cầu kỹ thuật thi công", "Yêu cầu kỹ thuật", "Yêu cầu kỹ thuật khi thi công");

            return (reqs, aliases);
        }

        private static LoiRequirement Req(
            string? code, string? name, string field, LoiParamGroup g, int stage, bool common) => new()
            {
                Id = Guid.NewGuid(),
                Discipline = LoiDiscipline.KienTrucKetCau,
                ComponentCode = code,
                ComponentName = name,
                FieldName = field,
                FieldNameNormalized = IfcFieldText.Normalize(field),
                ParamGroup = g,
                Stage = stage,
                IsCommon = common
            };
    }
}

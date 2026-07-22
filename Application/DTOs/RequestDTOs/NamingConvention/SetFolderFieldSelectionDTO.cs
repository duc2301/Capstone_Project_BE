namespace Application.DTOs.RequestDTOs.NamingConvention
{
    // Danh sách field (không bắt buộc) được BẬT áp dụng cho folder — thay thế toàn bộ lựa chọn cũ.
    public class SetFolderFieldSelectionDTO
    {
        public List<Guid> FieldIds { get; set; } = new();
    }
}

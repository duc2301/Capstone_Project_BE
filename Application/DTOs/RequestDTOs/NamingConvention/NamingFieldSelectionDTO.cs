namespace Application.DTOs.RequestDTOs.NamingConvention
{
    // 1 lựa chọn dropdown của user khi upload: field nào -> value nào.
    // Upload gửi dạng JSON array trong form field "NamingSelections":
    // [{"fieldId":"...","valueId":"..."}]
    public class NamingFieldSelectionDTO
    {
        public Guid FieldId { get; set; }
        public Guid ValueId { get; set; }
    }
}

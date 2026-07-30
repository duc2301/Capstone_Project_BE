namespace Domain.Enum.Loi
{
    // Giá trị số chính là con số ghi trong ô của bảng chuẩn -> so sánh trực tiếp Stage <= giai đoạn kiểm.
    public enum LoiStage
    {
        SchematicDesign = 2,      // Thiết kế cơ sở
        DetailedDesign = 3,       // Thiết kế kỹ thuật
        ConstructionDrawing = 4,  // Thiết kế bản vẽ thi công
        Construction = 5          // Thi công
    }
}

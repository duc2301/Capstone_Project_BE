namespace Domain.Enum.Submittal
{
    // 1 bước: chỉ duyệt (vd hồ sơ nghiệm thu).
    // 2 bước: thẩm tra rồi mới duyệt (vd biện pháp/tiến độ thi công).
    public enum SubmittalWorkflowType
    {
        OneStep,
        TwoStep
    }
}

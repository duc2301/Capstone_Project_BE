namespace Domain.Enum.Cde
{
    // Hành động cần kiểm tra quyền trên 1 folder (ánh xạ 1-1 với 6 cờ FolderPermission).
    public enum FolderAction
    {
        View,
        Edit,
        Update,
        Download,
        Verify,
        Approve
    }
}

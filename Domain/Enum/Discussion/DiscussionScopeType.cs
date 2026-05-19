namespace Domain.Enum.Discussion
{
    // Thảo luận có thể đứng độc lập hoặc gắn vào 1 đối tượng khác
    public enum DiscussionScopeType
    {
        Standalone,
        File,
        Note,
        Submittal,
        Issue
    }
}

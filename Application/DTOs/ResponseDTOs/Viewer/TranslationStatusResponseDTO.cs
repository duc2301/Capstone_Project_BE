namespace Application.DTOs.ResponseDTOs.Viewer
{
    public static class TranslationStatuses
    {
        public const string Success = "success";
        public const string Failed = "failed";
        public const string Timeout = "timeout";
        public const string NotFound = "notfound";
    }

    public class TranslationStatusResponseDTO
    {
        public string Status { get; set; } = string.Empty;
        public string Progress { get; set; } = string.Empty;
    }
}

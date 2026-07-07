namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>Response tho tu goi API VNPT SmartCA, da parse san HTTP status va business success.</summary>
    public sealed record ExternalSmartCaResponse(
        string RawRequest,
        string SafeRawRequest,
        string RawResponse,
        int StatusCode,
        bool HttpSucceeded,
        bool IsBusinessSuccess,
        string Message);
}

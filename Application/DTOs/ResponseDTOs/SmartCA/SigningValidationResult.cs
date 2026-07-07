namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>Ket qua xac thuc ngu canh ky: co Context (thanh cong) hoac Error (that bai), khong bao gio ca hai.</summary>
    public sealed record SigningValidationResult(SigningContext? Context, string? Error)
    {
        public static SigningValidationResult Success(SigningContext context) => new(context, null);
        public static SigningValidationResult Fail(string error) => new(null, error);
    }
}

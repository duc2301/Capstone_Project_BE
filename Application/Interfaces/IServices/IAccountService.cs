using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;

namespace Application.Interfaces.IServices
{
    public interface IAccountService
    {
        Task<IEnumerable<AccountResponseDTO>> GetAllAsync();
        Task<AccountResponseDTO?> GetByIdAsync(Guid id);
        Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto, Guid actorId);
        Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto, Guid actorId);
        Task<AccountResponseDTO> SetAvatarAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid actorId);

        // Sinh file Excel template (UserName, Email) cho admin tải về điền thông tin nhân viên.
        byte[] GenerateImportTemplate();

        // Import hàng loạt tài khoản từ file Excel. Partial-success: tạo các dòng hợp lệ,
        // bỏ qua & báo cáo các dòng lỗi. Mật khẩu mặc định "123456", vai trò User, trạng thái Active.
        Task<ImportAccountsResultDTO> ImportFromExcelAsync(Stream file, Guid actorId);
    }
}

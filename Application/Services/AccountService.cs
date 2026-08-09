using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Audit;
using Syncfusion.XlsIO;
using System.ComponentModel.DataAnnotations;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private const string AvatarPrefix = "avatars";

        // Import Excel: mật khẩu mặc định cho mọi tài khoản tạo qua template.
        private const string DefaultImportPassword = "123456";
        private const int MaxImportRows = 500;
        private static readonly string[] ImportTemplateHeaders = { "UserName", "Email" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;
        private readonly IImageUploadService _imageUpload;

        public AccountService(
            IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog, IImageUploadService imageUpload)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
            _imageUpload = imageUpload;
        }

        public async Task<IEnumerable<AccountResponseDTO>> GetAllAsync()
        {
            var items = (await _unitOfWork.AccountRepository.GetAllAsync(nameof(Account.Organization))).ToList();
            var dtos = _mapper.Map<List<AccountResponseDTO>>(items);

            var avatarPaths = items.Select(a => a.AvatarStoragePath).ToList();
            for (var i = 0; i < dtos.Count; i++)
                dtos[i].AvatarUrl = await _imageUpload.GetImageUrlAsync(avatarPaths[i]);

            await AttachManagedProjectsAsync(dtos);
            return dtos;
        }

        public async Task<AccountResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id);
            return entity == null ? null : await BuildResponseAsync(entity);
        }

        public async Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto, Guid actorId)
        {
            if (await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email))
                throw new ApiExceptionResponse("Email already exists.", 409);

            if (dto.OrganizationId.HasValue)
                await GetOrganizationOrThrowAsync(dto.OrganizationId.Value);

            var account = _mapper.Map<Account>(dto);
            account.Id = Guid.NewGuid();
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            account.Status = AccountStatus.Active;
            account.Role = AccountRole.User;
            account.CreatedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.AccountRepository.CreateAsync(account);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Create, nameof(Account), account.Id.ToString(), actorId,
                detail: $"Tạo tài khoản '{account.UserName}' ({account.Email}) — vai trò {account.Role}");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(account);
        }

        public async Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            if (dto.OrganizationId.HasValue)
                await GetOrganizationOrThrowAsync(dto.OrganizationId.Value);

            _mapper.Map(dto, entity);

            if (dto.ClearOrganization == true)
                entity.OrganizationId = null;

            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(entity);

            var revokedSessions = 0;
            if (entity.Status != AccountStatus.Active)
                revokedSessions = await RevokeActiveRefreshTokensAsync(entity.Id);

            var sessionNote = revokedSessions > 0 ? $" — thu hồi {revokedSessions} phiên đăng nhập" : string.Empty;
            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Cập nhật tài khoản '{entity.UserName}' — vai trò {entity.Role}, trạng thái {entity.Status}{sessionNote}");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _unitOfWork.AccountRepository.Delete(entity);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Delete, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Xoá tài khoản '{entity.UserName}' ({entity.Email})");

            await _unitOfWork.CommitAsync();
        }

        public byte[] GenerateImportTemplate()
        {
            using var engine = new ExcelEngine();
            engine.Excel.DefaultVersion = ExcelVersion.Excel2016;
            var wb = engine.Excel.Workbooks.Create(1);
            var ws = wb.Worksheets[0];
            ws.Name = "Accounts";

            for (int column = 1; column <= ImportTemplateHeaders.Length; column++)
                ws[1, column].Text = ImportTemplateHeaders[column - 1];
            ws["A1:B1"].CellStyle.Font.Bold = true;

            // Dòng ví dụ để admin biết định dạng (mật khẩu mặc định "123456", vai trò User — không cần nhập).
            ws[2, 1].Text = "Nguyen Van A";
            ws[2, 2].Text = "nguyenvana@example.com";

            ws.UsedRange.AutofitColumns();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<ImportAccountsResultDTO> ImportFromExcelAsync(Stream file, Guid actorId)
        {
            var result = new ImportAccountsResultDTO();

            using var engine = new ExcelEngine();
            IWorkbook workbook;
            try
            {
                workbook = engine.Excel.Workbooks.Open(file, ExcelOpenType.Automatic);
            }
            catch (Exception)
            {
                throw new ApiExceptionResponse("Không đọc được file. Hãy dùng file .xlsx theo template mẫu.", 400);
            }

            var ws = workbook.Worksheets[0];

            // Kiểm tra header đúng template.
            for (int column = 1; column <= ImportTemplateHeaders.Length; column++)
            {
                var header = ws.Range[1, column].DisplayText?.Trim();
                if (!string.Equals(header, ImportTemplateHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
                    throw new ApiExceptionResponse(
                        $"File không đúng template (cột {column} phải là '{ImportTemplateHeaders[column - 1]}'). Hãy tải template mẫu và điền theo.", 400);
            }

            var emailValidator = new EmailAddressAttribute();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pass 1: đọc & tiền-kiểm tra từng dòng, gom các dòng hợp lệ chờ kiểm tra trùng trong DB.
            var candidates = new List<(int RowNumber, string UserName, string Email)>();

            int lastRow = ws.UsedRange.LastRow;
            for (int row = 2; row <= lastRow; row++)
            {
                var userName = ws.Range[row, 1].DisplayText?.Trim();
                var email = ws.Range[row, 2].DisplayText?.Trim();

                // Dòng trống hoàn toàn -> bỏ qua, không tính.
                if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(email))
                    continue;

                result.TotalRows++;

                if (result.TotalRows > MaxImportRows)
                    throw new ApiExceptionResponse($"File vượt quá {MaxImportRows} dòng dữ liệu.", 400);

                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(email))
                {
                    AddError(result, row, email, "Thiếu UserName hoặc Email.");
                    continue;
                }

                if (!emailValidator.IsValid(email))
                {
                    AddError(result, row, email, "Email không đúng định dạng.");
                    continue;
                }

                if (!seenEmails.Add(email))
                {
                    AddError(result, row, email, "Email bị trùng trong file.");
                    continue;
                }

                candidates.Add((row, userName, email));
            }

            // Pass 2: kiểm tra trùng với DB theo lô (1 truy vấn).
            var existingEmails = await _unitOfWork.AccountRepository
                .GetExistingEmailsAsync(candidates.Select(c => c.Email));

            var toCreate = new List<Account>();
            foreach (var (rowNumber, userName, email) in candidates)
            {
                if (existingEmails.Contains(email.ToLower()))
                {
                    AddError(result, rowNumber, email, "Email đã tồn tại trong hệ thống.");
                    continue;
                }

                var now = DateTime.UtcNow;
                toCreate.Add(new Account
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultImportPassword),
                    Role = AccountRole.User,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true, // Admin bảo lãnh — bỏ qua luồng OTP.
                    CreatedAt = now,
                    UpdatedAt = now
                });

                result.Created.Add(new ImportAccountCreatedDTO
                {
                    RowNumber = rowNumber,
                    UserName = userName,
                    Email = email
                });
            }

            if (toCreate.Count > 0)
            {
                await _unitOfWork.AccountRepository.CreateRangeAsync(toCreate);

                await _auditLog.LogAsync(
                    LogScope.System, AuditAction.Create, nameof(Account), actorId.ToString(), actorId,
                    detail: $"Import Excel: tạo {toCreate.Count} tài khoản (vai trò User), bỏ qua {result.SkippedCount} dòng.");

                await _unitOfWork.CommitAsync();
            }

            result.CreatedCount = toCreate.Count;
            return result;
        }

        private static void AddError(ImportAccountsResultDTO result, int rowNumber, string? email, string reason)
        {
            result.Errors.Add(new ImportAccountRowErrorDTO
            {
                RowNumber = rowNumber,
                Email = string.IsNullOrEmpty(email) ? null : email,
                Reason = reason
            });
            result.SkippedCount++;
        }

        public async Task<AccountResponseDTO> SetAvatarAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            entity.AvatarStoragePath = await _imageUpload.SaveImageAsync(
                content, fileName, sizeBytes, $"{AvatarPrefix}/{id}", ct);
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(entity);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Cập nhật ảnh đại diện tài khoản '{entity.UserName}'");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(entity);
        }

        private async Task<int> RevokeActiveRefreshTokensAsync(Guid accountId)
        {
            var tokens = (await _unitOfWork.RefreshTokenRepository
                    .FindAsync(rt => rt.AccountId == accountId && rt.RevokedAt == null))
                .ToList();

            foreach (var token in tokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                _unitOfWork.RefreshTokenRepository.Update(token);
            }

            return tokens.Count;
        }

        private async Task<Organization> GetOrganizationOrThrowAsync(Guid organizationId)
        {
            return await _unitOfWork.Repository<Organization>().GetByIdAsync(organizationId)
                ?? throw new ApiExceptionResponse($"Organization with ID {organizationId} not found.", 404);
        }

        private async Task<AccountResponseDTO> BuildResponseAsync(Account entity)
        {
            var dto = _mapper.Map<AccountResponseDTO>(entity);

            if (entity.OrganizationId.HasValue)
            {
                var organization = await _unitOfWork.Repository<Organization>()
                    .GetByIdAsync(entity.OrganizationId.Value);
                dto.OrganizationName = organization == null
                    ? null
                    : (organization.DisplayName ?? organization.LegalName);
            }

            dto.AvatarUrl = await _imageUpload.GetImageUrlAsync(entity.AvatarStoragePath);

            await AttachManagedProjectsAsync(new[] { dto });
            return dto;
        }

        private async Task AttachManagedProjectsAsync(IReadOnlyCollection<AccountResponseDTO> accounts)
        {
            if (accounts.Count == 0) return;

            var accountIds = accounts.Select(a => a.Id).ToList();
            var projects = await _unitOfWork.Repository<Project>()
                .FindAsync(p => p.ManagerAccountId != null && accountIds.Contains(p.ManagerAccountId.Value));

            var byManager = projects
                .GroupBy(p => p.ManagerAccountId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new AccountManagedProjectDTO
                    {
                        ProjectId = p.Id,
                        ProjectName = p.ProjectName
                    }).ToList());

            foreach (var account in accounts)
            {
                if (byManager.TryGetValue(account.Id, out var managed))
                    account.ManagedProjects = managed;
            }
        }
    }
}

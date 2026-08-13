using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services.Loi
{
    public sealed class LoiAliasService : ILoiAliasService
    {
        private readonly IUnitOfWork _uow;
        private readonly IFolderTreeRepository _folderTreeRepository;

        public LoiAliasService(IUnitOfWork uow, IFolderTreeRepository folderTreeRepository)
        {
            _uow = uow;
            _folderTreeRepository = folderTreeRepository;
        }

        public async Task<IReadOnlyList<LoiAliasResponseDTO>> GetByProjectAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            await RequireProjectManagerAsync(projectId, actor, isSystemAdmin);

            var aliases = (await _uow.Repository<LoiFieldAlias>()
                    .FindAsync(a => a.ProjectId == null || a.ProjectId == projectId))
                .ToList();

            return aliases
                .OrderBy(a => a.ProjectId == null)
                .ThenBy(a => a.FieldNameNormalized)
                .Select(Map)
                .ToList();
        }

        public async Task<LoiAliasResponseDTO> CreateAsync(
            Guid projectId, CreateLoiAliasDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            await RequireProjectManagerAsync(projectId, actor, isSystemAdmin);

            var alias = IfcFieldText.Normalize(dto.ParamNameInModel);
            var standard = IfcFieldText.Normalize(dto.StandardParamName);

            if (alias.Length == 0 || standard.Length == 0)
                throw new ApiExceptionResponse("Tên tham số không hợp lệ.", 400);
            if (alias == standard)
                throw new ApiExceptionResponse("Tên trong model trùng với tham số chuẩn, không cần ánh xạ.", 400);

            var isKnownParam = (await _uow.Repository<LoiRequirement>()
                .FindAsync(r => r.ParamNameNormalized == standard)).Any();
            if (!isKnownParam)
                throw new ApiExceptionResponse(
                    $"\"{dto.StandardParamName}\" không phải tham số chuẩn trong bảng BXD 347 Phụ lục 02.", 400);

            var existing = (await _uow.Repository<LoiFieldAlias>()
                    .FindAsync(a => a.AliasNormalized == alias && (a.ProjectId == null || a.ProjectId == projectId)))
                .FirstOrDefault();
            if (existing is not null)
                throw new ApiExceptionResponse(
                    $"\"{dto.ParamNameInModel}\" đã được ánh xạ sang \"{existing.FieldNameNormalized}\".", 409);

            var entity = new LoiFieldAlias
            {
                Id = Guid.NewGuid(),
                FieldNameNormalized = standard,
                AliasNormalized = alias,
                ProjectId = projectId,
                CreatedByAccountId = actor,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<LoiFieldAlias>().CreateAsync(entity);
            await _uow.CommitAsync();

            return Map(entity);
        }

        public async Task DeleteAsync(
            Guid projectId, Guid aliasId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            await RequireProjectManagerAsync(projectId, actor, isSystemAdmin);

            var alias = await _uow.Repository<LoiFieldAlias>().GetByIdAsync(aliasId)
                ?? throw new ApiExceptionResponse("Alias not found.", 404);

            if (alias.ProjectId != projectId)
                throw new ApiExceptionResponse(
                    "Chỉ xoá được ánh xạ của chính dự án này (ánh xạ dùng chung do hệ thống quản lý).", 403);

            _uow.Repository<LoiFieldAlias>().Delete(alias);
            await _uow.CommitAsync();
        }

        private async Task<Project> RequireProjectAsync(Guid projectId) =>
            await _uow.Repository<Project>().GetByIdAsync(projectId)
            ?? throw new ApiExceptionResponse("Project not found.", 404);

        private async Task RequireProjectManagerAsync(Guid projectId, Guid actor, bool isSystemAdmin)
        {
            var project = await RequireProjectAsync(projectId);
            if (isSystemAdmin || project.ManagerAccountId == actor) return;

            if (!await _folderTreeRepository.HasFullAccessAsync(projectId, actor))
                throw new ApiExceptionResponse(
                    "Chỉ quản trị hệ thống hoặc quản lý dự án được sửa từ điển tên tham số.", 403);
        }

        private static LoiAliasResponseDTO Map(LoiFieldAlias a) => new()
        {
            Id = a.Id,
            ParamNameInModel = a.AliasNormalized,
            StandardParamName = a.FieldNameNormalized,
            IsSystemWide = a.ProjectId is null,
            CreatedAt = a.CreatedAt
        };
    }
}

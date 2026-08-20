using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public sealed class ProjectLoiRuleService : IProjectLoiRuleService
    {
        private const string RuleSetEntity = "LoiRuleSet";

        private readonly IUnitOfWork _uow;
        private readonly ILoiRuleRepository _rules;
        private readonly ILoiRuleAdminService _admin;
        private readonly ILoiRuleImportService _import;
        private readonly IAuditLogService _audit;

        public ProjectLoiRuleService(
            IUnitOfWork uow,
            ILoiRuleRepository rules,
            ILoiRuleAdminService admin,
            ILoiRuleImportService import,
            IAuditLogService audit)
        {
            _uow = uow;
            _rules = rules;
            _admin = admin;
            _import = import;
            _audit = audit;
        }

        public async Task<LoiRuleSetDTO?> GetRuleSetSummaryAsync(Guid projectId, CancellationToken ct = default)
            => await _admin.GetProjectRuleSetAsync(projectId, ct);

        public async Task<LoiRuleSetDTO?> GetRuleSetAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            await RequireManagerAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.GetProjectRuleSetAsync(projectId, ct);
        }

        public async Task<LoiRuleSetDTO> UpdateRuleSetAsync(
            Guid projectId, UpdateLoiRuleSetDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.UpdateRuleSetAsync(ruleSetId, dto, actor, ct);
        }

        public async Task DeleteRuleSetAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);

            await DetachFromProjectAsync(projectId, actor);
            await _admin.DeleteRuleSetAsync(ruleSetId, actor, ct);
        }

        public async Task<byte[]> GenerateTemplateAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var project = await RequireManagerAsync(projectId, actor, isSystemAdmin, ct);
            return await _import.GenerateTemplateAsync(project.LoiRuleSetId, ct);
        }

        public async Task<LoiImportPreviewDTO> ParseImportAsync(
            Guid projectId, Stream stream, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var project = await RequireManagerAsync(projectId, actor, isSystemAdmin, ct);
            return await _import.ParseAsync(stream, project.LoiRuleSetId, ct);
        }

        public async Task<LoiRuleSetDTO> CommitImportAsync(
            Guid projectId, LoiImportCommitDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var project = await RequireManagerAsync(projectId, actor, isSystemAdmin, ct);

            if (project.LoiRuleSetId is null)
            {
                dto.Mode = LoiImportMode.CreateNew;
                dto.TargetRuleSetId = null;
                if (string.IsNullOrWhiteSpace(dto.NewRuleSetName))
                    dto.NewRuleSetName = BuildRuleSetName(project.ProjectName);
            }
            else
            {
                if (dto.Mode == LoiImportMode.CreateNew)
                    throw new ApiExceptionResponse(
                        "Dự án đã có bộ luật riêng — hãy chọn gộp hoặc thay toàn bộ thay vì tạo bộ mới.", 400);

                dto.TargetRuleSetId = project.LoiRuleSetId;
            }

            var result = await _import.CommitAsync(dto, actor, ct);

            if (project.LoiRuleSetId != result.Id)
                await AttachToProjectAsync(projectId, result.Id, actor);

            return result;
        }

        public async Task<IReadOnlyList<LoiComponentDTO>> GetComponentsAsync(
            Guid projectId, LoiDiscipline? discipline, string? search, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.GetComponentsAsync(ruleSetId, discipline, search, ct);
        }

        public async Task<LoiComponentDTO> CreateComponentAsync(
            Guid projectId, CreateLoiComponentDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.CreateComponentAsync(ruleSetId, dto, actor, ct);
        }

        public async Task<LoiComponentDTO> UpdateComponentAsync(
            Guid projectId, Guid componentId, UpdateLoiComponentDTO dto, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.UpdateComponentAsync(ruleSetId, componentId, dto, actor, ct);
        }

        public async Task DeleteComponentAsync(
            Guid projectId, Guid componentId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            await _admin.DeleteComponentAsync(ruleSetId, componentId, actor, ct);
        }

        public async Task<LoiMatrixDTO> GetMatrixAsync(
            Guid projectId, Guid componentId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.GetMatrixAsync(ruleSetId, componentId, ct);
        }

        public async Task<LoiMatrixDTO> SaveMatrixAsync(
            Guid projectId, Guid componentId, SaveLoiMatrixDTO dto, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.SaveMatrixAsync(ruleSetId, componentId, dto, actor, ct);
        }

        public async Task<LoiMatrixDTO> RenameVariantAsync(
            Guid projectId, Guid componentId, RenameLoiVariantDTO dto, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.RenameVariantAsync(ruleSetId, componentId, dto, actor, ct);
        }

        public async Task<LoiMatrixDTO> DeleteVariantAsync(
            Guid projectId, Guid componentId, string? variant, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.DeleteVariantAsync(ruleSetId, componentId, variant, actor, ct);
        }

        public async Task<IReadOnlyList<LoiParameterDTO>> GetParametersAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.GetParametersAsync(ruleSetId, ct);
        }

        public async Task<LoiParameterDTO> CreateParameterAsync(
            Guid projectId, CreateLoiParameterDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.CreateParameterAsync(ruleSetId, dto, actor, ct);
        }

        public async Task<LoiParameterDTO> UpdateParameterAsync(
            Guid projectId, Guid parameterId, UpdateLoiParameterDTO dto, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            return await _admin.UpdateParameterAsync(ruleSetId, parameterId, dto, actor, ct);
        }

        public async Task DeleteParameterAsync(
            Guid projectId, Guid parameterId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var ruleSetId = await RequireRuleSetIdAsync(projectId, actor, isSystemAdmin, ct);
            await _admin.DeleteParameterAsync(ruleSetId, parameterId, actor, ct);
        }

        private static string BuildRuleSetName(string projectName) =>
            $"Bộ luật phi hình học — {projectName}";

        private async Task<Project> RequireManagerAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct)
        {
            var project = await _rules.GetProjectAsync(projectId, ct)
                ?? throw new ApiExceptionResponse("Không tìm thấy dự án.", 404);

            if (!isSystemAdmin && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse(
                    "Chỉ quản trị hệ thống hoặc quản lý dự án được thiết lập bộ luật thông tin phi hình học.", 403);

            return project;
        }

        private async Task<Guid> RequireRuleSetIdAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct)
        {
            var project = await RequireManagerAsync(projectId, actor, isSystemAdmin, ct);

            return project.LoiRuleSetId
                ?? throw new ApiExceptionResponse("Dự án chưa cấu hình bộ luật thông tin phi hình học.", 404);
        }

        private async Task AttachToProjectAsync(Guid projectId, Guid ruleSetId, Guid actor)
        {
            var project = await RequireTrackedProjectAsync(projectId);

            project.LoiRuleSetId = ruleSetId;
            project.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Project>().Update(project);

            await _audit.LogAsync(LogScope.Project, AuditAction.Update, RuleSetEntity, projectId.ToString(), actor,
                "Dự án nhận bộ luật thông tin phi hình học riêng nhập từ Excel", projectId);
            await _uow.CommitAsync();
        }

        private async Task DetachFromProjectAsync(Guid projectId, Guid actor)
        {
            var project = await RequireTrackedProjectAsync(projectId);

            project.LoiRuleSetId = null;
            project.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Project>().Update(project);

            await _audit.LogAsync(LogScope.Project, AuditAction.Delete, RuleSetEntity, projectId.ToString(), actor,
                "Xoá bộ luật thông tin phi hình học của dự án", projectId);
            await _uow.CommitAsync();
        }

        private async Task<Project> RequireTrackedProjectAsync(Guid projectId) =>
            await _uow.Repository<Project>().GetByIdAsync(projectId)
            ?? throw new ApiExceptionResponse("Không tìm thấy dự án.", 404);
    }
}

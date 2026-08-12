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
    public sealed class LoiRuleAdminService : ILoiRuleAdminService
    {
        private const string RuleSetEntity = "LoiRuleSet";
        private const string ComponentEntity = "LoiComponent";
        private const string ParameterEntity = "LoiParameter";
        private const string RequirementEntity = "LoiRequirement";
        private const string AliasEntity = "LoiFieldAlias";

        private readonly IUnitOfWork _uow;
        private readonly ILoiRuleRepository _rules;
        private readonly IAuditLogService _audit;

        public LoiRuleAdminService(IUnitOfWork uow, ILoiRuleRepository rules, IAuditLogService audit)
        {
            _uow = uow;
            _rules = rules;
            _audit = audit;
        }

        public async Task<IReadOnlyList<LoiRuleSetDTO>> GetRuleSetsAsync(CancellationToken ct = default)
        {
            var sets = (await _uow.Repository<LoiRuleSet>().GetAllAsync()).ToList();
            var counts = await _rules.GetRuleSetCountsAsync(ct);
            var inheriting = await _rules.CountProjectsInheritingDefaultAsync(ct);

            return sets
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.Name)
                .Select(s => Map(s, counts, inheriting))
                .ToList();
        }

        public async Task<LoiRuleSetDTO> CreateRuleSetAsync(
            CreateLoiRuleSetDTO dto, Guid actor, CancellationToken ct = default)
        {
            var name = RequireText(dto.Name, "Tên bộ luật không được để trống.");
            await RequireUniqueRuleSetNameAsync(name, null);

            var isFirst = !(await _uow.Repository<LoiRuleSet>().GetAllAsync()).Any();

            var entity = new LoiRuleSet
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                IsDefault = isFirst,
                IsSystem = false,
                CreatedByAccountId = actor,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<LoiRuleSet>().CreateAsync(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Create, RuleSetEntity, entity.Id.ToString(), actor,
                $"Tạo bộ luật LOI \"{entity.Name}\"");
            await _uow.CommitAsync();

            return Map(entity, await _rules.GetRuleSetCountsAsync(ct),
                await _rules.CountProjectsInheritingDefaultAsync(ct));
        }

        public async Task<LoiRuleSetDTO> UpdateRuleSetAsync(
            Guid ruleSetId, UpdateLoiRuleSetDTO dto, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireRuleSetAsync(ruleSetId);

            if (dto.Name is not null)
            {
                var name = RequireText(dto.Name, "Tên bộ luật không được để trống.");
                await RequireUniqueRuleSetNameAsync(name, ruleSetId);
                entity.Name = name;
            }

            if (dto.Description is not null)
                entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

            entity.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<LoiRuleSet>().Update(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Update, RuleSetEntity, entity.Id.ToString(), actor,
                $"Sửa bộ luật LOI \"{entity.Name}\"");
            await _uow.CommitAsync();

            return Map(entity, await _rules.GetRuleSetCountsAsync(ct),
                await _rules.CountProjectsInheritingDefaultAsync(ct));
        }

        public async Task DeleteRuleSetAsync(Guid ruleSetId, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireRuleSetAsync(ruleSetId);

            if (entity.IsSystem)
                throw new ApiExceptionResponse(
                    "Không xoá được bộ luật gốc của hệ thống. Hãy tạo bộ luật mới nếu cần bộ khác.", 409);

            if (entity.IsDefault)
                throw new ApiExceptionResponse(
                    "Không xoá được bộ luật đang đặt làm mặc định. Hãy đặt bộ khác làm mặc định trước.", 409);

            var projectCount = await _rules.CountProjectsUsingAsync(ruleSetId, ct);
            if (projectCount > 0)
                throw new ApiExceptionResponse(
                    $"Bộ luật đang được {projectCount} dự án sử dụng. Hãy chuyển các dự án đó sang bộ khác trước.", 409);

            _uow.Repository<LoiRuleSet>().Delete(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Delete, RuleSetEntity, entity.Id.ToString(), actor,
                $"Xoá bộ luật LOI \"{entity.Name}\"");
            await _uow.CommitAsync();
        }

        public async Task<LoiRuleSetDTO> SetDefaultRuleSetAsync(
            Guid ruleSetId, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireRuleSetAsync(ruleSetId);

            var current = (await _uow.Repository<LoiRuleSet>().FindAsync(s => s.IsDefault)).ToList();
            foreach (var other in current.Where(s => s.Id != ruleSetId))
            {
                other.IsDefault = false;
                _uow.Repository<LoiRuleSet>().Update(other);
            }

            entity.IsDefault = true;
            entity.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<LoiRuleSet>().Update(entity);

            await _audit.LogAsync(LogScope.System, AuditAction.Update, RuleSetEntity, entity.Id.ToString(), actor,
                $"Đặt \"{entity.Name}\" làm bộ luật LOI mặc định");
            await _uow.CommitAsync();

            return Map(entity, await _rules.GetRuleSetCountsAsync(ct),
                await _rules.CountProjectsInheritingDefaultAsync(ct));
        }

        public async Task<IReadOnlyList<LoiComponentDTO>> GetComponentsAsync(
            Guid ruleSetId, LoiDiscipline? discipline, string? search, CancellationToken ct = default)
        {
            await RequireRuleSetAsync(ruleSetId);

            var components = await _rules.SearchComponentsAsync(ruleSetId, discipline, search, ct);
            var usage = await _rules.GetComponentUsageAsync(ruleSetId, ct);

            return components.Select(c => MapComponent(c, usage)).ToList();
        }

        public async Task<LoiComponentDTO> CreateComponentAsync(
            Guid ruleSetId, CreateLoiComponentDTO dto, Guid actor, CancellationToken ct = default)
        {
            await RequireRuleSetAsync(ruleSetId);

            var code = RequireText(dto.Code, "Mã cấu kiện không được để trống.");
            var name = RequireText(dto.Name, "Tên cấu kiện không được để trống.");
            var codeNormalized = LoiEvaluator.NormalizeCode(code);

            if (codeNormalized.Length == 0)
                throw new ApiExceptionResponse("Mã cấu kiện phải chứa ít nhất một chữ hoặc số.", 400);

            var duplicate = (await _uow.Repository<LoiComponent>()
                .FindAsync(c => c.RuleSetId == ruleSetId && c.CodeNormalized == codeNormalized)).Any();
            if (duplicate)
                throw new ApiExceptionResponse($"Mã cấu kiện \"{code}\" đã có trong bộ luật này.", 409);

            var entity = new LoiComponent
            {
                Id = Guid.NewGuid(),
                RuleSetId = ruleSetId,
                Discipline = dto.Discipline,
                Code = code,
                CodeNormalized = codeNormalized,
                Name = name
            };

            await _uow.Repository<LoiComponent>().CreateAsync(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Create, ComponentEntity, entity.Id.ToString(), actor,
                $"Thêm cấu kiện LOI {entity.Code} — {entity.Name}");
            await _uow.CommitAsync();

            return MapComponent(entity, await _rules.GetComponentUsageAsync(ruleSetId, ct));
        }

        public async Task<LoiComponentDTO> UpdateComponentAsync(
            Guid ruleSetId, Guid componentId, UpdateLoiComponentDTO dto, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireComponentAsync(ruleSetId, componentId);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, entity.Code, ct);

            if (dto.Code is not null)
            {
                var code = RequireText(dto.Code, "Mã cấu kiện không được để trống.");
                var codeNormalized = LoiEvaluator.NormalizeCode(code);
                if (codeNormalized.Length == 0)
                    throw new ApiExceptionResponse("Mã cấu kiện phải chứa ít nhất một chữ hoặc số.", 400);

                var duplicate = (await _uow.Repository<LoiComponent>()
                    .FindAsync(c => c.RuleSetId == ruleSetId && c.CodeNormalized == codeNormalized && c.Id != componentId)).Any();
                if (duplicate)
                    throw new ApiExceptionResponse($"Mã cấu kiện \"{code}\" đã có trong bộ luật này.", 409);

                entity.Code = code;
                entity.CodeNormalized = codeNormalized;
            }

            if (dto.Name is not null)
                entity.Name = RequireText(dto.Name, "Tên cấu kiện không được để trống.");

            if (dto.Discipline.HasValue && dto.Discipline.Value != entity.Discipline)
            {
                if (requirements.Count > 0)
                    throw new ApiExceptionResponse(
                        $"Cấu kiện đang có {requirements.Count} dòng yêu cầu theo bộ môn \"{DisciplineName(entity.Discipline)}\". "
                        + "Hai bộ môn dùng danh mục tham số riêng nên phải xoá các biến thể trước khi đổi bộ môn.", 409);

                entity.Discipline = dto.Discipline.Value;
            }

            foreach (var requirement in requirements)
            {
                requirement.ComponentCode = entity.Code;
                requirement.ComponentName = entity.Name;
                requirement.Discipline = entity.Discipline;
                _uow.Repository<LoiRequirement>().Update(requirement);
            }

            _uow.Repository<LoiComponent>().Update(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Update, ComponentEntity, entity.Id.ToString(), actor,
                $"Sửa cấu kiện LOI {entity.Code} — {entity.Name}");
            await _uow.CommitAsync();

            return MapComponent(entity, await _rules.GetComponentUsageAsync(ruleSetId, ct));
        }

        public async Task DeleteComponentAsync(
            Guid ruleSetId, Guid componentId, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireComponentAsync(ruleSetId, componentId);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, entity.Code, ct);

            foreach (var requirement in requirements)
                _uow.Repository<LoiRequirement>().Delete(requirement);

            _uow.Repository<LoiComponent>().Delete(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Delete, ComponentEntity, entity.Id.ToString(), actor,
                $"Xoá cấu kiện LOI {entity.Code} — {entity.Name} ({requirements.Count} dòng yêu cầu)");
            await _uow.CommitAsync();
        }

        public async Task<LoiMatrixDTO> GetMatrixAsync(
            Guid ruleSetId, Guid componentId, CancellationToken ct = default)
        {
            var component = await RequireComponentAsync(ruleSetId, componentId);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, component.Code, ct);
            var parameters = await LoadParametersAsync(ruleSetId, component.Discipline);

            return BuildMatrix(component, requirements, parameters);
        }

        public async Task<LoiMatrixDTO> SaveMatrixAsync(
            Guid ruleSetId, Guid componentId, SaveLoiMatrixDTO dto, Guid actor, CancellationToken ct = default)
        {
            var component = await RequireComponentAsync(ruleSetId, componentId);
            var parameters = await LoadParametersAsync(ruleSetId, component.Discipline);
            var byNormalizedName = parameters.ToDictionary(p => p.NameNormalized);

            var variant = NormalizeVariant(dto.Variant);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, component.Code, ct);

            foreach (var stale in requirements.Where(r => NormalizeVariant(r.Variant) == variant))
                _uow.Repository<LoiRequirement>().Delete(stale);

            var seenFields = new HashSet<string>();
            var fieldOrder = 0;
            var cellCount = 0;

            foreach (var row in dto.Rows)
            {
                var fieldName = RequireText(row.FieldName, "Tên thông tin cần có không được để trống.");
                var fieldNameNormalized = IfcFieldText.Normalize(fieldName);

                if (fieldNameNormalized.Length == 0)
                    throw new ApiExceptionResponse($"\"{fieldName}\" không phải tên hợp lệ.", 400);

                if (!seenFields.Add(fieldNameNormalized))
                    throw new ApiExceptionResponse($"\"{fieldName}\" bị lặp trong cùng một biến thể.", 400);

                if (row.Cells.Count == 0)
                    throw new ApiExceptionResponse(
                        $"Dòng \"{fieldName}\" chưa đánh dấu tham số nào nên không có gì để lưu. "
                        + "Hãy đánh dấu ít nhất một ô hoặc xoá dòng đó.", 400);

                var seenParams = new HashSet<string>();

                foreach (var cell in row.Cells)
                {
                    if (!System.Enum.IsDefined(cell.Stage))
                        throw new ApiExceptionResponse("Giai đoạn không hợp lệ (chỉ nhận 2, 3, 4, 5).", 400);

                    var paramNormalized = IfcFieldText.Normalize(cell.ParamName);
                    if (!byNormalizedName.TryGetValue(paramNormalized, out var parameter))
                        throw new ApiExceptionResponse(
                            $"\"{cell.ParamName}\" không thuộc danh mục tham số chuẩn của bộ môn cấu kiện này.", 400);

                    if (!seenParams.Add(paramNormalized))
                        throw new ApiExceptionResponse(
                            $"Tham số \"{parameter.Name}\" bị đánh dấu hai lần trên cùng dòng \"{fieldName}\".", 400);

                    await _uow.Repository<LoiRequirement>().CreateAsync(new LoiRequirement
                    {
                        Id = Guid.NewGuid(),
                        RuleSetId = ruleSetId,
                        Discipline = component.Discipline,
                        ComponentCode = component.Code,
                        ComponentName = component.Name,
                        Variant = variant,
                        FieldOrder = fieldOrder,
                        FieldName = fieldName,
                        FieldNameNormalized = fieldNameNormalized,
                        ParamName = parameter.Name,
                        ParamNameNormalized = parameter.NameNormalized,
                        ParamGroup = parameter.ParamGroup,
                        Stage = cell.Stage
                    });
                    cellCount++;
                }

                fieldOrder++;
            }

            await _audit.LogAsync(LogScope.System, AuditAction.Update, RequirementEntity, component.Id.ToString(), actor,
                $"Cập nhật yêu cầu LOI của {component.Code} — biến thể \"{variant ?? "(mặc định)"}\": {dto.Rows.Count} dòng / {cellCount} ô");
            await _uow.CommitAsync();

            return await GetMatrixAsync(ruleSetId, componentId, ct);
        }

        public async Task<LoiMatrixDTO> RenameVariantAsync(
            Guid ruleSetId, Guid componentId, RenameLoiVariantDTO dto, Guid actor, CancellationToken ct = default)
        {
            var component = await RequireComponentAsync(ruleSetId, componentId);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, component.Code, ct);

            var currentVariant = NormalizeVariant(dto.CurrentVariant);
            var newVariant = NormalizeVariant(dto.NewVariant);

            if (currentVariant == newVariant)
                return BuildMatrix(component, requirements, await LoadParametersAsync(ruleSetId, component.Discipline));

            var alreadyUsed = requirements.Any(r => NormalizeVariant(r.Variant) == newVariant);
            if (alreadyUsed)
                throw new ApiExceptionResponse(
                    $"Cấu kiện này đã có biến thể \"{newVariant ?? "(mặc định)"}\".", 409);

            var target = requirements.Where(r => NormalizeVariant(r.Variant) == currentVariant).ToList();
            if (target.Count == 0)
                throw new ApiExceptionResponse("Không tìm thấy biến thể cần đổi tên.", 404);

            foreach (var requirement in target)
            {
                requirement.Variant = newVariant;
                _uow.Repository<LoiRequirement>().Update(requirement);
            }

            await _audit.LogAsync(LogScope.System, AuditAction.Update, RequirementEntity, component.Id.ToString(), actor,
                $"Đổi tên biến thể LOI của {component.Code}: \"{currentVariant ?? "(mặc định)"}\" thành \"{newVariant ?? "(mặc định)"}\"");
            await _uow.CommitAsync();

            return await GetMatrixAsync(ruleSetId, componentId, ct);
        }

        public async Task<LoiMatrixDTO> DeleteVariantAsync(
            Guid ruleSetId, Guid componentId, string? variant, Guid actor, CancellationToken ct = default)
        {
            var component = await RequireComponentAsync(ruleSetId, componentId);
            var requirements = await _rules.GetRequirementsByComponentAsync(ruleSetId, component.Code, ct);

            var normalized = NormalizeVariant(variant);
            var target = requirements.Where(r => NormalizeVariant(r.Variant) == normalized).ToList();
            if (target.Count == 0)
                throw new ApiExceptionResponse("Không tìm thấy biến thể cần xoá.", 404);

            foreach (var requirement in target)
                _uow.Repository<LoiRequirement>().Delete(requirement);

            await _audit.LogAsync(LogScope.System, AuditAction.Delete, RequirementEntity, component.Id.ToString(), actor,
                $"Xoá biến thể LOI \"{normalized ?? "(mặc định)"}\" của {component.Code} ({target.Count} dòng)");
            await _uow.CommitAsync();

            return await GetMatrixAsync(ruleSetId, componentId, ct);
        }

        public async Task<IReadOnlyList<LoiParameterDTO>> GetParametersAsync(
            Guid ruleSetId, CancellationToken ct = default)
        {
            await RequireRuleSetAsync(ruleSetId);

            var parameters = await LoadParametersAsync(ruleSetId);
            var usage = await _rules.GetParameterUsageAsync(ruleSetId, ct);

            return parameters.Select(p => MapParameter(p, usage)).ToList();
        }

        public async Task<LoiParameterDTO> CreateParameterAsync(
            Guid ruleSetId, CreateLoiParameterDTO dto, Guid actor, CancellationToken ct = default)
        {
            await RequireRuleSetAsync(ruleSetId);

            var name = RequireText(dto.Name, "Tên tham số không được để trống.");
            var normalized = IfcFieldText.Normalize(name);
            if (normalized.Length == 0)
                throw new ApiExceptionResponse($"\"{name}\" không phải tên tham số hợp lệ.", 400);

            var duplicate = (await _uow.Repository<LoiParameter>()
                .FindAsync(p => p.RuleSetId == ruleSetId
                             && p.Discipline == dto.Discipline
                             && p.NameNormalized == normalized)).Any();
            if (duplicate)
                throw new ApiExceptionResponse($"Tham số \"{name}\" đã có trong bộ môn này.", 409);

            var entity = new LoiParameter
            {
                Id = Guid.NewGuid(),
                RuleSetId = ruleSetId,
                Discipline = dto.Discipline,
                Name = name,
                NameNormalized = normalized,
                ParamGroup = dto.ParamGroup,
                OrderIndex = dto.OrderIndex
            };

            await _uow.Repository<LoiParameter>().CreateAsync(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Create, ParameterEntity, entity.Id.ToString(), actor,
                $"Thêm tham số chuẩn LOI \"{entity.Name}\"");
            await _uow.CommitAsync();

            return MapParameter(entity, await _rules.GetParameterUsageAsync(ruleSetId, ct));
        }

        public async Task<LoiParameterDTO> UpdateParameterAsync(
            Guid ruleSetId, Guid parameterId, UpdateLoiParameterDTO dto, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireParameterAsync(ruleSetId, parameterId);
            var previousNormalized = entity.NameNormalized;

            if (dto.Name is not null)
            {
                var name = RequireText(dto.Name, "Tên tham số không được để trống.");
                var normalized = IfcFieldText.Normalize(name);
                if (normalized.Length == 0)
                    throw new ApiExceptionResponse($"\"{name}\" không phải tên tham số hợp lệ.", 400);

                var duplicate = (await _uow.Repository<LoiParameter>()
                    .FindAsync(p => p.RuleSetId == ruleSetId
                                 && p.Discipline == entity.Discipline
                                 && p.NameNormalized == normalized
                                 && p.Id != parameterId)).Any();
                if (duplicate)
                    throw new ApiExceptionResponse($"Tham số \"{name}\" đã có trong bộ môn này.", 409);

                entity.Name = name;
                entity.NameNormalized = normalized;
            }

            if (dto.ParamGroup.HasValue)
                entity.ParamGroup = dto.ParamGroup.Value;

            if (dto.OrderIndex.HasValue)
                entity.OrderIndex = dto.OrderIndex.Value;

            var affected = (await _uow.Repository<LoiRequirement>()
                .FindAsync(r => r.RuleSetId == ruleSetId
                             && r.Discipline == entity.Discipline
                             && r.ParamNameNormalized == previousNormalized)).ToList();

            foreach (var requirement in affected)
            {
                requirement.ParamName = entity.Name;
                requirement.ParamNameNormalized = entity.NameNormalized;
                requirement.ParamGroup = entity.ParamGroup;
                _uow.Repository<LoiRequirement>().Update(requirement);
            }

            _uow.Repository<LoiParameter>().Update(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Update, ParameterEntity, entity.Id.ToString(), actor,
                $"Sửa tham số chuẩn LOI \"{entity.Name}\" ({affected.Count} dòng yêu cầu cập nhật theo)");
            await _uow.CommitAsync();

            return MapParameter(entity, await _rules.GetParameterUsageAsync(ruleSetId, ct));
        }

        public async Task DeleteParameterAsync(
            Guid ruleSetId, Guid parameterId, Guid actor, CancellationToken ct = default)
        {
            var entity = await RequireParameterAsync(ruleSetId, parameterId);

            var usage = await _rules.GetParameterUsageAsync(ruleSetId, ct);
            if (usage.TryGetValue((entity.Discipline, entity.NameNormalized), out var count) && count > 0)
                throw new ApiExceptionResponse(
                    $"Tham số \"{entity.Name}\" đang được {count} dòng yêu cầu sử dụng. Hãy bỏ đánh dấu ở các cấu kiện trước.", 409);

            _uow.Repository<LoiParameter>().Delete(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Delete, ParameterEntity, entity.Id.ToString(), actor,
                $"Xoá tham số chuẩn LOI \"{entity.Name}\"");
            await _uow.CommitAsync();
        }

        public async Task<IReadOnlyList<LoiAliasResponseDTO>> GetSystemAliasesAsync(
            string? search, CancellationToken ct = default)
        {
            var aliases = (await _uow.Repository<LoiFieldAlias>().FindAsync(a => a.ProjectId == null)).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = IfcFieldText.Normalize(search);
                if (term.Length > 0)
                    aliases = aliases
                        .Where(a => a.AliasNormalized.Contains(term) || a.FieldNameNormalized.Contains(term))
                        .ToList();
            }

            return aliases
                .OrderBy(a => a.FieldNameNormalized)
                .ThenBy(a => a.AliasNormalized)
                .Select(MapAlias)
                .ToList();
        }

        public async Task<LoiAliasResponseDTO> CreateSystemAliasAsync(
            CreateSystemLoiAliasDTO dto, Guid actor, CancellationToken ct = default)
        {
            var alias = IfcFieldText.Normalize(dto.ParamNameInModel);
            var standard = IfcFieldText.Normalize(dto.StandardParamName);

            if (alias.Length == 0 || standard.Length == 0)
                throw new ApiExceptionResponse("Tên tham số không hợp lệ.", 400);
            if (alias == standard)
                throw new ApiExceptionResponse("Tên trong model trùng với tham số chuẩn, không cần ánh xạ.", 400);

            if (!await _rules.ParamNameExistsAsync(standard, ct))
                throw new ApiExceptionResponse(
                    $"\"{dto.StandardParamName}\" không có trong danh mục tham số chuẩn của bất kỳ bộ luật nào.", 400);

            var existing = (await _uow.Repository<LoiFieldAlias>()
                .FindAsync(a => a.ProjectId == null && a.AliasNormalized == alias)).FirstOrDefault();
            if (existing is not null)
                throw new ApiExceptionResponse(
                    $"\"{dto.ParamNameInModel}\" đã được ánh xạ sang \"{existing.FieldNameNormalized}\".", 409);

            var entity = new LoiFieldAlias
            {
                Id = Guid.NewGuid(),
                FieldNameNormalized = standard,
                AliasNormalized = alias,
                ProjectId = null,
                CreatedByAccountId = actor,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<LoiFieldAlias>().CreateAsync(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Create, AliasEntity, entity.Id.ToString(), actor,
                $"Thêm ánh xạ tên tham số dùng chung: \"{alias}\" thành \"{standard}\"");
            await _uow.CommitAsync();

            return MapAlias(entity);
        }

        public async Task DeleteSystemAliasAsync(Guid aliasId, Guid actor, CancellationToken ct = default)
        {
            var entity = await _uow.Repository<LoiFieldAlias>().GetByIdAsync(aliasId)
                ?? throw new ApiExceptionResponse("Không tìm thấy ánh xạ.", 404);

            if (entity.ProjectId is not null)
                throw new ApiExceptionResponse(
                    "Đây là ánh xạ riêng của một dự án — hãy xoá trong trang dự án đó.", 400);

            _uow.Repository<LoiFieldAlias>().Delete(entity);
            await _audit.LogAsync(LogScope.System, AuditAction.Delete, AliasEntity, entity.Id.ToString(), actor,
                $"Xoá ánh xạ tên tham số dùng chung: \"{entity.AliasNormalized}\"");
            await _uow.CommitAsync();
        }

        public async Task<LoiRuleSetDTO?> SetProjectRuleSetAsync(
            Guid projectId, Guid? ruleSetId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var project = await _uow.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Không tìm thấy dự án.", 404);

            if (!isSystemAdmin && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse(
                    "Chỉ quản trị hệ thống hoặc quản lý dự án được đổi bộ luật kiểm LOI của dự án.", 403);

            LoiRuleSet? ruleSet = null;
            if (ruleSetId.HasValue)
                ruleSet = await RequireRuleSetAsync(ruleSetId.Value);

            project.LoiRuleSetId = ruleSet?.Id;
            project.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Project>().Update(project);

            await _audit.LogAsync(LogScope.Project, AuditAction.Update, RuleSetEntity, projectId.ToString(), actor,
                ruleSet is null
                    ? "Dự án dùng bộ luật kiểm LOI mặc định của hệ thống"
                    : $"Dự án chuyển sang bộ luật kiểm LOI \"{ruleSet.Name}\"",
                projectId);
            await _uow.CommitAsync();

            return ruleSet is null
                ? null
                : Map(ruleSet, await _rules.GetRuleSetCountsAsync(ct),
                    await _rules.CountProjectsInheritingDefaultAsync(ct));
        }

        public async Task<LoiRuleSetDTO?> GetProjectRuleSetAsync(Guid projectId, CancellationToken ct = default)
        {
            var project = await _uow.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Không tìm thấy dự án.", 404);

            if (project.LoiRuleSetId is null) return null;

            var ruleSet = await _uow.Repository<LoiRuleSet>().GetByIdAsync(project.LoiRuleSetId.Value);
            return ruleSet is null
                ? null
                : Map(ruleSet, await _rules.GetRuleSetCountsAsync(ct),
                    await _rules.CountProjectsInheritingDefaultAsync(ct));
        }

        public async Task<IReadOnlyList<LoiRuleSetDTO>> GetSelectableRuleSetsAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var project = await _uow.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Không tìm thấy dự án.", 404);

            if (!isSystemAdmin && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse(
                    "Chỉ quản trị hệ thống hoặc quản lý dự án được xem danh sách bộ luật kiểm LOI.", 403);

            return await GetRuleSetsAsync(ct);
        }

        private async Task<LoiRuleSet> RequireRuleSetAsync(Guid ruleSetId) =>
            await _uow.Repository<LoiRuleSet>().GetByIdAsync(ruleSetId)
            ?? throw new ApiExceptionResponse("Không tìm thấy bộ luật kiểm LOI.", 404);

        private async Task<LoiComponent> RequireComponentAsync(Guid ruleSetId, Guid componentId)
        {
            var component = await _uow.Repository<LoiComponent>().GetByIdAsync(componentId)
                ?? throw new ApiExceptionResponse("Không tìm thấy cấu kiện.", 404);

            if (component.RuleSetId != ruleSetId)
                throw new ApiExceptionResponse("Cấu kiện không thuộc bộ luật này.", 400);

            return component;
        }

        private async Task<LoiParameter> RequireParameterAsync(Guid ruleSetId, Guid parameterId)
        {
            var parameter = await _uow.Repository<LoiParameter>().GetByIdAsync(parameterId)
                ?? throw new ApiExceptionResponse("Không tìm thấy tham số chuẩn.", 404);

            if (parameter.RuleSetId != ruleSetId)
                throw new ApiExceptionResponse("Tham số không thuộc bộ luật này.", 400);

            return parameter;
        }

        private async Task RequireUniqueRuleSetNameAsync(string name, Guid? exceptId)
        {
            var taken = (await _uow.Repository<LoiRuleSet>().GetAllAsync())
                .Any(s => s.Id != exceptId && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (taken)
                throw new ApiExceptionResponse($"Đã có bộ luật tên \"{name}\".", 409);
        }

        private async Task<List<LoiParameter>> LoadParametersAsync(Guid ruleSetId, LoiDiscipline? discipline = null)
        {
            var parameters = await _uow.Repository<LoiParameter>().FindAsync(p => p.RuleSetId == ruleSetId);

            return parameters
                .Where(p => discipline is null || p.Discipline == discipline)
                .OrderBy(p => p.Discipline)
                .ThenBy(p => p.OrderIndex)
                .ThenBy(p => p.Name)
                .ToList();
        }

        private static LoiMatrixDTO BuildMatrix(
            LoiComponent component, IReadOnlyList<LoiRequirement> requirements, List<LoiParameter> parameters)
        {
            var paramOrder = parameters
                .Select((p, index) => (p.NameNormalized, index))
                .ToDictionary(x => x.NameNormalized, x => x.index);

            var variants = requirements
                .GroupBy(r => NormalizeVariant(r.Variant))
                .OrderBy(g => g.Key is null ? 0 : 1)
                .ThenBy(g => g.Key)
                .Select(variantGroup => new LoiMatrixVariantDTO
                {
                    Variant = variantGroup.Key,
                    Rows = variantGroup
                        .GroupBy(r => r.FieldNameNormalized)
                        .OrderBy(fieldGroup => fieldGroup.Min(r => r.FieldOrder))
                        .ThenBy(fieldGroup => fieldGroup.First().FieldName)
                        .Select(fieldGroup => new LoiMatrixRowDTO
                        {
                            FieldName = fieldGroup.First().FieldName,
                            Cells = fieldGroup
                                .OrderBy(r => paramOrder.TryGetValue(r.ParamNameNormalized, out var index) ? index : int.MaxValue)
                                .Select(r => new LoiMatrixCellDTO { ParamName = r.ParamName, Stage = r.Stage })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return new LoiMatrixDTO
            {
                RuleSetId = component.RuleSetId,
                ComponentId = component.Id,
                ComponentCode = component.Code,
                ComponentName = component.Name,
                Discipline = component.Discipline,
                Parameters = parameters.Select(p => new LoiParameterDTO
                {
                    Id = p.Id,
                    Discipline = p.Discipline,
                    Name = p.Name,
                    ParamGroup = p.ParamGroup,
                    OrderIndex = p.OrderIndex
                }).ToList(),
                Variants = variants
            };
        }

        private static string RequireText(string? value, string message)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new ApiExceptionResponse(message, 400);
            return trimmed;
        }

        private static string DisciplineName(LoiDiscipline discipline) =>
            discipline == LoiDiscipline.Mep ? "MEP" : "Kiến trúc - Kết cấu";

        private static string? NormalizeVariant(string? variant)
        {
            var trimmed = variant?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static LoiRuleSetDTO Map(
            LoiRuleSet entity, IReadOnlyDictionary<Guid, LoiRuleSetCounts> counts, int inheritingProjectCount)
        {
            counts.TryGetValue(entity.Id, out var count);
            return new LoiRuleSetDTO
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                IsDefault = entity.IsDefault,
                IsSystem = entity.IsSystem,
                ComponentCount = count?.ComponentCount ?? 0,
                RequirementCount = count?.RequirementCount ?? 0,
                ParameterCount = count?.ParameterCount ?? 0,
                ProjectCount = count?.ProjectCount ?? 0,
                InheritingProjectCount = inheritingProjectCount,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static LoiComponentDTO MapComponent(
            LoiComponent entity, IReadOnlyDictionary<string, LoiComponentUsage> usage)
        {
            usage.TryGetValue(entity.Code, out var used);
            return new LoiComponentDTO
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Discipline = entity.Discipline,
                VariantCount = used?.VariantCount ?? 0,
                RequirementCount = used?.RequirementCount ?? 0
            };
        }

        private static LoiParameterDTO MapParameter(
            LoiParameter entity, IReadOnlyDictionary<(LoiDiscipline, string), int> usage)
        {
            usage.TryGetValue((entity.Discipline, entity.NameNormalized), out var count);
            return new LoiParameterDTO
            {
                Id = entity.Id,
                Discipline = entity.Discipline,
                Name = entity.Name,
                ParamGroup = entity.ParamGroup,
                OrderIndex = entity.OrderIndex,
                UsageCount = count
            };
        }

        private static LoiAliasResponseDTO MapAlias(LoiFieldAlias entity) => new()
        {
            Id = entity.Id,
            ParamNameInModel = entity.AliasNormalized,
            StandardParamName = entity.FieldNameNormalized,
            IsSystemWide = true,
            CreatedAt = entity.CreatedAt
        };
    }
}

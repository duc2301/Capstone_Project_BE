using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Loi;
using Syncfusion.XlsIO;

namespace Application.Services.Loi
{
    public sealed class LoiRuleImportService : ILoiRuleImportService
    {
        private const string RuleSetEntity = "LoiRuleSet";

        private readonly IUnitOfWork _uow;
        private readonly ILoiRuleRepository _rules;
        private readonly IAuditLogService _audit;

        public LoiRuleImportService(IUnitOfWork uow, ILoiRuleRepository rules, IAuditLogService audit)
        {
            _uow = uow;
            _rules = rules;
            _audit = audit;
        }

        public async Task<byte[]> GenerateTemplateAsync(Guid? ruleSetId, CancellationToken ct = default)
        {
            var source = await ResolveTemplateSourceAsync(ruleSetId);

            using var engine = new ExcelEngine();
            engine.Excel.DefaultVersion = ExcelVersion.Excel2016;
            var workbook = engine.Excel.Workbooks.Create(4);

            WriteGuideSheet(workbook.Worksheets[0], source.Name);
            WriteParameterSheet(workbook.Worksheets[1], source.Parameters);
            WriteComponentSheet(workbook.Worksheets[2], source.Components);

            var matrixSheet = workbook.Worksheets[3];
            WriteRequirementSheet(matrixSheet, LoiDiscipline.KienTrucKetCau,
                source.Parameters, source.Components, source.Requirements);

            var mepSheet = workbook.Worksheets.Create(LoiRuleImportSheet.RequirementsMep);
            WriteRequirementSheet(mepSheet, LoiDiscipline.Mep,
                source.Parameters, source.Components, source.Requirements);

            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        }

        public async Task<LoiImportPreviewDTO> ParseAsync(
            Stream stream, Guid? ruleSetId, CancellationToken ct = default)
        {
            var result = new LoiImportPreviewDTO();

            using var engine = new ExcelEngine();
            IWorkbook workbook;
            try
            {
                workbook = engine.Excel.Workbooks.Open(stream, ExcelOpenType.Automatic);
            }
            catch (Exception)
            {
                throw new ApiExceptionResponse("Không đọc được file. Hãy dùng file .xlsx tải từ nút \"Tải template\".", 400);
            }

            var parameterSheet = FindSheet(workbook, LoiRuleImportSheet.Parameters)
                ?? throw new ApiExceptionResponse(
                    $"File thiếu sheet \"{LoiRuleImportSheet.Parameters}\". Hãy tải template mẫu và điền theo.", 400);

            var parameters = ReadParameters(parameterSheet, result.Warnings);
            if (parameters.Count == 0)
                throw new ApiExceptionResponse($"Sheet \"{LoiRuleImportSheet.Parameters}\" không có tham số hợp lệ nào.", 400);

            result.Parameters = parameters;

            var componentSheet = FindSheet(workbook, LoiRuleImportSheet.Components);
            var components = componentSheet is null
                ? new List<LoiImportComponentDTO>()
                : ReadComponents(componentSheet, result.Warnings);

            var paramLookup = parameters
                .GroupBy(p => (p.Discipline, IfcFieldText.Normalize(p.Name)))
                .ToDictionary(g => g.Key, g => g.First());

            var rows = new List<LoiImportRowDTO>();
            foreach (var (sheetName, discipline) in LoiRuleImportSheet.RequirementSheets)
            {
                var sheet = FindSheet(workbook, sheetName);
                if (sheet is null)
                {
                    result.Warnings.Add($"Không thấy sheet \"{sheetName}\" — bỏ qua bộ môn {LoiRuleImportSheet.DisciplineName(discipline)}.");
                    continue;
                }
                rows.AddRange(ReadRequirementSheet(sheet, sheetName, discipline, paramLookup, result.Warnings));
            }

            if (rows.Count == 0)
                throw new ApiExceptionResponse("File không có dòng yêu cầu hợp lệ nào.", 400);

            result.Rows = rows;
            result.TotalCellCount = rows.Sum(r => r.Cells.Count);
            result.Components = MergeComponents(components, rows, result.Warnings);

            var codesWithRules = rows
                .Select(r => LoiEvaluator.NormalizeCode(r.ComponentCode))
                .ToHashSet(StringComparer.Ordinal);
            result.TaxonomyOnlyCount = result.Components
                .Count(c => !codesWithRules.Contains(LoiEvaluator.NormalizeCode(c.Code)));

            await BuildDiffAsync(result, ruleSetId, ct);
            return result;
        }

        public async Task<LoiRuleSetDTO> CommitAsync(
            LoiImportCommitDTO dto, Guid actor, CancellationToken ct = default)
        {
            if (!System.Enum.IsDefined(dto.Mode))
                throw new ApiExceptionResponse("Chế độ ghi không hợp lệ.", 400);
            if (dto.Rows.Count == 0)
                throw new ApiExceptionResponse("Không có dòng yêu cầu nào để ghi.", 400);

            var selected = dto.SelectedComponentCodes.Count == 0
                ? dto.Rows.Select(r => LoiEvaluator.NormalizeCode(r.ComponentCode)).ToHashSet(StringComparer.Ordinal)
                : dto.SelectedComponentCodes.Select(LoiEvaluator.NormalizeCode).ToHashSet(StringComparer.Ordinal);

            var ruleSet = dto.Mode == LoiImportMode.CreateNew
                ? await CreateRuleSetAsync(dto, actor)
                : await RequireRuleSetAsync(
                    dto.TargetRuleSetId ?? throw new ApiExceptionResponse("Chưa chọn bộ luật đích.", 400));

            if (dto.Mode == LoiImportMode.ReplaceAll)
                await ClearRuleSetAsync(ruleSet.Id);

            await WriteParametersAsync(ruleSet.Id, dto.Parameters, dto.Mode);
            var writtenComponents = await WriteComponentsAsync(ruleSet.Id, dto);
            var writtenRows = await WriteRequirementsAsync(ruleSet.Id, dto, selected);

            var removedComponents = 0;
            if (dto.Mode == LoiImportMode.Merge && dto.DeleteMissing)
                removedComponents = await DeleteMissingComponentsAsync(ruleSet.Id, dto);

            ruleSet.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<LoiRuleSet>().Update(ruleSet);

            await _audit.LogAsync(LogScope.System, AuditAction.Update, RuleSetEntity, ruleSet.Id.ToString(), actor,
                $"Nhập bộ luật thông tin phi hình học từ Excel ({ModeLabel(dto.Mode)}) vào \"{ruleSet.Name}\": "
                + $"{writtenComponents} cấu kiện · {writtenRows} dòng yêu cầu · {dto.Parameters.Count} tham số"
                + (removedComponents > 0 ? $" · xoá {removedComponents} cấu kiện vắng mặt" : string.Empty));

            await _uow.CommitAsync();

            var counts = await _rules.GetRuleSetCountsAsync(ct);
            counts.TryGetValue(ruleSet.Id, out var count);
            return new LoiRuleSetDTO
            {
                Id = ruleSet.Id,
                Name = ruleSet.Name,
                Description = ruleSet.Description,
                IsSystem = ruleSet.IsSystem,
                ComponentCount = count?.ComponentCount ?? 0,
                RequirementCount = count?.RequirementCount ?? 0,
                ParameterCount = count?.ParameterCount ?? 0,
                ProjectCount = count?.ProjectCount ?? 0,
                CreatedAt = ruleSet.CreatedAt,
                UpdatedAt = ruleSet.UpdatedAt
            };
        }

        private static string ModeLabel(LoiImportMode mode) => mode switch
        {
            LoiImportMode.CreateNew => "tạo bộ mới",
            LoiImportMode.ReplaceAll => "thay toàn bộ",
            _ => "gộp"
        };

        private async Task<LoiRuleSet> CreateRuleSetAsync(LoiImportCommitDTO dto, Guid actor)
        {
            var name = dto.NewRuleSetName?.Trim();
            if (string.IsNullOrEmpty(name))
                throw new ApiExceptionResponse("Chưa đặt tên cho bộ luật mới.", 400);

            var taken = (await _uow.Repository<LoiRuleSet>().GetAllAsync())
                .Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (taken)
                throw new ApiExceptionResponse($"Đã có bộ luật tên \"{name}\".", 409);

            var entity = new LoiRuleSet
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.NewRuleSetDescription) ? null : dto.NewRuleSetDescription.Trim(),
                IsSystem = false,
                CreatedByAccountId = actor,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<LoiRuleSet>().CreateAsync(entity);
            await _uow.CommitAsync();
            return entity;
        }

        private async Task ClearRuleSetAsync(Guid ruleSetId)
        {
            foreach (var row in await _uow.Repository<LoiRequirement>().FindAsync(r => r.RuleSetId == ruleSetId))
                _uow.Repository<LoiRequirement>().Delete(row);
            foreach (var row in await _uow.Repository<LoiComponent>().FindAsync(c => c.RuleSetId == ruleSetId))
                _uow.Repository<LoiComponent>().Delete(row);
            foreach (var row in await _uow.Repository<LoiParameter>().FindAsync(p => p.RuleSetId == ruleSetId))
                _uow.Repository<LoiParameter>().Delete(row);
        }

        private async Task WriteParametersAsync(
            Guid ruleSetId, List<LoiImportParameterDTO> parameters, LoiImportMode mode)
        {
            var existing = mode == LoiImportMode.ReplaceAll
                ? new List<LoiParameter>()
                : (await _uow.Repository<LoiParameter>().FindAsync(p => p.RuleSetId == ruleSetId)).ToList();

            var byKey = existing.ToDictionary(p => (p.Discipline, p.NameNormalized));

            foreach (var parameter in parameters)
            {
                var normalized = IfcFieldText.Normalize(parameter.Name);
                if (normalized.Length == 0) continue;

                if (byKey.TryGetValue((parameter.Discipline, normalized), out var current))
                {
                    current.Name = parameter.Name.Trim();
                    current.ParamGroup = parameter.ParamGroup;
                    current.OrderIndex = parameter.OrderIndex;
                    _uow.Repository<LoiParameter>().Update(current);
                    continue;
                }

                var created = new LoiParameter
                {
                    Id = Guid.NewGuid(),
                    RuleSetId = ruleSetId,
                    Discipline = parameter.Discipline,
                    Name = parameter.Name.Trim(),
                    NameNormalized = normalized,
                    ParamGroup = parameter.ParamGroup,
                    OrderIndex = parameter.OrderIndex
                };
                await _uow.Repository<LoiParameter>().CreateAsync(created);
                byKey[(parameter.Discipline, normalized)] = created;
            }
        }

        private async Task<int> WriteComponentsAsync(Guid ruleSetId, LoiImportCommitDTO dto)
        {
            var existing = dto.Mode == LoiImportMode.ReplaceAll
                ? new List<LoiComponent>()
                : (await _uow.Repository<LoiComponent>().FindAsync(c => c.RuleSetId == ruleSetId)).ToList();

            var byCode = existing.ToDictionary(c => c.CodeNormalized, StringComparer.Ordinal);
            var written = 0;

            foreach (var component in dto.Components)
            {
                var normalized = LoiEvaluator.NormalizeCode(component.Code);
                if (normalized.Length == 0) continue;

                written++;
                if (byCode.TryGetValue(normalized, out var current))
                {
                    current.Code = component.Code.Trim();
                    current.Name = component.Name.Trim();
                    current.Discipline = component.Discipline;
                    _uow.Repository<LoiComponent>().Update(current);
                    continue;
                }

                var created = new LoiComponent
                {
                    Id = Guid.NewGuid(),
                    RuleSetId = ruleSetId,
                    Discipline = component.Discipline,
                    Code = component.Code.Trim(),
                    CodeNormalized = normalized,
                    Name = component.Name.Trim()
                };
                await _uow.Repository<LoiComponent>().CreateAsync(created);
                byCode[normalized] = created;
            }

            return written;
        }

        private async Task<int> WriteRequirementsAsync(
            Guid ruleSetId, LoiImportCommitDTO dto, HashSet<string> selected)
        {
            if (dto.Mode != LoiImportMode.ReplaceAll)
            {
                var stale = (await _uow.Repository<LoiRequirement>().FindAsync(r => r.RuleSetId == ruleSetId))
                    .Where(r => r.ComponentCode != null && selected.Contains(LoiEvaluator.NormalizeCode(r.ComponentCode)))
                    .ToList();
                foreach (var row in stale)
                    _uow.Repository<LoiRequirement>().Delete(row);
            }

            var paramGroups = dto.Parameters
                .GroupBy(p => (p.Discipline, IfcFieldText.Normalize(p.Name)))
                .ToDictionary(g => g.Key, g => g.First().ParamGroup);

            var written = 0;
            foreach (var row in dto.Rows)
            {
                var normalizedCode = LoiEvaluator.NormalizeCode(row.ComponentCode);
                if (normalizedCode.Length == 0 || !selected.Contains(normalizedCode)) continue;

                var fieldName = row.FieldName.Trim();
                var fieldNameNormalized = IfcFieldText.Normalize(fieldName);
                if (fieldNameNormalized.Length == 0) continue;

                foreach (var cell in row.Cells)
                {
                    if (!System.Enum.IsDefined(cell.Stage)) continue;

                    var paramName = cell.ParamName.Trim();
                    var paramNormalized = IfcFieldText.Normalize(paramName);
                    if (paramNormalized.Length == 0) continue;
                    if (!paramGroups.TryGetValue((row.Discipline, paramNormalized), out var group)) continue;

                    await _uow.Repository<LoiRequirement>().CreateAsync(new LoiRequirement
                    {
                        Id = Guid.NewGuid(),
                        RuleSetId = ruleSetId,
                        Discipline = row.Discipline,
                        ComponentCode = row.ComponentCode.Trim(),
                        ComponentName = row.ComponentName.Trim(),
                        Variant = string.IsNullOrWhiteSpace(row.Variant) ? null : row.Variant.Trim(),
                        FieldOrder = row.FieldOrder,
                        FieldName = fieldName,
                        FieldNameNormalized = fieldNameNormalized,
                        ParamName = paramName,
                        ParamNameNormalized = paramNormalized,
                        ParamGroup = group,
                        Stage = cell.Stage
                    });
                    written++;
                }
            }

            return written;
        }

        private async Task<int> DeleteMissingComponentsAsync(Guid ruleSetId, LoiImportCommitDTO dto)
        {
            var inFile = dto.Rows
                .Select(r => LoiEvaluator.NormalizeCode(r.ComponentCode))
                .Concat(dto.Components.Select(c => LoiEvaluator.NormalizeCode(c.Code)))
                .ToHashSet(StringComparer.Ordinal);

            var components = (await _uow.Repository<LoiComponent>().FindAsync(c => c.RuleSetId == ruleSetId))
                .Where(c => !inFile.Contains(c.CodeNormalized))
                .ToList();

            if (components.Count == 0) return 0;

            var codes = components.Select(c => c.CodeNormalized).ToHashSet(StringComparer.Ordinal);
            var requirements = (await _uow.Repository<LoiRequirement>().FindAsync(r => r.RuleSetId == ruleSetId))
                .Where(r => r.ComponentCode != null && codes.Contains(LoiEvaluator.NormalizeCode(r.ComponentCode)))
                .ToList();

            foreach (var row in requirements)
                _uow.Repository<LoiRequirement>().Delete(row);
            foreach (var component in components)
                _uow.Repository<LoiComponent>().Delete(component);

            return components.Count;
        }

        private async Task BuildDiffAsync(LoiImportPreviewDTO result, Guid? ruleSetId, CancellationToken ct)
        {
            var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
            var currentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var currentNames = new Dictionary<string, LoiComponent>(StringComparer.Ordinal);

            if (ruleSetId.HasValue)
            {
                var existingComponents = await _uow.Repository<LoiComponent>()
                    .FindAsync(c => c.RuleSetId == ruleSetId.Value);
                foreach (var component in existingComponents)
                    currentNames[component.CodeNormalized] = component;

                var existingRequirements = (await _uow.Repository<LoiRequirement>()
                    .FindAsync(r => r.RuleSetId == ruleSetId.Value)).ToList();

                foreach (var byCode in existingRequirements
                             .Where(r => r.ComponentCode != null)
                             .GroupBy(r => LoiEvaluator.NormalizeCode(r.ComponentCode!)))
                {
                    signatures[byCode.Key] = BuildSignature(byCode.Select(r =>
                        (r.Variant, r.FieldNameNormalized, r.ParamNameNormalized, (int)r.Stage)));
                    currentCounts[byCode.Key] = byCode.Count();
                }
            }

            var diffs = new List<LoiImportComponentDiffDTO>();
            foreach (var byCode in result.Rows.GroupBy(r => LoiEvaluator.NormalizeCode(r.ComponentCode)))
            {
                var first = byCode.First();
                var fileSignature = BuildSignature(byCode.SelectMany(row =>
                    row.Cells.Select(cell => (
                        row.Variant,
                        IfcFieldText.Normalize(row.FieldName),
                        IfcFieldText.Normalize(cell.ParamName),
                        (int)cell.Stage))));

                var status = !signatures.TryGetValue(byCode.Key, out var current)
                    ? LoiImportStatus.Added
                    : current == fileSignature
                        ? LoiImportStatus.Unchanged
                        : LoiImportStatus.Updated;

                diffs.Add(new LoiImportComponentDiffDTO
                {
                    Discipline = first.Discipline,
                    Code = first.ComponentCode,
                    Name = first.ComponentName,
                    Status = status,
                    VariantCount = byCode.Select(r => r.Variant ?? string.Empty).Distinct().Count(),
                    RequirementCount = byCode.Sum(r => r.Cells.Count),
                    CurrentRequirementCount = currentCounts.TryGetValue(byCode.Key, out var count) ? count : 0
                });
            }

            var inFile = result.Rows
                .Select(r => LoiEvaluator.NormalizeCode(r.ComponentCode))
                .ToHashSet(StringComparer.Ordinal);

            var inFileTaxonomy = result.Components
                .Select(c => LoiEvaluator.NormalizeCode(c.Code))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var (code, component) in currentNames)
            {
                if (inFile.Contains(code)) continue;

                var currentCount = currentCounts.TryGetValue(code, out var count) ? count : 0;
                var declaredWithoutRules = inFileTaxonomy.Contains(code) && currentCount == 0;

                diffs.Add(new LoiImportComponentDiffDTO
                {
                    Discipline = component.Discipline,
                    Code = component.Code,
                    Name = component.Name,
                    Status = declaredWithoutRules ? LoiImportStatus.Unchanged : LoiImportStatus.MissingInFile,
                    VariantCount = 0,
                    RequirementCount = 0,
                    CurrentRequirementCount = currentCount
                });
            }

            result.Diffs = diffs.OrderBy(d => d.Discipline).ThenBy(d => d.Code, StringComparer.Ordinal).ToList();
            result.AddedCount = diffs.Count(d => d.Status == LoiImportStatus.Added);
            result.UpdatedCount = diffs.Count(d => d.Status == LoiImportStatus.Updated);
            result.UnchangedCount = diffs.Count(d => d.Status == LoiImportStatus.Unchanged);
            result.MissingCount = diffs.Count(d => d.Status == LoiImportStatus.MissingInFile);
        }

        private static string BuildSignature(IEnumerable<(string? Variant, string Field, string Param, int Stage)> rows) =>
            string.Join("\n", rows
                .Select(r => $"{r.Variant ?? string.Empty}|{r.Field}|{r.Param}|{r.Stage}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

        private static List<LoiImportComponentDTO> MergeComponents(
            List<LoiImportComponentDTO> declared, List<LoiImportRowDTO> rows, List<string> warnings)
        {
            var byCode = new Dictionary<string, LoiImportComponentDTO>(StringComparer.Ordinal);

            foreach (var component in declared)
            {
                var key = LoiEvaluator.NormalizeCode(component.Code);
                if (key.Length > 0) byCode[key] = component;
            }

            foreach (var row in rows)
            {
                var key = LoiEvaluator.NormalizeCode(row.ComponentCode);
                if (key.Length == 0) continue;

                if (!byCode.TryGetValue(key, out var component))
                {
                    byCode[key] = new LoiImportComponentDTO
                    {
                        Discipline = row.Discipline,
                        Code = row.ComponentCode,
                        Name = row.ComponentName
                    };
                    continue;
                }

                if (component.Discipline == row.Discipline) continue;

                warnings.Add(
                    $"{LoiRuleImportSheet.Components}: mã \"{component.Code}\" ghi bộ môn "
                    + $"{LoiRuleImportSheet.DisciplineName(component.Discipline)} nhưng dòng yêu cầu nằm ở sheet "
                    + $"{LoiRuleImportSheet.DisciplineName(row.Discipline)} — đã lấy theo sheet yêu cầu.");
                component.Discipline = row.Discipline;
            }

            return byCode.Values.ToList();
        }

        private static IWorksheet? FindSheet(IWorkbook workbook, string name)
        {
            foreach (IWorksheet sheet in workbook.Worksheets)
                if (string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return sheet;
            return null;
        }

        private static List<LoiImportParameterDTO> ReadParameters(IWorksheet sheet, List<string> warnings)
        {
            var parameters = new List<LoiImportParameterDTO>();
            var seen = new HashSet<(LoiDiscipline, string)>();
            var lastRow = Math.Min(sheet.UsedRange.LastRow, LoiRuleImportSheet.MaxRows);

            for (var row = 2; row <= lastRow; row++)
            {
                var disciplineText = sheet.Range[row, 1].DisplayText?.Trim();
                var name = sheet.Range[row, 2].DisplayText?.Trim();
                if (string.IsNullOrEmpty(disciplineText) && string.IsNullOrEmpty(name)) continue;

                var discipline = LoiRuleImportSheet.ParseDiscipline(disciplineText);
                if (discipline is null)
                {
                    warnings.Add($"{LoiRuleImportSheet.Parameters} dòng {row}: bộ môn \"{disciplineText}\" không hợp lệ — đã bỏ qua.");
                    continue;
                }
                if (string.IsNullOrEmpty(name))
                {
                    warnings.Add($"{LoiRuleImportSheet.Parameters} dòng {row}: thiếu tên tham số — đã bỏ qua.");
                    continue;
                }

                var group = LoiRuleImportSheet.ParseGroup(sheet.Range[row, 3].DisplayText);
                if (group is null)
                {
                    warnings.Add($"{LoiRuleImportSheet.Parameters} dòng {row}: nhóm tham số không hợp lệ — đã bỏ qua.");
                    continue;
                }

                var normalized = IfcFieldText.Normalize(name);
                if (normalized.Length == 0 || !seen.Add((discipline.Value, normalized)))
                {
                    warnings.Add($"{LoiRuleImportSheet.Parameters} dòng {row}: tham số \"{name}\" bị lặp trong cùng bộ môn — đã bỏ qua.");
                    continue;
                }

                var orderText = sheet.Range[row, 4].DisplayText?.Trim();
                parameters.Add(new LoiImportParameterDTO
                {
                    Discipline = discipline.Value,
                    Name = name,
                    ParamGroup = group.Value,
                    OrderIndex = int.TryParse(orderText, out var order) ? order : parameters.Count
                });
            }

            return parameters;
        }

        private static List<LoiImportComponentDTO> ReadComponents(IWorksheet sheet, List<string> warnings)
        {
            var components = new List<LoiImportComponentDTO>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var lastRow = Math.Min(sheet.UsedRange.LastRow, LoiRuleImportSheet.MaxRows);

            for (var row = 2; row <= lastRow; row++)
            {
                var code = sheet.Range[row, 1].DisplayText?.Trim();
                var name = sheet.Range[row, 2].DisplayText?.Trim();
                if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) continue;

                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                {
                    warnings.Add($"{LoiRuleImportSheet.Components} dòng {row}: thiếu mã hoặc tên cấu kiện — đã bỏ qua.");
                    continue;
                }

                var discipline = LoiRuleImportSheet.ParseDiscipline(sheet.Range[row, 3].DisplayText)
                    ?? (code.Replace(" ", string.Empty).StartsWith("2104", StringComparison.Ordinal)
                        ? LoiDiscipline.Mep
                        : LoiDiscipline.KienTrucKetCau);

                var normalized = LoiEvaluator.NormalizeCode(code);
                if (normalized.Length == 0 || !seen.Add(normalized))
                {
                    warnings.Add($"{LoiRuleImportSheet.Components} dòng {row}: mã \"{code}\" bị lặp — đã bỏ qua.");
                    continue;
                }

                components.Add(new LoiImportComponentDTO { Discipline = discipline, Code = code, Name = name });
            }

            return components;
        }

        private static List<LoiImportRowDTO> ReadRequirementSheet(
            IWorksheet sheet,
            string sheetName,
            LoiDiscipline discipline,
            IReadOnlyDictionary<(LoiDiscipline, string), LoiImportParameterDTO> paramLookup,
            List<string> warnings)
        {
            var rows = new List<LoiImportRowDTO>();
            var lastRow = Math.Min(sheet.UsedRange.LastRow, LoiRuleImportSheet.MaxRows);
            var lastColumn = Math.Min(
                sheet.UsedRange.LastColumn,
                LoiRuleImportSheet.FirstParamColumn + LoiRuleImportSheet.MaxParamColumns);

            var columnParam = new Dictionary<int, string>();
            for (var column = LoiRuleImportSheet.FirstParamColumn; column <= lastColumn; column++)
            {
                var header = sheet.Range[LoiRuleImportSheet.HeaderRow, column].DisplayText?.Trim();
                if (string.IsNullOrEmpty(header)) continue;

                var normalized = IfcFieldText.Normalize(header);
                if (!paramLookup.ContainsKey((discipline, normalized)))
                {
                    warnings.Add($"{sheetName} cột {column}: tham số \"{header}\" không có trong sheet {LoiRuleImportSheet.Parameters} của bộ môn này — cột bị bỏ qua.");
                    continue;
                }
                columnParam[column] = header;
            }

            if (columnParam.Count == 0)
            {
                warnings.Add($"{sheetName}: không nhận ra cột tham số nào ở hàng {LoiRuleImportSheet.HeaderRow}.");
                return rows;
            }

            var currentCode = string.Empty;
            var currentName = string.Empty;
            string? currentVariant = null;
            var fieldOrder = 0;
            var orderKey = string.Empty;

            for (var row = LoiRuleImportSheet.FirstDataRow; row <= lastRow; row++)
            {
                var code = sheet.Range[row, LoiRuleImportSheet.CodeColumn].DisplayText?.Trim();
                var name = sheet.Range[row, LoiRuleImportSheet.NameColumn].DisplayText?.Trim();
                var variant = sheet.Range[row, LoiRuleImportSheet.VariantColumn].DisplayText?.Trim();
                var fieldName = sheet.Range[row, LoiRuleImportSheet.FieldColumn].DisplayText?.Trim();

                if (!string.IsNullOrEmpty(code)) currentCode = code;
                if (!string.IsNullOrEmpty(name)) currentName = name;
                if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(variant))
                    currentVariant = string.IsNullOrEmpty(variant) ? null : variant;

                if (string.IsNullOrEmpty(fieldName)) continue;

                if (string.IsNullOrEmpty(currentCode))
                {
                    warnings.Add($"{sheetName} dòng {row}: có \"{fieldName}\" nhưng chưa có mã cấu kiện ở cột A — đã bỏ qua.");
                    continue;
                }

                var cells = new List<LoiImportCellDTO>();
                foreach (var (column, paramName) in columnParam)
                {
                    var raw = sheet.Range[row, column].DisplayText?.Trim();
                    if (string.IsNullOrEmpty(raw)) continue;

                    if (!int.TryParse(raw, out var stageNumber) || !System.Enum.IsDefined((LoiStage)stageNumber))
                    {
                        warnings.Add($"{sheetName} ô {row}:{column}: giá trị \"{raw}\" không phải giai đoạn 2/3/4/5 — đã bỏ qua.");
                        continue;
                    }
                    cells.Add(new LoiImportCellDTO { ParamName = paramName, Stage = (LoiStage)stageNumber });
                }

                if (cells.Count == 0)
                {
                    warnings.Add($"{sheetName} dòng {row}: \"{fieldName}\" chưa đánh dấu tham số nào — đã bỏ qua.");
                    continue;
                }

                var key = $"{LoiEvaluator.NormalizeCode(currentCode)}|{currentVariant}";
                if (!string.Equals(key, orderKey, StringComparison.Ordinal))
                {
                    orderKey = key;
                    fieldOrder = 0;
                }

                rows.Add(new LoiImportRowDTO
                {
                    Discipline = discipline,
                    ComponentCode = currentCode,
                    ComponentName = currentName,
                    Variant = currentVariant,
                    FieldOrder = fieldOrder++,
                    FieldName = fieldName,
                    Cells = cells
                });
            }

            return rows;
        }

        private static void WriteGuideSheet(IWorksheet sheet, string ruleSetName)
        {
            sheet.Name = LoiRuleImportSheet.Guide;

            var lines = new (string Label, string Value)[]
            {
                ("Bộ luật", ruleSetName),
                ("Nguồn", "QĐ 347/QĐ-BXD ngày 02/04/2021 — Phụ lục 02"),
                (string.Empty, string.Empty),
                ("Sheet ThamSo", "Danh mục tham số chuẩn. Cột: Bộ môn · Tên tham số · Nhóm · Thứ tự cột."),
                ("", "Tên ở đây chính là tiêu đề CỘT của 2 sheet YeuCau. Sửa tên ở đây phải sửa cả tiêu đề cột."),
                ("Sheet CauKien", "Cây phân loại OmniClass, kể cả mã mà chuẩn không quy định trường nào."),
                ("Sheet YeuCau-KTKC", "Ma trận bộ môn Kiến trúc - Kết cấu."),
                ("Sheet YeuCau-MEP", "Ma trận bộ môn MEP. Hai bộ môn có bộ tham số RIÊNG, không dùng chung."),
                (string.Empty, string.Empty),
                ("Cách điền ma trận", "Cột A mã cấu kiện · B tên cấu kiện · C biến thể · D thông tin cần có · E trở đi là tham số."),
                ("", "Mã và tên chỉ cần ghi ở dòng đầu của mỗi cấu kiện, các dòng sau để trống là hiểu tiếp tục."),
                ("", "Cột C để trống nghĩa là bộ trường mặc định. Ghi tên biến thể để mở một bộ trường riêng."),
                ("", "Ô giao giữa dòng và cột điền 2, 3, 4 hoặc 5 — là giai đoạn bắt đầu bắt buộc. Để trống là không yêu cầu."),
                (string.Empty, string.Empty),
                ("Giai đoạn 2", "Thiết kế cơ sở"),
                ("Giai đoạn 3", "Thiết kế kỹ thuật"),
                ("Giai đoạn 4", "Thiết kế bản vẽ thi công"),
                ("Giai đoạn 5", "Thi công"),
                (string.Empty, string.Empty),
                ("Lưu ý quan trọng", "Nhãn ở cột D là cách gọi cho người đọc, KHÔNG phải tên tham số khai trong model."),
                ("", "Ví dụ: dòng \"Thể tích\" đánh dấu ở cột \"Khối tích\" nghĩa là model phải có tham số tên Khối tích."),
            };

            sheet.Range[1, 1].Text = "HƯỚNG DẪN ĐIỀN BỘ LUẬT THÔNG TIN PHI HÌNH HỌC";
            sheet.Range[1, 1].CellStyle.Font.Bold = true;
            sheet.Range[1, 1].CellStyle.Font.Size = 14;

            for (var i = 0; i < lines.Length; i++)
            {
                sheet.Range[i + 3, 1].Text = lines[i].Label;
                sheet.Range[i + 3, 2].Text = lines[i].Value;
                sheet.Range[i + 3, 1].CellStyle.Font.Bold = true;
            }

            sheet.Columns[0].ColumnWidth = 22;
            sheet.Columns[1].ColumnWidth = 110;
        }

        private static void WriteParameterSheet(IWorksheet sheet, List<LoiParameter> parameters)
        {
            sheet.Name = LoiRuleImportSheet.Parameters;

            var headers = new[] { "Bộ môn", "Tên tham số", "Nhóm", "Thứ tự" };
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Range[1, i + 1].Text = headers[i];
                sheet.Range[1, i + 1].CellStyle.Font.Bold = true;
            }

            var row = 2;
            foreach (var parameter in parameters.OrderBy(p => p.Discipline).ThenBy(p => p.OrderIndex))
            {
                sheet.Range[row, 1].Text = LoiRuleImportSheet.DisciplineName(parameter.Discipline);
                sheet.Range[row, 2].Text = parameter.Name;
                sheet.Range[row, 3].Text = LoiRuleImportSheet.GroupName(parameter.ParamGroup);
                sheet.Range[row, 4].Number = parameter.OrderIndex;
                row++;
            }

            sheet.Columns[0].ColumnWidth = 22;
            sheet.Columns[1].ColumnWidth = 34;
            sheet.Columns[2].ColumnWidth = 24;
        }

        private static void WriteComponentSheet(IWorksheet sheet, List<LoiComponent> components)
        {
            sheet.Name = LoiRuleImportSheet.Components;

            var headers = new[] { "Mã cấu kiện", "Tên cấu kiện", "Bộ môn" };
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Range[1, i + 1].Text = headers[i];
                sheet.Range[1, i + 1].CellStyle.Font.Bold = true;
            }

            var row = 2;
            foreach (var component in components.OrderBy(c => c.Discipline).ThenBy(c => c.CodeNormalized, StringComparer.Ordinal))
            {
                sheet.Range[row, 1].Text = component.Code;
                sheet.Range[row, 2].Text = component.Name;
                sheet.Range[row, 3].Text = LoiRuleImportSheet.DisciplineName(component.Discipline);
                row++;
            }

            sheet.Columns[0].ColumnWidth = 20;
            sheet.Columns[1].ColumnWidth = 46;
            sheet.Columns[2].ColumnWidth = 22;
        }

        private static void WriteRequirementSheet(
            IWorksheet sheet,
            LoiDiscipline discipline,
            List<LoiParameter> parameters,
            List<LoiComponent> components,
            List<LoiRequirement> requirements)
        {
            sheet.Name = discipline == LoiDiscipline.Mep
                ? LoiRuleImportSheet.RequirementsMep
                : LoiRuleImportSheet.RequirementsKienTrucKetCau;

            var columns = parameters
                .Where(p => p.Discipline == discipline)
                .OrderBy(p => p.OrderIndex)
                .ToList();

            sheet.Range[LoiRuleImportSheet.TitleRow, 1].Text =
                $"MA TRẬN YÊU CẦU THÔNG TIN — BỘ MÔN {LoiRuleImportSheet.DisciplineName(discipline).ToUpperInvariant()}";
            sheet.Range[LoiRuleImportSheet.TitleRow, 1].CellStyle.Font.Bold = true;

            var headers = new[] { "Mã cấu kiện", "Tên cấu kiện", "Biến thể", "Thông tin cần có" };
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Range[LoiRuleImportSheet.HeaderRow, i + 1].Text = headers[i];
                sheet.Range[LoiRuleImportSheet.HeaderRow, i + 1].CellStyle.Font.Bold = true;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var column = LoiRuleImportSheet.FirstParamColumn + i;
                sheet.Range[LoiRuleImportSheet.GroupRow, column].Text = LoiRuleImportSheet.GroupName(columns[i].ParamGroup);
                sheet.Range[LoiRuleImportSheet.HeaderRow, column].Text = columns[i].Name;
                sheet.Range[LoiRuleImportSheet.HeaderRow, column].CellStyle.Font.Bold = true;
                sheet.Columns[column - 1].ColumnWidth = 16;
            }

            var columnIndex = columns
                .Select((p, i) => (p.NameNormalized, Index: LoiRuleImportSheet.FirstParamColumn + i))
                .ToDictionary(x => x.NameNormalized, x => x.Index, StringComparer.Ordinal);

            var componentOrder = components
                .Where(c => c.Discipline == discipline)
                .OrderBy(c => c.CodeNormalized, StringComparer.Ordinal)
                .Select(c => c.CodeNormalized)
                .ToList();

            var byComponent = requirements
                .Where(r => r.Discipline == discipline && r.ComponentCode != null)
                .GroupBy(r => LoiEvaluator.NormalizeCode(r.ComponentCode!))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var row = LoiRuleImportSheet.FirstDataRow;
            foreach (var code in componentOrder)
            {
                if (!byComponent.TryGetValue(code, out var componentRows)) continue;

                var first = true;
                foreach (var byVariant in componentRows
                             .GroupBy(r => r.Variant)
                             .OrderBy(g => g.Key is null ? 0 : 1)
                             .ThenBy(g => g.Key, StringComparer.Ordinal))
                {
                    var variantFirst = true;
                    foreach (var byField in byVariant
                                 .GroupBy(r => r.FieldNameNormalized, StringComparer.Ordinal)
                                 .OrderBy(g => g.Min(r => r.FieldOrder)))
                    {
                        if (first)
                        {
                            sheet.Range[row, LoiRuleImportSheet.CodeColumn].Text = byField.First().ComponentCode;
                            sheet.Range[row, LoiRuleImportSheet.NameColumn].Text = byField.First().ComponentName;
                            first = false;
                        }
                        if (variantFirst && byVariant.Key is not null)
                        {
                            sheet.Range[row, LoiRuleImportSheet.VariantColumn].Text = byVariant.Key;
                        }
                        variantFirst = false;

                        sheet.Range[row, LoiRuleImportSheet.FieldColumn].Text = byField.First().FieldName;

                        foreach (var requirement in byField)
                            if (columnIndex.TryGetValue(requirement.ParamNameNormalized, out var column))
                                sheet.Range[row, column].Number = (int)requirement.Stage;

                        row++;
                    }
                }
            }

            sheet.Columns[LoiRuleImportSheet.CodeColumn - 1].ColumnWidth = 18;
            sheet.Columns[LoiRuleImportSheet.NameColumn - 1].ColumnWidth = 40;
            sheet.Columns[LoiRuleImportSheet.VariantColumn - 1].ColumnWidth = 26;
            sheet.Columns[LoiRuleImportSheet.FieldColumn - 1].ColumnWidth = 32;
            sheet.Range[LoiRuleImportSheet.HeaderRow, 1, LoiRuleImportSheet.HeaderRow, 4].FreezePanes();
        }

        private async Task<LoiStandardRuleTable> ResolveTemplateSourceAsync(Guid? ruleSetId)
        {
            if (!ruleSetId.HasValue) return LoiStandardRuleTable.Load();

            var chosen = await RequireRuleSetAsync(ruleSetId.Value);
            var requirements = (await _uow.Repository<LoiRequirement>()
                .FindAsync(r => r.RuleSetId == chosen.Id)).ToList();

            if (requirements.Count == 0) return LoiStandardRuleTable.Load();

            return new LoiStandardRuleTable(
                chosen.Name,
                chosen.Description,
                (await _uow.Repository<LoiParameter>().FindAsync(p => p.RuleSetId == chosen.Id)).ToList(),
                (await _uow.Repository<LoiComponent>().FindAsync(c => c.RuleSetId == chosen.Id)).ToList(),
                requirements);
        }

        private async Task<LoiRuleSet> RequireRuleSetAsync(Guid ruleSetId) =>
            await _uow.Repository<LoiRuleSet>().GetByIdAsync(ruleSetId)
            ?? throw new ApiExceptionResponse("Không tìm thấy bộ luật thông tin phi hình học.", 404);
    }
}

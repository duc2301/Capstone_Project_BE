using Application.DTOs.RequestDTOs.NamingConvention;
using Application.DTOs.ResponseDTOs.NamingConvention;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Syncfusion.XlsIO;
using System.Text.Json;

namespace Application.Services
{
    public class NamingConventionService : INamingConventionService
    {
        // Delimiter được hỗ trợ (giống Autodesk Docs).
        private static readonly string[] AllowedDelimiters = { "-", "_", "." };

        private static readonly JsonSerializerOptions SelectionJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IUnitOfWork _unitOfWork;

        public NamingConventionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // =========================================================
        // Cấu hình (admin)
        // =========================================================

        public async Task<NamingConventionResponseDTO> CreateAsync(CreateNamingConventionDTO dto, Guid actor)
        {
            ValidateDelimiter(dto.Delimiter);

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(dto.ProjectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var duplicatedFieldCode = dto.Fields
                .GroupBy(f => f.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicatedFieldCode != null)
                throw new ApiExceptionResponse($"Duplicated field code '{duplicatedFieldCode.Key}'.", 400);

            var convention = new NamingConvention
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = dto.Name.Trim(),
                Delimiter = dto.Delimiter,
                IsActive = true,
                CreatedById = actor,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var fieldDto in dto.Fields)
                convention.Fields.Add(BuildField(convention.Id, fieldDto, actor));

            await _unitOfWork.NamingConventionRepository.CreateAsync(convention);
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(convention.Id);
        }

        public async Task<NamingConventionResponseDTO> GetByIdAsync(Guid id)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetWithDetailsAsync(id)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);

            var folders = await _unitOfWork.NamingConventionRepository.GetAssignedFoldersAsync(id);
            return MapDetail(convention, folders);
        }

        public async Task<IEnumerable<NamingConventionResponseDTO>> GetByProjectAsync(Guid projectId)
        {
            var conventions = await _unitOfWork.NamingConventionRepository.GetByProjectIdAsync(projectId);

            var result = new List<NamingConventionResponseDTO>();
            foreach (var convention in conventions)
            {
                var folders = await _unitOfWork.NamingConventionRepository.GetAssignedFoldersAsync(convention.Id);
                result.Add(MapDetail(convention, folders));
            }
            return result;
        }

        public async Task<NamingConventionResponseDTO> UpdateAsync(Guid id, UpdateNamingConventionDTO dto)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetWithDetailsAsync(id, track: true)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);

            if (dto.Delimiter != null)
            {
                ValidateDelimiter(dto.Delimiter);
                convention.Delimiter = dto.Delimiter;
            }
            if (!string.IsNullOrWhiteSpace(dto.Name))
                convention.Name = dto.Name.Trim();
            if (dto.IsActive.HasValue)
                convention.IsActive = dto.IsActive.Value;

            convention.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetWithDetailsAsync(id, track: true)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);

            // LockedValue có FK Restrict -> phải xóa trước khi cascade fields/values.
            foreach (var field in convention.Fields.Where(f => f.LockedValue != null))
                _unitOfWork.Repository<NamingConventionLockedValue>().Delete(field.LockedValue!);

            _unitOfWork.NamingConventionRepository.Delete(convention);
            await _unitOfWork.CommitAsync();
        }

        public async Task<NamingConventionResponseDTO> AddFieldAsync(Guid conventionId, CreateNamingFieldDTO dto, Guid actor)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetWithDetailsAsync(conventionId, track: true)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);

            if (convention.Fields.Any(f => string.Equals(f.Code, dto.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ApiExceptionResponse($"Field code '{dto.Code}' already exists in this naming convention.", 400);

            var field = BuildField(convention.Id, dto, actor);
            await _unitOfWork.Repository<NamingConventionField>().CreateAsync(field);

            convention.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(conventionId);
        }

        public async Task<NamingConventionResponseDTO> UpdateFieldAsync(Guid fieldId, UpdateNamingFieldDTO dto)
        {
            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(fieldId, track: true)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);

            if (!string.IsNullOrWhiteSpace(dto.Code))
                field.Code = dto.Code.Trim();
            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                field.DisplayName = dto.DisplayName.Trim();
            if (dto.Description != null)
                field.Description = dto.Description;
            if (dto.OrderIndex.HasValue)
                field.OrderIndex = dto.OrderIndex.Value;
            if (dto.IsRequired.HasValue)
                field.IsRequired = dto.IsRequired.Value;
            if (dto.MinLength.HasValue)
                field.MinLength = dto.MinLength;
            if (dto.MaxLength.HasValue)
                field.MaxLength = dto.MaxLength;
            if (dto.FieldType.HasValue)
                field.FieldType = dto.FieldType.Value;

            field.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(field.NamingConventionId);
        }

        public async Task DeleteFieldAsync(Guid fieldId)
        {
            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(fieldId, track: true)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);

            if (field.LockedValue != null)
                _unitOfWork.Repository<NamingConventionLockedValue>().Delete(field.LockedValue);

            _unitOfWork.Repository<NamingConventionField>().Delete(field);
            await _unitOfWork.CommitAsync();
        }

        public async Task<NamingConventionResponseDTO> AddFieldValuesAsync(
            Guid fieldId, List<CreateNamingFieldValueDTO> dtos, Guid actor)
        {
            if (dtos == null || dtos.Count == 0)
                throw new ApiExceptionResponse("At least one value is required.", 400);

            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(fieldId, track: true)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);

            var existingCodes = field.AllowedValues
                .Select(v => v.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var dto in dtos)
            {
                var code = dto.Code.Trim();
                if (!existingCodes.Add(code))
                    throw new ApiExceptionResponse($"Value code '{code}' already exists in field '{field.DisplayName}'.", 400);

                await _unitOfWork.Repository<NamingConventionFieldValue>().CreateAsync(new NamingConventionFieldValue
                {
                    Id = Guid.NewGuid(),
                    NamingConventionFieldId = field.Id,
                    Code = code,
                    DisplayName = dto.DisplayName.Trim(),
                    Description = dto.Description,
                    OrderIndex = dto.OrderIndex,
                    IsActive = true,
                    CreatedById = actor,
                    CreatedAt = DateTime.UtcNow
                });
            }

            field.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(field.NamingConventionId);
        }

        public async Task<NamingConventionResponseDTO> UpdateFieldValueAsync(Guid valueId, UpdateNamingFieldValueDTO dto)
        {
            var value = await _unitOfWork.NamingConventionRepository.GetFieldValueAsync(valueId, track: true)
                ?? throw new ApiExceptionResponse("Naming field value not found.", 404);

            if (!string.IsNullOrWhiteSpace(dto.Code))
                value.Code = dto.Code.Trim();
            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                value.DisplayName = dto.DisplayName.Trim();
            if (dto.Description != null)
                value.Description = dto.Description;
            if (dto.OrderIndex.HasValue)
                value.OrderIndex = dto.OrderIndex.Value;
            if (dto.IsActive.HasValue)
            {
                if (!dto.IsActive.Value && value.LockedValue != null)
                    throw new ApiExceptionResponse("Cannot deactivate a value that is locked. Unlock the field first.", 400);
                value.IsActive = dto.IsActive.Value;
            }

            value.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(value.NamingConventionFieldId)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);
            return await GetByIdAsync(field.NamingConventionId);
        }

        public async Task DeleteFieldValueAsync(Guid valueId)
        {
            var value = await _unitOfWork.NamingConventionRepository.GetFieldValueAsync(valueId, track: true)
                ?? throw new ApiExceptionResponse("Naming field value not found.", 404);

            if (value.LockedValue != null)
                throw new ApiExceptionResponse("Cannot delete a value that is locked. Unlock the field first.", 400);

            _unitOfWork.Repository<NamingConventionFieldValue>().Delete(value);
            await _unitOfWork.CommitAsync();
        }

        public async Task<NamingConventionResponseDTO> SetLockedValueAsync(Guid fieldId, SetLockedValueDTO dto, Guid actor)
        {
            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(fieldId, track: true)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);

            var value = field.AllowedValues.FirstOrDefault(v => v.Id == dto.ValueId)
                ?? throw new ApiExceptionResponse("Value does not belong to this field.", 400);
            if (!value.IsActive)
                throw new ApiExceptionResponse("Cannot lock an inactive value.", 400);

            if (field.LockedValue != null)
            {
                field.LockedValue.NamingConventionFieldValueId = value.Id;
                field.LockedValue.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await _unitOfWork.Repository<NamingConventionLockedValue>().CreateAsync(new NamingConventionLockedValue
                {
                    Id = Guid.NewGuid(),
                    NamingConventionFieldId = field.Id,
                    NamingConventionFieldValueId = value.Id,
                    IsActive = true,
                    CreatedById = actor,
                    CreatedAt = DateTime.UtcNow
                });
            }

            field.IsLocked = true;
            field.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(field.NamingConventionId);
        }

        public async Task<NamingConventionResponseDTO> RemoveLockedValueAsync(Guid fieldId)
        {
            var field = await _unitOfWork.NamingConventionRepository.GetFieldWithDetailsAsync(fieldId, track: true)
                ?? throw new ApiExceptionResponse("Naming field not found.", 404);

            if (field.LockedValue != null)
                _unitOfWork.Repository<NamingConventionLockedValue>().Delete(field.LockedValue);

            field.IsLocked = false;
            field.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(field.NamingConventionId);
        }

        // =========================================================
        // Gán folder
        // =========================================================

        public async Task<NamingConventionResponseDTO> AssignFoldersAsync(Guid conventionId, AssignFoldersDTO dto, Guid actor, string? actorRole)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetByIdAsync(conventionId)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);

            var folders = await _unitOfWork.NamingConventionRepository
                .GetFoldersByIdsAsync(dto.FolderIds.Distinct(), track: true);

            var missing = dto.FolderIds.Distinct().Except(folders.Select(f => f.Id)).ToList();
            if (missing.Count > 0)
                throw new ApiExceptionResponse($"Folder(s) not found: {string.Join(", ", missing)}.", 404);

            var outsideProject = folders.Where(f => f.ProjectId != convention.ProjectId).ToList();
            if (outsideProject.Count > 0)
                throw new ApiExceptionResponse(
                    $"Folder(s) do not belong to the naming convention's project: {string.Join(", ", outsideProject.Select(f => f.Name))}.", 400);

            // Chỉ check các folder được yêu cầu trực tiếp — cây con mở rộng hưởng quyền từ gốc được gán.
            foreach (var folder in folders)
                await RequireFolderNamingAuthorityAsync(folder.Id, actor, actorRole);

            var targets = folders;
            if (dto.ApplyToSubfolders)
            {
                // Duyệt cây con trong bộ nhớ: load 1 lần toàn bộ folder của project.
                var projectFolders = await _unitOfWork.NamingConventionRepository
                    .GetProjectFoldersAsync(convention.ProjectId, track: true);
                var childrenByParent = projectFolders
                    .Where(f => f.ParentFolderId != null)
                    .ToLookup(f => f.ParentFolderId!.Value);

                var visited = new HashSet<Guid>(folders.Select(f => f.Id));
                var queue = new Queue<Guid>(visited);
                targets = projectFolders.Where(f => visited.Contains(f.Id)).ToList();

                while (queue.Count > 0)
                {
                    foreach (var child in childrenByParent[queue.Dequeue()])
                    {
                        if (visited.Add(child.Id))
                        {
                            targets.Add(child);
                            queue.Enqueue(child.Id);
                        }
                    }
                }
            }

            var now = DateTime.UtcNow;
            foreach (var folder in targets)
            {
                folder.NamingConventionId = convention.Id;
                folder.UpdatedAt = now;
            }

            await _unitOfWork.CommitAsync();
            return await GetByIdAsync(conventionId);
        }

        public async Task UnassignFolderAsync(Guid folderId, Guid actor, string? actorRole)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            await RequireFolderNamingAuthorityAsync(folderId, actor, actorRole);

            folder.NamingConventionId = null;
            folder.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();
        }

        // =========================================================
        // Upload flow
        // =========================================================

        public async Task<FolderNamingConventionResponseDTO> GetByFolderAsync(Guid folderId)
        {
            _ = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            var convention = await _unitOfWork.NamingConventionRepository.GetByFolderIdAsync(folderId);
            if (convention == null || !convention.IsActive)
                return new FolderNamingConventionResponseDTO { HasNamingConvention = false };

            return new FolderNamingConventionResponseDTO
            {
                HasNamingConvention = true,
                NamingConventionId = convention.Id,
                Delimiter = convention.Delimiter,
                Fields = convention.Fields
                    .OrderBy(f => f.OrderIndex)
                    .Select(f =>
                    {
                        var locked = f.IsLocked && f.LockedValue != null;
                        return new UploadNamingFieldDTO
                        {
                            Id = f.Id,
                            DisplayName = f.DisplayName,
                            OrderIndex = f.OrderIndex,
                            Required = f.IsRequired,
                            Locked = locked,
                            LockedValue = locked
                                ? new UploadNamingValueDTO
                                {
                                    Id = f.LockedValue!.Value.Id,
                                    Code = f.LockedValue.Value.Code,
                                    DisplayName = f.LockedValue.Value.DisplayName
                                }
                                : null,
                            Values = locked
                                ? null
                                : f.AllowedValues
                                    .Where(v => v.IsActive)
                                    .OrderBy(v => v.OrderIndex)
                                    .Select(v => new UploadNamingValueDTO
                                    {
                                        Id = v.Id,
                                        Code = v.Code,
                                        DisplayName = v.DisplayName
                                    })
                                    .ToList()
                        };
                    })
                    .ToList()
            };
        }

        public async Task<FileNameGenerationResultDTO> GenerateFileNameAsync(
            Guid folderId, string? selectionsJson, string originalFileName, CancellationToken ct = default)
        {
            var convention = await _unitOfWork.NamingConventionRepository.GetByFolderIdAsync(folderId);
            if (convention == null || !convention.IsActive)
                return new FileNameGenerationResultDTO { HasNamingConvention = false };

            var orderedFields = convention.Fields.OrderBy(f => f.OrderIndex).ToList();
            if (orderedFields.Count == 0)
                throw new ApiExceptionResponse("Naming convention has no fields configured.", 400);

            var selections = ParseSelections(selectionsJson);

            var segments = new List<ResolvedNamingSegmentDTO>();
            foreach (var field in orderedFields)
            {
                NamingConventionFieldValue? resolved;

                if (field.IsLocked)
                {
                    // Locked: backend luôn tự chèn, bỏ qua lựa chọn của user nếu có gửi lên.
                    resolved = field.LockedValue?.Value
                        ?? throw new ApiExceptionResponse(
                            $"Field '{field.DisplayName}' is locked but has no locked value configured.", 400);
                }
                else if (selections.TryGetValue(field.Id, out var valueId))
                {
                    resolved = field.AllowedValues.FirstOrDefault(v => v.Id == valueId)
                        ?? throw new ApiExceptionResponse(
                            $"Selected value does not belong to field '{field.DisplayName}'.", 400);
                    if (!resolved.IsActive)
                        throw new ApiExceptionResponse(
                            $"Selected value '{resolved.Code}' of field '{field.DisplayName}' is inactive.", 400);
                }
                else if (field.IsRequired)
                {
                    throw new ApiExceptionResponse($"Field '{field.DisplayName}' is required.", 400);
                }
                else
                {
                    continue; // optional, không chọn -> bỏ segment
                }

                if (field.MinLength.HasValue && resolved.Code.Length < field.MinLength.Value)
                    throw new ApiExceptionResponse(
                        $"Value '{resolved.Code}' of field '{field.DisplayName}' is shorter than the minimum length {field.MinLength.Value}.", 400);
                if (field.MaxLength.HasValue && resolved.Code.Length > field.MaxLength.Value)
                    throw new ApiExceptionResponse(
                        $"Value '{resolved.Code}' of field '{field.DisplayName}' exceeds the maximum length {field.MaxLength.Value}.", 400);

                segments.Add(new ResolvedNamingSegmentDTO
                {
                    FieldId = field.Id,
                    FieldCode = field.Code,
                    ValueId = resolved.Id,
                    ValueCode = resolved.Code,
                    ValueDisplayName = resolved.DisplayName
                });

                selections.Remove(field.Id);
            }

            // Chặn lựa chọn trỏ tới field không thuộc convention này.
            // (Lựa chọn cho field bị khóa thì được bỏ qua chứ không báo lỗi.)
            var unknown = selections.Keys
                .Where(fieldId => orderedFields.All(f => f.Id != fieldId))
                .ToList();
            if (unknown.Count > 0)
                throw new ApiExceptionResponse("Selection contains field(s) that do not belong to this naming convention.", 400);

            if (segments.Count == 0)
                throw new ApiExceptionResponse("No naming values resolved. At least one field value is required.", 400);

            var extension = Path.GetExtension(originalFileName); // giữ nguyên đuôi file gốc
            var nameWithoutExtension = string.Join(convention.Delimiter, segments.Select(s => s.ValueCode));

            return new FileNameGenerationResultDTO
            {
                HasNamingConvention = true,
                FileName = nameWithoutExtension + extension,
                FileNameWithoutExtension = nameWithoutExtension,
                Segments = segments
            };
        }

        public async Task StageFileNamingMetadataAsync(Guid fileItemId, FileNameGenerationResultDTO generation)
        {
            if (!generation.HasNamingConvention || generation.Segments.Count == 0)
                return;

            var now = DateTime.UtcNow;
            await _unitOfWork.Repository<FileNamingMetadata>().CreateRangeAsync(
                generation.Segments.Select(s => new FileNamingMetadata
                {
                    Id = Guid.NewGuid(),
                    FileItemId = fileItemId,
                    NamingConventionFieldId = s.FieldId,
                    SelectedValueId = s.ValueId,
                    Value = s.ValueCode,
                    DisplayValue = s.ValueDisplayName,
                    CreatedAt = now
                }));
            // Không SaveChanges: upload flow commit chung.
        }

        // =========================================================
        // Nội bộ
        // =========================================================

        private static void ValidateDelimiter(string delimiter)
        {
            if (!AllowedDelimiters.Contains(delimiter))
                throw new ApiExceptionResponse(
                    $"Delimiter '{delimiter}' is not supported. Allowed: {string.Join(" ", AllowedDelimiters)}.", 400);
        }

        private static NamingConventionField BuildField(Guid conventionId, CreateNamingFieldDTO dto, Guid actor)
        {
            if (dto.MinLength.HasValue && dto.MaxLength.HasValue && dto.MinLength > dto.MaxLength)
                throw new ApiExceptionResponse($"Field '{dto.Code}': MinLength cannot exceed MaxLength.", 400);

            var duplicatedValueCode = dto.AllowedValues
                .GroupBy(v => v.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicatedValueCode != null)
                throw new ApiExceptionResponse(
                    $"Field '{dto.Code}': duplicated value code '{duplicatedValueCode.Key}'.", 400);

            var now = DateTime.UtcNow;
            var field = new NamingConventionField
            {
                Id = Guid.NewGuid(),
                NamingConventionId = conventionId,
                Code = dto.Code.Trim(),
                DisplayName = dto.DisplayName.Trim(),
                Description = dto.Description,
                OrderIndex = dto.OrderIndex,
                IsRequired = dto.IsRequired,
                IsLocked = dto.IsLocked,
                MinLength = dto.MinLength,
                MaxLength = dto.MaxLength,
                FieldType = dto.FieldType,
                CreatedById = actor,
                CreatedAt = now
            };

            foreach (var valueDto in dto.AllowedValues)
            {
                field.AllowedValues.Add(new NamingConventionFieldValue
                {
                    Id = Guid.NewGuid(),
                    NamingConventionFieldId = field.Id,
                    Code = valueDto.Code.Trim(),
                    DisplayName = valueDto.DisplayName.Trim(),
                    Description = valueDto.Description,
                    OrderIndex = valueDto.OrderIndex,
                    IsActive = true,
                    CreatedById = actor,
                    CreatedAt = now
                });
            }

            if (dto.IsLocked)
            {
                if (string.IsNullOrWhiteSpace(dto.LockedValueCode))
                    throw new ApiExceptionResponse(
                        $"Field '{dto.Code}' is locked: LockedValueCode is required.", 400);

                var lockedValue = field.AllowedValues.FirstOrDefault(
                        v => string.Equals(v.Code, dto.LockedValueCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new ApiExceptionResponse(
                        $"Field '{dto.Code}': LockedValueCode '{dto.LockedValueCode}' is not in AllowedValues.", 400);

                field.LockedValue = new NamingConventionLockedValue
                {
                    Id = Guid.NewGuid(),
                    NamingConventionFieldId = field.Id,
                    NamingConventionFieldValueId = lockedValue.Id,
                    IsActive = true,
                    CreatedById = actor,
                    CreatedAt = now
                };
            }

            return field;
        }

        private static Dictionary<Guid, Guid> ParseSelections(string? selectionsJson)
        {
            if (string.IsNullOrWhiteSpace(selectionsJson))
                return new Dictionary<Guid, Guid>();

            List<NamingFieldSelectionDTO>? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<List<NamingFieldSelectionDTO>>(selectionsJson, SelectionJsonOptions);
            }
            catch (JsonException)
            {
                throw new ApiExceptionResponse(
                    "NamingSelections is not valid JSON. Expected: [{\"fieldId\":\"...\",\"valueId\":\"...\"}].", 400);
            }

            var selections = new Dictionary<Guid, Guid>();
            foreach (var selection in parsed ?? new List<NamingFieldSelectionDTO>())
            {
                if (selection.FieldId == Guid.Empty || selection.ValueId == Guid.Empty)
                    throw new ApiExceptionResponse("NamingSelections contains an empty fieldId/valueId.", 400);
                if (!selections.TryAdd(selection.FieldId, selection.ValueId))
                    throw new ApiExceptionResponse("NamingSelections contains duplicated field selections.", 400);
            }
            return selections;
        }

        private static NamingConventionResponseDTO MapDetail(NamingConvention convention, IEnumerable<Folder> assignedFolders)
        {
            return new NamingConventionResponseDTO
            {
                Id = convention.Id,
                ProjectId = convention.ProjectId,
                Name = convention.Name,
                Delimiter = convention.Delimiter,
                IsActive = convention.IsActive,
                CreatedAt = convention.CreatedAt,
                UpdatedAt = convention.UpdatedAt,
                Fields = convention.Fields
                    .OrderBy(f => f.OrderIndex)
                    .Select(f => new NamingFieldResponseDTO
                    {
                        Id = f.Id,
                        Code = f.Code,
                        DisplayName = f.DisplayName,
                        Description = f.Description,
                        OrderIndex = f.OrderIndex,
                        IsRequired = f.IsRequired,
                        IsLocked = f.IsLocked,
                        MinLength = f.MinLength,
                        MaxLength = f.MaxLength,
                        FieldType = f.FieldType,
                        AllowedValues = f.AllowedValues
                            .OrderBy(v => v.OrderIndex)
                            .Select(MapValue)
                            .ToList(),
                        LockedValue = f.LockedValue == null ? null : MapValue(f.LockedValue.Value)
                    })
                    .ToList(),
                AssignedFolders = assignedFolders
                    .Select(f => new AssignedFolderResponseDTO { Id = f.Id, Name = f.Name })
                    .OrderBy(f => f.Name)
                    .ToList()
            };
        }

        private static NamingFieldValueResponseDTO MapValue(NamingConventionFieldValue value) => new()
        {
            Id = value.Id,
            Code = value.Code,
            DisplayName = value.DisplayName,
            Description = value.Description,
            OrderIndex = value.OrderIndex,
            IsActive = value.IsActive
        };

        private const int MaxImportFields = 30;
        private const int MaxValuesPerField = 500;
        private static readonly string[] TemplateHeaders =
            { "FieldCode", "FieldName", "FieldDescription", "ValueCode", "ValueName", "ValueDescription" };

        public NamingConventionImportPreviewDTO ParseImportFile(Stream stream)
        {
            var engine = new ExcelEngine();
            IWorkbook workbook;
            try
            {
                workbook = engine.Excel.Workbooks.Open(stream, ExcelOpenType.Automatic);
            }
            catch (Exception)
            {

                throw new ApiExceptionResponse("Không đọc được file. Hãy dùng file .xlsx theo template mẫu.", 400);
            }

            var ws = workbook.Worksheets[0];

            for (int collum = 1; collum <= TemplateHeaders.Length; collum++)
            {
                var header = ws.Range[1, collum].DisplayText?.Trim();
                if (!string.Equals(header, TemplateHeaders[collum -1 ], StringComparison.OrdinalIgnoreCase))
                    throw new ApiExceptionResponse(
                $"File không đúng template (cột {collum} phải là '{TemplateHeaders[collum - 1]}'). Hãy tải template mẫu và điền theo.", 400);
            }

            var result = new NamingConventionImportPreviewDTO();
            var fieldsByCode = new Dictionary<string, ImportedNamingFieldDTO>(StringComparer.OrdinalIgnoreCase);
            var valueCodesByField = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            int lastRow = ws.UsedRange.LastRow;
            for (int row = 2; row <= lastRow; row++)
            {
                var fieldCode = ws.Range[row, 1].DisplayText?.Trim();
                var fieldName = ws.Range[row, 2].DisplayText?.Trim();
                var fieldDescription = ws.Range[row, 3].DisplayText?.Trim();
                var valueCode = ws.Range[row, 4].DisplayText?.Trim();
                var valueName = ws.Range[row, 5].DisplayText?.Trim();
                var valueDescription = ws.Range[row, 6].DisplayText?.Trim();

                if (string.IsNullOrEmpty(fieldCode) && string.IsNullOrEmpty(valueCode))
                    continue;

                if (string.IsNullOrEmpty(fieldCode) || string.IsNullOrEmpty(valueCode))
                {
                    result.Warnings.Add($"Dòng {row}: thiếu FieldCode hoặc ValueCode — đã bỏ qua.");
                    continue;
                }

                if (!fieldsByCode.TryGetValue(fieldCode, out var field))
                {
                    if (fieldsByCode.Count >= MaxImportFields)
                        throw new ApiExceptionResponse($"File vượt quá {MaxImportFields} field.", 400);

                    field = new ImportedNamingFieldDTO
                    {
                        Code = fieldCode,
                        DisplayName = string.IsNullOrEmpty(fieldName) ? fieldCode : fieldName,
                        Description = string.IsNullOrEmpty(fieldDescription) ? null : fieldDescription,
                        OrderIndex = fieldsByCode.Count
                    };

                    if (string.IsNullOrEmpty(fieldName))
                        result.Warnings.Add($"Dòng {row}: field '{fieldCode}' thiếu FieldName — tạm dùng chính code.");

                    fieldsByCode.Add(fieldCode, field);
                    valueCodesByField.Add(fieldCode, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    result.Fields.Add(field);
                }

                if (!valueCodesByField[fieldCode].Add(valueCode))
                {
                    result.Warnings.Add($"Dòng {row}: value '{valueCode}' trùng trong field '{fieldCode}' — đã bỏ qua.");
                    continue;
                }

                if (field.Values.Count >= MaxValuesPerField)
                    throw new ApiExceptionResponse($"Field '{fieldCode}' vượt quá {MaxValuesPerField} giá trị.", 400);

                field.Values.Add(new ImportedNamingValueDTO
                {
                    Code = valueCode,
                    DisplayName = string.IsNullOrEmpty(valueName) ? valueCode : valueName,
                    Description = string.IsNullOrEmpty(valueDescription) ? null : valueDescription,
                    OrderIndex = field.Values.Count
                });
            }

            if (result.Fields.Count == 0)
                throw new ApiExceptionResponse("File không có dữ liệu field/value hợp lệ nào.", 400);

            return result;
        }

        public byte[] GenerateImportTemplate()
        {
            using var engine = new ExcelEngine();
            engine.Excel.DefaultVersion = ExcelVersion.Excel2016;
            var wb = engine.Excel.Workbooks.Create(1);
            var ws = wb.Worksheets[0];
            ws.Name = "NamingFields";

            for (int collum = 1; collum <= TemplateHeaders.Length; collum++)
                ws[1, collum].Text = TemplateHeaders[collum - 1];

            ws["A1:F1"].CellStyle.Font.Bold = true;

            // Bộ trường chuẩn ISO 19650 (mã theo phụ lục quốc gia UK — BS EN ISO 19650-2).
            // Thứ tự trường = thứ tự xuất hiện trong tên file: PROJ-ORIG-VOL-LEV-TYPE-ROLE.
            // Admin sửa trực tiếp trên file hoặc ở bước preview: thay giá trị ví dụ (PROJ/ORIG),
            // thêm bớt tầng/khối theo dự án. Required/Khóa giá trị cấu hình ở UI sau khi import.
            string[,] sample =
            {
                { "PROJ", "Project",       "Mã dự án — thay bằng mã thật rồi khóa giá trị ở UI", "P01", "Dự án mẫu",                 "Ví dụ — sửa theo dự án" },

                { "ORIG", "Originator",    "Đơn vị tạo tài liệu",                                "ABC", "Công ty ABC",               "Ví dụ — thay bằng đơn vị thật" },
                { "ORIG", "",              "",                                                   "XYZ", "Nhà thầu XYZ",              "Ví dụ" },
                { "ORIG", "",              "",                                                   "TVG", "Tư vấn giám sát",           "Ví dụ" },

                { "VOL",  "Volume/System", "Khối tích / hệ thống",                               "ZZ",  "Toàn bộ khối tích",         "Áp dụng cho mọi khối/hệ" },
                { "VOL",  "",              "",                                                   "XX",  "Không áp dụng",             "" },
                { "VOL",  "",              "",                                                   "01",  "Khối/Hệ thống 01",          "Ví dụ — đặt theo dự án" },
                { "VOL",  "",              "",                                                   "02",  "Khối/Hệ thống 02",          "Ví dụ — đặt theo dự án" },

                { "LEV",  "Level/Location","Tầng / vị trí",                                      "ZZ",  "Nhiều tầng",                "" },
                { "LEV",  "",              "",                                                   "XX",  "Không áp dụng",             "" },
                { "LEV",  "",              "",                                                   "00",  "Tầng trệt / mặt bằng chung","" },
                { "LEV",  "",              "",                                                   "01",  "Tầng 1",                    "" },
                { "LEV",  "",              "",                                                   "02",  "Tầng 2",                    "" },
                { "LEV",  "",              "",                                                   "B1",  "Tầng hầm 1",                "" },
                { "LEV",  "",              "",                                                   "M1",  "Tầng lửng 1",               "" },
                { "LEV",  "",              "",                                                   "RF",  "Mái",                       "" },

                { "TYPE", "Document Type", "Loại tài liệu",                                      "DR",  "Bản vẽ 2D",                 "Drawing" },
                { "TYPE", "",              "",                                                   "M2",  "Mô hình 2D",                "2D model" },
                { "TYPE", "",              "",                                                   "M3",  "Mô hình 3D",                "3D model" },
                { "TYPE", "",              "",                                                   "CM",  "Mô hình tổng hợp",          "Combined model" },
                { "TYPE", "",              "",                                                   "CR",  "Báo cáo xung đột",          "Clash report" },
                { "TYPE", "",              "",                                                   "VS",  "Diễn họa / phối cảnh",      "Visualization" },
                { "TYPE", "",              "",                                                   "SP",  "Chỉ dẫn kỹ thuật",          "Specification" },
                { "TYPE", "",              "",                                                   "RP",  "Báo cáo",                   "Report" },
                { "TYPE", "",              "",                                                   "CA",  "Bản tính",                  "Calculations" },
                { "TYPE", "",              "",                                                   "SH",  "Bảng thống kê",             "Schedule" },
                { "TYPE", "",              "",                                                   "BQ",  "Bảng khối lượng",           "Bill of quantities" },
                { "TYPE", "",              "",                                                   "CP",  "Kế hoạch chi phí",          "Cost plan" },
                { "TYPE", "",              "",                                                   "CO",  "Văn bản trao đổi",          "Correspondence" },
                { "TYPE", "",              "",                                                   "MI",  "Biên bản họp",              "Minutes" },
                { "TYPE", "",              "",                                                   "MS",  "Biện pháp thi công",        "Method statement" },
                { "TYPE", "",              "",                                                   "PR",  "Tiến độ",                   "Programme" },
                { "TYPE", "",              "",                                                   "RI",  "Yêu cầu cung cấp thông tin","Request for information" },
                { "TYPE", "",              "",                                                   "SU",  "Khảo sát",                  "Survey" },
                { "TYPE", "",              "",                                                   "HS",  "An toàn & sức khỏe",        "Health and safety" },

                { "ROLE", "Role",          "Vai trò / bộ môn",                                   "A",   "Kiến trúc",                 "Architect" },
                { "ROLE", "",              "",                                                   "C",   "Kỹ sư hạ tầng",             "Civil engineer" },
                { "ROLE", "",              "",                                                   "D",   "Thoát nước / giao thông",   "Drainage, highways engineer" },
                { "ROLE", "",              "",                                                   "E",   "Kỹ sư điện",                "Electrical engineer" },
                { "ROLE", "",              "",                                                   "G",   "Trắc đạc",                  "Land surveyor" },
                { "ROLE", "",              "",                                                   "I",   "Thiết kế nội thất",         "Interior designer" },
                { "ROLE", "",              "",                                                   "K",   "Chủ đầu tư",                "Client" },
                { "ROLE", "",              "",                                                   "L",   "Kiến trúc cảnh quan",       "Landscape architect" },
                { "ROLE", "",              "",                                                   "M",   "Kỹ sư cơ / HVAC",           "Mechanical engineer" },
                { "ROLE", "",              "",                                                   "P",   "Cấp thoát nước công trình", "Public health engineer" },
                { "ROLE", "",              "",                                                   "Q",   "Dự toán",                   "Quantity surveyor" },
                { "ROLE", "",              "",                                                   "S",   "Kỹ sư kết cấu",             "Structural engineer" },
                { "ROLE", "",              "",                                                   "W",   "Nhà thầu thi công",         "Contractor" },
                { "ROLE", "",              "",                                                   "X",   "Thầu phụ",                  "Subcontractor" },
                { "ROLE", "",              "",                                                   "Y",   "Thiết kế chuyên ngành",     "Specialist designer" },
                { "ROLE", "",              "",                                                   "Z",   "Nhiều bộ môn",              "General / multiple" },
            };
            for (int i = 0; i < sample.GetLength(0); i++)
                for (int j = 0; j < sample.GetLength(1); j++)
                    ws[i + 2, j + 1].Text = sample[i, j];

            ws.UsedRange.AutofitColumns();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<NamingConventionResponseDTO> CloneForFolderAsync(Guid conventionId, Guid folderId, Guid actor, string? actorRole)
        {
            var source = await _unitOfWork.NamingConventionRepository.GetWithDetailsAsync(conventionId)
                ?? throw new ApiExceptionResponse("Naming convention not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            if (folder.ProjectId != source.ProjectId)
                throw new ApiExceptionResponse("Folder does not belong to the naming convention's project.", 400);

            await RequireFolderNamingAuthorityAsync(folderId, actor, actorRole);

            var now = DateTime.UtcNow;
            var clone = new NamingConvention
            {
                Id = Guid.NewGuid(),
                ProjectId = source.ProjectId,
                Name = $"{source.Name} – {folder.Name}",
                Delimiter = source.Delimiter,
                IsActive = true,
                CreatedById = actor,
                CreatedAt = now
            };

            foreach (var f in source.Fields.OrderBy(f => f.OrderIndex))
            {
                var newField = new NamingConventionField
                {
                    Id = Guid.NewGuid(),
                    NamingConventionId = clone.Id,
                    Code = f.Code,
                    DisplayName = f.DisplayName,
                    Description = f.Description,
                    OrderIndex = f.OrderIndex,
                    IsRequired = f.IsRequired,
                    IsLocked = f.IsLocked,
                    MinLength = f.MinLength,
                    MaxLength = f.MaxLength,
                    FieldType = f.FieldType,
                    CreatedById = actor,
                    CreatedAt = now
                };

                foreach (var v in f.AllowedValues.OrderBy(v => v.OrderIndex))
                {
                    var newValue = new NamingConventionFieldValue
                    {
                        Id = Guid.NewGuid(),
                        NamingConventionFieldId = newField.Id,
                        Code = v.Code,
                        DisplayName = v.DisplayName,
                        Description = v.Description,
                        OrderIndex = v.OrderIndex,
                        IsActive = v.IsActive,
                        CreatedById = actor,
                        CreatedAt = now
                    };
                    newField.AllowedValues.Add(newValue);

                    // Giữ nguyên khóa: map value bị khóa của bản gốc sang value tương ứng của bản clone.
                    if (f.LockedValue != null && f.LockedValue.NamingConventionFieldValueId == v.Id)
                    {
                        newField.LockedValue = new NamingConventionLockedValue
                        {
                            Id = Guid.NewGuid(),
                            NamingConventionFieldId = newField.Id,
                            NamingConventionFieldValueId = newValue.Id,
                            IsActive = true,
                            CreatedById = actor,
                            CreatedAt = now
                        };
                    }
                }
                clone.Fields.Add(newField);
            }

            await _unitOfWork.NamingConventionRepository.CreateAsync(clone);

            // Gán bản riêng cho folder ngay trong cùng transaction.
            folder.NamingConventionId = clone.Id;
            folder.UpdatedAt = now;
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(clone.Id);
        }



        private async Task RequireFolderNamingAuthorityAsync(Guid folderId, Guid actor, string? actorRole)
        {
            if (actorRole == AccountRole.Admin.ToString())
                return;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            var participants = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == folder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .ToDictionary(p => p.Id, p => p.GroupId);

            // Folder + toàn bộ tổ tiên (quyền cấp trên phủ xuống cây con).
            var allFolders = (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == folder.ProjectId))
                .ToDictionary(f => f.Id);
            var folderIds = new HashSet<Guid>();
            var current = folder;
            while (folderIds.Add(current.Id) && current.ParentFolderId.HasValue
                   && allFolders.TryGetValue(current.ParentFolderId.Value, out var parent))
                current = parent;

            var groupIds = new HashSet<Guid>();
            var acls = await _unitOfWork.Repository<FolderPermission>().FindAsync(
                p => folderIds.Contains(p.FolderId)
                     && p.ProjectParticipantId.HasValue
                     && p.Status == PermissionStatus.Active   // ⚠ check lại tên value trong enum PermissionStatus của bạn
                     && p.CanEdit);
            foreach (var acl in acls)
                if (participants.TryGetValue(acl.ProjectParticipantId!.Value, out var groupId))
                    groupIds.Add(groupId);

            // Chưa cấu hình ACL nào -> fallback mọi group active của project (nhất quán ApprovalService).
            if (groupIds.Count == 0)
                groupIds = participants.Values.ToHashSet();

            var isLeader = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == actor
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();
            if (!isLeader)
                throw new ApiExceptionResponse(
                    "Chỉ Leader của nhóm phụ trách thư mục mới được thay đổi quy tắc đặt tên cho thư mục này.", 403);
        }
    }
}

using Application.DTOs.RequestDTOs.NamingConvention;
using Application.DTOs.ResponseDTOs.NamingConvention;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
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

        public async Task<NamingConventionResponseDTO> AssignFoldersAsync(Guid conventionId, AssignFoldersDTO dto)
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

        public async Task UnassignFolderAsync(Guid folderId)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

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
    }
}

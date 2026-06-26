using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using iText.Kernel.Pdf;

namespace Application.Services
{
    /// <summary>
    /// Luu vi tri dat chu ky truc quan tren file PDF. Chi ap dung cho FileType.Pdf va khi file dang o WIP.
    /// </summary>
    public class FileSignaturePositionService : IFileSignaturePositionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IApprovalService _approvalService;

        public FileSignaturePositionService(IUnitOfWork unitOfWork, IFileStorageService storage, IApprovalService approvalService)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _approvalService = approvalService;
        }

        public async Task<FileSignaturePositionResponseDTO> SaveAsync(Guid fileItemId, SaveSignaturePositionDTO dto, Guid actor)
        {
            await _approvalService.RequireTeamLeaderAsync(fileItemId, actor);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (fileItem.FileType != FileType.Pdf)
                throw new ApiExceptionResponse("Only PDF files support visual signature.", 400);

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);
            if (folder.Area != CdeArea.Wip)
                throw new ApiExceptionResponse("File must be in WIP to set signature position.", 409);

            var version = await GetCurrentPdfVersionAsync(fileItem);
            var (pageCount, _, pageWidth, pageHeight) = await ReadPageInfoAsync(version.StoragePath, dto.PageNumber);

            if (dto.PageNumber < 1 || dto.PageNumber > pageCount)
                throw new ApiExceptionResponse($"Page number must be between 1 and {pageCount}.", 400);

            if (dto.X < 0 || dto.Y < 0 || dto.Width <= 0 || dto.Height <= 0
                || dto.X + dto.Width > pageWidth || dto.Y + dto.Height > pageHeight)
                throw new ApiExceptionResponse("Signature position must be inside the PDF page boundary.", 400);

            var now = DateTime.UtcNow;
            var existing = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
                    p => p.FileItemId == fileItemId))
                .FirstOrDefault();

            var isNew = existing == null;
            existing ??= new FileSignaturePosition
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItemId,
                CreatedBy = actor,
                CreatedAt = now
            };

            existing.PageNumber = dto.PageNumber;
            existing.X = dto.X;
            existing.Y = dto.Y;
            existing.Width = dto.Width;
            existing.Height = dto.Height;

            if (isNew)
                await _unitOfWork.Repository<FileSignaturePosition>().CreateAsync(existing);
            else
                existing.UpdatedAt = now;

            await _unitOfWork.CommitAsync();

            return Map(existing);
        }

        public async Task<FileSignaturePositionResponseDTO> GetAsync(Guid fileItemId)
        {
            var position = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
                    p => p.FileItemId == fileItemId))
                .FirstOrDefault()
                ?? throw new ApiExceptionResponse("Signature position not found.", 404);

            return Map(position);
        }

        public async Task<PdfPageInfoResponseDTO> GetPageInfoAsync(Guid fileItemId, int pageNumber = 1)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (fileItem.FileType != FileType.Pdf)
                throw new ApiExceptionResponse("Only PDF files support visual signature.", 400);

            var version = await GetCurrentPdfVersionAsync(fileItem);
            var (pageCount, resolvedPageNumber, pageWidth, pageHeight) = await ReadPageInfoAsync(version.StoragePath, pageNumber);

            return new PdfPageInfoResponseDTO
            {
                FileItemId = fileItemId,
                PageNumber = resolvedPageNumber,
                PageCount = pageCount,
                Width = pageWidth,
                Height = pageHeight
            };
        }

        private async Task<FileVersion> GetCurrentPdfVersionAsync(FileItem fileItem)
        {
            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            return await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);
        }

        private async Task<(int PageCount, int PageNumber, float PageWidth, float PageHeight)> ReadPageInfoAsync(string storagePath, int pageNumber)
        {
            using var stream = await _storage.OpenReadAsync(storagePath);
            using var reader = new PdfReader(stream);
            using var document = new PdfDocument(reader);

            var pageCount = document.GetNumberOfPages();
            var targetPage = pageNumber >= 1 && pageNumber <= pageCount ? pageNumber : 1;
            var size = document.GetPage(targetPage).GetPageSize();
            return (pageCount, targetPage, size.GetWidth(), size.GetHeight());
        }

        private static FileSignaturePositionResponseDTO Map(FileSignaturePosition position) => new()
        {
            Id = position.Id,
            FileItemId = position.FileItemId,
            PageNumber = position.PageNumber,
            X = position.X,
            Y = position.Y,
            Width = position.Width,
            Height = position.Height,
            CreatedBy = position.CreatedBy,
            CreatedAt = position.CreatedAt,
            UpdatedAt = position.UpdatedAt
        };
    }
}

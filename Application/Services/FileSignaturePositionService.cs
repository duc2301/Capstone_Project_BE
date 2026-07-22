using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Services.Signing;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using iText.Kernel.Pdf;

namespace Application.Services
{
    /// <summary>
    /// Luu vi tri dat chu ky truc quan tren PDF/Word/Excel.
    /// </summary>
    public class FileSignaturePositionService : IFileSignaturePositionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IApprovalService _approvalService;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly ICadToPdfConverter _cadConverter;

        public FileSignaturePositionService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IApprovalService approvalService,
            IOfficeToPdfConverter officeConverter,
            ICadToPdfConverter cadConverter)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _approvalService = approvalService;
            _officeConverter = officeConverter;
            _cadConverter = cadConverter;
        }

        public async Task<FileSignaturePositionResponseDTO> SaveAsync(Guid fileItemId, SaveSignaturePositionDTO dto, Guid actor)
        {
            await _approvalService.RequireTeamLeaderAsync(fileItemId, actor);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            var version = await GetCurrentVersionAsync(fileItem);
            var isPdf = fileItem.FileType == FileType.Pdf;
            var isWord = FileSignatureFormatRules.IsWordFormat(version.Format);
            var isExcel = FileSignatureFormatRules.IsExcelFormat(version.Format);
            var isCad2D = fileItem.FileType == FileType.Cad && FileSignatureFormatRules.IsCad2DFormat(version.Format);
            if (!isPdf && !isWord && !isExcel && !isCad2D)
                throw new ApiExceptionResponse("Only PDF, Word, Excel and 2D CAD (DWG/DWGX) files support visual signature.", 400);

            var (pageCount, _, pageWidth, pageHeight) = isPdf
                ? await ReadPdfPageInfoAsync(version.StoragePath!, dto.PageNumber)
                : await ReadOfficePreviewPageInfoAsync(fileItem, version, dto.PageNumber);

            if (dto.PageNumber < 1 || dto.PageNumber > pageCount)
                throw new ApiExceptionResponse($"Page number must be between 1 and {pageCount}.", 400);

            if (dto.X < 0 || dto.Y < 0 || dto.Width <= 0 || dto.Height <= 0
                || dto.X + dto.Width > pageWidth || dto.Y + dto.Height > pageHeight)
                throw new ApiExceptionResponse("Signature position must be inside the page boundary.", 400);

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

            var version = await GetCurrentVersionAsync(fileItem);
            var isPdf = fileItem.FileType == FileType.Pdf;
            var isWord = FileSignatureFormatRules.IsWordFormat(version.Format);
            var isExcel = FileSignatureFormatRules.IsExcelFormat(version.Format);
            var isCad2D = fileItem.FileType == FileType.Cad && FileSignatureFormatRules.IsCad2DFormat(version.Format);
            if (!isPdf && !isWord && !isExcel && !isCad2D)
                throw new ApiExceptionResponse("Only PDF, Word, Excel and 2D CAD (DWG/DWGX) files support visual signature.", 400);

            var (pageCount, resolvedPageNumber, pageWidth, pageHeight) = isPdf
                ? await ReadPdfPageInfoAsync(version.StoragePath!, pageNumber)
                : await ReadOfficePreviewPageInfoAsync(fileItem, version, pageNumber);

            var previewUrl = !string.IsNullOrWhiteSpace(version.PreviewStoragePath)
                ? await _storage.GetPresignedUrlAsync(version.PreviewStoragePath, 60)
                : isPdf
                    ? await _storage.GetPresignedUrlAsync(version.StoragePath!, 60)
                    : null;

            return new PdfPageInfoResponseDTO
            {
                FileItemId = fileItemId,
                PageNumber = resolvedPageNumber,
                PageCount = pageCount,
                Width = pageWidth,
                Height = pageHeight,
                PreviewUrl = previewUrl
            };
        }

        private async Task<FileVersionState> GetCurrentVersionAsync(FileItem fileItem)
        {
            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            return await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);
        }

        private async Task<(int PageCount, int PageNumber, float PageWidth, float PageHeight)> ReadPdfPageInfoAsync(string storagePath, int pageNumber)
        {
            await using var source = await _storage.OpenReadAsync(storagePath);
            using var stream = new MemoryStream();
            await source.CopyToAsync(stream);
            stream.Position = 0;

            using var reader = new PdfReader(stream);
            using var document = new PdfDocument(reader);

            var pageCount = document.GetNumberOfPages();
            var targetPage = pageNumber >= 1 && pageNumber <= pageCount ? pageNumber : 1;
            var size = document.GetPage(targetPage).GetPageSize();
            return (pageCount, targetPage, size.GetWidth(), size.GetHeight());
        }

        private async Task<(int PageCount, int PageNumber, float PageWidth, float PageHeight)> ReadOfficePreviewPageInfoAsync(FileItem fileItem, FileVersionState version, int pageNumber)
        {
            Stream pdfStream;
            if (!string.IsNullOrWhiteSpace(version.PreviewStoragePath))
            {
                pdfStream = await _storage.OpenReadAsync(version.PreviewStoragePath);
            }
            else if (FileSignatureFormatRules.IsCad2DFormat(version.Format))
            {
                // Ban ve CAD 2D (dwg/dwgx) convert dong bo qua ConvertAPI (thay APS Model Derivative - da
                // xac nhan thuc te KHONG xuat duoc PDF tu DWG voi tai khoan hien co).
                // Cache lai PreviewStoragePath (giong Office) de: (1) FE co preview de dat vi tri ky,
                // (2) luc stamp thuc su dung LAI dung ban PDF nay thay vi goi ConvertAPI lan nua (co the
                // ra ket qua khac nhau giua 2 lan goi, gay lech vi tri chu ky).
                var ext = "." + (version.Format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
                await using var source = await _storage.OpenReadAsync(version.StoragePath!);
                await using var converted = await _cadConverter.ConvertToPdfAsync(source, ext);

                var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                    ?? throw new ApiExceptionResponse("Folder not found.", 404);
                var stored = await _storage.SaveAsync(converted, folder.ProjectId, folder.Id, ".pdf");
                version.PreviewStoragePath = stored.RelativePath;
                await _unitOfWork.CommitAsync();

                pdfStream = await _storage.OpenReadAsync(version.PreviewStoragePath);
            }
            else
            {
                var ext = "." + (version.Format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
                await using var source = await _storage.OpenReadAsync(version.StoragePath!);
                pdfStream = await _officeConverter.ConvertToPdfAsync(source, ext);
            }

            await using (pdfStream)
            {
                using var buffer = new MemoryStream();
                await pdfStream.CopyToAsync(buffer);
                buffer.Position = 0;

                using var reader = new PdfReader(buffer);
                using var document = new PdfDocument(reader);

                var pageCount = document.GetNumberOfPages();
                var targetPage = pageNumber >= 1 && pageNumber <= pageCount ? pageNumber : 1;
                var size = document.GetPage(targetPage).GetPageSize();
                return (pageCount, targetPage, size.GetWidth(), size.GetHeight());
            }
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

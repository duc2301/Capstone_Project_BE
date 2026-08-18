using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services
{
    /// <inheritdoc cref="IDocumentIndexSyncService"/>
    public class DocumentIndexSyncService : IDocumentIndexSyncService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDocumentIndexRepository _indexRepository;
        private readonly IIngestBackgroundService _ingestQueue;

        public DocumentIndexSyncService(
            IUnitOfWork unitOfWork,
            IDocumentIndexRepository indexRepository,
            IIngestBackgroundService ingestQueue)
        {
            _unitOfWork = unitOfWork;
            _indexRepository = indexRepository;
            _ingestQueue = ingestQueue;
        }

        public async Task RequestIndexAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (file is null || !file.CurrentVersionId.HasValue)
                return;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(file.FolderId);

            // Tệp ở WIP/Shared thì im lặng bỏ qua: caller không cần biết luật vùng, cứ báo mỗi khi
            // tệp đổi chỗ hay đổi nội dung là đủ.
            if (folder is null || !folder.Area.IsOfficialArea())
                return;

            _ingestQueue.Enqueue(fileItemId);
        }

        public async Task<int> SyncPendingAsync(CancellationToken ct = default)
        {
            var fileItemIds = await _indexRepository.GetUnindexedFileItemIdsAsync(ct);

            foreach (var fileItemId in fileItemIds)
                _ingestQueue.Enqueue(fileItemId);

            return fileItemIds.Count;
        }
    }
}

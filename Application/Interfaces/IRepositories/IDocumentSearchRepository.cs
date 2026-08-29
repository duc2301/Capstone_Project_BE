using Domain.Entities;
using Domain.Enum.Cde;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IDocumentSearchRepository
    {
        /// <param name="viewableFolderIds">
        /// Tập thư mục người tìm được phép xem. <c>null</c> = không giới hạn (Admin hệ thống / PM / ProjectAdmin).
        /// Lọc quyền nằm ngay trong mệnh đề where của truy vấn vector để "tệp nào được phép trích dẫn"
        /// chỉ có đúng một nơi định nghĩa.
        /// </param>
        /// <param name="extraViewableFileIds">
        /// Các tệp được cấp quyền RIÊNG LẺ tuy thư mục chứa không xem được. Phải cộng vào vùng ứng
        /// viên: quyền cấp cho từng tệp không nằm trong tập thư mục nên bộ lọc thư mục sẽ loại nhầm.
        /// Bỏ qua khi viewableFolderIds là null.
        /// </param>
        Task<IReadOnlyList<DocumentSearchHit>> SearchByVectorAsync(
           Guid projectId, Vector queryEmbedding, int k,
           IReadOnlyCollection<Guid>? viewableFolderIds,
           IReadOnlyCollection<Guid>? extraViewableFileIds = null,
           CancellationToken ct = default);

        public record DocumentSearchHit(
        Guid FileItemId,
        string FileName,
        string Snippet,          // nội dung child khớp
        string? ParentContext,
        double Distance,
        CdeArea Area, 
        bool IsUnderRevision);
    }
    
}

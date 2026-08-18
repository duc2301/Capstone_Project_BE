using Application.DTOs.ResponseDTOs.Search;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface ISemanticSearchService
    {
        /// <summary>
        /// Tra cứu ngữ nghĩa trong phạm vi dự án. Kết quả LUÔN bị giới hạn theo quyền xem thư mục của
        /// <paramref name="actorId"/> — snippet nội dung cũng là dữ liệu, không được lộ ra ngoài ACL.
        /// </summary>
        Task<IReadOnlyList<FileSearchResultDTO>> SearchAsync(
            Guid projectId, string query, Guid actorId, CancellationToken ct = default);
    }
}

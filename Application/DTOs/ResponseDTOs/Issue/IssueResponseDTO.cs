using Application.DTOs.ResponseDTOs.Common;
using Domain.Enum.Issue;

namespace Application.DTOs.ResponseDTOs.Issue
{
    public class IssueResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public IssueType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public IssueStatus Status { get; set; }
        public IssuePriority Priority { get; set; }
        public Guid? RaisedByAccountId { get; set; }
        public string? RaisedByName { get; set; }
        public Guid? AssignedToAccountId { get; set; }
        public string? AssignedToName { get; set; }
        public Guid? AssignedToOrganizationId { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? LinkedFolderId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? ModelLocationJson { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Danh sach nguoi duoc them tham gia issue (IssueMention), kem ten hien thi.</summary>
        public List<AccountRefDTO> Participants { get; set; } = new();

        /// <summary>Id cua Discussion (ScopeType=Issue) gan voi issue nay, dung de goi API thao luan.</summary>
        public Guid? DiscussionId { get; set; }

        /// <summary>Trang thai cua return-request gan nhat duoc tao tu issue nay (neu co).</summary>
        public string? LinkedReturnRequestStatus { get; set; }

        /// <summary>File/anh duoc upload truc tiep vao issue (khac voi file dinh kem trong comment thao luan).</summary>
        public List<IssueAttachmentResponseDTO> Attachments { get; set; } = new();
    }
}

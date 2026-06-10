using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    // Operation thuộc luồng dự án không hợp với CRUD generic:
    // - Admin gán 1 account hiện có làm PM của project (1 account có thể làm PM nhiều dự án)
    // - PM add nhiều bên tham gia (department/team/organization) vô project trong 1 transaction
    public interface IProjectFlowService
    {
        Task<ProjectResponseDTO> AssignManagerAsync(Guid projectId, AssignProjectManagerDTO dto);
        Task<List<ParticipantResponseDTO>> AddParticipantsAsync(Guid projectId, AddParticipantsBulkDTO dto);
        Task<List<ParticipantResponseDTO>> GetParticipantsAsync(Guid projectId);

        // Danh sách dự án người dùng hiện tại đang tham gia (qua group) hoặc làm PM.
        Task<List<ProjectResponseDTO>> GetMyProjectsAsync();
    }
}

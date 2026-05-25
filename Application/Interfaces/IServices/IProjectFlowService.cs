using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    // Các operation thuộc luồng nghiệp vụ dự án không hợp với CRUD generic:
    // - Admin tạo PM cho 1 project trống (account + Project.ManagerAccountId atomic)
    // - PM add bên tham gia (Organization/Group) vô project
    public interface IProjectFlowService
    {
        Task<ProjectManagerCreatedResponseDTO> CreateManagerAsync(Guid projectId, CreateProjectManagerDTO dto);
        Task<ParticipantResponseDTO> AddParticipantAsync(Guid projectId, AddParticipantDTO dto);
    }
}

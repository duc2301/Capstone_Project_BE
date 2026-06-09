using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.RequestDTOs.DigitalSite;
using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.RequestDTOs.FolderTemplate;
using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.RequestDTOs.LandParcel;
using Application.DTOs.RequestDTOs.ModelFile;
using Application.DTOs.RequestDTOs.Notification;
using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.RequestDTOs.OrganizationType;
using Application.DTOs.RequestDTOs.ProgressReport;
using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.RequestDTOs.ProjectModel;
using Application.DTOs.RequestDTOs.Schedule;
using Application.DTOs.RequestDTOs.Submittal;
using Application.DTOs.RequestDTOs.WorkTask;
using Application.DTOs.ResponseDTOs.Account;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.Contract;
using Application.DTOs.ResponseDTOs.DigitalSite;
using Application.DTOs.ResponseDTOs.Discussion;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.Folder;
using Application.DTOs.ResponseDTOs.FolderTemplate;
using Application.DTOs.ResponseDTOs.Group;
using Application.DTOs.ResponseDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Issue;
using Application.DTOs.ResponseDTOs.LandParcel;
using Application.DTOs.ResponseDTOs.ModelFile;
using Application.DTOs.ResponseDTOs.Notification;
using Application.DTOs.ResponseDTOs.Organization;
using Application.DTOs.ResponseDTOs.OrganizationType;
using Application.DTOs.ResponseDTOs.ProgressReport;
using Application.DTOs.ResponseDTOs.Project;
using Application.DTOs.ResponseDTOs.ProjectModel;
using Application.DTOs.ResponseDTOs.Schedule;
using Application.DTOs.ResponseDTOs.Submittal;
using Application.DTOs.ResponseDTOs.WorkTask;
using AutoMapper;
using Domain.Entities;

namespace Application.Mapping
{
    public class MappingProfile : Profile
    {
        // Quy ước Update = partial: chỉ field khác null mới được ánh xạ đè.
        private void Crud<TEntity, TCreate, TUpdate, TResponse>()
        {
            CreateMap<TEntity, TResponse>();
            CreateMap<TCreate, TEntity>();
            CreateMap<TUpdate, TEntity>()
                .ForAllMembers(o => o.Condition((src, dest, val) => val != null));
        }

        public MappingProfile()
        {
            // --- Account (giữ nguyên bản gốc) ---
            CreateMap<Account, AccountResponseDTO>();
            CreateMap<CreateAccountDTO, Account>();
            CreateMap<UpdateAccountDTO, Account>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // --- Module A/B ---
            Crud<OrganizationType, CreateOrganizationTypeDTO, UpdateOrganizationTypeDTO, OrganizationTypeResponseDTO>();
            Crud<Organization, CreateOrganizationDTO, UpdateOrganizationDTO, OrganizationResponseDTO>();
            Crud<Group, CreateGroupDTO, UpdateGroupDTO, GroupResponseDTO>();
            // Member sub-DTO (Group.Members trong response build thủ công ở service)
            CreateMap<GroupMember, GroupMemberDTO>()
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.Account != null ? s.Account.UserName : ""))
                .ForMember(d => d.Email, o => o.MapFrom(s => s.Account != null ? s.Account.Email : null));
            Crud<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO>();
            Crud<ContractPackage, CreateContractPackageDTO, UpdateContractPackageDTO, ContractPackageResponseDTO>();

            // Notification: set thời điểm gửi + chưa đọc khi tạo
            CreateMap<Notification, NotificationResponseDTO>();
            CreateMap<CreateNotificationDTO, Notification>()
                .ForMember(d => d.SendAt, o => o.MapFrom(_ => DateTime.UtcNow))
                .ForMember(d => d.IsRead, o => o.MapFrom(_ => false));
            CreateMap<UpdateNotificationDTO, Notification>()
                .ForAllMembers(o => o.Condition((src, dest, val) => val != null));

            // --- Module C/D/E/F ---
            Crud<Folder, CreateFolderDTO, UpdateFolderDTO, FolderResponseDTO>();
            Crud<FileItem, CreateFileItemDTO, UpdateFileItemDTO, FileItemResponseDTO>();
            Crud<FolderTemplate, CreateFolderTemplateDTO, UpdateFolderTemplateDTO, FolderTemplateResponseDTO>();
            Crud<Submittal, CreateSubmittalDTO, UpdateSubmittalDTO, SubmittalResponseDTO>();
            Crud<Discussion, CreateDiscussionDTO, UpdateDiscussionDTO, DiscussionResponseDTO>();
            Crud<Issue, CreateIssueDTO, UpdateIssueDTO, IssueResponseDTO>();

            // Invitation
            CreateMap<ProjectInvitation, InvitationResponseDTO>();
            CreateMap<InviteRequestDTO, ProjectInvitation>();

            // ProjectParticipant -> ParticipantResponseDTO
            CreateMap<ProjectParticipant, ParticipantResponseDTO>();

            // ACL thư mục: 1 dòng override -> response
            CreateMap<FolderPermission, FolderPermissionResponseDTO>();

            // --- Module I/J/K/L/M ---
            Crud<Schedule, CreateScheduleDTO, UpdateScheduleDTO, ScheduleResponseDTO>();
            Crud<WorkTask, CreateWorkTaskDTO, UpdateWorkTaskDTO, WorkTaskResponseDTO>();
            Crud<ProgressReport, CreateProgressReportDTO, UpdateProgressReportDTO, ProgressReportResponseDTO>();
            Crud<Contract, CreateContractDTO, UpdateContractDTO, ContractResponseDTO>();
            Crud<ProjectModel, CreateProjectModelDTO, UpdateProjectModelDTO, ProjectModelResponseDTO>();
            Crud<ModelFile, CreateModelFileDTO, UpdateModelFileDTO, ModelFileResponseDTO>();
            Crud<DigitalSite, CreateDigitalSiteDTO, UpdateDigitalSiteDTO, DigitalSiteResponseDTO>();
            Crud<LandParcel, CreateLandParcelDTO, UpdateLandParcelDTO, LandParcelResponseDTO>();
        }
    }
}

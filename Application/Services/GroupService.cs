using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GroupService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GroupResponseDTO>> GetAllAsync()
        {
            var groups = await _unitOfWork.Repository<Group>().GetAllAsync();
            var members = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return groups.Select(g => Build(g, members, accounts));
        }

        public async Task<GroupResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id);
            if (entity == null) return null;

            var members = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return Build(entity, members, accounts);
        }

        public async Task<GroupResponseDTO> CreateAsync(CreateGroupDTO dto)
        {
            var entity = _mapper.Map<Group>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Group>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            // Nhóm vừa tạo chưa có member, trả ra DTO rỗng members
            return new GroupResponseDTO
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                OrganizationId = entity.OrganizationId,
                CreatedAt = entity.CreatedAt,
                Members = new List<GroupMemberDTO>()
            };
        }

        public async Task<GroupResponseDTO> UpdateAsync(Guid id, UpdateGroupDTO dto)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Group>().Update(entity);
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(id)
                ?? throw new ApiExceptionResponse("Group not found after update.", 500);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);
            _unitOfWork.Repository<Group>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }

        // Build DTO + join members + accounts (tra dictionary trong-mem, dataset CDE nhỏ -> chấp nhận).
        private static GroupResponseDTO Build(
            Group group,
            IEnumerable<GroupMember> allMembers,
            IDictionary<Guid, Account> accountIndex)
            => new()
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                OrganizationId = group.OrganizationId,
                CreatedAt = group.CreatedAt,
                Members = allMembers
                    .Where(m => m.GroupId == group.Id)
                    .Select(m => new GroupMemberDTO
                    {
                        AccountId = m.AccountId,
                        UserName = accountIndex.TryGetValue(m.AccountId, out var a) ? a.UserName : "",
                        Email = accountIndex.TryGetValue(m.AccountId, out var ae) ? ae.Email : null,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToList()
            };
    }
}

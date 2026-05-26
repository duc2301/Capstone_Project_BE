using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AccountService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AccountResponseDTO>> GetAllAsync()
        {
            var items = await _unitOfWork.AccountRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AccountResponseDTO>>(items);
        }

        public async Task<AccountResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<AccountResponseDTO>(entity);
        }

        public async Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto)
        {
            if (await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email))
                throw new ApiExceptionResponse("Email already exists.", 409);

            var account = _mapper.Map<Account>(dto);
            account.Id = Guid.NewGuid();
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            account.Status = AccountStatus.Active;
            account.Role = AccountRole.User;
            account.CreatedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.AccountRepository.CreateAsync(account);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AccountResponseDTO>(account);
        }

        public async Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(entity);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AccountResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _unitOfWork.AccountRepository.Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}

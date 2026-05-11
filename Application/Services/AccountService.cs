using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using BCrypt.Net;
using Domain.Entities;

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
            var accounts = await _unitOfWork.AccountRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AccountResponseDTO>>(accounts);
        }

        public async Task<AccountResponseDTO?> GetByIdAsync(Guid id)
        {
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(id);
            return account == null ? null : _mapper.Map<AccountResponseDTO>(account);
        }

        public async Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto)
        {
            var emailExists = await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email);
            if (emailExists)
                throw new ApiExceptionResponse("Email already exists.", 409);

            var account = _mapper.Map<Account>(dto);
            account.Id = Guid.NewGuid();
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            account.Status = "Active";
            account.Role ??= "Viewer";
            account.CreatedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.AccountRepository.CreateAsync(account);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AccountResponseDTO>(account);
        }

        public async Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto)
        {
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _mapper.Map(dto, account);
            account.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(account);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AccountResponseDTO>(account);
        }

        public async Task DeleteAsync(Guid id)
        {
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _unitOfWork.AccountRepository.Delete(account);
            await _unitOfWork.CommitAsync();
        }
    }
}

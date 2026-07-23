using Application.DTOs.RequestDTOs.NamingConvention;
using Application.ExceptionMiddleware;
using Application.Interfaces.IUnitOfWork;
using Application.Services;
using Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Capstone_Project.Tests
{
    public class NamingConventionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly NamingConventionService _namingConventionService;

        public NamingConventionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _namingConventionService = new NamingConventionService(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task CreateAsync_WhenProjectNotFound_ShouldThrowApiExceptionResponse()
        {
            // Arrange
            var dto = new CreateNamingConventionDTO { ProjectId = Guid.NewGuid(), Delimiter = "-" };
            
            // Giả lập (Mock) Project không tồn tại (trả về null)
            _mockUnitOfWork.Setup(u => u.Repository<Project>().GetByIdAsync(dto.ProjectId))
                .ReturnsAsync((Project)null!);

            // Act
            Func<Task> action = async () => await _namingConventionService.CreateAsync(dto, Guid.NewGuid());

            // Assert
            await action.Should().ThrowAsync<ApiExceptionResponse>()
                .WithMessage("Project not found.");
        }

        [Theory]
        [InlineData("DOC", "DOC", "Duplicated field code 'DOC'.")]
        [InlineData("PROJ", "PROJ", "Duplicated field code 'PROJ'.")]
        [InlineData("ZONE", "ZONE", "Duplicated field code 'ZONE'.")]
        public async Task CreateAsync_WhenDuplicatedFieldCode_ShouldThrowApiExceptionResponse(
            string code1, string code2, string expectedMessage)
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var dto = new CreateNamingConventionDTO 
            { 
                ProjectId = projectId, 
                Delimiter = "-",
                Fields = new List<CreateNamingFieldDTO>
                {
                    new CreateNamingFieldDTO { Code = code1 },
                    new CreateNamingFieldDTO { Code = code2 } 
                }
            };
            
            _mockUnitOfWork.Setup(u => u.Repository<Project>().GetByIdAsync(projectId))
                .ReturnsAsync(new Project { Id = projectId });

            // Act
            Func<Task> action = async () => await _namingConventionService.CreateAsync(dto, Guid.NewGuid());

            // Assert
            await action.Should().ThrowAsync<ApiExceptionResponse>()
                .WithMessage(expectedMessage);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("\\")]
        [InlineData(",")]
        [InlineData("|")]
        [InlineData(" ")]
        public async Task CreateAsync_WhenInvalidDelimiter_ShouldThrowException(string invalidDelimiter)
        {
             // Arrange
            var dto = new CreateNamingConventionDTO { ProjectId = Guid.NewGuid(), Delimiter = invalidDelimiter };
            
            // Act
            Func<Task> action = async () => await _namingConventionService.CreateAsync(dto, Guid.NewGuid());

            // Assert
            // Assuming ValidateDelimiter throws ApiExceptionResponse
            await action.Should().ThrowAsync<ApiExceptionResponse>();
        }
    }
}

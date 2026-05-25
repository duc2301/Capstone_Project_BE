using Application.DTOs.RequestDTOs.Department;
using Application.DTOs.ResponseDTOs.Department;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/departments")]
    public class DepartmentsController
        : BaseCrudController<Department, CreateDepartmentDTO, UpdateDepartmentDTO, DepartmentResponseDTO>
    {
        public DepartmentsController(
            IGenericService<Department, CreateDepartmentDTO, UpdateDepartmentDTO, DepartmentResponseDTO> service)
            : base(service) { }
    }
}

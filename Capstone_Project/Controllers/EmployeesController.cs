using Application.DTOs.RequestDTOs.Employee;
using Application.DTOs.ResponseDTOs.Employee;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/employees")]
    public class EmployeesController
        : BaseCrudController<Employee, CreateEmployeeDTO, UpdateEmployeeDTO, EmployeeResponseDTO>
    {
        public EmployeesController(
            IGenericService<Employee, CreateEmployeeDTO, UpdateEmployeeDTO, EmployeeResponseDTO> service)
            : base(service) { }
    }
}

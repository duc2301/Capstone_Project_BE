using Domain.Enum.Department;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DepartmentType Type { get; set; }
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    }
}

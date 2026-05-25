using Domain.Enum.Department;
using System;
using System.Collections.Generic;
using System.Text;

using Domain.Common;

namespace Domain.Entities
{
    public class Department : IEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    }
}

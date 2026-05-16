using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Project
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        public Guid DepartmentId { get; set; }
        public ICollection<Department> Departments { get; set; }
    }
}

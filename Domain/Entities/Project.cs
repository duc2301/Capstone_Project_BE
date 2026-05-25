using System;
using System.Collections.Generic;
using System.Text;

using Domain.Common;
using Domain.Enum.Project;

namespace Domain.Entities
{
    public class Project : IEntity
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; }
        public string ProjectDescription { get; set; }
        public Guid DepartmentId { get; set; }
        public Guid? ManagerAccountId { get; set; }   // Manager dự án (Admin cấp tài khoản & gán)
        public ProjectStatus Status { get; set; }     // Planning/Active/OnHold/Completed/Closed - cho Admin close/archive
        public ProjectPhase Phase { get; set; }       // Concept/Design/Construction/Handover/Operation - toàn vòng đời
        public ICollection<Department> Departments { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Employee
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Account Account { get; set; }
        public Department? Department { get; set; }
    }
}

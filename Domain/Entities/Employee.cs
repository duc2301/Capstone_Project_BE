using System;
using System.Collections.Generic;
using System.Text;

using Domain.Common;

namespace Domain.Entities
{
    public class Employee : IEntity
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Account Account { get; set; }
        public Department? Department { get; set; }
    }
}

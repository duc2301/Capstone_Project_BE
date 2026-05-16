using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class CDESystemDbContext : DbContext
    {
        public CDESystemDbContext() { }

        public CDESystemDbContext(DbContextOptions<CDESystemDbContext> options) : base(options) { }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Department> Departments { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Notification> Notifications { get; set; }
    }
}

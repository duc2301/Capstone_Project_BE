using Domain.Entities;
using Infrastructure.DbContexts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Repositories
{
    public class FolderTreeRepository : GenericRepository<Folder>
    {
        private readonly CDESystemDbContext _context;

        public FolderTreeRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }


    }
}

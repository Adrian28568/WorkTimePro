using Microsoft.EntityFrameworkCore;
using WorkTimePro.Api.Models.Entities;

namespace WorkTimePro.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<WorkSession> WorkSessions { get; set; }
    }
}

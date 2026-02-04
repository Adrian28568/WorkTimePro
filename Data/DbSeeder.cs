using WorkTimePro.Api.Models.Entities;
using BCrypt.Net;

namespace WorkTimePro.Api.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            if (!context.Users.Any())
            {
                var admin = new AppUser
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    IsAdmin = true
                };

                context.Users.Add(admin);
                context.SaveChanges();
            }
        }
    }
}

// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using BitrixChecker.Models;

namespace BitrixChecker.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<CheckedLink> CheckedLinks { get; set; }
    }
}
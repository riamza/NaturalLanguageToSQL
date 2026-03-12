using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; }
    public DbSet<QueryHistory> QueryHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Employee>().ToTable("employees");
        modelBuilder.Entity<QueryHistory>().ToTable("query_histories");
        
        // Seed some data for testing the Natural Language to SQL
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, FirstName = "John", LastName = "Doe", Department = "IT", Salary = 75000, HireDate = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 2, FirstName = "Jane", LastName = "Smith", Department = "HR", Salary = 65000, HireDate = new DateTime(2019, 5, 20, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 3, FirstName = "Robert", LastName = "Johnson", Department = "Sales", Salary = 85000, HireDate = new DateTime(2021, 3, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Employee { Id = 4, FirstName = "Emily", LastName = "Davis", Department = "IT", Salary = 80000, HireDate = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
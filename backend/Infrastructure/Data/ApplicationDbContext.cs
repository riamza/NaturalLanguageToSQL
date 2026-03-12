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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Ensure table name is safe for natural language querying
        modelBuilder.Entity<Employee>().ToTable("employees");
        
        // Seed some data for testing the Natural Language to SQL
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, FirstName = "John", LastName = "Doe", Department = "IT", Salary = 75000, HireDate = new DateTime(2020, 1, 15) },
            new Employee { Id = 2, FirstName = "Jane", LastName = "Smith", Department = "HR", Salary = 65000, HireDate = new DateTime(2019, 5, 20) },
            new Employee { Id = 3, FirstName = "Robert", LastName = "Johnson", Department = "Sales", Salary = 85000, HireDate = new DateTime(2021, 3, 10) },
            new Employee { Id = 4, FirstName = "Emily", LastName = "Davis", Department = "IT", Salary = 80000, HireDate = new DateTime(2022, 7, 1) }
        );
    }
}
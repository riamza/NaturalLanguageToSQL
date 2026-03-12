using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Salary = table.Column<decimal>(type: "numeric", nullable: false),
                    HireDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "employees",
                columns: new[] { "Id", "Department", "FirstName", "HireDate", "LastName", "Salary" },
                values: new object[,]
                {
                    { 1, "IT", "John", new DateTime(2020, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Doe", 75000m },
                    { 2, "HR", "Jane", new DateTime(2019, 5, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "Smith", 65000m },
                    { 3, "Sales", "Robert", new DateTime(2021, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Johnson", 85000m },
                    { 4, "IT", "Emily", new DateTime(2022, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Davis", 80000m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}

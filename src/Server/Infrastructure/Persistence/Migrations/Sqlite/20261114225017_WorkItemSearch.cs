using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Everdue.Server.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class WorkItemSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchDocument",
                table: "WorkItems",
                type: "TEXT",
                maxLength: 4400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Entities",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchDocument",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "Entities");
        }
    }
}

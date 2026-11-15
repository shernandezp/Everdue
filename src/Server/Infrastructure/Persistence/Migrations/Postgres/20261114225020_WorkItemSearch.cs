using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Everdue.Server.Infrastructure.Persistence.Migrations.Postgres
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
                type: "character varying(4400)",
                maxLength: 4400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Entities",
                type: "character varying(200)",
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

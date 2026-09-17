using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attractive.catalog.backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoStorageKey",
                table: "brands",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "brands",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoStorageKey",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "brands");
        }
    }
}

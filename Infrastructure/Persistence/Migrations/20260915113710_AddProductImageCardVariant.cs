using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attractive.catalog.backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageCardVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardStorageKey",
                table: "product_images",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardUrl",
                table: "product_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardStorageKey",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "CardUrl",
                table: "product_images");
        }
    }
}

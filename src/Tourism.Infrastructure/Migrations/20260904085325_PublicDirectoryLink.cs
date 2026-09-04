using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourism.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublicDirectoryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicDirectoryId",
                table: "TourismOrganizationProfiles",
                type: "varchar(12)",
                maxLength: 12,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TourismOrganizationProfiles_PublicDirectoryId",
                table: "TourismOrganizationProfiles",
                column: "PublicDirectoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TourismOrganizationProfiles_PublicDirectoryId",
                table: "TourismOrganizationProfiles");

            migrationBuilder.DropColumn(
                name: "PublicDirectoryId",
                table: "TourismOrganizationProfiles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourism.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTourismSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TourismOrganizationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProfileType = table.Column<int>(type: "int", nullable: false),
                    CategoryCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastProofOfLifeAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CurrentBadge = table.Column<int>(type: "int", nullable: true),
                    BadgeAssessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEvaluationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BadgeReasons = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourismOrganizationProfiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TourismOrganizationProfiles_OrganizationId",
                table: "TourismOrganizationProfiles",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TourismOrganizationProfiles");
        }
    }
}

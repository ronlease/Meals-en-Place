using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalTaskLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalTaskLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalProjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalTaskId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PushedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceScope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaskLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskLinks_Provider_ExternalProjectId",
                table: "ExternalTaskLinks",
                columns: new[] { "Provider", "ExternalProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskLinks_Provider_SourceType_SourceScope",
                table: "ExternalTaskLinks",
                columns: new[] { "Provider", "SourceType", "SourceScope" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskLinks_SourceType_SourceId_Provider",
                table: "ExternalTaskLinks",
                columns: new[] { "SourceType", "SourceId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalTaskLinks");
        }
    }
}

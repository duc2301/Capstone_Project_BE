using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileNamingConvetionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NamingConventionId",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NamingConventions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delimiter = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamingConventions_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NamingConventionFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingConventionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    MinLength = table.Column<int>(type: "integer", nullable: true),
                    MaxLength = table.Column<int>(type: "integer", nullable: true),
                    FieldType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConventionFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamingConventionFields_NamingConventions_NamingConventionId",
                        column: x => x.NamingConventionId,
                        principalTable: "NamingConventions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NamingConventionFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingConventionFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConventionFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamingConventionFieldValues_NamingConventionFields_NamingCo~",
                        column: x => x.NamingConventionFieldId,
                        principalTable: "NamingConventionFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileNamingMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedValueId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingConventionFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    DisplayValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileNamingMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileNamingMetadata_FileItems_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "FileItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileNamingMetadata_NamingConventionFieldValues_SelectedValu~",
                        column: x => x.SelectedValueId,
                        principalTable: "NamingConventionFieldValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileNamingMetadata_NamingConventionFields_NamingConventionF~",
                        column: x => x.NamingConventionFieldId,
                        principalTable: "NamingConventionFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NamingConventionLockedValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    NamingConventionFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamingConventionFieldValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConventionLockedValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamingConventionLockedValues_NamingConventionFieldValues_Na~",
                        column: x => x.NamingConventionFieldValueId,
                        principalTable: "NamingConventionFieldValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NamingConventionLockedValues_NamingConventionFields_NamingC~",
                        column: x => x.NamingConventionFieldId,
                        principalTable: "NamingConventionFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileNamingMetadata_FileItemId_NamingConventionFieldId",
                table: "FileNamingMetadata",
                columns: new[] { "FileItemId", "NamingConventionFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileNamingMetadata_FileItemId_SelectedValueId",
                table: "FileNamingMetadata",
                columns: new[] { "FileItemId", "SelectedValueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileNamingMetadata_NamingConventionFieldId",
                table: "FileNamingMetadata",
                column: "NamingConventionFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_FileNamingMetadata_SelectedValueId",
                table: "FileNamingMetadata",
                column: "SelectedValueId");

            migrationBuilder.CreateIndex(
                name: "IX_NamingConventionFields_NamingConventionId_Code",
                table: "NamingConventionFields",
                columns: new[] { "NamingConventionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamingConventionFieldValues_NamingConventionFieldId_Code",
                table: "NamingConventionFieldValues",
                columns: new[] { "NamingConventionFieldId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamingConventionLockedValues_NamingConventionFieldId",
                table: "NamingConventionLockedValues",
                column: "NamingConventionFieldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamingConventionLockedValues_NamingConventionFieldValueId",
                table: "NamingConventionLockedValues",
                column: "NamingConventionFieldValueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamingConventions_FolderId",
                table: "NamingConventions",
                column: "FolderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileNamingMetadata");

            migrationBuilder.DropTable(
                name: "NamingConventionLockedValues");

            migrationBuilder.DropTable(
                name: "NamingConventionFieldValues");

            migrationBuilder.DropTable(
                name: "NamingConventionFields");

            migrationBuilder.DropTable(
                name: "NamingConventions");

            migrationBuilder.DropColumn(
                name: "NamingConventionId",
                table: "Folders");
        }
    }
}

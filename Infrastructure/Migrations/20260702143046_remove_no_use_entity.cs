using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class remove_no_use_entity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelObjects");

            migrationBuilder.DropTable(
                name: "SubmittalAttachments");

            migrationBuilder.DropTable(
                name: "SubmittalCitedFolders");

            migrationBuilder.DropTable(
                name: "SubmittalSteps");

            migrationBuilder.DropTable(
                name: "ModelFiles");

            migrationBuilder.DropTable(
                name: "Submittals");

            migrationBuilder.DropTable(
                name: "ProjectModels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submittals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentSubmittalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkflowType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submittals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submittals_ContractPackages_ContractPackageId",
                        column: x => x.ContractPackageId,
                        principalTable: "ContractPackages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Submittals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Submittals_Submittals_ParentSubmittalId",
                        column: x => x.ParentSubmittalId,
                        principalTable: "Submittals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OffsetX = table.Column<double>(type: "double precision", nullable: true),
                    OffsetY = table.Column<double>(type: "double precision", nullable: true),
                    OffsetZ = table.Column<double>(type: "double precision", nullable: true),
                    RotationJson = table.Column<string>(type: "text", nullable: true),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelFiles_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalTable: "ProjectModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModelFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmittalAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttachedByAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmittalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmittalAttachments_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmittalAttachments_Submittals_SubmittalId",
                        column: x => x.SubmittalId,
                        principalTable: "Submittals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmittalCitedFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittalId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmittalCitedFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmittalCitedFolders_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmittalCitedFolders_Submittals_SubmittalId",
                        column: x => x.SubmittalId,
                        principalTable: "Submittals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmittalSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    AssignedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmittalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmittalSteps_Submittals_SubmittalId",
                        column: x => x.SubmittalId,
                        principalTable: "Submittals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    ObjectGuid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelObjects_ModelFiles_ModelFileId",
                        column: x => x.ModelFileId,
                        principalTable: "ModelFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelFiles_ProjectId",
                table: "ModelFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelFiles_ProjectModelId",
                table: "ModelFiles",
                column: "ProjectModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelObjects_ModelFileId",
                table: "ModelObjects",
                column: "ModelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModels_ProjectId",
                table: "ProjectModels",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittalAttachments_FileVersionId",
                table: "SubmittalAttachments",
                column: "FileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittalAttachments_SubmittalId",
                table: "SubmittalAttachments",
                column: "SubmittalId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittalCitedFolders_FolderId",
                table: "SubmittalCitedFolders",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittalCitedFolders_SubmittalId",
                table: "SubmittalCitedFolders",
                column: "SubmittalId");

            migrationBuilder.CreateIndex(
                name: "IX_Submittals_ContractPackageId",
                table: "Submittals",
                column: "ContractPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Submittals_ParentSubmittalId",
                table: "Submittals",
                column: "ParentSubmittalId");

            migrationBuilder.CreateIndex(
                name: "IX_Submittals_ProjectId",
                table: "Submittals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittalSteps_SubmittalId",
                table: "SubmittalSteps",
                column: "SubmittalId");
        }
    }
}

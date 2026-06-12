using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeleteUnusedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandParcels");

            migrationBuilder.DropTable(
                name: "Panorama360s");

            migrationBuilder.DropTable(
                name: "ProgressReportItems");

            migrationBuilder.DropTable(
                name: "SiteAnnotations");

            migrationBuilder.DropTable(
                name: "SiteImages");

            migrationBuilder.DropTable(
                name: "WorkTaskDependencies");

            migrationBuilder.DropTable(
                name: "WorkTaskModelLinks");

            migrationBuilder.DropTable(
                name: "WorkTaskPermissions");

            migrationBuilder.DropTable(
                name: "ProgressReports");

            migrationBuilder.DropTable(
                name: "CaptureStages");

            migrationBuilder.DropTable(
                name: "WorkTasks");

            migrationBuilder.DropTable(
                name: "DigitalSites");

            migrationBuilder.DropTable(
                name: "Schedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CenterLongitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MapType = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalSites_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandParcels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClearanceStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeoJson = table.Column<string>(type: "text", nullable: false),
                    HouseholdName = table.Column<string>(type: "text", nullable: true),
                    InfoJson = table.Column<string>(type: "text", nullable: true),
                    ParcelCode = table.Column<string>(type: "text", nullable: true),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandParcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandParcels_ContractPackages_ContractPackageId",
                        column: x => x.ContractPackageId,
                        principalTable: "ContractPackages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandParcels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_ContractPackages_ContractPackageId",
                        column: x => x.ContractPackageId,
                        principalTable: "ContractPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredecessorWorkTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuccessorWorkTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskDependencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaptureStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptureStages_DigitalSites_DigitalSiteId",
                        column: x => x.DigitalSiteId,
                        principalTable: "DigitalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeometryJson = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteAnnotations_DigitalSites_DigitalSiteId",
                        column: x => x.DigitalSiteId,
                        principalTable: "DigitalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgressReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportType = table.Column<int>(type: "integer", nullable: false),
                    ReportedByOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressReports_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentWorkTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualProduction = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PercentComplete = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedProduction = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTasks_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkTasks_WorkTasks_ParentWorkTaskId",
                        column: x => x.ParentWorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Panorama360s",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImageStoragePath = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Panorama360s", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Panorama360s_CaptureStages_CaptureStageId",
                        column: x => x.CaptureStageId,
                        principalTable: "CaptureStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImageStoragePath = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteImages_CaptureStages_CaptureStageId",
                        column: x => x.CaptureStageId,
                        principalTable: "CaptureStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgressReportItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgressReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualProduction = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressReportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressReportItems_ProgressReports_ProgressReportId",
                        column: x => x.ProgressReportId,
                        principalTable: "ProgressReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgressReportItems_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTaskModelLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskModelLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskModelLinks_ModelObjects_ModelObjectId",
                        column: x => x.ModelObjectId,
                        principalTable: "ModelObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkTaskModelLinks_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTaskPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanAssignPermission = table.Column<bool>(type: "boolean", nullable: false),
                    CanRenameTask = table.Column<bool>(type: "boolean", nullable: false),
                    CanReport = table.Column<bool>(type: "boolean", nullable: false),
                    CanUpdateDependency = table.Column<bool>(type: "boolean", nullable: false),
                    CanUpdatePlannedProduction = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskPermissions_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkTaskPermissions_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureStages_DigitalSiteId",
                table: "CaptureStages",
                column: "DigitalSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSites_ProjectId",
                table: "DigitalSites",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LandParcels_ContractPackageId",
                table: "LandParcels",
                column: "ContractPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_LandParcels_ProjectId",
                table: "LandParcels",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Panorama360s_CaptureStageId",
                table: "Panorama360s",
                column: "CaptureStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReportItems_ProgressReportId",
                table: "ProgressReportItems",
                column: "ProgressReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReportItems_WorkTaskId",
                table: "ProgressReportItems",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReports_ScheduleId",
                table: "ProgressReports",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ContractPackageId",
                table: "Schedules",
                column: "ContractPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAnnotations_DigitalSiteId",
                table: "SiteAnnotations",
                column: "DigitalSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteImages_CaptureStageId",
                table: "SiteImages",
                column: "CaptureStageId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskModelLinks_ModelObjectId",
                table: "WorkTaskModelLinks",
                column: "ModelObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskModelLinks_WorkTaskId",
                table: "WorkTaskModelLinks",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskPermissions_GroupId",
                table: "WorkTaskPermissions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskPermissions_WorkTaskId",
                table: "WorkTaskPermissions",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_ParentWorkTaskId",
                table: "WorkTasks",
                column: "ParentWorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_ScheduleId",
                table: "WorkTasks",
                column: "ScheduleId");
        }
    }
}

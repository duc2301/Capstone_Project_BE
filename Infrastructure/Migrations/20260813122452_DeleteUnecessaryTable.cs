using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeleteUnecessaryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillItems");

            migrationBuilder.DropTable(
                name: "ContractAppendices");

            migrationBuilder.DropTable(
                name: "DiscussionCitedFolders");

            migrationBuilder.DropTable(
                name: "IssueCitedFolders");

            migrationBuilder.DropTable(
                name: "IssueComments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentBillItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdjustedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    AdjustedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    AdjustedUnitPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ContractAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractAppendixId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractUnitPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sheet = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillItems_BillItems_ParentBillItemId",
                        column: x => x.ParentBillItemId,
                        principalTable: "BillItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAppendices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppendixNo = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    SignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceFileVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAppendices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAppendices_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionCitedFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscussionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionCitedFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionCitedFolders_Discussions_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscussionCitedFolders_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueCitedFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueCitedFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueCitedFolders_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueCitedFolders_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueComments_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillItems_ContractId",
                table: "BillItems",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_BillItems_ParentBillItemId",
                table: "BillItems",
                column: "ParentBillItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAppendices_ContractId",
                table: "ContractAppendices",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionCitedFolders_DiscussionId",
                table: "DiscussionCitedFolders",
                column: "DiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionCitedFolders_FolderId",
                table: "DiscussionCitedFolders",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueCitedFolders_FolderId",
                table: "IssueCitedFolders",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueCitedFolders_IssueId",
                table: "IssueCitedFolders",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_IssueId",
                table: "IssueComments",
                column: "IssueId");
        }
    }
}

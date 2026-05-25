using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Seed_RealisticGuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"));

            migrationBuilder.InsertData(
                table: "OrganizationTypes",
                columns: new[] { "Id", "Code", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("3fe93ed9-2e6a-47a6-90cf-6e5aac24c645"), "Supplier", null, true, "Nhà cung cấp" },
                    { new Guid("7f947ce1-e7c6-49b2-aa41-f9b30292917a"), "Client", null, true, "Chủ đầu tư" },
                    { new Guid("8c0dcb7d-87fe-413e-b8d6-83eb91171cbe"), "Subcontractor", null, true, "Nhà thầu phụ" },
                    { new Guid("ad4c917e-b170-4ff8-bca3-10764641c8d9"), "Surveyor", null, true, "Tư vấn giám sát" },
                    { new Guid("ad5b98c7-b28f-4c40-861a-5a363b84eb00"), "ProjectManagementUnit", null, true, "Ban quản lý dự án" },
                    { new Guid("ae2fd257-cca8-4bb4-8f90-c0c45100702b"), "MainContractor", null, true, "Nhà thầu chính" },
                    { new Guid("d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5"), "Consultant", null, true, "Tư vấn (thiết kế/BIM)" },
                    { new Guid("e48c6618-c877-46bf-9d6d-7d9fb92a50e9"), "FacilityManagement", null, true, "Đơn vị vận hành" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("3fe93ed9-2e6a-47a6-90cf-6e5aac24c645"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("7f947ce1-e7c6-49b2-aa41-f9b30292917a"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("8c0dcb7d-87fe-413e-b8d6-83eb91171cbe"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("ad4c917e-b170-4ff8-bca3-10764641c8d9"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("ad5b98c7-b28f-4c40-861a-5a363b84eb00"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("ae2fd257-cca8-4bb4-8f90-c0c45100702b"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("d692eaa8-4cf1-4a12-8bf8-4d0e1529acb5"));

            migrationBuilder.DeleteData(
                table: "OrganizationTypes",
                keyColumn: "Id",
                keyValue: new Guid("e48c6618-c877-46bf-9d6d-7d9fb92a50e9"));

            migrationBuilder.InsertData(
                table: "OrganizationTypes",
                columns: new[] { "Id", "Code", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Client", null, true, "Chủ đầu tư" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "ProjectManagementUnit", null, true, "Ban quản lý dự án" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "Surveyor", null, true, "Tư vấn giám sát" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "Consultant", null, true, "Tư vấn (thiết kế/BIM)" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "MainContractor", null, true, "Nhà thầu chính" },
                    { new Guid("66666666-6666-6666-6666-666666666666"), "Subcontractor", null, true, "Nhà thầu phụ" },
                    { new Guid("77777777-7777-7777-7777-777777777777"), "Supplier", null, true, "Nhà cung cấp" },
                    { new Guid("88888888-8888-8888-8888-888888888888"), "FacilityManagement", null, true, "Đơn vị vận hành" }
                });
        }
    }
}

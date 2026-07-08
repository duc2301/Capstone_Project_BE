using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op. All of these changes were already applied by
            // 20260618063524_AddStatusForPermissionTablesAndDeleteInheritField and
            // 20260625092117_AddFileNamingConvetionTable. This migration only exists to
            // resync the model snapshot after it had drifted from the actual applied schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op.
        }
    }
}

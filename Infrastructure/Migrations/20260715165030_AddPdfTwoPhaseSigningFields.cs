using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfTwoPhaseSigningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DigestBase64",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashAlgorithm",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedPdfStoragePath",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureValueBase64",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedAttributesBase64",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerCertificateBase64",
                table: "ApprovalSignatureTransactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigestBase64",
                table: "ApprovalSignatureTransactions");

            migrationBuilder.DropColumn(
                name: "HashAlgorithm",
                table: "ApprovalSignatureTransactions");

            migrationBuilder.DropColumn(
                name: "PreparedPdfStoragePath",
                table: "ApprovalSignatureTransactions");

            migrationBuilder.DropColumn(
                name: "SignatureValueBase64",
                table: "ApprovalSignatureTransactions");

            migrationBuilder.DropColumn(
                name: "SignedAttributesBase64",
                table: "ApprovalSignatureTransactions");

            migrationBuilder.DropColumn(
                name: "SignerCertificateBase64",
                table: "ApprovalSignatureTransactions");
        }
    }
}

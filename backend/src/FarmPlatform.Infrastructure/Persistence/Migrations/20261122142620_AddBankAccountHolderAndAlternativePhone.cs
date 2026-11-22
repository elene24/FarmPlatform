using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountHolderAndAlternativePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHolderFirstName",
                table: "PersonalDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderLastName",
                table: "PersonalDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderPersonalIdCiphertext",
                table: "PersonalDetails",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderPersonalIdLast4",
                table: "PersonalDetails",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlternativePhoneNumber",
                table: "PersonalDetails",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BankAccountOwnedByOther",
                table: "PersonalDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolderFirstName",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "AccountHolderLastName",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "AccountHolderPersonalIdCiphertext",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "AccountHolderPersonalIdLast4",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "AlternativePhoneNumber",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "BankAccountOwnedByOther",
                table: "PersonalDetails");
        }
    }
}

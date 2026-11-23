using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedPersons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkedPersons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepresentativeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PersonalIdNumberCiphertext = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PersonalIdNumberHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PersonalIdNumberLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LegalAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActualAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PayToRepresentative = table.Column<bool>(type: "boolean", nullable: false),
                    BankAccountCiphertext = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BankAccountLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ConsentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConsentedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentWithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentTextVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConsentTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConsentCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsentCodeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentCodeAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastConsentSmsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentSmsWindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentSmsInWindow = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkedPersons_AspNetUsers_RepresentativeUserId",
                        column: x => x.RepresentativeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedPersons_ConsentTokenHash",
                table: "LinkedPersons",
                column: "ConsentTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedPersons_PersonalIdNumberHash",
                table: "LinkedPersons",
                column: "PersonalIdNumberHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedPersons_RepresentativeUserId",
                table: "LinkedPersons",
                column: "RepresentativeUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkedPersons");
        }
    }
}

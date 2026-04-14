using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddEventRegistrationAndParticipantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Surname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CellNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IdNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsPearcMember = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttendanceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SAHFTANumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClubName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Division = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OtherDivision = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GunType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GuardianName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuardianSurname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InfoAccurateConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IndemnityAgreed = table.Column<bool>(type: "INTEGER", nullable: false),
                    GuardianIndemnityAgreed = table.Column<bool>(type: "INTEGER", nullable: false),
                    SocialMediaConsent = table.Column<bool>(type: "INTEGER", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    PaymentReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventRegistrations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Surname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventParticipants_EventRegistrations_EventRegistrationId",
                        column: x => x.EventRegistrationId,
                        principalTable: "EventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_EventRegistrationId",
                table: "EventParticipants",
                column: "EventRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_EventId",
                table: "EventRegistrations",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventParticipants");

            migrationBuilder.DropTable(
                name: "EventRegistrations");
        }
    }
}

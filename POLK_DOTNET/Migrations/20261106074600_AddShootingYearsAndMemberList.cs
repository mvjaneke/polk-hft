using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddShootingYearsAndMemberList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AdminFee",
                table: "MembershipApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "MembershipApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationType",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Flags",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FlagsReviewed",
                table: "MembershipApplications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MembershipFee",
                table: "MembershipApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidDate",
                table: "MembershipApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ShootingYearId",
                table: "MembershipApplications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SocialMediaConsent",
                table: "MembershipApplications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClubMemberId",
                table: "Members",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SahftaNumber",
                table: "Members",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Surname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IdNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ContactNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SahftaNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsJunior = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreetAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ComplexOrBuilding = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Suburb = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SocialMediaConsent = table.Column<bool>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShootingYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IndividualFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    FamilyFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    PensionerFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    NewMemberAdminFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    SahftaAdultFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    SahftaJuniorFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    RenewalReminderDaysBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShootingYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipReminderLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClubMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShootingYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipReminderLogs_ClubMembers_ClubMemberId",
                        column: x => x.ClubMemberId,
                        principalTable: "ClubMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipReminderLogs_ShootingYears_ShootingYearId",
                        column: x => x.ShootingYearId,
                        principalTable: "ShootingYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClubMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShootingYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    MembershipType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    MembershipApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberYears_ClubMembers_ClubMemberId",
                        column: x => x.ClubMemberId,
                        principalTable: "ClubMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberYears_MembershipApplications_MembershipApplicationId",
                        column: x => x.MembershipApplicationId,
                        principalTable: "MembershipApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemberYears_ShootingYears_ShootingYearId",
                        column: x => x.ShootingYearId,
                        principalTable: "ShootingYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_AccessToken",
                table: "MembershipApplications",
                column: "AccessToken");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_ShootingYearId",
                table: "MembershipApplications",
                column: "ShootingYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_ClubMemberId",
                table: "Members",
                column: "ClubMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_IdNumber",
                table: "ClubMembers",
                column: "IdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipReminderLogs_ClubMemberId_ShootingYearId",
                table: "MembershipReminderLogs",
                columns: new[] { "ClubMemberId", "ShootingYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipReminderLogs_ShootingYearId",
                table: "MembershipReminderLogs",
                column: "ShootingYearId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberYears_ClubMemberId_ShootingYearId",
                table: "MemberYears",
                columns: new[] { "ClubMemberId", "ShootingYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberYears_MembershipApplicationId",
                table: "MemberYears",
                column: "MembershipApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberYears_ShootingYearId",
                table: "MemberYears",
                column: "ShootingYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_ClubMembers_ClubMemberId",
                table: "Members",
                column: "ClubMemberId",
                principalTable: "ClubMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipApplications_ShootingYears_ShootingYearId",
                table: "MembershipApplications",
                column: "ShootingYearId",
                principalTable: "ShootingYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_ClubMembers_ClubMemberId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipApplications_ShootingYears_ShootingYearId",
                table: "MembershipApplications");

            migrationBuilder.DropTable(
                name: "MembershipReminderLogs");

            migrationBuilder.DropTable(
                name: "MemberYears");

            migrationBuilder.DropTable(
                name: "ClubMembers");

            migrationBuilder.DropTable(
                name: "ShootingYears");

            migrationBuilder.DropIndex(
                name: "IX_MembershipApplications_AccessToken",
                table: "MembershipApplications");

            migrationBuilder.DropIndex(
                name: "IX_MembershipApplications_ShootingYearId",
                table: "MembershipApplications");

            migrationBuilder.DropIndex(
                name: "IX_Members_ClubMemberId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "AdminFee",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "ApplicationType",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "Flags",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "FlagsReviewed",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "MembershipFee",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "ShootingYearId",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "SocialMediaConsent",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "ClubMemberId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "SahftaNumber",
                table: "Members");
        }
    }
}

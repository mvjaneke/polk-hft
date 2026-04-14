using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class EventRegistrationStage1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPearcMember",
                table: "EventRegistrations");

            migrationBuilder.AddColumn<bool>(
                name: "AllowsClubRifle",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BankingDetailsHtml",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableYocoPayment",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EntryFee",
                table: "Events",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntryFeeDescription",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistrationOpen",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationCloseDate",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAttendanceType",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresClubName",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDivision",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSahfta",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "SocialMediaConsent",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            // Remap any legacy bool-as-string values from previous schema
            migrationBuilder.Sql("UPDATE EventRegistrations SET SocialMediaConsent = 'Agreed' WHERE SocialMediaConsent IN ('1', 'True');");
            migrationBuilder.Sql("UPDATE EventRegistrations SET SocialMediaConsent = 'Declined' WHERE SocialMediaConsent IN ('0', 'False');");

            migrationBuilder.AlterColumn<string>(
                name: "SAHFTANumber",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Division",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ClubName",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AttendanceType",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RifleOwnership",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YocoCheckoutId",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YocoPaymentId",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsClubRifle",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "BankingDetailsHtml",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EnableYocoPayment",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EntryFee",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EntryFeeDescription",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsRegistrationOpen",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RegistrationCloseDate",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RequiresAttendanceType",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RequiresClubName",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RequiresDivision",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RequiresSahfta",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "RifleOwnership",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "YocoCheckoutId",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "YocoPaymentId",
                table: "EventRegistrations");

            migrationBuilder.AlterColumn<bool>(
                name: "SocialMediaConsent",
                table: "EventRegistrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SAHFTANumber",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Division",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClubName",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AttendanceType",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPearcMember",
                table: "EventRegistrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAddressWithStructuredFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "MembershipApplications");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ComplexOrBuilding",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Suburb",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "ComplexOrBuilding",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "MembershipApplications");

            migrationBuilder.DropColumn(
                name: "Suburb",
                table: "MembershipApplications");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}

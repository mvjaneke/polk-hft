using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressToMembershipApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "MembershipApplications",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "MembershipApplications");
        }
    }
}

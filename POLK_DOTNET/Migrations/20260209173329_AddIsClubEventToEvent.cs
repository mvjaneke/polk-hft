using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddIsClubEventToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClubEvent",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsClubEvent",
                table: "Events");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleHeaderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DoubleHeaderFee",
                table: "Events",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDoubleHeader",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseSameCourseForBothShoots",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ShootSelection",
                table: "EventRegistrations",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Shoot",
                table: "CourseTargets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoubleHeaderFee",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsDoubleHeader",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "UseSameCourseForBothShoots",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ShootSelection",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "Shoot",
                table: "CourseTargets");
        }
    }
}

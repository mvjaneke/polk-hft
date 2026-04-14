using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseTargetCount",
                table: "Events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeagueShoot",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CourseTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Lane = table.Column<int>(type: "INTEGER", nullable: false),
                    Posture = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsInclineDecline = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsShaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    KillZoneMm = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceMeters = table.Column<decimal>(type: "decimal(18, 2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseTargets_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseTargets_EventId",
                table: "CourseTargets",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseTargets");

            migrationBuilder.DropColumn(
                name: "CourseTargetCount",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsLeagueShoot",
                table: "Events");
        }
    }
}

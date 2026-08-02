using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddProvincialMealsAndBookingParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProvincialTwoDay",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MealDescription",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MealFee",
                table: "Events",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OffersMeals",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ExtraMeals",
                table: "EventRegistrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceType",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClubName",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Division",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GuardianIndemnityAgreed",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianSurname",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GunType",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdNumber",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IndemnityAgreed",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InfoAccurateConfirmed",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OtherDivision",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RifleOwnership",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SAHFTANumber",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShootSelection",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialMediaConsent",
                table: "EventParticipants",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartingLaneShoot1",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartingLaneShoot2",
                table: "EventParticipants",
                type: "INTEGER",
                nullable: true);

            // Bookings can now cover several people, and everything downstream (scorecards,
            // starting lanes, score sheets, exports) reads EventParticipants. Registrations
            // captured before this change hold their person's details on the booking row
            // itself, so each one becomes a booking with a single participant. Without this
            // they'd vanish from every scorecard and score sheet.
            //
            // The NOT EXISTS guard keeps the statement safe to re-run and skips any booking
            // that already has people on it.
            migrationBuilder.Sql(@"
                INSERT INTO EventParticipants (
                    EventRegistrationId, Position, Name, Surname, IdNumber, AttendanceType,
                    GunType, RifleOwnership, Division, OtherDivision, SAHFTANumber, ClubName,
                    ShootSelection, GuardianName, GuardianSurname, InfoAccurateConfirmed,
                    IndemnityAgreed, GuardianIndemnityAgreed, SocialMediaConsent,
                    StartingLaneShoot1, StartingLaneShoot2)
                SELECT
                    r.Id, 1, r.Name, r.Surname, r.IdNumber, r.AttendanceType,
                    COALESCE(r.GunType, ''), r.RifleOwnership, r.Division, r.OtherDivision,
                    r.SAHFTANumber, r.ClubName, r.ShootSelection, r.GuardianName,
                    r.GuardianSurname, r.InfoAccurateConfirmed, r.IndemnityAgreed,
                    r.GuardianIndemnityAgreed, r.SocialMediaConsent,
                    r.StartingLaneShoot1, r.StartingLaneShoot2
                FROM EventRegistrations r
                WHERE NOT EXISTS (
                    SELECT 1 FROM EventParticipants p WHERE p.EventRegistrationId = r.Id
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Undo the backfill first: the old columns on EventRegistrations still hold
            // this data, so the copies are safe to drop. Bookings with more than one person
            // can't be represented by the old schema — their extra people are lost on a
            // rollback, which is why the copy is deleted rather than merged back.
            migrationBuilder.Sql(@"
                DELETE FROM EventParticipants
                WHERE Position = 1
                  AND EXISTS (
                      SELECT 1 FROM EventRegistrations r
                      WHERE r.Id = EventParticipants.EventRegistrationId
                        AND r.Name = EventParticipants.Name
                        AND r.Surname = EventParticipants.Surname
                  );");

            migrationBuilder.DropColumn(
                name: "IsProvincialTwoDay",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MealDescription",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MealFee",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OffersMeals",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ExtraMeals",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "AttendanceType",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "ClubName",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "Division",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "GuardianIndemnityAgreed",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "GuardianSurname",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "GunType",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "IdNumber",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "IndemnityAgreed",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "InfoAccurateConfirmed",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "OtherDivision",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "RifleOwnership",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "SAHFTANumber",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "ShootSelection",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "SocialMediaConsent",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "StartingLaneShoot1",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "StartingLaneShoot2",
                table: "EventParticipants");
        }
    }
}

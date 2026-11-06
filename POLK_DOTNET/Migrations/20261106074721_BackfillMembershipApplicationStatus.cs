using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMembershipApplicationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing applications came from the old form, where one Status column carried both
            // payment and approval (and a payment overwrote an approval). Split it, and give every
            // row the values the new columns would have had. They were all new-member applications:
            // the old form had no renewal option. Shooting years and the member list are filled in
            // by MembershipSetup at startup, once the years exist.
            migrationBuilder.Sql(@"
                UPDATE MembershipApplications SET
                    PaymentStatus  = CASE WHEN Status = 'Paid' THEN 'Paid' ELSE 'Unpaid' END,
                    ApprovalStatus = CASE WHEN Status IN ('Approved', 'Rejected') THEN Status ELSE 'Pending' END,
                    ApplicationType = 'New',
                    PaymentMethod   = 'Card',
                    AccessToken     = lower(hex(randomblob(16)))
                WHERE PaymentStatus = '' OR AccessToken = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

# Project Update - POLK_DOTNET

This document summarizes the recent changes and current state of the POLK_DOTNET application, as of Monday, 9 February 2026, with the August 2026 changes added below.

## Implemented Features and Bug Fixes:

### 1. Committee Management Functionality Restored
*   **Issue:** Previously, new committee members could not be added via the Admin page.
*   **Resolution:** The `OnPostAddCommitteeMemberAsync` handler in `Admin.cshtml.cs` was refactored to align with the successful pattern used for adding events. This involved changing the method to accept individual parameters (`name`, `position`, `order`) and manually constructing the `CommitteeMember` object. The corresponding form in `Admin.cshtml` was updated to match this new signature, and all previous debugging logs were removed.
*   **Status:** **FIXED**

### 2. Event `IsClubEvent` Flag Integrated
*   **Feature:** Introduced a boolean flag `IsClubEvent` to the `Event` model.
*   **Resolution:** This property was already present in the `Event` model (via Entity Framework migration) and was confirmed to be correctly integrated into both the "Add Event" and "Edit Event" forms (`Admin.cshtml`, `EditEvent.cshtml`) with proper backend handling in `Admin.cshtml.cs` and `EditEvent.cshtml.cs`.
*   **Status:** **COMPLETED**

### 3. Event Registrations Dropdown Filtered
*   **Feature:** The "Select Event" dropdown in the Admin -> Event Registrations section now only lists events that have at least one registered participant.
*   **Resolution:** The `OnGetAsync` method in `Admin.cshtml.cs` was modified to filter the `Events` list populated for the dropdown, ensuring only events with associated `EventRegistration` entries are displayed.
*   **Status:** **COMPLETED**

### 4. Club Event Statistics Display
*   **Feature:** A new section on the Admin page to display statistics for club events, including counts for signed-up, paid, and not-paid registrations.
*   **Resolution:** A new nested class `EventStatsViewModel` was added to `Admin.cshtml.cs` to structure the statistics. The `OnGetAsync` method calculates these statistics specifically for club events (`e.IsClubEvent`) by querying `EventRegistrations` and populating a new `EventStatistics` property. A dedicated UI section was added to `Admin.cshtml` to present this data in a table format.
*   **Status:** **COMPLETED**

### 5. Constitution Management Functionality Restored
*   **Issue:** The "Constitution Management" section on the Admin page was non-functional due to reliance on Alpine.js directives, which were disabled.
*   **Resolution:** The form in `Admin.cshtml` was converted to a standard Razor Pages form submission (`method="post" asp-page-handler="SaveConstitution"`) with the `textarea`'s content bound via `name="Content"`. In `Admin.cshtml.cs`, `OnGetAsync` was updated to fetch existing constitution content into a new `CurrentConstitution` property, and `OnPostSaveConstitutionAsync` was implemented to manage saving (creating new or updating existing) constitution content to the database.
*   **Status:** **FIXED**

### 6. Admin Page Tabbed Interface Restored
*   **Issue:** The Admin page was displaying all sections linearly after Alpine.js-based tab management was removed.
*   **Resolution:** A functional tabbed interface was re-implemented using pure HTML/CSS and Razor Pages' postback mechanism (no JavaScript for tab switching). Tab buttons were converted to `<a>` tags that set an `ActiveTab` query parameter (`asp-route-ActiveTab`). Each content section is now conditionally rendered using `@if (Model.ActiveTab == "tabName")`, with `ActiveTab` defaulting to "events" in `Admin.cshtml.cs` and binding from the query string.
*   **Status:** **FIXED**

## August 2026 Changes

### 7. Provincial Two-Day Events
*   **Feature:** Added a new `Event.IsProvincialTwoDay` flag that doesn't depend on the League flags. It turns on Troyer course setup by itself. It also splits the course, starting lanes, scorecards and score sheets into Day 1 / Day 2, reusing the double-header code.
*   **Resolution:** Every competitor is entered for both days, so the form has no per-day choice and the entry fee covers the whole event. Day 2 takes its date from the event's `EndDate`. `Event.RoundLabel` sets the round name ("Shoot" for league events, "Day" for provincial events), and exports, filenames, lane-import sheet tabs and scorecard badges all use it.
*   **Status:** **COMPLETED** (`630b6f5`, 2 Aug 2026)

### 8. Meal Bookings
*   **Feature:** Events can offer meals at a price each (`OffersMeals`, `MealFee`, `MealDescription`), and each booking says how many meals it needs.
*   **Resolution:** Spectators pay for meals too, even though they pay no entry fee. A spectator-only booking therefore now gets a payment method and appears on the "To Collect (Unpaid)" sheet when it owes money. The sheet now decides this from the amount owed, not the attendance type, so free spectators still don't appear.
*   **Status:** **COMPLETED** (`630b6f5`, 2 Aug 2026)

### 9. Multi-Person Registrations (One Form for a Whole Party)
*   **Feature:** An `EventRegistration` is now a *booking* (contact + payment), and each person attending is an `EventParticipant` with full competitor details and their own indemnity and media-release signatures.
*   **Resolution:** People are added and removed with server-side postbacks, not Alpine.js. Scorecards, starting lanes, score sheets, the CSV and the collection sheet all read from participants, so there is one code path. The migration only adds columns, plus a backfill that turned each existing registration into a one-person booking.
*   **Also:**
    *   New `EventFeeCalculator` is the only place booking costs are calculated (it replaces `RegisterEventModel.ComputeEffectiveFee`).
    *   The CSV export now formats money with `InvariantCulture`. Under the ZA locale it was writing "390,00", which split the column and broke the file.
    *   If a registrant chooses "pay online" and the gateway returns no payment link, the confirmation page now says so and the failure is logged. Before, the page made the booking look settled.
*   **Status:** **COMPLETED** (`630b6f5`, 2 Aug 2026)

### 10. "Submit Registration" Re-Added a Person Instead of Saving
*   **Issue:** After clicking "Add another person", the form's URL kept `?handler=AddParticipant`, so every later submit ran that handler again. Anyone booking more than one person could never submit. Separately, spectators couldn't submit at all because of a hidden required rifle-type field.
*   **Resolution:** The form's action is now fixed to the page URL without a handler, and the add/remove buttons use their own `formaction`. Participant fields are now nullable, so `ValidateParticipant` alone decides what each person must fill in. The validation summary now appears only when there are errors, links to the problem section or person, and scrolls into view. Opening the add/remove handler URLs directly (GET) now redirects to a clean form instead of returning 400.
*   **Status:** **FIXED** (`03b13d4`, 10 Aug 2026)

### 11. Dashboard Counts Reconciled
*   **Issue:** The dashboard tiles counted different groups without saying so: "Bookings" included cancelled bookings, but People, Competitors, Spectators and Meals did not. Someone who registered twice was also counted as two people.
*   **Resolution:** All tiles now count live bookings (anything not cancelled), and the page says so. Cancelled bookings are shown separately and are greyed out, so Paid + Pending + Confirmed = Bookings. Possible duplicate entries are matched by ID number, then by name, and **listed for the organiser to check rather than subtracted**. Two shooters can share a name, and these counts are used to buy goodie bags and meals. Tiles show the entry count as the headline and the unique-person count beneath it. The events list now shows bookings and people as separate figures.
*   **Status:** **FIXED** (`4681cb0`, 10 Aug 2026)

### 12. Cancelled and Settled Bookings No Longer Show Money Owed
*   **Issue:** The "Owing" column was showing the booking total, whatever the status or the amount already paid.
*   **Resolution:** The registrations table now shows Total, Paid and Owing as separate columns. Whether a booking is settled is decided by its status, the same way the "To Collect (Unpaid)" sheet decides it. On a cancelled booking the total is struck through and there is no owing figure. If a booking is cancelled after payment, it is flagged "refund due". If a booking is marked Paid with no amount recorded, it reads "marked paid — amount not recorded". The single-booking page follows the same rules.
*   **Status:** **FIXED** (`6fbd51d`, 10 Aug 2026)

### 13. Development Database No Longer Published
*   **Issue:** Every publish copied the local `app.db` into the deploy folder. Uploading that folder over FTP could have overwritten the live database and every registration in it.
*   **Resolution:** `CopyToPublishDirectory=Never` on `app.db` (and its `-shm`/`-wal` files) in the csproj. The database still copies next to the build output so the app runs from `bin`. The server keeps its own database and applies migrations on startup.
*   **Status:** **FIXED** (`a2266bd`, 10 Aug 2026)

## November 2026 Changes: Membership

### 14. Membership Application Reworked
*   **Issue:** The Apply page's fees were hard-coded, and POLK charged SAHFTA fees (R200/R140) that members should pay SAHFTA directly (R180/R120). Whether someone was a renewal was guessed from earlier applications marked "Approved". A payment overwrote that status, so paid renewers were charged the admin fee again.
*   **Resolution:** The form now asks **New or Renewal**. The admin fee (R250) is charged once per *new* application, never on renewals. SAHFTA is an information panel with an optional SAHFTA number per person. The paper-form declarations were added: social-media photo permission, plus a parent/guardian declaration when anyone is under 18. Applicants pay by card or EFT; EFT shows bank details and a reference (`APP{id} {surname}`). Family members are added by server postback, and the total updates with CSS `:has()` only (no JavaScript, no Alpine.js). The server always recalculates the amount. Applicants land on `/apply/submitted/{token}`, reached by an unguessable token.
*   **Status:** **COMPLETED**

### 15. Shooting Years and Fees
*   **Feature:** New `ShootingYear` table (1 Nov – 31 Oct, as SAHFTA): Individual, Family and Pensioner fees, the new-member admin fee, SAHFTA fees (for information) and the number of days before year-end that reminders start (default 10). It's edited at **Admin → Membership → Shooting years & fees**, along with the EFT bank details.
*   **Resolution:** Applications copy the fees they were charged (`MembershipFee`, `AdminFee`), so later changes never rewrite history. From the reminder date, the Apply page sells the following year if it has been set up. 2025/2026 and 2026/2027 are seeded at startup with the 2026 paper-form prices.
*   **Status:** **COMPLETED**

### 16. Member List
*   **Feature:** New `ClubMember` (one row per person, matched by ID number with spaces removed) and `MemberYear` (one row per member per paid year) tables, at **Admin → Membership → Members**. Filters: paid this year, not yet renewed, no paid year, archived. There's a CSV export, and paper-era members can be added by hand with the years they paid.
*   **Resolution:** Paying an application (iKhokha webhook, Yoco webhook, payment-return page or the admin's **Mark paid**) all goes through `MembershipService.MarkPaidAsync`. It adds or updates everyone on the application and records the year, and a second call does nothing. Application status is split into `PaymentStatus` and `ApprovalStatus`; the legacy `Status` column is kept but unused. Mismatches (a renewal not on the list, a "new" applicant already on it, someone already paid for the year) are stored as **Flags** for the admin to check. They don't block the application, because the list is still being compiled from paper records.
*   **Status:** **COMPLETED**

### 17. Renewal Reminders
*   **Feature:** `MembershipReminderService` emails members who paid for the ending year but not the next one. Reminders start N days before year-end, go out once per member, and people sharing an email (a family) get one email. `MembershipReminderLog` prevents repeats; an address that fails three times is shown to the admin instead of being retried.
*   **Resolution:** A background job checks hourly during office hours, **only when switched on** at **Admin → Membership → Renewal reminders**. That page also has a preview, **Send now** and a log. Nothing is sent until the next year has been set up. In Development the job never sends by itself (unless `Jobs:AutoRun` is set), so a local copy of the live database can't email members.
*   **Status:** **COMPLETED** (off until switched on)

### 18. Security Fixes in the Membership Flow
*   **Issue:** Visiting `/payment-result?status=success&applicationId=N` marked any application paid. `/api/members/check-existence` told anyone whether an ID number belonged to a member.
*   **Resolution:** With iKhokha, the payment-return page now asks the gateway before marking an application paid; otherwise the signed webhook does it. The lookup API was removed. *Not yet changed:* event registrations still trust the success link, and the Yoco webhook is not signature-checked.
*   **Status:** **FIXED** (membership only)

### 19. Duplicate Applications After a Failed Card Payment
*   **Issue:** After the first live renewals, the applications list filled with unpaid leftovers. A failed or cancelled card payment only offered EFT, so applicants who wanted to pay by card filled in the form again. Each try created a new application (Leon Booyens: #7 and #8 unpaid, then #9 paid). There was also no way to clear old dead applications from "Needs action" and "Unpaid".
*   **Resolution:**
    *   The applicant's confirmation page has a **Pay by card** button ("Try again" after a failure). It makes a new payment link for the *same* application. Each retry gets its own iKhokha reference (`APP-{id}-{yyMMddHHmmss}`), so the status check reads that attempt and not the failed one. The webhook accepts both reference forms.
    *   New approval status **Closed**. When an application is paid, unpaid applications for the same year whose people are all on the paid one are closed automatically, with a note. A family application isn't closed because one member also applied alone. A one-time startup sweep (setting `Membership:DuplicatesSwept`) closed the leftovers already on the live site.
    *   The admin application page has **Close application** (duplicate / abandoned / test) for unpaid ones, and **Reopen**. Closed applications are left out of every count and only show under a new **Closed** filter. Nothing is deleted. If a payment still arrives for a closed one, it reopens itself.
    *   The applicant's page for a closed application says nothing is due, and shows no payment details.
*   **Status:** **FIXED**

## Important Development Notes:

*   **Alpine.js Usage:** **Do NOT use Alpine.js for any new or existing frontend functionality in this project.** All new UI features or fixes must strictly adhere to a pure HTML/CSS and Razor Pages approach, utilizing server-side rendering and postbacks. Previous reliance on Alpine.js has caused significant debugging challenges and broken existing functionality.
*   **Deployment:** Publish with `dotnet publish -c Release -p:PublishDir=C:/PUBLISH_PK/`. The CLI ignores the publish profile's `PublishUrl`. Then upload the folder with FileZilla. **Upload, never mirror:** gallery images added through the admin exist only on the server in `wwwroot/img`, and a mirror would delete them.
*   **Live data:** The site takes real entries and payments. Back up `app.db` before writing test data, tag test rows so they're easy to find (e.g. `CLAUDETEST` in the email), and delete them afterwards.
*   **Membership deploys:** The membership migrations run on startup against live data. Back up the live `app.db` before uploading a build that contains new migrations.

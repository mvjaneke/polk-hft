using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    /// <summary>
    /// One member: their details, the years they paid for, the applications they came in on and
    /// the reminders sent to them. Also used to add someone by hand ("new"), which is how members
    /// from the paper era get onto the list.
    /// </summary>
    public class MemberModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;

        public MemberModel(ApplicationDbContext context) { _context = context; }

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = "new";

        [BindProperty]
        public MemberInput Input { get; set; } = new();

        public ClubMember? Member { get; private set; }
        public bool IsNew => Member == null;
        public List<ShootingYear> Years { get; private set; } = new();
        public List<MembershipApplication> Applications { get; private set; } = new();
        public List<MembershipReminderLog> Reminders { get; private set; } = new();
        public string Today => ClubClock.Today.ToString("yyyy-MM-dd");
        public int? DefaultYearId { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await LoadAsync()) return NotFound();
            if (Member != null)
            {
                Input = new MemberInput
                {
                    FirstName = Member.FirstName, Surname = Member.Surname, IdNumber = Member.IdNumber, Gender = Member.Gender,
                    ContactNumber = Member.ContactNumber, EmailAddress = Member.EmailAddress, SahftaNumber = Member.SahftaNumber,
                    IsJunior = Member.IsJunior, StreetAddress = Member.StreetAddress, ComplexOrBuilding = Member.ComplexOrBuilding,
                    Suburb = Member.Suburb, City = Member.City, Province = Member.Province, PostalCode = Member.PostalCode,
                    SocialMediaConsent = Member.SocialMediaConsent switch { true => "yes", false => "no", _ => "" },
                    Notes = Member.Notes
                };
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!await LoadAsync()) return NotFound();

            var id = ClubMember.NormaliseIdNumber(Input.IdNumber);
            if (string.IsNullOrWhiteSpace(Input.FirstName)) ModelState.AddModelError("Input.FirstName", "Enter a first name.");
            if (string.IsNullOrWhiteSpace(Input.Surname)) ModelState.AddModelError("Input.Surname", "Enter a surname.");
            if (id.Length < 6) ModelState.AddModelError("Input.IdNumber", "Enter the ID or passport number. Renewals are matched on it.");
            if (!string.IsNullOrWhiteSpace(Input.EmailAddress) && !new EmailAddressAttribute().IsValid(Input.EmailAddress.Trim()))
                ModelState.AddModelError("Input.EmailAddress", "Enter a valid email address.");

            var duplicate = await _context.ClubMembers.FirstOrDefaultAsync(m => m.IdNumber == id && m.Id != (Member == null ? 0 : Member.Id));
            if (duplicate != null)
                ModelState.AddModelError("Input.IdNumber", $"{duplicate.FullName} already has this ID number on the member list.");

            if (!ModelState.IsValid) return Page();

            var m = Member ?? new ClubMember { CreatedAt = DateTime.UtcNow };
            m.FirstName = Input.FirstName!.Trim();
            m.Surname = Input.Surname!.Trim();
            m.IdNumber = id;
            m.Gender = MembershipService.NullIfEmpty(Input.Gender);
            m.ContactNumber = MembershipService.NullIfEmpty(Input.ContactNumber);
            m.EmailAddress = MembershipService.NullIfEmpty(Input.EmailAddress);
            m.SahftaNumber = MembershipService.NullIfEmpty(Input.SahftaNumber);
            m.IsJunior = Input.IsJunior;
            m.StreetAddress = MembershipService.NullIfEmpty(Input.StreetAddress);
            m.ComplexOrBuilding = MembershipService.NullIfEmpty(Input.ComplexOrBuilding);
            m.Suburb = MembershipService.NullIfEmpty(Input.Suburb);
            m.City = MembershipService.NullIfEmpty(Input.City);
            m.Province = MembershipService.NullIfEmpty(Input.Province);
            m.PostalCode = MembershipService.NullIfEmpty(Input.PostalCode);
            m.SocialMediaConsent = Input.SocialMediaConsent switch { "yes" => true, "no" => false, _ => null };
            m.Notes = MembershipService.NullIfEmpty(Input.Notes);
            m.UpdatedAt = DateTime.UtcNow;

            if (Member == null) _context.ClubMembers.Add(m);

            // Adding someone from the paper records usually comes with the year they paid for.
            if (Member == null && Input.AddYearId is int yearId && Years.Any(y => y.Id == yearId))
            {
                _context.MemberYears.Add(new MemberYear
                {
                    ClubMember = m,
                    ShootingYearId = yearId,
                    MembershipType = Input.AddYearType,
                    PaidDate = TryParseDate(Input.AddYearPaidOn, out var d) ? d : null,
                    Source = "Manual"
                });
            }

            await _context.SaveChangesAsync();
            Message = Member == null ? $"{m.FullName} added to the member list." : "Details saved.";
            return RedirectToPage(new { id = m.Id.ToString() });
        }

        public async Task<IActionResult> OnPostAddYearAsync(int yearId, string? membershipType, string? paidOn, string? source)
        {
            if (!await LoadAsync() || Member == null) return NotFound();

            if (!Years.Any(y => y.Id == yearId))
                ErrorMessage = "Choose a shooting year.";
            else if (Member.Years.Any(y => y.ShootingYearId == yearId))
                ErrorMessage = "That year is already recorded for this member.";
            else
            {
                _context.MemberYears.Add(new MemberYear
                {
                    ClubMemberId = Member.Id,
                    ShootingYearId = yearId,
                    MembershipType = MembershipService.NullIfEmpty(membershipType),
                    PaidDate = TryParseDate(paidOn, out var d) ? d : null,
                    Source = source is "EFT" or "Cash" or "Online" ? source : "Manual"
                });
                Member.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                Message = "Paid year recorded.";
            }
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostRemoveYearAsync(int memberYearId)
        {
            if (!await LoadAsync() || Member == null) return NotFound();
            var row = Member.Years.FirstOrDefault(y => y.Id == memberYearId);
            if (row != null)
            {
                _context.MemberYears.Remove(row);
                await _context.SaveChangesAsync();
                Message = $"{row.ShootingYear.Name} removed from {Member.FullName}'s paid years.";
            }
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostArchiveAsync(bool archive)
        {
            if (!await LoadAsync() || Member == null) return NotFound();
            Member.IsArchived = archive;
            Member.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            Message = archive ? $"{Member.FullName} archived. They won't get renewal reminders." : $"{Member.FullName} is back on the member list.";
            return RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            Years = await _context.ShootingYears.OrderByDescending(y => y.StartDate).ToListAsync();
            var today = ClubClock.Today;
            DefaultYearId = Years.FirstOrDefault(y => y.Contains(today))?.Id ?? Years.FirstOrDefault()?.Id;

            if (string.Equals(Id, "new", StringComparison.OrdinalIgnoreCase)) return true;
            if (!int.TryParse(Id, out var memberId)) return false;

            Member = await _context.ClubMembers
                .Include(m => m.Years).ThenInclude(y => y.ShootingYear)
                .Include(m => m.Years).ThenInclude(y => y.MembershipApplication)
                .FirstOrDefaultAsync(m => m.Id == memberId);
            if (Member == null) return false;

            Applications = await _context.MembershipApplications
                .Include(a => a.ShootingYear)
                .Where(a => a.Members.Any(p => p.ClubMemberId == memberId || p.IdNumber == Member.IdNumber))
                .ToListAsync();
            Applications = Applications.OrderByDescending(a => a.SubmittedDate).ToList();

            Reminders = await _context.MembershipReminderLogs
                .Include(r => r.ShootingYear)
                .Where(r => r.ClubMemberId == memberId)
                .OrderByDescending(r => r.SentAt)
                .ToListAsync();
            return true;
        }

        public class MemberInput
        {
            public string? FirstName { get; set; }
            public string? Surname { get; set; }
            public string? IdNumber { get; set; }
            public string? Gender { get; set; }
            public string? ContactNumber { get; set; }
            public string? EmailAddress { get; set; }
            public string? SahftaNumber { get; set; }
            public bool IsJunior { get; set; }
            public string? StreetAddress { get; set; }
            public string? ComplexOrBuilding { get; set; }
            public string? Suburb { get; set; }
            public string? City { get; set; }
            public string? Province { get; set; }
            public string? PostalCode { get; set; }
            public string? SocialMediaConsent { get; set; }
            public string? Notes { get; set; }

            // Only when adding someone new
            public int? AddYearId { get; set; }
            public string? AddYearType { get; set; }
            public string? AddYearPaidOn { get; set; }
        }
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages
{
    /// <summary>
    /// Public membership application. Everything is server-rendered: adding and removing
    /// family members are postbacks (no Alpine.js, per the project rules), and the fee summary
    /// reacts to the radio buttons with CSS :has() only. The server always recalculates the
    /// amount from the shooting year's fees; nothing the browser sends is trusted for money.
    /// </summary>
    [ValidateAntiForgeryToken]
    public class ApplyModel : PageModel
    {
        public const int MaxPeople = 8;

        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;
        private readonly YocoCheckoutService _yoco;
        private readonly IkhokhaPaymentService _ikhokha;
        private readonly ILogger<ApplyModel> _logger;

        public ApplyModel(ApplicationDbContext context, MembershipService membership, YocoCheckoutService yoco, IkhokhaPaymentService ikhokha, ILogger<ApplyModel> logger)
        {
            _context = context;
            _membership = membership;
            _yoco = yoco;
            _ikhokha = ikhokha;
            _logger = logger;
        }

        [BindProperty]
        public ApplyInput Input { get; set; } = new();

        public ShootingYear? Year { get; private set; }
        public PaymentDetails Bank { get; private set; } = new();

        public static readonly string[] Provinces =
        {
            "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
            "Mpumalanga", "North West", "Northern Cape", "Western Cape"
        };

        public async Task OnGetAsync()
        {
            await LoadAsync();
            if (Input.People.Count == 0) Input.People.Add(new PersonInput());
        }

        // Adding/removing someone re-renders instead of redirecting, so ?handler=… stays in the
        // address bar. A reload then arrives as a GET; send it back to a clean form.
        public IActionResult OnGetAddPerson() => RedirectToPage();
        public IActionResult OnGetRemovePerson() => RedirectToPage();

        public async Task<IActionResult> OnPostAddPersonAsync()
        {
            await LoadAsync();
            if (Input.People.Count < MaxPeople) Input.People.Add(new PersonInput());
            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostRemovePersonAsync(int index)
        {
            await LoadAsync();
            if (Input.People.Count > 1 && index > 0 && index < Input.People.Count)
                Input.People.RemoveAt(index);
            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAsync();
            if (Input.People.Count == 0) Input.People.Add(new PersonInput());

            if (Year == null)
            {
                ModelState.AddModelError(string.Empty, "Membership applications are not open at the moment. Please contact the club.");
                return Page();
            }

            Validate();
            if (!ModelState.IsValid) return Page();

            var price = MembershipService.Price(Year, Input.MembershipType, Input.ApplicationType);
            var people = Input.People.Select((p, i) => (Person: p, Index: i)).ToList();

            var application = new MembershipApplication
            {
                MembershipType = Input.MembershipType!,
                ApplicationType = Input.ApplicationType!,
                ShootingYearId = Year.Id,
                MembershipFee = price.MembershipFee,
                AdminFee = price.AdminFee,
                TotalAmount = price.Total,
                PaymentMethod = Input.PaymentMethod!,
                PaymentStatus = PaymentStatuses.Unpaid,
                ApprovalStatus = ApprovalStatuses.Pending,
                Status = "Pending",
                StreetAddress = Input.StreetAddress!.Trim(),
                ComplexOrBuilding = MembershipService.NullIfEmpty(Input.ComplexOrBuilding),
                Suburb = Input.Suburb!.Trim(),
                City = Input.City!.Trim(),
                Province = Input.Province!,
                PostalCode = Input.PostalCode!.Trim(),
                SocialMediaConsent = Input.SocialMediaConsent == "yes",
                GuardianName = Input.People.Any(p => p.IsJunior) ? MembershipService.NullIfEmpty(Input.GuardianName) : null,
                SubmittedDate = DateTime.UtcNow,
                Flags = await _membership.BuildFlagsAsync(Year, Input.ApplicationType!,
                    Input.People.Select(p => ($"{p.FirstName} {p.Surname}".Trim(), p.IdNumber ?? "")))
            };

            foreach (var (p, i) in people)
            {
                application.Members.Add(new Member
                {
                    FirstName = p.FirstName!.Trim(),
                    Surname = p.Surname!.Trim(),
                    IdNumber = ClubMember.NormaliseIdNumber(p.IdNumber),
                    Gender = p.Gender!,
                    ContactNumber = p.ContactNumber?.Trim() ?? "",
                    EmailAddress = p.EmailAddress?.Trim() ?? "",
                    IsPrimary = i == 0,
                    IsJunior = p.IsJunior,
                    SahftaNumber = MembershipService.NullIfEmpty(p.SahftaNumber)
                });
            }

            _context.MembershipApplications.Add(application);
            await _context.SaveChangesAsync();
            application.ShootingYear = Year;

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var confirmationUrl = $"{baseUrl}/apply/submitted/{application.AccessToken}";

            try
            {
                await _membership.SendSubmittedEmailsAsync(application, confirmationUrl);
            }
            catch (Exception ex)
            {
                // The application is saved; a mail hiccup must not lose it or block payment.
                _logger.LogError(ex, "Membership application {Id}: confirmation emails failed", application.Id);
            }

            if (application.PaymentMethod == "Card" && application.TotalAmount > 0)
            {
                var payUrl = await CreateCardPaymentAsync(application, baseUrl);
                if (payUrl != null) return Redirect(payUrl);

                _logger.LogWarning("Membership application {Id}: no card payment link was returned", application.Id);
                return RedirectToPage("/ApplicationSubmitted", new { token = application.AccessToken, payment = "unavailable" });
            }

            return RedirectToPage("/ApplicationSubmitted", new { token = application.AccessToken });
        }

        private async Task<string?> CreateCardPaymentAsync(MembershipApplication application, string baseUrl)
        {
            var id = application.Id;
            var t = application.AccessToken;
            var successUrl = $"{baseUrl}/payment-result?status=success&applicationId={id}&token={t}";
            var cancelUrl = $"{baseUrl}/payment-result?status=cancelled&applicationId={id}&token={t}";
            var failureUrl = $"{baseUrl}/payment-result?status=failed&applicationId={id}&token={t}";
            var description = $"POLK Membership {application.ShootingYear?.Name} #{id}";

            try
            {
                var gateway = (await _membership.GetSettingAsync("PaymentGateway")) ?? "Ikhokha";
                if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
                {
                    var link = await _ikhokha.CreatePaymentLinkAsync(
                        application.TotalAmount, description,
                        externalTransactionId: $"APP-{id}",
                        requesterUrl: baseUrl,
                        callbackUrl: $"{baseUrl}/api/ikhokha/callback",
                        successPageUrl: successUrl,
                        failurePageUrl: failureUrl,
                        cancelUrl: cancelUrl);
                    return string.IsNullOrEmpty(link?.PaylinkUrl) ? null : link!.PaylinkUrl;
                }

                var checkout = await _yoco.CreateCheckoutAsync(application.TotalAmount, description, successUrl, cancelUrl, failureUrl,
                    new Dictionary<string, string> { { "applicationId", id.ToString() } });
                return string.IsNullOrEmpty(checkout?.RedirectUrl) ? null : checkout!.RedirectUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Membership application {Id}: creating the card payment failed", id);
                return null;
            }
        }

        private async Task LoadAsync()
        {
            Year = await _membership.GetYearForApplicationsAsync();
            Bank = await _membership.GetPaymentDetailsAsync();
        }

        private void Validate()
        {
            // Data annotations would add generic "field is required" text next to the wording
            // written for each field, so all rules live here instead.
            ModelState.Clear();

            void Need(bool ok, string key, string message)
            {
                if (!ok) ModelState.AddModelError(key, message);
            }

            Need(Input.ApplicationType is ApplicationTypes.New or ApplicationTypes.Renewal,
                "Input.ApplicationType", "Choose whether this is a new membership or a renewal.");
            Need(MembershipTypes.All.Contains(Input.MembershipType),
                "Input.MembershipType", "Choose a membership type.");

            if (Input.MembershipType == MembershipTypes.Family && Input.People.Count < 2)
                ModelState.AddModelError(string.Empty, "A family membership covers two or more people. Add the other family members, or choose Individual.");
            if (Input.MembershipType is MembershipTypes.Individual or MembershipTypes.Pensioner && Input.People.Count > 1)
                ModelState.AddModelError(string.Empty, $"A {Input.MembershipType!.ToLowerInvariant()} membership is for one person. Remove the other people, or choose Family.");

            var seenIds = new HashSet<string>();
            for (var i = 0; i < Input.People.Count; i++)
            {
                var p = Input.People[i];
                var k = $"Input.People[{i}]";
                Need(!string.IsNullOrWhiteSpace(p.FirstName), $"{k}.FirstName", "Enter a first name.");
                Need(!string.IsNullOrWhiteSpace(p.Surname), $"{k}.Surname", "Enter a surname.");
                Need(!string.IsNullOrWhiteSpace(p.Gender), $"{k}.Gender", "Choose a gender.");

                var id = ClubMember.NormaliseIdNumber(p.IdNumber);
                if (id.Length < 6)
                    ModelState.AddModelError($"{k}.IdNumber", "Enter the ID number (or passport number).");
                else if (!seenIds.Add(id))
                    ModelState.AddModelError($"{k}.IdNumber", "This ID number is already on this application.");

                // The first person is the contact for the application. Family members, often
                // children, may not have their own phone or email.
                if (i == 0)
                {
                    Need(!string.IsNullOrWhiteSpace(p.ContactNumber), $"{k}.ContactNumber", "Enter a contact number.");
                    Need(!string.IsNullOrWhiteSpace(p.EmailAddress), $"{k}.EmailAddress", "Enter an email address. Your confirmation is sent there.");
                }
                if (!string.IsNullOrWhiteSpace(p.EmailAddress) && !new EmailAddressAttribute().IsValid(p.EmailAddress.Trim()))
                    ModelState.AddModelError($"{k}.EmailAddress", "Enter a valid email address, like name@example.com.");
            }

            Need(!string.IsNullOrWhiteSpace(Input.StreetAddress), "Input.StreetAddress", "Enter the street address.");
            Need(!string.IsNullOrWhiteSpace(Input.Suburb), "Input.Suburb", "Enter the suburb.");
            Need(!string.IsNullOrWhiteSpace(Input.City), "Input.City", "Enter the city or town.");
            Need(Provinces.Contains(Input.Province), "Input.Province", "Choose a province.");
            Need(!string.IsNullOrWhiteSpace(Input.PostalCode), "Input.PostalCode", "Enter the postal code.");

            Need(Input.SocialMediaConsent is "yes" or "no", "Input.SocialMediaConsent", "Choose whether photos may be published.");
            Need(Input.DeclarationAgreed, "Input.DeclarationAgreed", "Tick the box to accept the declaration.");

            if (Input.People.Any(p => p.IsJunior))
            {
                Need(!string.IsNullOrWhiteSpace(Input.GuardianName), "Input.GuardianName", "Enter the parent or guardian's full name.");
                Need(Input.GuardianConsent, "Input.GuardianConsent", "The parent or guardian must accept the declaration for anyone under 18.");
            }

            Need(Input.PaymentMethod is "Card" or "EFT", "Input.PaymentMethod", "Choose how you'll pay.");
        }

        public int ErrorCount(string prefix) =>
            ModelState.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(kv => kv.Value.Errors.Count);

        public class ApplyInput
        {
            public string? ApplicationType { get; set; }
            public string? MembershipType { get; set; }
            public List<PersonInput> People { get; set; } = new();

            public string? StreetAddress { get; set; }
            public string? ComplexOrBuilding { get; set; }
            public string? Suburb { get; set; }
            public string? City { get; set; }
            public string? Province { get; set; }
            public string? PostalCode { get; set; }

            public string? SocialMediaConsent { get; set; }
            public bool DeclarationAgreed { get; set; }
            public string? GuardianName { get; set; }
            public bool GuardianConsent { get; set; }

            public string? PaymentMethod { get; set; }
        }

        public class PersonInput
        {
            public string? FirstName { get; set; }
            public string? Surname { get; set; }
            public string? IdNumber { get; set; }
            public string? Gender { get; set; }
            public string? ContactNumber { get; set; }
            public string? EmailAddress { get; set; }
            public bool IsJunior { get; set; }
            public string? SahftaNumber { get; set; }
        }
    }
}

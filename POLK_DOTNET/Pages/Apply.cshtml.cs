using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Pages
{
    public class ApplyModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ApplyModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public MembershipApplication MembershipApplication { get; set; }

        [BindProperty]
        public List<MemberInput> MemberInputs { get; set; } = new List<MemberInput>();

        [BindProperty]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the declaration.")]
        public bool DeclarationAgreed { get; set; }

        // Properties for displaying membership costs
        public decimal IndividualMembershipCost { get; set; } = 450.00m;
        public decimal FamilyMembershipCost { get; set; } = 750.00m;
        public decimal PensionerMembershipCost { get; set; } = 300.00m;
        public decimal SahftaAdultCost { get; set; } = 180.00m;
        public decimal SahftaChildCost { get; set; } = 120.00m;

        public void OnGet()
        {
            // Initialize with one member for individual or primary family member
            MemberInputs.Add(new MemberInput { IsPrimary = true });
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            // Manual validation for MemberInputs as it's not directly bound to MembershipApplication
            // The validation attributes on MemberInput will be checked by ModelState.IsValid
            if (!ModelState.IsValid || !DeclarationAgreed)
            {
                // If validation fails, return the page with current model to display errors
                // MemberInputs are already bound, so they will be re-rendered.
                return Page();
            }

            // Calculate total amount based on selected membership type and SAHFTA affiliations
            MembershipApplication.TotalAmount = await CalculateTotalAmount();
            MembershipApplication.SubmittedDate = DateTime.UtcNow;

            _context.MembershipApplications.Add(MembershipApplication);
            await _context.SaveChangesAsync();

            foreach (var memberInput in MemberInputs)
            {
                decimal sahftaFee = 0;
                if (memberInput.HasSahftaAffiliation)
                {
                    sahftaFee = memberInput.IsJunior ? SahftaChildCost : SahftaAdultCost;
                }

                var member = new Member
                {
                    MembershipApplicationId = MembershipApplication.Id,
                    FirstName = memberInput.FirstName,
                    Surname = memberInput.Surname,
                    IdNumber = memberInput.IdNumber,
                    Gender = memberInput.Gender,
                    ContactNumber = memberInput.ContactNumber,
                    EmailAddress = memberInput.EmailAddress,
                    IsPrimary = memberInput.IsPrimary,
                    HasSahftaAffiliation = memberInput.HasSahftaAffiliation,
                    IsJunior = memberInput.IsJunior,
                    SahftaFee = sahftaFee
                };
                _context.Members.Add(member);
            }
            await _context.SaveChangesAsync();

            // Redirect to a confirmation page or home page
            return RedirectToPage("/Index");
        }

        private async Task<decimal> CalculateTotalAmount()
        {
            decimal total = 0;

            if (MembershipApplication.MembershipType == "Individual")
            {
                total += IndividualMembershipCost;
            }
            else if (MembershipApplication.MembershipType == "Family")
            {
                total += FamilyMembershipCost;
            }
            else if (MembershipApplication.MembershipType == "Pensioner")
            {
                total += PensionerMembershipCost;
            }

            foreach (var memberInput in MemberInputs)
            {
                if (memberInput.HasSahftaAffiliation)
                {
                    total += memberInput.IsJunior ? SahftaChildCost : SahftaAdultCost;
                }
            }

            // Admin Fee Logic
            var primaryApplicantIdNumber = MemberInputs.FirstOrDefault(m => m.IsPrimary)?.IdNumber;
            if (!string.IsNullOrEmpty(primaryApplicantIdNumber))
            {
                var memberExists = await _context.Members
                    .AnyAsync(m => m.IdNumber == primaryApplicantIdNumber && m.MembershipApplication.Status == "Approved");

                if (!memberExists)
                {
                    var adminFeeOption = await _context.MembershipOptions
                        .FirstOrDefaultAsync(mo => mo.Title.Contains("One Time Admin fee"));

                    if (adminFeeOption != null)
                    {
                        string priceString = adminFeeOption.Price.Replace("R", "").Replace("/month", "").Trim();
                        if (decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal adminFee))
                        {
                            total += adminFee;
                        }
                    }
                }
            }

            return total;
        }

        // Helper class for binding member data from the form
        public class MemberInput
        {
            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string FirstName { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string Surname { get; set; }

            [Required]
            [StringLength(20, MinimumLength = 5)]
            public string IdNumber { get; set; }

            [Required]
            public string Gender { get; set; }

            [Required]
            [Phone]
            public string ContactNumber { get; set; }

            [Required]
            [EmailAddress]
            public string EmailAddress { get; set; }

            public bool IsPrimary { get; set; }

            public bool HasSahftaAffiliation { get; set; }

            public bool IsJunior { get; set; }
        }
    }
}

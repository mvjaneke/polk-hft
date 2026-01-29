using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc; // Required for ModelBinderAttribute
using POLK_DOTNET.CustomModelBinders; // Required for DecimalInvariantModelBinder

namespace POLK_DOTNET.Data
{
    public class MembershipApplication
    {
        public int Id { get; set; }

        [Required]
        public string MembershipType { get; set; } // Individual, Family, Pensioner

        [Required]
        [ModelBinder(typeof(DecimalInvariantModelBinder))]
        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        public ICollection<Member> Members { get; set; } = new List<Member>();
    }

    public class Member
    {
        public int Id { get; set; }

        [Required]
        public int MembershipApplicationId { get; set; }
        public MembershipApplication MembershipApplication { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string Surname { get; set; }

        [Required]
        [StringLength(20)]
        public string IdNumber { get; set; }

        [StringLength(20)]
        public string Gender { get; set; } // Male, Female, Other

        [Required]
        [StringLength(20)]
        public string ContactNumber { get; set; }

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string EmailAddress { get; set; }

        public bool IsPrimary { get; set; } = false; // True for the main contact in a family application

        public bool HasSahftaAffiliation { get; set; } = false;
        public decimal SahftaFee { get; set; } = 0;
        public bool IsJunior { get; set; } = false; // For SAHFTA fee calculation
    }
}

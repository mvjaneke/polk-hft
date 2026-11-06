using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class ApplicationPdfService
    {
        public byte[] GenerateApplicationPdf(MembershipApplication application)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Pretoria Oos Lug Geweer Klub")
                            .FontSize(18).Bold().FontColor(Colors.Red.Darken2);
                        col.Item().Text("Membership Application").FontSize(14).SemiBold();
                        col.Item().PaddingBottom(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(8);

                        // Application details
                        // Applications from the old form didn't record a fee breakdown.
                        var isLegacy = application.MembershipFee == 0m && application.AdminFee == 0m;
                        col.Item().Text($"Application #{application.Id}").SemiBold();
                        col.Item().Text($"Date: {application.SubmittedDate:dd MMMM yyyy}");
                        if (application.ShootingYear != null)
                            col.Item().Text($"Membership year: {application.ShootingYear.Name} ({application.ShootingYear.StartDate:d MMM yyyy} to {application.ShootingYear.EndDate:d MMM yyyy})");
                        col.Item().Text($"Application: {(isLegacy ? "" : application.ApplicationType + " ")}{application.MembershipType} membership");
                        var paidText = application.IsPaid || application.Status == "Paid"
                            ? "Paid" + (application.PaidDate.HasValue ? $" on {application.PaidDate:dd MMMM yyyy}" : "")
                            : "Awaiting payment";
                        if (!isLegacy)
                            paidText += application.PaymentMethod == "EFT" ? " (EFT)" : " (card)";
                        col.Item().Text($"Payment: {paidText}");

                        col.Item().PaddingTop(10).Text("Residential Address").FontSize(12).SemiBold();
                        col.Item().Text(application.StreetAddress);
                        if (!string.IsNullOrEmpty(application.ComplexOrBuilding))
                            col.Item().Text(application.ComplexOrBuilding);
                        col.Item().Text($"{application.Suburb}, {application.City}");
                        col.Item().Text($"{application.Province}, {application.PostalCode}");

                        col.Item().PaddingTop(10).Text("Members").FontSize(12).SemiBold();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Name
                                columns.RelativeColumn(2); // ID Number
                                columns.RelativeColumn(1); // Gender
                                columns.RelativeColumn(2); // Contact
                                columns.RelativeColumn(2); // Email
                                columns.RelativeColumn(1); // SAHFTA
                            });

                            table.Header(header =>
                            {
                                var headerStyle = TextStyle.Default.SemiBold().FontSize(9);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Name").Style(headerStyle);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("ID Number").Style(headerStyle);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Gender").Style(headerStyle);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Contact").Style(headerStyle);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Email").Style(headerStyle);
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(isLegacy ? "SAHFTA" : "SAHFTA no.").Style(headerStyle);
                            });

                            foreach (var member in application.Members)
                            {
                                var cellStyle = TextStyle.Default.FontSize(8);
                                var nameText = $"{member.FirstName} {member.Surname}"
                                               + (member.IsPrimary ? " (Primary)" : "")
                                               + (!isLegacy && member.IsJunior ? " (under 18)" : "");
                                var sahftaText = isLegacy
                                    ? (member.HasSahftaAffiliation ? $"Yes (R{member.SahftaFee:F2})" : "No")
                                    : (member.SahftaNumber ?? "");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(nameText).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(member.IdNumber).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(member.Gender ?? "").Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(member.ContactNumber ?? "").Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(member.EmailAddress ?? "").Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(sahftaText).Style(cellStyle);
                            }
                        });

                        col.Item().PaddingTop(15).Text("Payment Summary").FontSize(12).SemiBold();
                        if (isLegacy)
                        {
                            // Applications from before shooting years did not record their fee
                            // split; show what was charged without inventing a breakdown.
                            decimal sahftaTotal = application.Members.Where(m => m.HasSahftaAffiliation).Sum(m => m.SahftaFee);
                            if (sahftaTotal > 0)
                                col.Item().Text($"Includes SAHFTA affiliations: R{sahftaTotal:F2}");
                        }
                        else
                        {
                            col.Item().Text($"{application.MembershipType} membership: R{application.MembershipFee:F2}");
                            if (application.AdminFee > 0)
                                col.Item().Text($"Once-off registration and admin fee (new members): R{application.AdminFee:F2}");
                        }
                        col.Item().PaddingTop(5).Text($"Total: R{application.TotalAmount:F2}").Bold().FontSize(12);

                        if (!isLegacy)
                        {
                            col.Item().PaddingTop(15).Text("Declaration").FontSize(12).SemiBold();
                            col.Item().Text("The applicant accepted the club's declaration and indemnity when submitting this application online.").FontSize(9);
                            if (application.SocialMediaConsent.HasValue)
                                col.Item().Text(application.SocialMediaConsent.Value
                                    ? "Photos of the members on this application MAY be published on social media and used in marketing material."
                                    : "Photos of the members on this application may NOT be published on social media or used in marketing material.").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(application.GuardianName))
                                col.Item().Text($"Accepted on behalf of members under 18 by parent/guardian: {application.GuardianName}").FontSize(9);
                            col.Item().PaddingTop(6).Text("SAHFTA affiliation is optional and is paid directly to SAHFTA, with proof of payment sent to both POLK and SAHFTA.").FontSize(9).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Pretoria Oos Lug Geweer Klub - Membership Application").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}

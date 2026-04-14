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
                        col.Item().Text($"Application #{application.Id}").SemiBold();
                        col.Item().Text($"Date: {application.SubmittedDate:dd MMMM yyyy}");
                        col.Item().Text($"Membership Type: {application.MembershipType}");
                        col.Item().Text($"Payment Status: {application.Status}");
                        col.Item().Text($"Total Amount: R{application.TotalAmount:F2}").SemiBold();

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
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("SAHFTA").Style(headerStyle);
                            });

                            foreach (var member in application.Members)
                            {
                                var cellStyle = TextStyle.Default.FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text($"{member.FirstName} {member.Surname}{(member.IsPrimary ? " (Primary)" : "")}").Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(member.IdNumber).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(member.Gender).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(member.ContactNumber).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(member.EmailAddress).Style(cellStyle);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                    .Text(member.HasSahftaAffiliation ? $"Yes (R{member.SahftaFee:F2})" : "No").Style(cellStyle);
                            }
                        });

                        col.Item().PaddingTop(15).Text("Payment Summary").FontSize(12).SemiBold();
                        col.Item().Text($"Membership ({application.MembershipType}): R{GetMembershipCost(application.MembershipType):F2}");

                        decimal sahftaTotal = application.Members.Where(m => m.HasSahftaAffiliation).Sum(m => m.SahftaFee);
                        if (sahftaTotal > 0)
                            col.Item().Text($"SAHFTA Affiliations: R{sahftaTotal:F2}");

                        decimal adminFee = application.TotalAmount - GetMembershipCost(application.MembershipType) - sahftaTotal;
                        if (adminFee > 0)
                            col.Item().Text($"Admin Fee: R{adminFee:F2}");

                        col.Item().PaddingTop(5).Text($"Total: R{application.TotalAmount:F2}").Bold().FontSize(12);
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Pretoria Oos Lug Geweer Klub - Membership Application").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static decimal GetMembershipCost(string membershipType)
        {
            return membershipType switch
            {
                "Individual" => 450.00m,
                "Family" => 750.00m,
                "Pensioner" => 300.00m,
                _ => 0m
            };
        }
    }
}

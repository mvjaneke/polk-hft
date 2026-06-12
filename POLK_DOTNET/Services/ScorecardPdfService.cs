using POLK_DOTNET.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POLK_DOTNET.Services
{
    public class ScorecardPdfService
    {
        public class ParticipantInfo
        {
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string Club { get; set; } = "";
            public string MembershipNumber { get; set; } = "";
            public string? Division { get; set; }
            public string? GunType { get; set; }
            public bool IsPaid { get; set; }
        }

        static ScorecardPdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private readonly IWebHostEnvironment _env;

        public ScorecardPdfService(IWebHostEnvironment env) { _env = env; }

        public byte[] GenerateBatch(Event ev, IList<CourseTarget> course, IList<ParticipantInfo> participants)
        {
            var logoPath = Path.Combine(_env.ContentRootPath, "wwwroot", "img", "scorecard-logo.jpg");
            byte[]? logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

            var doc = Document.Create(container =>
            {
                foreach (var p in participants)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A5);
                        page.Margin(7, Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontSize(9));
                        page.Content().Element(c => RenderCard(c, ev, course, p, logoBytes));
                        page.Footer().Row(r =>
                        {
                            r.RelativeItem().BorderTop(1).PaddingTop(2).Text("Shooter").SemiBold();
                            r.ConstantItem(8, Unit.Millimetre);
                            r.RelativeItem().BorderTop(1).PaddingTop(2).Text("Scorekeeper").SemiBold();
                        });
                    });
                }
            });

            return doc.GeneratePdf();
        }

        private static void RenderCard(IContainer card, Event ev, IList<CourseTarget> course, ParticipantInfo? p, byte[]? logo)
        {
            if (p == null)
            {
                card.AlignCenter().AlignMiddle().Text("(Blank)").FontColor(Colors.Grey.Lighten1);
                return;
            }

            var targetCount = course.Count > 0 ? course.Count : (ev.CourseTargetCount ?? 40);

            card.Column(col =>
            {
                col.Spacing(3);

                // Top: Logo (left) + Safety First (right). PAID stamp overlays the top-right when payment is received.
                col.Item().Layers(layers =>
                {
                    layers.PrimaryLayer().Row(r =>
                    {
                        r.RelativeItem(3).AlignLeft().AlignMiddle().Height(32, Unit.Millimetre).Element(e =>
                        {
                            if (logo != null) e.Image(logo).FitHeight();
                            else e.Text("CLUB LOGO").FontColor(Colors.Grey.Medium);
                        });
                        r.RelativeItem(4).AlignMiddle().Column(c =>
                        {
                            c.Item().Text("> Safety First!!").SemiBold().FontSize(10);
                            c.Item().Text("> Never Cross the Firing Line").FontSize(9);
                            c.Item().Text("> 1 Whistle:  Range Closed").FontSize(9);
                            c.Item().Text("> 2 Whistles: Range Open").FontSize(9);
                        });
                    });

                    if (p.IsPaid)
                    {
                        layers.Layer().AlignTop().AlignRight().Element(e =>
                            e.Border(2).BorderColor(Colors.Green.Darken2)
                             .Background(Colors.Green.Lighten5)
                             .PaddingHorizontal(8).PaddingVertical(2)
                             .Text("PAID ✓").Bold().FontSize(13).FontColor(Colors.Green.Darken3));
                    }
                });

                // Good Conduct (left) + Starting Lane (right)
                col.Item().Row(r =>
                {
                    r.RelativeItem(3).AlignMiddle().Text(txt =>
                    {
                        txt.DefaultTextStyle(s => s.FontSize(7).Italic());
                        txt.Span("Good Conduct:  ").SemiBold();
                        txt.Span("Always respect your shooting partner and other shooters on the range. Keep noise to a minimum and ALWAYS practice good Sportsmanship. Cheating is for Losers!!");
                    });
                    r.ConstantItem(3, Unit.Millimetre);
                    r.RelativeItem(1).Border(1).Column(c =>
                    {
                        c.Item().Background(Colors.Grey.Lighten3).AlignCenter().Padding(2).Text("Starting Lane").SemiBold().FontSize(9);
                        c.Item().Height(9, Unit.Millimetre);
                    });
                });

                // Info table: NAME / CLUB / CLASS / HFT
                col.Item().PaddingTop(2).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(22, Unit.Millimetre);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });

                    // NAME — field spans 3 cols
                    t.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(3).Text("NAME:").SemiBold();
                    t.Cell().ColumnSpan(3).Border(1).Padding(3).MinHeight(5, Unit.Millimetre).Text(string.IsNullOrWhiteSpace($"{p.Name} {p.Surname}".Trim()) ? "" : $"{p.Name} {p.Surname}".Trim());

                    // CLUB — field spans 3 cols
                    t.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(3).Text("CLUB:").SemiBold();
                    t.Cell().ColumnSpan(3).Border(1).Padding(3).MinHeight(5, Unit.Millimetre).Text(p.Club ?? "");

                    // CLASS — spans 2 rows
                    t.Cell().RowSpan(2).Border(1).Background(Colors.Grey.Lighten3).Padding(3).AlignMiddle().Text("CLASS:").SemiBold();
                    RenderTickCell(t, "0/13", IsDivisionMatch(p.Division, "0/13"));
                    RenderTickCell(t, "0/18", IsDivisionMatch(p.Division, "0/18"));
                    RenderTickCell(t, "Ladies", IsDivisionMatch(p.Division, "Ladies"));
                    RenderTickCell(t, "Open", IsDivisionMatch(p.Division, "Open"));
                    RenderTickCell(t, "Veteran", IsDivisionMatch(p.Division, "Veteran"));
                    t.Cell().Border(1); // trailing empty

                    // HFT row
                    t.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(3).Text("HFT:").SemiBold();
                    RenderTickCell(t, "PCP", IsGunMatch(p.GunType, "PCP"));
                    t.Cell().ColumnSpan(2).Border(1).Padding(3).AlignCenter().Row(r =>
                    {
                        r.AutoItem().Text(IsGunMatch(p.GunType, "SPRINGER / RECOIL") ? "☒ " : "☐ ").FontSize(11);
                        r.AutoItem().AlignMiddle().Text("SPRINGER / RECOIL").SemiBold();
                    });
                });

                // Drop = X / Miss = O + posture legend
                col.Item().PaddingTop(2).AlignCenter().Text("Drop = X  /  Miss = O").SemiBold().FontSize(10);
                col.Item().PaddingTop(1).Row(r =>
                {
                    r.RelativeItem();
                    foreach (var (label, colour) in new[]
                    {
                        ("Prone", Colors.White),
                        ("Unsup Stand", Colors.Red.Lighten4),
                        ("Unsup Kneel", Colors.Blue.Lighten4),
                        ("Sup Stand", Colors.Yellow.Lighten4),
                        ("Sup Kneel", Colors.Green.Lighten4),
                    })
                    {
                        r.AutoItem().PaddingRight(3).Row(inner =>
                        {
                            inner.AutoItem().Width(4, Unit.Millimetre).Height(4, Unit.Millimetre).Border(0.5f).Background(colour);
                            inner.AutoItem().PaddingLeft(2).AlignMiddle().Text(label).FontSize(7);
                        });
                    }
                    r.RelativeItem();
                });

                // Grid
                col.Item().Element(c => RenderGrid(c, targetCount, course));
            });
        }

        private static void RenderTickCell(TableDescriptor t, string label, bool isTicked)
        {
            t.Cell().Border(1).Padding(3).AlignCenter().Row(r =>
            {
                r.AutoItem().Text(isTicked ? "☒ " : "☐ ").FontSize(11);
                r.AutoItem().AlignMiddle().Text(label).SemiBold();
            });
        }

        private static void RenderGrid(IContainer container, int targetCount, IList<CourseTarget> course)
        {
            int targetsPerGridRow = 10;

            // Build ordered (targetNumber, lane, posture) list. Fall back to default layout when course not configured.
            var ordered = course
                .Where(c => c.TargetNumber >= 1 && c.TargetNumber <= targetCount)
                .OrderBy(c => c.TargetNumber)
                .Select(c => (Target: c.TargetNumber, Lane: c.Lane > 0 ? c.Lane : (c.TargetNumber + 1) / 2, Posture: c.Posture))
                .ToList();

            if (ordered.Count < targetCount)
            {
                var present = ordered.Select(x => x.Target).ToHashSet();
                for (int t = 1; t <= targetCount; t++)
                {
                    if (!present.Contains(t))
                        ordered.Add((t, (t + 1) / 2, (string?)null));
                }
                ordered = ordered.OrderBy(x => x.Target).ToList();
            }

            var gridRowsData = new List<List<(int Target, int Lane, string? Posture)>>();
            for (int i = 0; i < ordered.Count; i += targetsPerGridRow)
                gridRowsData.Add(ordered.Skip(i).Take(targetsPerGridRow).ToList());

            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    for (int c = 0; c < targetsPerGridRow; c++) cols.RelativeColumn(1);
                    cols.RelativeColumn(1.6f); // TOTAL column
                });

                for (int r = 0; r < gridRowsData.Count; r++)
                {
                    var row = gridRowsData[r];

                    // Lane header row: contiguous runs of same lane become one header with ColumnSpan
                    int colsEmitted = 0;
                    int i2 = 0;
                    while (i2 < row.Count)
                    {
                        int lane = row[i2].Lane;
                        int span = 1;
                        while (i2 + span < row.Count && row[i2 + span].Lane == lane) span++;

                        var laneBg = lane % 2 == 1 ? Colors.Grey.Lighten3 : Colors.White;
                        table.Cell().ColumnSpan((uint)span).Border(1).BorderColor(Colors.Grey.Darken1)
                            .Background(laneBg).AlignCenter().PaddingVertical(2)
                            .Text($"Lane {lane}").SemiBold().FontSize(9);

                        i2 += span;
                        colsEmitted += span;
                    }
                    // Pad any unused columns so the header row always has 10 target-columns + 1 total
                    while (colsEmitted < targetsPerGridRow)
                    {
                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1);
                        colsEmitted++;
                    }

                    // TOTAL header only on first grid row
                    if (r == 0)
                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1)
                            .Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().PaddingVertical(2)
                            .Text("TOTAL").Bold().FontSize(9);
                    else
                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1);

                    // Target cells — always 10 slots; pad empty at end of last row if fewer targets
                    for (int t = 0; t < targetsPerGridRow; t++)
                    {
                        if (t < row.Count)
                        {
                            int num = row[t].Target;
                            var bg = PostureColour(row[t].Posture);
                            table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(bg).Height(9, Unit.Millimetre)
                                .Column(c => c.Item().AlignCenter().Text(num.ToString()).FontSize(8));
                        }
                        else
                        {
                            table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Height(9, Unit.Millimetre);
                        }
                    }

                    // Row-total cell
                    table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Height(9, Unit.Millimetre);
                }

                // Final grand-total row
                table.Cell().ColumnSpan((uint)targetsPerGridRow).Border(1).BorderColor(Colors.Grey.Darken1)
                    .Background(Colors.Grey.Lighten3).AlignRight().PaddingRight(4).PaddingVertical(2)
                    .Text("TOTAL").Bold().FontSize(10);
                table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Height(9, Unit.Millimetre);
            });
        }

        private static string PostureColour(string? posture) => posture switch
        {
            "UnStand" => Colors.Red.Lighten4,
            "UnKneel" => Colors.Blue.Lighten4,
            "SupStand" => Colors.Yellow.Lighten4,
            "SupKneel" => Colors.Green.Lighten4,
            _ => Colors.White  // Prone or unset
        };

        private static bool IsDivisionMatch(string? division, string optionLabel)
        {
            if (string.IsNullOrWhiteSpace(division)) return false;
            var d = division.Trim().ToLowerInvariant();
            return optionLabel switch
            {
                "0/13" => d.Contains("13"),
                "0/18" => d.Contains("18"),
                "Ladies" => d.Contains("ladies"),
                "Open" => d.Contains("open"),
                "Veteran" => d.Contains("veteran"),
                _ => false
            };
        }

        private static bool IsGunMatch(string? gun, string optionLabel)
        {
            if (string.IsNullOrWhiteSpace(gun)) return false;
            var g = gun.Trim().ToLowerInvariant();
            return optionLabel switch
            {
                "PCP" => g.StartsWith("pcp"),
                "SPRINGER / RECOIL" => g.Contains("recoil") || g.Contains("springer"),
                _ => false
            };
        }
    }
}

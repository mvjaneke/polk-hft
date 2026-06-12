using ClosedXML.Excel;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    // Excel exports for after-event admin use:
    //   * Score sheet — one row per competitor, single Score column for the admin
    //     to fill in by hand. Captures the day's result before uploading to SAHFTA.
    //   * Course sheet — Troyer course details + summary, for printing/reference.
    public class ExcelExportService
    {
        private const int WalkInRows = 6;

        // Builds a score-capture spreadsheet for the given competitors. The Excel
        // is a roster only — no per-target scoring grid (HFT score is entered as
        // a single total per competitor).
        public byte[] GenerateScoresheet(Event ev, IList<ScorecardPdfService.ParticipantInfo> participants, int shoot)
        {
            using var wb = new XLWorkbook();
            var sheetName = ev.IsDoubleHeader ? $"Shoot {shoot}" : "Scores";
            var ws = wb.Worksheets.Add(sheetName);

            // Title row
            ws.Cell(1, 1).Value = ev.Title;
            ws.Range(1, 1, 1, 9).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = $"{ev.StartDate:dd MMMM yyyy} • {ev.Location}" +
                                  (ev.IsDoubleHeader ? $" • Shoot {shoot}" : "");
            ws.Range(2, 1, 2, 9).Merge();
            ws.Cell(2, 1).Style.Font.FontSize = 10;
            ws.Cell(2, 1).Style.Font.Italic = true;

            // Header row at row 4
            const int headerRow = 4;
            var headers = new[] { "Lane", "Surname", "First Name", "Club", "SAHFTA #", "Division", "Gun", "Score", "Notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            // Data rows
            int row = headerRow + 1;
            int lane = 1;
            foreach (var p in participants)
            {
                ws.Cell(row, 1).Value = lane;
                ws.Cell(row, 2).Value = p.Surname;
                ws.Cell(row, 3).Value = p.Name;
                ws.Cell(row, 4).Value = p.Club ?? "";
                ws.Cell(row, 5).Value = p.MembershipNumber ?? "";
                ws.Cell(row, 6).Value = p.Division ?? "";
                ws.Cell(row, 7).Value = p.GunType ?? "";
                // Score (col 8) and Notes (col 9) are intentionally left blank for the admin
                row++;
                lane++;
            }

            // Walk-in blank rows
            for (int w = 0; w < WalkInRows; w++)
            {
                ws.Cell(row, 1).Value = lane;
                // Light grey shading on lane number so walk-in rows are visually distinct
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                ws.Range(row, 2, row, 9).Style.Border.BottomBorder = XLBorderStyleValues.Dotted;
                row++;
                lane++;
            }

            // Borders on the whole data block
            var dataLastRow = row - 1;
            var dataBlock = ws.Range(headerRow, 1, dataLastRow, 9);
            dataBlock.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataBlock.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // Column widths — surname/firstname/club need room, target columns aren't here
            ws.Column(1).Width = 6;   // Lane
            ws.Column(2).Width = 22;  // Surname
            ws.Column(3).Width = 18;  // First Name
            ws.Column(4).Width = 28;  // Club
            ws.Column(5).Width = 12;  // SAHFTA #
            ws.Column(6).Width = 14;  // Division
            ws.Column(7).Width = 16;  // Gun
            ws.Column(8).Width = 10;  // Score
            ws.Column(9).Width = 28;  // Notes

            // Freeze the header so it stays visible while scrolling
            ws.SheetView.FreezeRows(headerRow);

            // Print setup — landscape, fit to one page wide
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.4;
            ws.PageSetup.Margins.Right = 0.4;
            ws.PageSetup.PrintAreas.Add(1, 1, dataLastRow, 9);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // One unpaid registrant for the day-of payment collection sheet.
        public class UnpaidRow
        {
            public string Surname { get; set; } = "";
            public string Name { get; set; } = "";
            public string Cell { get; set; } = "";
            public string Gun { get; set; } = "";
            public string Division { get; set; } = "";
            public string Shoot { get; set; } = "";
            public decimal AmountOwing { get; set; }
            public string Method { get; set; } = "";
        }

        // Builds a "to collect" payment sheet listing everyone who still owes money,
        // with blank columns the admin marks off by hand on the day (Paid tick,
        // amount received, method, signature). Print-ready, sorted by surname.
        public byte[] GenerateUnpaidSheet(Event ev, IList<UnpaidRow> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("To Collect");

            bool dh = ev.IsDoubleHeader;
            // Column layout shifts by one when the Shoot column is present.
            var headers = dh
                ? new[] { "#", "Surname", "First Name", "Cell", "Gun", "Division", "Shoot", "Owing (R)", "Paid", "Received (R)", "Method", "Signature" }
                : new[] { "#", "Surname", "First Name", "Cell", "Gun", "Division", "Owing (R)", "Paid", "Received (R)", "Method", "Signature" };
            int cols = headers.Length;

            // Title
            ws.Cell(1, 1).Value = $"{ev.Title} — Outstanding Payments";
            ws.Range(1, 1, 1, cols).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = $"{ev.StartDate:dd MMMM yyyy} • {ev.Location} • {rows.Count} to collect";
            ws.Range(2, 1, 2, cols).Merge();
            ws.Cell(2, 1).Style.Font.FontSize = 10;
            ws.Cell(2, 1).Style.Font.Italic = true;

            // Header row at row 4
            const int headerRow = 4;
            for (int i = 0; i < cols; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            // Data rows
            int row = headerRow + 1;
            int num = 1;
            foreach (var r in rows)
            {
                int c = 1;
                ws.Cell(row, c++).Value = num;
                ws.Cell(row, c++).Value = r.Surname;
                ws.Cell(row, c++).Value = r.Name;
                ws.Cell(row, c++).Value = r.Cell;
                ws.Cell(row, c++).Value = r.Gun;
                ws.Cell(row, c++).Value = r.Division;
                if (dh) ws.Cell(row, c++).Value = r.Shoot;
                ws.Cell(row, c++).Value = r.AmountOwing;
                ws.Cell(row, c - 1).Style.NumberFormat.Format = "0.00";
                // Remaining columns (Paid, Received, Method, Signature) left blank for the admin
                row++;
                num++;
            }

            // A few blank rows for walk-ins / late entries
            for (int w = 0; w < WalkInRows; w++)
            {
                ws.Cell(row, 1).Value = num;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                row++;
                num++;
            }

            var dataLastRow = row - 1;
            var dataBlock = ws.Range(headerRow, 1, dataLastRow, cols);
            dataBlock.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataBlock.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            // Taller rows so there's room to write/sign by hand
            for (int rr = headerRow + 1; rr <= dataLastRow; rr++)
                ws.Row(rr).Height = 22;

            // Column widths — final write-in columns are roomy
            int col = 1;
            ws.Column(col++).Width = 5;    // #
            ws.Column(col++).Width = 22;   // Surname
            ws.Column(col++).Width = 18;   // First Name
            ws.Column(col++).Width = 16;   // Cell
            ws.Column(col++).Width = 14;   // Gun
            ws.Column(col++).Width = 14;   // Division
            if (dh) ws.Column(col++).Width = 10; // Shoot
            ws.Column(col++).Width = 11;   // Owing
            ws.Column(col++).Width = 7;    // Paid (tick)
            ws.Column(col++).Width = 13;   // Received
            ws.Column(col++).Width = 14;   // Method
            ws.Column(col++).Width = 22;   // Signature

            ws.SheetView.FreezeRows(headerRow);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;
            ws.PageSetup.Margins.Left = 0.4;
            ws.PageSetup.Margins.Right = 0.4;
            ws.PageSetup.PrintAreas.Add(1, 1, dataLastRow, cols);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // Builds a Troyer-course reference workbook. Summary block at the top,
        // per-target table below with computed difficulty values + rating.
        public byte[] GenerateCourseSheet(Event ev, IList<CourseTarget> targets, int shoot)
        {
            using var wb = new XLWorkbook();
            var sheetName = (ev.IsDoubleHeader && !ev.UseSameCourseForBothShoots) ? $"Course — Shoot {shoot}" : "Course";
            var ws = wb.Worksheets.Add(sheetName);

            // Title
            ws.Cell(1, 1).Value = $"{ev.Title} — Troyer Course";
            ws.Range(1, 1, 1, 10).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = $"{ev.StartDate:dd MMMM yyyy} • {ev.Location}" +
                                  (ev.IsDoubleHeader && !ev.UseSameCourseForBothShoots ? $" • Shoot {shoot}" : "");
            ws.Range(2, 1, 2, 10).Merge();
            ws.Cell(2, 1).Style.Font.Italic = true;

            // Summary block
            var summary = TroyerCalculator.ComputeSummary(targets, targets.Count);
            int sr = 4;
            void Pair(int rowNum, string label, string value, bool error = false)
            {
                ws.Cell(rowNum, 1).Value = label;
                ws.Cell(rowNum, 1).Style.Font.Bold = true;
                ws.Cell(rowNum, 2).Value = value;
                if (error) ws.Cell(rowNum, 2).Style.Font.FontColor = XLColor.Red;
            }

            Pair(sr++, "Target Count", summary.TargetCount.ToString());
            Pair(sr++, "Standing / Required", $"{summary.StandCount} / {summary.RequiredStanding}",
                error: summary.StandCount != summary.RequiredStanding);
            Pair(sr++, "Kneeling / Required", $"{summary.KneelCount} / {summary.RequiredKneeling}",
                error: summary.KneelCount != summary.RequiredKneeling);
            Pair(sr++, "25–34mm / Min", $"{summary.Count25to34} / {summary.Required25to34Min}",
                error: summary.Count25to34 < summary.Required25to34Min);
            Pair(sr++, "15–19mm / Min", $"{summary.Count15to19} / {summary.Required15to19Min}",
                error: summary.Count15to19 < summary.Required15to19Min);
            Pair(sr++, "Avg Distance", $"{summary.AverageDistance:F2} m");
            Pair(sr++, "Avg Difficulty", $"{summary.AverageDifficulty:F2}");
            Pair(sr++, "Killzones (35-40 / 30-34 / 25-29 / 20-24 / 15-19)",
                $"{summary.Count35_40} / {summary.Count30_34} / {summary.Count25_29} / {summary.Count20_24} / {summary.Count15_19}");
            Pair(sr++, "Ratings (Exp / Mod / Easy)",
                $"{summary.CountExp} / {summary.CountMod} / {summary.CountEasy}");

            if (summary.Errors.Count > 0)
            {
                ws.Cell(sr, 1).Value = "Course errors:";
                ws.Cell(sr, 1).Style.Font.Bold = true;
                ws.Cell(sr, 1).Style.Font.FontColor = XLColor.Red;
                sr++;
                foreach (var err in summary.Errors)
                {
                    ws.Cell(sr, 2).Value = "• " + err;
                    ws.Cell(sr, 2).Style.Font.FontColor = XLColor.Red;
                    sr++;
                }
            }
            sr++;

            // Per-target table
            int headerRow = sr;
            var headers = new[] { "Lane", "Target", "Posture", "Incl/Decl", "Shaded", "Killzone (mm)", "Distance (m)", "Diff Factor", "Difficulty", "Rating" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            int row = headerRow + 1;
            foreach (var t in targets.OrderBy(t => t.TargetNumber))
            {
                var rr = TroyerCalculator.ComputeRow(t);
                ws.Cell(row, 1).Value = t.Lane;
                ws.Cell(row, 2).Value = t.TargetNumber;
                ws.Cell(row, 3).Value = TroyerCalculator.PostureLabel(t.Posture);
                ws.Cell(row, 4).Value = t.IsInclineDecline ? "Yes" : "";
                ws.Cell(row, 5).Value = t.IsShaded ? "Yes" : "";
                ws.Cell(row, 6).Value = t.KillZoneMm;
                ws.Cell(row, 7).Value = t.DistanceMeters;
                ws.Cell(row, 8).Value = rr.DiffFactor;
                if (rr.DiffScore > 0) ws.Cell(row, 9).Value = rr.DiffScore;

                var ratingCell = ws.Cell(row, 10);
                ratingCell.Value = rr.Rating;
                switch (rr.Rating)
                {
                    case "Exp":
                        ratingCell.Style.Fill.BackgroundColor = XLColor.LightPink;
                        ratingCell.Style.Font.Bold = true;
                        break;
                    case "Mod":
                        ratingCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                        break;
                    case "Easy":
                        ratingCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                        break;
                }

                if (!rr.IsOk)
                {
                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                }
                row++;
            }

            var lastRow = row - 1;
            ws.Range(headerRow, 1, lastRow, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, lastRow, 10).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // Column widths
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 8;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 10;
            ws.Column(5).Width = 8;
            ws.Column(6).Width = 14;
            ws.Column(7).Width = 13;
            ws.Column(8).Width = 12;
            ws.Column(9).Width = 12;
            ws.Column(10).Width = 8;

            ws.SheetView.FreezeRows(headerRow);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}

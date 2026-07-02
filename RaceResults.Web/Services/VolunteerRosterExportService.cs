using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerRosterExportService : IVolunteerRosterExportService
{
    private readonly IVolunteerRosterService _rosterService;

    public VolunteerRosterExportService(IVolunteerRosterService rosterService)
    {
        _rosterService = rosterService;
    }

    public byte[] ExportPdf(int eventId)
    {
        var roster = _rosterService.GetRoster(eventId);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(roster.Event.EventName).FontSize(16).SemiBold();
                    col.Item().Text(t =>
                    {
                        t.Span($"Volunteer Roster — {roster.Event.EventDate:dd MMM yyyy}").FontSize(11);
                        if (roster.Event.StartTime is { } start)
                            t.Span($" — Start {start:hh\\:mm}").FontSize(11);
                    });
                    col.Item().Text(
                        $"{roster.TotalAssigned} assignment(s) across {roster.DistinctVolunteers} volunteer(s)")
                        .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(8).Column(column =>
                {
                    column.Spacing(10);

                    foreach (var category in new[] { RoleCategory.Leadership, RoleCategory.FinishArea, RoleCategory.Course })
                    {
                        if (!roster.ByCategory.TryGetValue(category, out var roles) || roles.Count == 0) continue;
                        column.Item().Text(CategoryLabel(category)).SemiBold().FontSize(13);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                def.RelativeColumn(3);
                                def.ConstantColumn(60);
                                def.RelativeColumn(4);
                                def.RelativeColumn(3);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Role").SemiBold().FontColor(Colors.White);
                                h.Cell().Element(HeaderCell).AlignCenter().Text("#").SemiBold().FontColor(Colors.White);
                                h.Cell().Element(HeaderCell).Text("Volunteers").SemiBold().FontColor(Colors.White);
                                h.Cell().Element(HeaderCell).Text("Notes").SemiBold().FontColor(Colors.White);
                            });

                            foreach (var row in roles)
                            {
                                table.Cell().Element(BodyCell).Text(row.Role.Name);
                                table.Cell().Element(BodyCell).AlignCenter().Text($"{row.AssignedCount}/{row.DefaultCount}");
                                table.Cell().Element(BodyCell).Text(t =>
                                {
                                    if (row.Assignments.Count == 0) { t.Span("—").FontColor(Colors.Grey.Medium); return; }
                                    var first = true;
                                    foreach (var a in row.Assignments)
                                    {
                                        if (!first) t.Span(", ");
                                        if (a.Assignment.IsNoShow)
                                        {
                                            t.Span(a.Volunteer.Name).Strikethrough().FontColor(Colors.Red.Darken1);
                                            t.Span(" (no show)").FontColor(Colors.Red.Darken1);
                                        }
                                        else
                                        {
                                            t.Span(a.Volunteer.Name);
                                        }
                                        if (a.Assignment.WillRunAfter) t.Span(" (running after)").FontColor(Colors.Blue.Darken1);
                                        if (!a.Volunteer.IsClubMember) t.Span(" *").FontColor(Colors.Grey.Darken1);
                                        first = false;
                                    }
                                });
                                table.Cell().Element(BodyCell).Text(string.Join("; ", row.Assignments
                                    .Where(a => !string.IsNullOrWhiteSpace(a.Assignment.Note))
                                    .Select(a => $"{a.Volunteer.Name}: {a.Assignment.Note}")));
                            }
                        });
                    }

                    column.Item().PaddingTop(6).Text("* non-member").FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Pitsea RC — Volunteer Roster").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();

        static IContainer HeaderCell(IContainer c) => c.Background(Colors.Black).Padding(4);
        static IContainer BodyCell(IContainer c) => c.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
    }

    public byte[] ExportExcel(int eventId)
    {
        var roster = _rosterService.GetRoster(eventId);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Roster");

        sheet.Cell(1, 1).Value = roster.Event.EventName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = $"Volunteer Roster — {roster.Event.EventDate:dd MMM yyyy}"
            + (roster.Event.StartTime is { } s ? $" — Start {s:hh\\:mm}" : "");
        sheet.Cell(3, 1).Value = $"{roster.TotalAssigned} assignment(s) across {roster.DistinctVolunteers} volunteer(s)";

        var headerRow = 5;
        var headers = new[] { "Category", "Role", "#", "Volunteer", "Member?", "First Aid?", "Running After?", "No Show?", "Note" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
            sheet.Cell(headerRow, i + 1).Style.Font.Bold = true;
            sheet.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.Black;
            sheet.Cell(headerRow, i + 1).Style.Font.FontColor = XLColor.White;
        }

        var row = headerRow + 1;
        foreach (var category in new[] { RoleCategory.Leadership, RoleCategory.FinishArea, RoleCategory.Course })
        {
            if (!roster.ByCategory.TryGetValue(category, out var roles)) continue;
            foreach (var r in roles)
            {
                if (r.Assignments.Count == 0)
                {
                    sheet.Cell(row, 1).Value = CategoryLabel(category);
                    sheet.Cell(row, 2).Value = r.Role.Name;
                    sheet.Cell(row, 3).Value = $"0/{r.Role.DefaultCount}";
                    sheet.Cell(row, 4).Value = "—";
                    row++;
                    continue;
                }
                foreach (var a in r.Assignments)
                {
                    sheet.Cell(row, 1).Value = CategoryLabel(category);
                    sheet.Cell(row, 2).Value = r.Role.Name;
                    sheet.Cell(row, 3).Value = $"{r.AssignedCount}/{r.Role.DefaultCount}";
                    sheet.Cell(row, 4).Value = a.Volunteer.Name;
                    sheet.Cell(row, 5).Value = a.Volunteer.IsClubMember ? "Yes" : "No";
                    sheet.Cell(row, 6).Value = a.Volunteer.IsFirstAidTrained ? "Yes" : "";
                    sheet.Cell(row, 7).Value = a.Assignment.WillRunAfter ? "Yes" : "";
                    sheet.Cell(row, 8).Value = a.Assignment.IsNoShow ? "Yes" : "";
                    if (a.Assignment.IsNoShow)
                    {
                        sheet.Cell(row, 4).Style.Font.Strikethrough = true;
                        sheet.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
                    }
                    sheet.Cell(row, 9).Value = a.Assignment.Note ?? "";
                    row++;
                }
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string CategoryLabel(RoleCategory c) => c switch
    {
        RoleCategory.Leadership => "Leadership",
        RoleCategory.FinishArea => "Finish Area",
        RoleCategory.Course => "Course",
        _ => c.ToString()
    };
}

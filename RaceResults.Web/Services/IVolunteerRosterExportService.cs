namespace RaceResults.Web.Services;

/// <summary>Exports a volunteer roster to PDF (QuestPDF) and Excel (ClosedXML) (US28 AC8).</summary>
public interface IVolunteerRosterExportService
{
    byte[] ExportPdf(int eventId);
    byte[] ExportExcel(int eventId);
}

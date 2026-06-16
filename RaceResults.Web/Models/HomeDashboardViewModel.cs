namespace RaceResults.Web.Models;

public class HomeDashboardViewModel
{
    public bool HasCurrentEvent { get; set; }
    public string CurrentEventName { get; set; } = string.Empty;
    public DateTime? CurrentEventDate { get; set; }
    public int Entrants { get; set; }
    public int FinishBibRows { get; set; }
    public int TimingRows { get; set; }
    public int ResultsRows { get; set; }
}

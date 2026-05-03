namespace RaceResults.Web.Models;

public class TopTenCategory
{
    public string Name { get; set; } = string.Empty;
    public List<ResultRecord> Results { get; set; } = new();
}

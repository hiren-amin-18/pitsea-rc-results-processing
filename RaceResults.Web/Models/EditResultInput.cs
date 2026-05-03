using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class EditResultInput
{
    [Required]
    public int OriginalPosition { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int NewPosition { get; set; }

    [Required]
    public string BibNumber { get; set; } = string.Empty;

    public string? Time { get; set; }
}

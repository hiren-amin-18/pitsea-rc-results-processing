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

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Club { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? Age { get; set; }

    public string? Time { get; set; }
}

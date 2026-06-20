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

    /// <summary>Vet status (US33). Populated from the entrant for display in the edit view; not bound from the form.</summary>
    public bool IsVet { get; set; }

    /// <summary>True when the entrant belongs to a Bluebell 5 event (US33). Controls whether the Age field or the Vet badge is shown.</summary>
    public bool IsBluebell { get; set; }
}

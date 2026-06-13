using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class EditRunnerInput
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Club { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? Age { get; set; }

    public string? ExternalReference { get; set; }
}

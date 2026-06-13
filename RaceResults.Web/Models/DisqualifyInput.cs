using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class DisqualifyInput
{
    [Required]
    public int Position { get; set; }

    [Required(ErrorMessage = "A reason is required to disqualify a finisher.")]
    public string Reason { get; set; } = string.Empty;
}

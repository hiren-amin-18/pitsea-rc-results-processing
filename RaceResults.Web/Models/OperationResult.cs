namespace RaceResults.Web.Models;

public class OperationResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Messages { get; } = new();

    public static OperationResult Ok(params string[] messages)
    {
        var result = new OperationResult { Success = true };
        result.Messages.AddRange(messages);
        return result;
    }

    public static OperationResult Fail(IEnumerable<string> errors)
    {
        var result = new OperationResult { Success = false };
        result.Errors.AddRange(errors);
        return result;
    }
}

namespace ResumeTailorAI.Web.Models;

/// <summary>Input for a tailoring run: the candidate's resume and the target job posting.</summary>
public class TailorRequest
{
    public string Resume { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
}

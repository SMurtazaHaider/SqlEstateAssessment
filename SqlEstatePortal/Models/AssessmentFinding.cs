using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentFinding
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>The severity actually shown, after the environment cap.</summary>
    [MaxLength(30)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// The severity the collector raised, before FindingSeverityPolicy capped it
    /// for a non-production server. Kept so the decision stays auditable and so
    /// re-running the policy always works from the original rather than from an
    /// already-capped value. Null on rows imported before the policy existed.
    /// </summary>
    [MaxLength(30)]
    public string? BaseSeverity { get; set; }

    [MaxLength(50)]
    public string Area { get; set; } = string.Empty;

    public string Finding { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

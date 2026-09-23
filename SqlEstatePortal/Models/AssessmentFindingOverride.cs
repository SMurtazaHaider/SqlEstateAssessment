using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace SqlEstatePortal.Models;

/// <summary>
/// A manual re-rank of a finding, recorded by a person.
///
/// The table is APPEND-ONLY: every move writes a new row and nothing is ever
/// updated or deleted. The most recent row for a finding is its current
/// override; the rows behind it are the history the grid shows on hover. That
/// is why there is no "current" flag - a flag would have to be maintained, and
/// the first bug in maintaining it would silently lose the audit trail, which
/// is the one thing this table exists to protect.
///
/// Rows are scoped to ONE ASSESSMENT RUN. A move applies to the run it was made
/// on and to nothing else: the next collection re-reports the finding at the
/// collector's own severity, capped by environment, exactly as if no move had
/// ever happened. That is deliberate. A move says "on this assessment we looked
/// at this and decided it was not Critical" - a judgement about one report, not
/// a standing rule - and carrying it forward would quietly suppress a finding
/// that has since got worse.
///
/// So the identity of an override is the PAIR (AssessmentRunId, FindingKey),
/// not FindingKey alone. FindingKey still exists because the AssessmentFindings
/// row is deleted and re-inserted on every run, so its Id is not stable even
/// within the same run being re-imported.
/// </summary>
public class AssessmentFindingOverride
{
    public int Id { get; set; }

    /// <summary>
    /// Identity of the finding this applies to: a hash of server + area +
    /// finding text. The collector does not give findings a stable id, and the
    /// row in AssessmentFindings is replaced on every run, so the text itself is
    /// the only thing that persists. Stored as a hash so it can be indexed -
    /// the finding text is nvarchar(max) and cannot be.
    /// </summary>
    [MaxLength(64)]
    public string FindingKey { get; set; } = string.Empty;

    // The three parts of the key are stored in full as well, so the table can be
    // read and audited on its own without joining back to a run that may since
    // have been deleted.
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Area { get; set; } = string.Empty;

    public string Finding { get; set; } = string.Empty;

    /// <summary>What the finding was showing when the change was made.</summary>
    [MaxLength(30)]
    public string FromSeverity { get; set; } = string.Empty;

    /// <summary>What it was moved to.</summary>
    [MaxLength(30)]
    public string ToSeverity { get; set; } = string.Empty;

    /// <summary>Mandatory. Enforced in the controller, not just in the browser.</summary>
    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedOnUtc { get; set; }

    /// <summary>
    /// The run this override applies to. Half of the row's identity, not
    /// decoration: the lookup is (AssessmentRunId, FindingKey).
    ///
    /// Nullable only because the column already exists and older rows may not
    /// have it. A row with no run is history - it is displayed if anything asks
    /// for it, but it never changes a severity, because there is no run it
    /// belongs to.
    /// </summary>
    public int? AssessmentRunId { get; set; }

    /// <summary>
    /// Builds the finding's identity WITHIN a run. Case- and
    /// whitespace-insensitive so that re-importing the same run, or a collector
    /// that pads or re-cases a value, still matches the row a person moved.
    ///
    /// Nothing here has to survive to the next assessment - overrides are
    /// run-scoped - so the key does not need to be stable across collections,
    /// only across a re-import of the same one.
    /// </summary>
    public static string BuildKey(string? serverName, string? area, string? finding)
    {
        static string N(string? s) =>
            string.Join(' ', (s ?? string.Empty).Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        var raw = $"{N(serverName)}\u001f{N(area)}\u001f{N(finding)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}

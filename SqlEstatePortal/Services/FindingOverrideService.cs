using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

/// <summary>
/// Reads and records manual re-ranks of findings.
///
/// Where this sits in the severity pipeline:
///
///     collector severity  ->  environment cap  ->  MANUAL OVERRIDE (this run only)
///
/// The manual move is last and beats the cap. A person looking at one specific
/// finding on one specific server knows more than a rule that only knows the
/// server's environment, so their decision wins - including moving something
/// back UP, which the cap alone can never do.
///
/// It wins ON THAT RUN ONLY. A new assessment starts clean: its findings are
/// ranked by the collector and the environment cap, and no earlier move touches
/// them. Every lookup here is therefore scoped by AssessmentRunId, and there is
/// deliberately no method that answers "what is the override for this finding"
/// without being told which run is being asked about - such a method is exactly
/// what would leak a move into the next assessment.
/// </summary>
public class FindingOverrideService
{
    private readonly AppDbContext _db;

    public FindingOverrideService(AppDbContext db) => _db = db;

    /// <summary>Severities a finding may be moved to, most severe first.</summary>
    public static readonly string[] TargetSeverities =
    {
        FindingSeverityPolicy.Critical,
        FindingSeverityPolicy.High,
        FindingSeverityPolicy.Medium,
        FindingSeverityPolicy.Low,
        FindingSeverityPolicy.Info
    };

    public static bool IsValidTarget(string? severity) =>
        TargetSeverities.Contains((severity ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

    public const int MaxCommentLength = 1000;

    /// <summary>
    /// The moves made ON ONE RUN, newest first, grouped by finding key - what
    /// the hover history in the Findings tab shows.
    ///
    /// Scoped to the run for the same reason the severities are. A row reading
    /// "Critical -> Info, last month" under a finding that this week's
    /// assessment reports as Critical would look like a bug, and the reading
    /// would be fair: that decision was made about a different report.
    /// </summary>
    public async Task<Dictionary<string, List<AssessmentFindingOverride>>> GetHistoryAsync(
        int assessmentRunId, IEnumerable<AssessmentFinding> findings, CancellationToken ct = default)
    {
        var keys = findings
            .Select(f => AssessmentFindingOverride.BuildKey(f.ServerName, f.Area, f.Finding))
            .ToHashSet(StringComparer.Ordinal);

        if (keys.Count == 0) return new Dictionary<string, List<AssessmentFindingOverride>>();

        // The run is filtered in SQL; the keys are matched in memory afterwards
        // rather than being sent as a WHERE ... IN (...). A findings tab can
        // carry several hundred rows and EF turns Contains into one parameter per
        // key - past SQL Server's 2,100 parameter limit that throws outright, and
        // well before that it plans badly. One run's moves are a handful of rows,
        // so reading them all and matching here costs nothing.
        //
        // The Finding text column is deliberately not selected: it is
        // nvarchar(max), the hover card does not show it, and the key is what the
        // lookup needs.
        var rows = await _db.AssessmentFindingOverrides
            .AsNoTracking()
            .Where(o => o.AssessmentRunId == assessmentRunId)
            .OrderByDescending(o => o.ChangedOnUtc)
            .ThenByDescending(o => o.Id)
            .Select(o => new AssessmentFindingOverride
            {
                Id = o.Id,
                FindingKey = o.FindingKey,
                ServerName = o.ServerName,
                Area = o.Area,
                FromSeverity = o.FromSeverity,
                ToSeverity = o.ToSeverity,
                Comment = o.Comment,
                ChangedBy = o.ChangedBy,
                ChangedOnUtc = o.ChangedOnUtc,
                AssessmentRunId = o.AssessmentRunId
            })
            .ToListAsync(ct);

        return rows
            .Where(o => keys.Contains(o.FindingKey))
            .GroupBy(o => o.FindingKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The severity every moved finding should be showing, for the runs given.
    /// Keyed on (run id, finding key): the same finding on two runs is two
    /// separate answers, and on a run nobody has touched there is no answer at
    /// all.
    ///
    /// The newest row per pair wins; ties on timestamp are broken by Id, so two
    /// moves in the same millisecond still resolve the same way every time.
    ///
    /// Rows with no AssessmentRunId are skipped. They are history from before
    /// moves were run-scoped, and there is no run they can be said to apply to -
    /// applying them anywhere is the behaviour being removed.
    /// </summary>
    public async Task<Dictionary<(int RunId, string Key), string>> GetCurrentAsync(
        CancellationToken ct = default)
    {
        var rows = await _db.AssessmentFindingOverrides
            .AsNoTracking()
            .Where(o => o.AssessmentRunId != null)
            .Select(o => new { o.AssessmentRunId, o.FindingKey, o.ToSeverity, o.ChangedOnUtc, o.Id })
            .ToListAsync(ct);

        return rows
            .GroupBy(o => (RunId: o.AssessmentRunId!.Value, Key: o.FindingKey))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.ChangedOnUtc).ThenByDescending(x => x.Id).First().ToSeverity);
    }

    /// <summary>
    /// Records a move against one run. Returns the row written, or null when
    /// nothing changed because the finding is already at that severity -
    /// recording a move to where it already is would put a meaningless entry in
    /// the audit trail.
    ///
    /// assessmentRunId is an int and not an int? on purpose: the run is half the
    /// row's identity, and a row written without one would never be applied to
    /// anything.
    /// </summary>
    public async Task<AssessmentFindingOverride?> MoveAsync(
        AssessmentFinding finding,
        string toSeverity,
        string comment,
        string changedBy,
        int assessmentRunId,
        CancellationToken ct = default)
    {
        var target = TargetSeverities.First(s => s.Equals(toSeverity.Trim(), StringComparison.OrdinalIgnoreCase));

        if (string.Equals(finding.Severity, target, StringComparison.OrdinalIgnoreCase))
            return null;

        var row = new AssessmentFindingOverride
        {
            FindingKey = AssessmentFindingOverride.BuildKey(finding.ServerName, finding.Area, finding.Finding),
            ServerName = finding.ServerName ?? string.Empty,
            Area = finding.Area ?? string.Empty,
            Finding = finding.Finding ?? string.Empty,
            FromSeverity = finding.Severity ?? string.Empty,
            ToSeverity = target,
            Comment = comment.Trim(),
            ChangedBy = changedBy,
            ChangedOnUtc = DateTime.UtcNow,
            AssessmentRunId = assessmentRunId
        };

        _db.AssessmentFindingOverrides.Add(row);

        // Apply to the finding on screen as well, so the grid and the run's
        // severity counts agree without waiting for the next re-rank.
        finding.Severity = target;

        await _db.SaveChangesAsync(ct);
        return row;
    }

    // There is deliberately no Apply(findings, overrides) helper here.
    //
    // There was one, and it took a dictionary keyed on the finding alone, which
    // made it trivially easy to apply somebody's old decision to a freshly
    // collected run - which is exactly what it did, and exactly what is not
    // wanted. The only place that re-applies moves is SeverityRerankPass, it
    // matches on (run id, finding key), and it should stay the only place.
}

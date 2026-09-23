using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Services;

namespace SqlEstatePortal.Data;

/// <summary>
/// NOTE ON THE FILENAME: this supersedes Data/FindingSeverityBackfill.cs and
/// Data/FindingSeverityRerank.cs - delete both. The first of those, whose
/// first version loaded AssessmentRun entities with .Include(r => r.Findings)
/// and hung startup - AssessmentRuns carries HtmlContent as nvarchar(max), and
/// the join repeated each run's whole HTML report once per finding. That file is
/// no longer called; delete it.
///
/// Re-ranks findings from runs imported before the environment cap existed, so
/// historical assessments agree with new ones instead of showing a different
/// number of Criticals for the same estate. It also picks up the case that
/// matters most in practice: someone corrects a server's Environment in the
/// register, and the existing assessments re-rank to match.
///
/// This runs on every startup, so it must stay cheap. It deliberately does NOT
/// load AssessmentRun entities with their Findings collection: AssessmentRuns
/// carries HtmlContent and OutputLog as nvarchar(max), and an Include would
/// repeat the whole HTML report of a run once per finding in that run - tens of
/// megabytes per run, which materialises for minutes and blocks startup. Only
/// the six small columns the decision needs are read, and only rows that
/// actually change are written.
///
/// The pass is idempotent: BaseSeverity is filled once from the original
/// Severity and every later decision is computed from BaseSeverity, so repeating
/// it can never walk a finding progressively down the scale.
/// </summary>
public static class SeverityRerankPass
{
    private const int ChunkSize = 500;

    /// <summary>
    /// The collector reports whatever name it was given - an FQDN, or
    /// host\instance - while the register may hold the bare host, or the other
    /// way round. Try the name as given, then its short form. A miss returns
    /// null, which the policy classifies as Production: a server nobody has
    /// registered is not evidence that it is non-production.
    ///
    /// Kept here rather than called from FindingSeverityPolicy so this file has
    /// no dependency on that one beyond the public Apply().
    /// </summary>
    private static string? LookupEnvironment(
        IReadOnlyDictionary<string, string?> map, string? serverName)
    {
        var name = (serverName ?? string.Empty).Trim();
        if (name.Length == 0) return null;
        if (map.TryGetValue(name, out var env)) return env;

        var host = name.Split('\\')[0];
        var dot = host.IndexOf('.');
        var shortName = dot > 0 ? host[..dot] : host;

        return shortName != name && map.TryGetValue(shortName, out var shortEnv) ? shortEnv : null;
    }

    public static async Task ApplyAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        // Capture the collector's original severity for rows that predate the
        // policy. One set-based statement, and after it BaseSeverity is never
        // null, so everything below can rely on it.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE AssessmentFindings SET BaseSeverity = Severity WHERE BaseSeverity IS NULL;", ct);

        var environments = await db.CtServers
            .AsNoTracking()
            .Where(s => s.ServerName != "")
            .Select(s => new { s.ServerName, s.Environment })
            .ToListAsync(ct);

        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in environments)
            map[r.ServerName.Trim()] = r.Environment;

        // Projection, not entities: no Finding/Recommendation text, no run.
        // Anonymous type rather than a named one, so there is no chance of EF
        // refusing the projection at startup - the failure mode we are fixing.
        var findings = await db.AssessmentFindings
            .AsNoTracking()
            .Select(f => new
            {
                f.Id,
                f.ServerName,
                f.Area,
                f.BaseSeverity,
                f.Severity
            })
            .ToListAsync(ct);

        var updates = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in findings)
        {
            // BaseSeverity was filled by the statement above, but fall back to
            // Severity anyway so a row inserted between the two still behaves.
            var baseSeverity = string.IsNullOrWhiteSpace(f.BaseSeverity) ? f.Severity : f.BaseSeverity;

            var effective = FindingSeverityPolicy.Apply(
                baseSeverity, f.Area, LookupEnvironment(map, f.ServerName));

            if (string.Equals(effective, f.Severity, StringComparison.OrdinalIgnoreCase)) continue;

            if (!updates.TryGetValue(effective, out var ids))
                updates[effective] = ids = new List<int>();
            ids.Add(f.Id);
        }

        var changed = 0;
        foreach (var (severity, ids) in updates)
        {
            for (var i = 0; i < ids.Count; i += ChunkSize)
            {
                var chunk = ids.Skip(i).Take(ChunkSize).ToList();

                // Concatenated rather than interpolated: EF1002 fires on any
                // interpolated string handed to ExecuteSqlRawAsync. The only
                // thing spliced in is a list of int primary keys read from this
                // same table, and the severity is a real parameter.
                var sql = "UPDATE AssessmentFindings SET Severity = {0} WHERE Id IN ("
                          + string.Join(",", chunk) + ");";

                await db.Database.ExecuteSqlRawAsync(sql, new object[] { severity }, ct);
                changed += chunk.Count;
            }
        }

        // Recompute every run's tiles from what the findings now say. Done in one
        // set-based statement so no run entity - and so no HtmlContent - is ever
        // loaded. Always run, not only when something changed: a run whose counts
        // drifted for any other reason is corrected here too.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE r
               SET CriticalCount = x.Critical,
                   HighCount     = x.High,
                   MediumCount   = x.Medium,
                   LowCount      = x.Low,
                   InfoCount     = x.Info
              FROM AssessmentRuns AS r
             CROSS APPLY (
                -- ISNULL matters: SUM over a run with no findings returns NULL,
                -- and these are NOT NULL int columns.
                SELECT
                    ISNULL(SUM(CASE WHEN f.Severity = N'Critical' THEN 1 ELSE 0 END), 0) AS Critical,
                    ISNULL(SUM(CASE WHEN f.Severity = N'High'     THEN 1 ELSE 0 END), 0) AS High,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Medium'   THEN 1 ELSE 0 END), 0) AS Medium,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Low'      THEN 1 ELSE 0 END), 0) AS Low,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Info'     THEN 1 ELSE 0 END), 0) AS Info
                FROM AssessmentFindings AS f
                WHERE f.AssessmentRunId = r.Id
             ) AS x
             WHERE r.CriticalCount <> x.Critical
                OR r.HighCount     <> x.High
                OR r.MediumCount   <> x.Medium
                OR r.LowCount      <> x.Low
                OR r.InfoCount     <> x.Info;
            """, ct);

        if (changed > 0)
            logger?.LogInformation(
                "Severity policy: re-ranked {Changed} of {Total} finding(s) by server environment.",
                changed, findings.Count);
    }
}

using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

/// <summary>
/// Caps a finding's severity by the environment of the server it was raised on,
/// so that a dev box with no backups stops competing with production for the
/// Critical tile.
///
/// Three rules govern what this does and does not touch:
///
/// 1. An unrecognised environment is treated as PRODUCTION. This is the whole
///    safety of the feature. A production server whose Environment is blank or
///    misspelled must keep its Critical findings - a hidden emergency is far
///    worse than the noise this is meant to remove, and noise at least is
///    visible. Never invert this default.
///
/// 2. A Development or Test server never exceeds Medium, in any area. Critical
///    and High are reserved for servers where someone would act today, and a dev
///    box is not one of them. Security and Encryption are capped there too.
///
///    On UAT and Production, Security and Encryption are NOT capped: a foothold
///    on a box on the same domain is still a foothold, and non-production often
///    holds a copy of production data, so the exposure is as raised.
///
/// 3. Licensing moves in both directions rather than only down. Developer
///    edition in production is a licensing breach; the same edition in dev is
///    exactly what is supposed to be there.
///
/// The original severity is preserved on the finding as BaseSeverity so the
/// decision stays auditable and the rule can be tuned later.
/// </summary>
public static class FindingSeverityPolicy
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
    public const string Info = "Info";

    public enum Environment
    {
        Production,
        Uat,
        Dev
    }

    /// <summary>Values offered on the server form, highest exposure first.</summary>
    public static readonly string[] EnvironmentValues =
    {
        "Production", "DR", "Pre-Production", "UAT", "Staging", "Test", "Development", "Sandbox"
    };

    private static readonly string[] UatTokens =
    {
        "uat", "user acceptance", "staging", "stage", "preprod", "pre-prod", "pre prod",
        "pre-production", "pre production", "qa", "sit"
    };

    private static readonly string[] DevTokens =
    {
        "dev", "development", "test", "tst", "sandbox", "sbx", "poc", "lab", "training"
    };

    /// <summary>
    /// Areas whose findings describe availability, recoverability or performance.
    /// These are the ones whose business impact genuinely depends on which
    /// environment the server is in, so these are the ones that get capped.
    /// </summary>
    private static readonly HashSet<string> CappedAreas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Status", "SLA", "Performance", "Alerts", "Standards", "Cost", "Supportability"
        };

    private static readonly Dictionary<string, int> Rank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Critical] = 4, [High] = 3, [Medium] = 2, [Low] = 1, [Info] = 0
        };

    /// <summary>
    /// Maps whatever is in ct_servers.environment onto the three levels that
    /// matter. Matching is on whole words so that a server named for a customer
    /// called "Devon" in a Production environment is not read as Development;
    /// only the environment field is consulted, never the server name.
    /// </summary>
    public static Environment ClassifyEnvironment(string? environment)
    {
        var text = (environment ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return Environment.Production;

        // Production wins outright when stated, even alongside another token
        // ("Pre-Production" is handled below because it is matched first).
        var words = text.Split(new[] { ' ', '-', '_', '/', '\\', ',', '.', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (UatTokens.Any(t => ContainsPhrase(text, words, t))) return Environment.Uat;
        if (DevTokens.Any(t => ContainsPhrase(text, words, t))) return Environment.Dev;

        // Production, DR, Prod, anything unrecognised, and blank.
        return Environment.Production;
    }

    private static bool ContainsPhrase(string text, string[] words, string token) =>
        token.Contains(' ') || token.Contains('-')
            ? text.Contains(token, StringComparison.Ordinal)
            : words.Contains(token, StringComparer.Ordinal);

    /// <summary>The highest severity a capped-area finding may reach.</summary>
    private static string Ceiling(Environment env) => env switch
    {
        Environment.Uat => High,
        Environment.Dev => Medium,
        _ => Critical
    };

    /// <summary>
    /// The ceiling for Security and Encryption, which are not judged by business
    /// impact the way availability is. Production and UAT keep whatever the
    /// collector raised, because a foothold or an expired certificate there is
    /// as serious as the collector says it is.
    ///
    /// Development and Test take the same Medium ceiling as everything else, so
    /// a dev server never reaches Critical or High in any area at all. That is a
    /// deliberate trade: a real security issue on a dev box now reads the same as
    /// a missing backup on one. The compensation is that BaseSeverity still holds
    /// what was raised, so filtering the Findings grid on a dev server shows
    /// "Medium - from Critical" and the original ranking is never lost.
    /// </summary>
    private static string SecurityCeiling(Environment env) =>
        env == Environment.Dev ? Medium : Critical;

    public static string Apply(string? baseSeverity, string? area, string? environment) =>
        Apply(baseSeverity, area, ClassifyEnvironment(environment));

    public static string Apply(string? baseSeverity, string? area, Environment env)
    {
        var severity = Normalise(baseSeverity);
        var areaName = (area ?? string.Empty).Trim();

        // Licensing is the one area that moves both ways: the same edition is a
        // breach in production and correct in development.
        if (areaName.Equals("Licensing", StringComparison.OrdinalIgnoreCase))
            return env == Environment.Dev ? Info : severity;

        // Security and Encryption are judged by exposure, not by business impact,
        // so they get their own ceiling rather than the operational one.
        var ceiling = CappedAreas.Contains(areaName) ? Ceiling(env) : SecurityCeiling(env);

        return Rank.GetValueOrDefault(severity, 0) > Rank[ceiling] ? ceiling : severity;
    }

    /// <summary>
    /// Rewrites a run's findings in place and returns how many changed. The
    /// original severity is captured in BaseSeverity the first time a finding is
    /// seen, so re-running this is idempotent and always works from what the
    /// collector actually raised rather than from an already-capped value.
    /// </summary>
    public static int ApplyToFindings(
        IEnumerable<AssessmentFinding> findings,
        IReadOnlyDictionary<string, string?> environmentByServer)
    {
        var changed = 0;
        foreach (var f in findings)
        {
            if (string.IsNullOrWhiteSpace(f.BaseSeverity))
                f.BaseSeverity = f.Severity;

            var effective = Apply(f.BaseSeverity, f.Area, LookupEnvironment(environmentByServer, f.ServerName));

            if (!string.Equals(effective, f.Severity, StringComparison.OrdinalIgnoreCase))
            {
                f.Severity = effective;
                changed++;
            }
        }
        return changed;
    }

    /// <summary>
    /// The collector reports whatever name it was given - an FQDN, or
    /// host\instance - while the register may hold the bare host, or the other
    /// way round. Try the name as given, then its short form. A miss returns
    /// null, which classifies as Production: a server nobody has registered is
    /// not evidence that it is non-production.
    /// </summary>
    private static string? LookupEnvironment(
        IReadOnlyDictionary<string, string?> map, string? serverName)
    {
        var name = (serverName ?? string.Empty).Trim();
        if (name.Length == 0) return null;
        if (map.TryGetValue(name, out var env)) return env;

        var shortName = ShortName(name);
        return shortName != name && map.TryGetValue(shortName, out var shortEnv) ? shortEnv : null;
    }

    private static string ShortName(string name)
    {
        var host = name.Split('\\')[0];
        var dot = host.IndexOf('.');
        return dot > 0 ? host[..dot] : host;
    }

    /// <summary>True when the finding was raised higher than it is now shown.</summary>
    public static bool WasCapped(AssessmentFinding f) =>
        !string.IsNullOrWhiteSpace(f.BaseSeverity) &&
        !string.Equals(f.BaseSeverity, f.Severity, StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string? severity)
    {
        var s = (severity ?? string.Empty).Trim();
        foreach (var known in new[] { Critical, High, Medium, Low, Info })
            if (s.Equals(known, StringComparison.OrdinalIgnoreCase)) return known;
        return s;
    }
}

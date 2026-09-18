using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Services;

public class ServerQaCompareService
{
    private readonly AppDbContext _db;

    public ServerQaCompareService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AssessmentRunSummary>> GetAvailableRunsAsync(CancellationToken ct = default)
    {
        return await _db.AssessmentRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(100)
            .Select(r => new AssessmentRunSummary
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Status = r.Status,
                TriggeredBy = r.TriggeredBy,
                ServerCount = r.ServerCount,
                ReachableCount = r.ReachableCount,
                CriticalCount = r.CriticalCount,
                HighCount = r.HighCount,
                MediumCount = r.MediumCount,
                LowCount = r.LowCount
            })
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetServersForRunAsync(int runId, CancellationToken ct = default)
    {
        return await _db.AssessmentServerSnapshots
            .AsNoTracking()
            .Where(s => s.AssessmentRunId == runId && s.Reachable && s.ServerName != "")
            .Select(s => s.ServerName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetAvailableServersAsync(CancellationToken ct = default)
    {
        return await _db.AssessmentServerSnapshots
            .AsNoTracking()
            .Where(s => s.Reachable && s.ServerName != "")
            .Select(s => s.ServerName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
    }

    public async Task<List<AssessmentRunSummary>> GetRunsForServerAsync(string serverName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return [];

        var runs = await (
            from snap in _db.AssessmentServerSnapshots.AsNoTracking()
            join run in _db.AssessmentRuns.AsNoTracking() on snap.AssessmentRunId equals run.Id
            where snap.ServerName == serverName
            orderby run.StartedAt descending
            select new AssessmentRunSummary
            {
                Id = run.Id,
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                Status = run.Status,
                TriggeredBy = run.TriggeredBy,
                ServerCount = run.ServerCount,
                ReachableCount = run.ReachableCount,
                CriticalCount = run.CriticalCount,
                HighCount = run.HighCount,
                MediumCount = run.MediumCount,
                LowCount = run.LowCount
            }
        ).ToListAsync(ct);

        return runs
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .OrderByDescending(r => r.StartedAt)
            .ToList();
    }

    public async Task<ServerQaSnapshot?> LoadAsync(string serverName, int? runId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return null;

        var name = serverName.Trim();
        var query =
            from snap in _db.AssessmentServerSnapshots.AsNoTracking()
            join run in _db.AssessmentRuns.AsNoTracking() on snap.AssessmentRunId equals run.Id
            where snap.ServerName == name
            select new { snap.AssessmentRunId, run.StartedAt, snap.ServerName, run.Status };

        var selected = runId.HasValue
            ? await query.FirstOrDefaultAsync(x => x.AssessmentRunId == runId.Value, ct)
            : await query
                .Where(x => x.Status == "Succeeded")
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync(ct)
                ?? await query
                    .OrderByDescending(x => x.StartedAt)
                    .FirstOrDefaultAsync(ct);

        if (selected == null)
            return null;

        var assessmentRunId = selected.AssessmentRunId;

        var snapshot = await _db.AssessmentServerSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AssessmentRunId == assessmentRunId && s.ServerName == name, ct);

        var configs = await _db.AssessmentConfigurations.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var services = await _db.AssessmentServices.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var databases = await _db.AssessmentDatabases.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var volumes = await _db.AssessmentVolumes.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var sysadmins = await _db.AssessmentSysadmins.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var linkedServers = await _db.AssessmentLinkedServers.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var sqlLogins = await _db.AssessmentSqlLogins.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var availabilityGroups = await _db.AssessmentAvailabilityGroups.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var certificates = await _db.AssessmentCertificates.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        var tlsCertificates = await _db.AssessmentTlsCertificates.AsNoTracking()
            .Where(c => c.AssessmentRunId == assessmentRunId && c.ServerName == name)
            .ToListAsync(ct);

        return new ServerQaSnapshot
        {
            ServerName = name,
            AssessmentRunId = assessmentRunId,
            AssessedAt = selected.StartedAt,
            Items = Flatten(
                snapshot, configs, services, databases, volumes, sysadmins,
                linkedServers, sqlLogins, availabilityGroups, certificates, tlsCertificates)
        };
    }

    public async Task<ServerQaSnapshot?> LoadLatestAsync(string serverName, CancellationToken ct = default)
        => await LoadAsync(serverName, null, ct);

    public ServerQaCompareViewModel Compare(
        ServerQaSnapshot server1,
        ServerQaSnapshot server2,
        List<AssessmentRunSummary> availableRuns,
        List<string>? availableServers1 = null,
        List<string>? availableServers2 = null)
    {
        var rows = MatchRows(server1, server2);
        return new ServerQaCompareViewModel
        {
            Server1 = server1.ServerName,
            Server2 = server2.ServerName,
            AvailableRuns = availableRuns,
            AvailableServers1 = availableServers1 ?? [],
            AvailableServers2 = availableServers2 ?? [],
            Server1RunId = server1.AssessmentRunId,
            Server2RunId = server2.AssessmentRunId,
            Server1AssessedAt = server1.AssessedAt,
            Server2AssessedAt = server2.AssessedAt,
            HasResult = true,
            Rows = rows,
            YesCount = rows.Count(r => r.Match == "Yes"),
            CloseCount = rows.Count(r => r.Match == "Close"),
            NoCount = rows.Count(r => r.Match == "No"),
            TotalCount = rows.Count
        };
    }

    internal static List<ServerQaCompareRow> MatchRows(ServerQaSnapshot server1, ServerQaSnapshot server2)
    {
        var rows = new List<ServerQaCompareRow>();
        var shown = new HashSet<string>(StringComparer.Ordinal);
        var sourceShown = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in server1.Items)
        {
            var pkvSource = Pkv(source);
            foreach (var target in server2.Items)
            {
                if (pkvSource != Pkv(target))
                    continue;

                shown.Add(Pkv(target));
                rows.Add(BuildRow(source, target, server1.ServerName, server2.ServerName, "Yes", 100));
            }
        }

        foreach (var source in server1.Items)
        {
            var pkvSource = Pkv(source);
            var pkSource = Pk(source);
            if (shown.Contains(pkvSource))
                continue;

            foreach (var target in server2.Items)
            {
                var pkvTarget = Pkv(target);
                if (pkSource != Pk(target) || shown.Contains(pkvSource) || shown.Contains(pkvTarget))
                    continue;

                shown.Add(pkvTarget);
                if (sourceShown.Add(pkvSource))
                    shown.Add(pkvSource);

                SimilarText(source.Value, target.Value, out var percent);
                var matchPct = (int)Math.Ceiling(percent);
                var match = pkvSource == pkvTarget
                    ? "Yes"
                    : matchPct >= 80 ? "Close" : "No";

                rows.Add(BuildRow(source, target, server1.ServerName, server2.ServerName, match, match == "Yes" ? 100 : matchPct));
            }
        }

        foreach (var source in server1.Items)
        {
            if (shown.Contains(Pkv(source)))
                continue;

            rows.Add(new ServerQaCompareRow
            {
                Parameter = source.Parameter,
                Key = source.Key,
                Server1Name = server1.ServerName,
                Server1Value = source.Value,
                Server2Name = server2.ServerName,
                Server2Value = "—",
                Match = "No",
                MatchPercent = 0
            });
        }

        foreach (var target in server2.Items)
        {
            if (shown.Contains(Pkv(target)))
                continue;

            rows.Add(new ServerQaCompareRow
            {
                Parameter = target.Parameter,
                Key = target.Key,
                Server1Name = server1.ServerName,
                Server1Value = "—",
                Server2Name = server2.ServerName,
                Server2Value = target.Value,
                Match = "No",
                MatchPercent = 0
            });
        }

        return rows;
    }

    internal static List<QaParameterItem> Flatten(
        AssessmentServerSnapshot? server,
        IEnumerable<AssessmentConfiguration> configs,
        IEnumerable<AssessmentService> services,
        IEnumerable<AssessmentDatabase> databases,
        IEnumerable<AssessmentVolume> volumes,
        IEnumerable<AssessmentSysadmin> sysadmins,
        IEnumerable<AssessmentLinkedServer>? linkedServers = null,
        IEnumerable<AssessmentSqlLogin>? sqlLogins = null,
        IEnumerable<AssessmentAvailabilityGroup>? availabilityGroups = null,
        IEnumerable<AssessmentCertificate>? certificates = null,
        IEnumerable<AssessmentTlsCertificate>? tlsCertificates = null)
    {
        var items = new List<QaParameterItem>();

        void Add(string parameter, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            items.Add(new QaParameterItem
            {
                Parameter = parameter,
                Key = key.Trim(),
                Value = value ?? string.Empty
            });
        }

        if (server != null)
        {
            Add("Server", "Reachable", server.Reachable ? "Yes" : "No");
            Add("Server", "Product", server.Product);
            Add("Server", "Support status", server.SupportStatus);
            Add("Server", "Edition", server.Edition);
            Add("Server", "Version", server.Version);
            Add("Server", "CPU count", Num(server.CpuCount));
            Add("Server", "Memory MB", Dec(server.MemoryMb));
            Add("Server", "Allocated GB", Dec(server.AllocatedGb));
            Add("Server", "User databases", Num(server.UserDatabaseCount));
            Add("Server", "User connections", Num(server.UserConnections));
            Add("Server", "Page life expectancy (sec)", Num(server.PageLifeExpectancySec));
            Add("Server", "Batch requests/sec", Dec(server.BatchRequestsPerSec));
            Add("Server", "Host platform", server.HostPlatform);
            Add("Server", "Host distribution", server.HostDistribution);
            Add("Server", "Collation", server.Collation);
            Add("Server", "VM type", server.VirtualMachineType);
            Add("Server", "License type", server.LicenseType);
        }

        foreach (var cfg in configs.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            Add("sp_configure", cfg.Name, Num(cfg.RunValue ?? cfg.ConfigValue));

        foreach (var svc in services.OrderBy(s => s.ServiceName, StringComparer.OrdinalIgnoreCase))
        {
            Add("Service", $"{svc.ServiceName} / Status", svc.Status);
            Add("Service", $"{svc.ServiceName} / Startup type", svc.StartupType);
            Add("Service", $"{svc.ServiceName} / Account", svc.ServiceAccount);
            Add("Service", $"{svc.ServiceName} / Instant file initialization", svc.InstantFileInitialization);
        }

        foreach (var db in databases.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            Add("Database", $"{db.Name} / State", db.State);
            Add("Database", $"{db.Name} / Recovery model", db.RecoveryModel);
            Add("Database", $"{db.Name} / Compatibility level", Num(db.CompatibilityLevel));
            Add("Database", $"{db.Name} / Page verify", db.PageVerify);
            Add("Database", $"{db.Name} / Encrypted", db.IsEncrypted ? "Yes" : "No");
            Add("Database", $"{db.Name} / Collation", db.CollationName);
            Add("Database", $"{db.Name} / Owner", db.OwnerName);
        }

        foreach (var vol in volumes.OrderBy(v => v.MountPoint, StringComparer.OrdinalIgnoreCase))
        {
            var mount = string.IsNullOrWhiteSpace(vol.LogicalName) ? vol.MountPoint : $"{vol.MountPoint} ({vol.LogicalName})";
            Add("Volume", $"{mount} / Total GB", Dec(vol.TotalGb));
            Add("Volume", $"{mount} / Free GB", Dec(vol.FreeGb));
            Add("Volume", $"{mount} / Free %", Dec(vol.FreePct));
        }

        foreach (var admin in sysadmins.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            var state = admin.IsDisabled ? "Disabled" : "Enabled";
            Add("Sysadmin", admin.Name, string.IsNullOrWhiteSpace(admin.TypeDesc) ? state : $"{admin.TypeDesc} · {state}");
        }

        foreach (var ls in (linkedServers ?? []).OrderBy(l => l.LinkedServerName, StringComparer.OrdinalIgnoreCase))
        {
            Add("Linked server", $"{ls.LinkedServerName} / Data source", ls.DataSource);
            Add("Linked server", $"{ls.LinkedServerName} / Provider", ls.Provider);
            Add("Linked server", $"{ls.LinkedServerName} / Remote login", ls.IsRemoteLoginEnabled ? "Yes" : "No");
            Add("Linked server", $"{ls.LinkedServerName} / RPC out", ls.IsRpcOutEnabled ? "Yes" : "No");
        }

        foreach (var login in (sqlLogins ?? []).OrderBy(l => l.LoginName, StringComparer.OrdinalIgnoreCase))
        {
            Add("SQL login", $"{login.LoginName} / State", login.IsDisabled ? "Disabled" : "Enabled");
            Add("SQL login", $"{login.LoginName} / Sysadmin", login.IsSysadmin ? "Yes" : "No");
            Add("SQL login", $"{login.LoginName} / Policy checked", login.IsPolicyChecked ? "Yes" : "No");
            Add("SQL login", $"{login.LoginName} / Expiration checked", login.IsExpirationChecked ? "Yes" : "No");
        }

        foreach (var ag in (availabilityGroups ?? [])
                     .OrderBy(g => g.AgName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.ReplicaServerName, StringComparer.OrdinalIgnoreCase))
        {
            var replica = string.IsNullOrWhiteSpace(ag.ReplicaServerName) ? ag.AgName : $"{ag.AgName} / {ag.ReplicaServerName}";
            Add("Availability group", $"{replica} / Role", ag.RoleDesc);
            Add("Availability group", $"{replica} / Operational state", ag.OperationalStateDesc);
            Add("Availability group", $"{replica} / Connected state", ag.ConnectedStateDesc);
            Add("Availability group", $"{replica} / Synchronisation health", ag.SynchronizationHealthDesc);
        }

        // Thumbprints are unique per server by design, so they are deliberately not
        // compared here - only the facts that are meaningful for server parity.
        foreach (var cert in (certificates ?? []).OrderBy(c => c.CertificateName, StringComparer.OrdinalIgnoreCase))
        {
            Add("Certificate", $"{cert.CertificateName} / Expiry", cert.ExpiryDate?.ToString("yyyy-MM-dd"));
            Add("Certificate", $"{cert.CertificateName} / Private key encryption", cert.PrivateKeyEncryption);
            Add("Certificate", $"{cert.CertificateName} / Protects", cert.ProtectedDatabases);
        }

        foreach (var tls in (tlsCertificates ?? []).OrderBy(t => t.InstanceName, StringComparer.OrdinalIgnoreCase))
        {
            Add("TLS certificate", "Source", tls.CertificateSource);
            Add("TLS certificate", "Force encryption", tls.ForceEncryption ? "Yes" : "No");
        }

        return items;
    }

    /// <summary>
    /// PHP <c>similar_text</c> percent (Oliver). Used so Close/No thresholds match the legacy QA report.
    /// </summary>
    internal static int SimilarText(string? first, string? second, out double percent)
    {
        var a = first ?? string.Empty;
        var b = second ?? string.Empty;
        var similar = SimilarChar(a.AsSpan(), b.AsSpan());
        percent = a.Length + b.Length == 0 ? 0 : similar * 200.0 / (a.Length + b.Length);
        return similar;
    }

    private static int SimilarChar(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        FindLongestMatch(a, b, out var pos1, out var pos2, out var max);
        if (max == 0)
            return 0;

        var sum = max;
        if (pos1 > 0 && pos2 > 0)
            sum += SimilarChar(a[..pos1], b[..pos2]);
        if (pos1 + max < a.Length && pos2 + max < b.Length)
            sum += SimilarChar(a[(pos1 + max)..], b[(pos2 + max)..]);
        return sum;
    }

    private static void FindLongestMatch(
        ReadOnlySpan<char> a,
        ReadOnlySpan<char> b,
        out int pos1,
        out int pos2,
        out int max)
    {
        pos1 = 0;
        pos2 = 0;
        max = 0;
        for (var i = 0; i < a.Length; i++)
        {
            for (var j = 0; j < b.Length; j++)
            {
                var l = 0;
                while (i + l < a.Length && j + l < b.Length && a[i + l] == b[j + l])
                    l++;
                if (l > max)
                {
                    max = l;
                    pos1 = i;
                    pos2 = j;
                }
            }
        }
    }

    private static ServerQaCompareRow BuildRow(
        QaParameterItem source,
        QaParameterItem target,
        string server1Name,
        string server2Name,
        string match,
        int percent)
        => new()
        {
            Parameter = source.Parameter,
            Key = source.Key,
            Server1Name = server1Name,
            Server1Value = source.Value,
            Server2Name = server2Name,
            Server2Value = target.Value,
            Match = match,
            MatchPercent = percent
        };

    private static string Pk(QaParameterItem item) => item.Parameter + "\u001f" + item.Key;
    private static string Pkv(QaParameterItem item) => Pk(item) + "\u001f" + item.Value;

    private static string? Num(int? value) => value?.ToString();
    private static string? Num(long? value) => value?.ToString();
    private static string? Dec(decimal? value) => value?.ToString("0.##");
}

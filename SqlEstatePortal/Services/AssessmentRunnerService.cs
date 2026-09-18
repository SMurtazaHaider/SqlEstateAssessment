using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;
using SqlEstatePortal.Services;

namespace SqlEstatePortal.Services;

public class AssessmentOptions
{
    public string ScriptPath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int SampleSeconds { get; set; } = 2;
    public bool TrustServerCertificate { get; set; } = true;
}

public class AssessmentRunnerService
{
    private readonly AppDbContext _db;
    private readonly AssessmentOptions _options;
    private readonly InventorySyncService _inventorySync;
    private readonly ILogger<AssessmentRunnerService> _logger;

    public AssessmentRunnerService(
        AppDbContext db,
        IOptions<AssessmentOptions> options,
        InventorySyncService inventorySync,
        ILogger<AssessmentRunnerService> logger)
    {
        _db = db;
        _options = options.Value;
        _inventorySync = inventorySync;
        _logger = logger;
    }

    public async Task<AssessmentRun> RunAsync(
        string? triggeredBy,
        IReadOnlyList<string>? selectedServers = null,
        CancellationToken cancellationToken = default)
    {
        var reachable = await _db.CtServers
            .Where(x => x.ServerStatus == ServerReachabilityService.StatusReachable &&
                       (x.ServerType == "SQL Servers" || x.ServerType == "SQL" || (string.IsNullOrEmpty(x.ServerType) && x.ServerName.Contains("SQL"))))
            .OrderBy(x => x.ServerName)
            .Select(x => x.ServerName)
            .ToListAsync(cancellationToken);

        List<string> servers;
        if (selectedServers == null || selectedServers.Count == 0)
        {
            servers = reachable;
        }
        else
        {
            var reachableSet = reachable.ToHashSet(StringComparer.OrdinalIgnoreCase);
            servers = selectedServers
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(reachableSet.Contains)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var run = new AssessmentRun
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TriggeredBy = triggeredBy,
            ServerListPath = servers.Count == 0 ? null : string.Join("; ", servers)
        };
        _db.AssessmentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        string? tempListPath = null;
        try
        {
            if (!File.Exists(_options.ScriptPath))
                throw new FileNotFoundException("Assessment script not found.", _options.ScriptPath);
            if (reachable.Count == 0)
                throw new InvalidOperationException(
                    "No Reachable servers found. Open Servers and run Check Server Status first.");
            if (servers.Count == 0)
                throw new InvalidOperationException(
                    "Select at least one Reachable server to assess.");

            tempListPath = Path.Combine(
                Path.GetTempPath(),
                $"sql-estate-servers-{run.Id}-{Guid.NewGuid():N}.txt");
            await File.WriteAllLinesAsync(tempListPath, servers, cancellationToken);

            var args = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", Quote(_options.ScriptPath),
                "-ServerListPath", Quote(tempListPath),
                "-SampleSeconds", _options.SampleSeconds.ToString()
            };
            if (_options.TrustServerCertificate)
                args.Add("-TrustServerCertificate");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = string.Join(" ", args),
                WorkingDirectory = string.IsNullOrWhiteSpace(_options.WorkingDirectory)
                    ? Path.GetDirectoryName(_options.ScriptPath)!
                    : _options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            run.OutputLog = (stdout + Environment.NewLine + stderr).Trim();

            if (process.ExitCode != 0)
            {
                run.Status = "Failed";
                run.ErrorMessage = string.IsNullOrWhiteSpace(stderr) ? $"PowerShell exited with code {process.ExitCode}." : stderr;
                run.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return run;
            }

            var jsonPath = ExtractPath(stdout, "JSON:");
            var htmlPath = ExtractPath(stdout, "HTML:");
            run.ReportJsonPath = jsonPath;
            run.ReportHtmlPath = htmlPath;

            if (!string.IsNullOrWhiteSpace(jsonPath) && File.Exists(jsonPath))
                await ImportJsonAsync(run, jsonPath, htmlPath, cancellationToken);
            else
            {
                run.Status = "Failed";
                run.ErrorMessage = "Assessment finished but JSON report path was not found.";
            }

            if (run.Status == "Running")
                run.Status = "Succeeded";

            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            if (run.Status == "Succeeded")
            {
                try
                {
                    await _inventorySync.AutoSyncIfEnabledAsync(run.Id, triggeredBy ?? "System (AutoDirect)", cancellationToken);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Auto-direct inventory sync failed for assessment #{RunId}", run.Id);
                }
            }

            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assessment run {RunId} failed", run.Id);
            run.Status = "Failed";
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempListPath) && File.Exists(tempListPath))
            {
                try { File.Delete(tempListPath); } catch { /* ignore */ }
            }
        }
    }

    public async Task<AssessmentRun> ImportFileAsync(
        string jsonPath,
        string? triggeredBy,
        string? htmlPath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.AssessmentRuns
            .Include(x => x.Databases)
            .FirstOrDefaultAsync(x => x.ReportJsonPath == jsonPath, cancellationToken);

        if (existing != null && !force && existing.Databases.Count > 0)
            return existing;

        if (existing != null)
        {
            _db.AssessmentRuns.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var run = new AssessmentRun
        {
            StartedAt = DateTime.UtcNow,
            Status = "Succeeded",
            TriggeredBy = triggeredBy,
            ReportJsonPath = jsonPath,
            ReportHtmlPath = htmlPath
        };
        _db.AssessmentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await ImportJsonAsync(run, jsonPath, htmlPath, cancellationToken);
        run.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (run.Status == "Succeeded")
        {
            try
            {
                await _inventorySync.AutoSyncIfEnabledAsync(run.Id, triggeredBy ?? "System (AutoDirect)", cancellationToken);
            }
            catch (Exception syncEx)
            {
                _logger.LogWarning(syncEx, "Auto-direct inventory sync failed for assessment #{RunId}", run.Id);
            }
        }

        return run;
    }

    private async Task ImportJsonAsync(AssessmentRun run, string jsonPath, string? htmlPath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!string.IsNullOrWhiteSpace(htmlPath) && File.Exists(htmlPath))
            run.HtmlContent = await File.ReadAllTextAsync(htmlPath, cancellationToken);

        if (root.TryGetProperty("ExecutiveSummary", out var summary))
        {
            run.ServerCount = GetInt(summary, "ServerCount");
            run.ReachableCount = GetInt(summary, "ReachableCount");
            run.UnreachableCount = GetInt(summary, "UnreachableCount");
            run.EndOfSupportCount = GetInt(summary, "EndOfSupportCount");
            run.AllocatedStorageGb = GetDecimal(summary, "AllocatedStorageGB") ?? 0;
            run.EstimatedLicensedCores = GetInt(summary, "EstimatedLicensedCores");
            var generated = GetDate(summary, "GeneratedUtc") ?? GetDate(summary, "GeneratedLocal");
            if (generated.HasValue)
                run.StartedAt = generated.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(generated.Value, DateTimeKind.Utc)
                    : generated.Value.ToUniversalTime();

            if (summary.TryGetProperty("FindingCounts", out var counts))
            {
                run.CriticalCount = GetInt(counts, "Critical");
                run.HighCount = GetInt(counts, "High");
                run.MediumCount = GetInt(counts, "Medium");
                run.LowCount = GetInt(counts, "Low");
                run.InfoCount = GetInt(counts, "Info");
            }
        }

        foreach (var f in Enumerate(root, "Findings"))
        {
            run.Findings.Add(new AssessmentFinding
            {
                ServerName = GetString(f, "Server"),
                Severity = GetString(f, "Severity"),
                Area = GetString(f, "Area"),
                Finding = GetString(f, "Finding"),
                Recommendation = GetString(f, "Recommendation")
            });
        }

        if (run.InfoCount == 0)
            run.InfoCount = run.Findings.Count(x => string.Equals(x.Severity, "Info", StringComparison.OrdinalIgnoreCase));

        foreach (var s in Enumerate(root, "Servers"))
        {
            var support = Obj(s, "Support");
            var instance = Obj(s, "Instance");
            var cost = Obj(s, "Cost");
            var perf = Obj(s, "Performance");
            var host = Enumerate(s, "Host").FirstOrDefault();
            var serverName = GetString(s, "Server");

            run.Servers.Add(new AssessmentServerSnapshot
            {
                ServerName = serverName,
                Reachable = GetBool(s, "Reachable"),
                Product = support.ValueKind == JsonValueKind.Object ? GetString(support, "Product") : null,
                SupportStatus = support.ValueKind == JsonValueKind.Object ? GetString(support, "Status") : null,
                Edition = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "Edition") : null,
                Version = instance.ValueKind == JsonValueKind.Object
                    ? $"{GetString(instance, "ProductVersion")} {GetString(instance, "ProductLevel")} {GetString(instance, "ProductUpdateLevel")}".Trim()
                    : null,
                CpuCount = instance.ValueKind == JsonValueKind.Object ? GetIntOrNull(instance, "CpuCount") : GetIntOrNull(cost, "CpuCount"),
                MemoryMb = instance.ValueKind == JsonValueKind.Object ? GetDecimal(instance, "PhysicalMemoryMB") : null,
                AllocatedGb = cost.ValueKind == JsonValueKind.Object ? GetDecimal(cost, "AllocatedDataAndLogGB") : null,
                UserDatabaseCount = cost.ValueKind == JsonValueKind.Object ? GetIntOrNull(cost, "UserDatabaseCount") : null,
                StartedAt = instance.ValueKind == JsonValueKind.Object ? GetDate(instance, "SqlServerStartTime") : null,
                UserConnections = perf.ValueKind == JsonValueKind.Object ? GetIntOrNull(perf, "UserConnections") : null,
                PageLifeExpectancySec = perf.ValueKind == JsonValueKind.Object ? GetIntOrNull(perf, "PageLifeExpectancySec") : null,
                BatchRequestsPerSec = perf.ValueKind == JsonValueKind.Object ? GetDecimal(perf, "BatchRequestsPerSec") : null,
                HostPlatform = host.ValueKind == JsonValueKind.Object ? GetString(host, "host_platform") : null,
                HostDistribution = host.ValueKind == JsonValueKind.Object ? GetString(host, "host_distribution") : null,
                Collation = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "Collation") : null,
                VirtualMachineType = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "VirtualMachineType") : null,
                LicenseType = cost.ValueKind == JsonValueKind.Object ? GetString(cost, "LicenseType") : null,
                Error = NullIfEmpty(GetString(s, "Error"))
            });

            foreach (var db in Enumerate(s, "Databases"))
            {
                run.Databases.Add(new AssessmentDatabase
                {
                    ServerName = serverName,
                    Name = GetString(db, "name"),
                    State = GetString(db, "state_desc"),
                    RecoveryModel = GetString(db, "recovery_model_desc"),
                    CompatibilityLevel = GetIntOrNull(db, "compatibility_level"),
                    PageVerify = GetString(db, "page_verify_option_desc"),
                    IsEncrypted = GetBool(db, "is_encrypted"),
                    DataMb = GetDecimal(db, "DataMB"),
                    LogMb = GetDecimal(db, "LogMB"),
                    OwnerName = GetString(db, "owner_name"),
                    LastGoodCheckDbTime = SanitizeCheckDb(GetDate(db, "LastGoodCheckDbTime")),
                    CollationName = NullIfEmpty(GetString(db, "collation_name")),
                    CreationDate = GetDate(db, "create_date")
                });
            }

            foreach (var v in Enumerate(s, "Volumes"))
            {
                run.Volumes.Add(new AssessmentVolume
                {
                    ServerName = serverName,
                    MountPoint = GetString(v, "volume_mount_point"),
                    LogicalName = GetString(v, "logical_volume_name"),
                    TotalGb = GetDecimal(v, "TotalGB"),
                    FreeGb = GetDecimal(v, "FreeGB"),
                    FreePct = GetDecimal(v, "FreePct")
                });
            }

            foreach (var svc in Enumerate(s, "Services"))
            {
                run.Services.Add(new AssessmentService
                {
                    ServerName = serverName,
                    ServiceName = GetString(svc, "servicename"),
                    StartupType = GetString(svc, "startup_type_desc"),
                    Status = GetString(svc, "status_desc"),
                    ServiceAccount = GetString(svc, "service_account"),
                    InstantFileInitialization = GetString(svc, "instant_file_initialization_enabled")
                });
            }

            foreach (var w in Enumerate(s, "Waits"))
            {
                run.Waits.Add(new AssessmentWait
                {
                    ServerName = serverName,
                    WaitType = GetString(w, "wait_type"),
                    WaitingTasks = GetLong(w, "waiting_tasks_count"),
                    WaitTimeMs = GetLong(w, "wait_time_ms"),
                    SignalWaitTimeMs = GetLong(w, "signal_wait_time_ms"),
                    WaitPct = GetDecimal(w, "WaitPct")
                });
            }

            foreach (var job in Enumerate(s, "Jobs"))
            {
                run.Jobs.Add(new AssessmentJob
                {
                    ServerName = serverName,
                    JobName = GetString(job, "JobName"),
                    Enabled = GetBool(job, "enabled") || GetInt(job, "enabled") == 1,
                    LastRunStatus = GetString(job, "LastRunStatus"),
                    LastRun = GetDate(job, "LastRun"),
                    Message = NullIfEmpty(GetString(job, "Message"))
                });
            }

            foreach (var admin in Enumerate(s, "Sysadmins"))
            {
                run.Sysadmins.Add(new AssessmentSysadmin
                {
                    ServerName = serverName,
                    Name = GetString(admin, "name"),
                    TypeDesc = GetString(admin, "type_desc"),
                    IsDisabled = GetBool(admin, "is_disabled"),
                    CreateDate = GetDate(admin, "create_date")
                });
            }

            foreach (var cfg in Enumerate(s, "Configuration"))
            {
                run.Configurations.Add(new AssessmentConfiguration
                {
                    ServerName = serverName,
                    Name = GetString(cfg, "name"),
                    Minimum = GetLongOrNull(cfg, "minimum"),
                    Maximum = GetLongOrNull(cfg, "maximum"),
                    ConfigValue = GetLongOrNull(cfg, "config_value") ?? GetLongOrNull(cfg, "value"),
                    RunValue = GetLongOrNull(cfg, "run_value") ?? GetLongOrNull(cfg, "value_in_use"),
                    Description = NullIfEmpty(GetString(cfg, "description")),
                    IsDynamic = GetBool(cfg, "is_dynamic"),
                    IsAdvanced = GetBool(cfg, "is_advanced")
                });
            }

            foreach (var b in Enumerate(s, "Backups"))
            {
                var dbName = FirstNonEmpty(GetString(b, "DatabaseName"), GetString(b, "database_name"));
                if (string.IsNullOrWhiteSpace(dbName))
                    continue;

                run.Backups.Add(new AssessmentBackup
                {
                    ServerName = serverName,
                    DatabaseName = dbName,
                    LastFullBackup = GetDate(b, "LastFullBackup") ?? GetDate(b, "LastFull"),
                    LastDifferentialBackup = GetDate(b, "LastDifferentialBackup") ?? GetDate(b, "LastDiff"),
                    LastLogBackup = GetDate(b, "LastLogBackup") ?? GetDate(b, "LastLog")
                });
            }

            foreach (var ls in Enumerate(s, "LinkedServers"))
            {
                var linkedName = FirstNonEmpty(GetString(ls, "name"), GetString(ls, "Name"));
                if (string.IsNullOrWhiteSpace(linkedName))
                    continue;

                run.LinkedServers.Add(new AssessmentLinkedServer
                {
                    ServerName = serverName,
                    LinkedServerName = linkedName,
                    DataSource = NullIfEmpty(GetString(ls, "data_source")),
                    Provider = NullIfEmpty(GetString(ls, "provider")),
                    IsRemoteLoginEnabled = GetBool(ls, "is_remote_login_enabled"),
                    IsRpcOutEnabled = GetBool(ls, "is_rpc_out_enabled")
                });
            }

            foreach (var login in Enumerate(s, "SqlLogins"))
            {
                var loginName = FirstNonEmpty(GetString(login, "name"), GetString(login, "Name"));
                if (string.IsNullOrWhiteSpace(loginName))
                    continue;

                run.SqlLogins.Add(new AssessmentSqlLogin
                {
                    ServerName = serverName,
                    LoginName = loginName,
                    IsDisabled = GetBool(login, "is_disabled"),
                    IsPolicyChecked = GetBool(login, "is_policy_checked"),
                    IsExpirationChecked = GetBool(login, "is_expiration_checked"),
                    IsSysadmin = GetBool(login, "is_sysadmin") || GetInt(login, "is_sysadmin") == 1,
                    CreateDate = GetDate(login, "create_date"),
                    ModifyDate = GetDate(login, "modify_date")
                });
            }

            foreach (var ag in Enumerate(s, "AvailabilityGroups"))
            {
                var agName = FirstNonEmpty(GetString(ag, "AgName"), GetString(ag, "ag_name"));
                if (string.IsNullOrWhiteSpace(agName))
                    continue;

                run.AvailabilityGroups.Add(new AssessmentAvailabilityGroup
                {
                    ServerName = serverName,
                    AgName = agName,
                    ReplicaServerName = NullIfEmpty(GetString(ag, "replica_server_name")),
                    RoleDesc = NullIfEmpty(GetString(ag, "role_desc")),
                    OperationalStateDesc = NullIfEmpty(GetString(ag, "operational_state_desc")),
                    ConnectedStateDesc = NullIfEmpty(GetString(ag, "connected_state_desc")),
                    SynchronizationHealthDesc = NullIfEmpty(GetString(ag, "synchronization_health_desc"))
                });
            }

            foreach (var cert in Enumerate(s, "Certificates"))
            {
                var certName = FirstNonEmpty(GetString(cert, "certificate_name"), GetString(cert, "CertificateName"));
                if (string.IsNullOrWhiteSpace(certName))
                    continue;

                run.Certificates.Add(new AssessmentCertificate
                {
                    ServerName = serverName,
                    DatabaseName = NullIfEmpty(GetString(cert, "database_name")),
                    CertificateName = certName,
                    Subject = NullIfEmpty(GetString(cert, "subject")),
                    IssuerName = NullIfEmpty(GetString(cert, "issuer_name")),
                    StartDate = GetDate(cert, "start_date"),
                    ExpiryDate = GetDate(cert, "expiry_date"),
                    DaysToExpiry = GetIntOrNull(cert, "days_to_expiry"),
                    PrivateKeyEncryption = NullIfEmpty(GetString(cert, "pvt_key_encryption")),
                    Thumbprint = NullIfEmpty(GetString(cert, "thumbprint")),
                    ProtectedDatabases = NullIfEmpty(GetString(cert, "protected_databases"))
                });
            }

            var tls = Obj(s, "TlsCertificate");
            if (tls.ValueKind == JsonValueKind.Object)
            {
                run.TlsCertificates.Add(new AssessmentTlsCertificate
                {
                    ServerName = serverName,
                    InstanceName = NullIfEmpty(GetString(tls, "instance_name")),
                    Thumbprint = NullIfEmpty(GetString(tls, "thumbprint")),
                    CertificateSource = NullIfEmpty(GetString(tls, "certificate_source")),
                    ForceEncryption = GetBool(tls, "force_encryption") || GetInt(tls, "force_encryption") == 1
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<JsonElement> Enumerate(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return [];
        if (el.ValueKind == JsonValueKind.Array)
            return el.EnumerateArray();
        if (el.ValueKind == JsonValueKind.Object)
            return [el];
        return [];
    }

    private static JsonElement Obj(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var el) ? el : default;

    private static string Quote(string value) => $"\"{value}\"";

    private static string? ExtractPath(string output, string marker)
    {
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return trimmed[marker.Length..].Trim();
        }
        return null;
    }

    private static int GetInt(JsonElement el, string name)
        => GetIntOrNull(el, name) ?? 0;

    private static int? GetIntOrNull(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetInt32(out var i)) return i;
        if (p.TryGetDecimal(out var d)) return (int)d;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static long GetLong(JsonElement el, string name)
        => GetLongOrNull(el, name) ?? 0;

    private static long? GetLongOrNull(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetInt64(out var i)) return i;
        if (p.TryGetDecimal(out var d)) return (long)d;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static decimal? GetDecimal(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static bool GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => p.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => bool.TryParse(p.GetString(), out var b) && b,
            _ => false
        };
    }

    private static string GetString(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return string.Empty;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return string.Empty;
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? string.Empty) : p.ToString();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return string.Empty;
    }

    private static DateTime? SanitizeCheckDb(DateTime? value)
        => value is { Year: < 1990 } ? null : value;

    private static DateTime? GetDate(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.ValueKind == JsonValueKind.String)
            return ParsePsDate(p.GetString());
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("value", out var inner))
            return ParsePsDate(inner.GetString());
        return null;
    }

    private static DateTime? ParsePsDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, out var dt)) return dt;
        var start = raw.IndexOf("/Date(", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var from = start + 6;
        var to = raw.IndexOf(')', from);
        if (to < 0) return null;
        var token = raw[from..to];
        var i = token.StartsWith('-') ? 1 : 0;
        while (i < token.Length && char.IsDigit(token[i])) i++;
        if (!long.TryParse(token[..i], out var ms)) return null;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        }
        catch
        {
            return null;
        }
    }
}

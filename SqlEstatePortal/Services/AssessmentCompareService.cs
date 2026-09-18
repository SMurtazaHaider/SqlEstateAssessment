using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Services;

public class AssessmentCompareService
{
    public AssessmentCompareViewModel Compare(AssessmentRun baseRun, AssessmentRun targetRun, List<AssessmentRunSummary> availableRuns)
    {
        var vm = new AssessmentCompareViewModel
        {
            BaseRunId = baseRun.Id,
            TargetRunId = targetRun.Id,
            BaseRun = baseRun,
            TargetRun = targetRun,
            AvailableRuns = availableRuns
        };

        // 1. KPIs
        vm.Kpis = BuildKpiComparison(baseRun, targetRun);

        // 2. Findings Diff
        vm.FindingsDiff = BuildFindingsDiff(baseRun, targetRun);
        vm.NewFindingsCount = vm.FindingsDiff.Count(f => f.Status == "New");
        vm.ResolvedFindingsCount = vm.FindingsDiff.Count(f => f.Status == "Resolved");
        vm.OngoingFindingsCount = vm.FindingsDiff.Count(f => f.Status == "Ongoing");

        // 3. Servers Diff
        vm.ServersDiff = BuildServersDiff(baseRun, targetRun);
        vm.NewServersCount = vm.ServersDiff.Count(s => s.Status == "New");
        vm.RemovedServersCount = vm.ServersDiff.Count(s => s.Status == "Removed");
        vm.ChangedServersCount = vm.ServersDiff.Count(s => s.Status == "Changed");
        vm.UnchangedServersCount = vm.ServersDiff.Count(s => s.Status == "Unchanged");

        // 4. Databases Diff
        vm.DatabasesDiff = BuildDatabasesDiff(baseRun, targetRun);
        vm.NewDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "New");
        vm.RemovedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Removed");
        vm.ChangedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Changed");
        vm.UnchangedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Unchanged");

        // 5. Backups Diff
        vm.BackupsDiff = BuildBackupsDiff(baseRun, targetRun);

        // 6. Configurations Diff
        vm.ConfigsDiff = BuildConfigsDiff(baseRun, targetRun);
        vm.ChangedConfigsCount = vm.ConfigsDiff.Count;

        // 7. Linked Servers Diff
        vm.LinkedServersDiff = BuildLinkedServersDiff(baseRun, targetRun);
        vm.NewLinkedServersCount = vm.LinkedServersDiff.Count(l => l.Status == "New");
        vm.RemovedLinkedServersCount = vm.LinkedServersDiff.Count(l => l.Status == "Removed");
        vm.ChangedLinkedServersCount = vm.LinkedServersDiff.Count(l => l.Status == "Changed");

        // 8. SQL Logins Diff
        vm.SqlLoginsDiff = BuildSqlLoginsDiff(baseRun, targetRun);
        vm.NewSqlLoginsCount = vm.SqlLoginsDiff.Count(l => l.Status == "New");
        vm.RemovedSqlLoginsCount = vm.SqlLoginsDiff.Count(l => l.Status == "Removed");
        vm.ChangedSqlLoginsCount = vm.SqlLoginsDiff.Count(l => l.Status == "Changed");

        // 9. Availability Groups Diff
        vm.AvailabilityGroupsDiff = BuildAvailabilityGroupsDiff(baseRun, targetRun);
        vm.NewAvailabilityGroupsCount = vm.AvailabilityGroupsDiff.Count(g => g.Status == "New");
        vm.RemovedAvailabilityGroupsCount = vm.AvailabilityGroupsDiff.Count(g => g.Status == "Removed");
        vm.ChangedAvailabilityGroupsCount = vm.AvailabilityGroupsDiff.Count(g => g.Status == "Changed");

        // 10. Certificates Diff
        vm.CertificatesDiff = BuildCertificatesDiff(baseRun, targetRun);
        vm.NewCertificatesCount = vm.CertificatesDiff.Count(c => c.Status == "New");
        vm.RemovedCertificatesCount = vm.CertificatesDiff.Count(c => c.Status == "Removed");
        vm.ChangedCertificatesCount = vm.CertificatesDiff.Count(c => c.Status == "Changed");
        vm.ExpiringCertificatesCount = vm.CertificatesDiff.Count(c => c.IsExpiring);

        return vm;
    }

    private static List<CompareKpiRow> BuildKpiComparison(AssessmentRun a, AssessmentRun b)
    {
        var list = new List<CompareKpiRow>();

        void AddIntKpi(string category, string name, int valA, int valB, bool higherIsBetter = false, bool isNeutral = false)
        {
            var delta = valB - valA;
            var deltaStr = delta > 0 ? $"+{delta}" : delta.ToString();
            string status;
            if (isNeutral || delta == 0)
            {
                status = delta == 0 ? "unchanged" : "neutral";
            }
            else if (higherIsBetter)
            {
                status = delta > 0 ? "improved" : "degraded";
            }
            else
            {
                status = delta < 0 ? "improved" : "degraded";
            }

            list.Add(new CompareKpiRow
            {
                Category = category,
                MetricName = name,
                BaseValue = valA.ToString("N0"),
                TargetValue = valB.ToString("N0"),
                Delta = deltaStr,
                Status = status
            });
        }

        void AddDecimalKpi(string category, string name, decimal valA, decimal valB, string format = "N2")
        {
            var delta = valB - valA;
            var deltaStr = delta > 0 ? $"+{delta.ToString(format)}" : delta.ToString(format);
            list.Add(new CompareKpiRow
            {
                Category = category,
                MetricName = name,
                BaseValue = valA.ToString(format),
                TargetValue = valB.ToString(format),
                Delta = deltaStr,
                Status = delta == 0 ? "unchanged" : "neutral"
            });
        }

        // Infrastructure
        AddIntKpi("Infrastructure", "Total Servers Assessed", a.ServerCount, b.ServerCount, isNeutral: true);
        AddIntKpi("Infrastructure", "Reachable Servers", a.ReachableCount, b.ReachableCount, higherIsBetter: true);
        AddIntKpi("Infrastructure", "Unreachable Servers", a.UnreachableCount, b.UnreachableCount, higherIsBetter: false);
        AddIntKpi("Infrastructure", "End of Support Servers", a.EndOfSupportCount, b.EndOfSupportCount, higherIsBetter: false);
        AddDecimalKpi("Infrastructure", "Allocated Storage (GB)", a.AllocatedStorageGb, b.AllocatedStorageGb);
        AddIntKpi("Infrastructure", "Estimated Licensed Cores", a.EstimatedLicensedCores, b.EstimatedLicensedCores, isNeutral: true);
        AddIntKpi("Infrastructure", "Databases Assessed", a.Databases.Count, b.Databases.Count, isNeutral: true);

        // Risk & Findings
        AddIntKpi("Risk & Findings", "Critical Findings", a.CriticalCount, b.CriticalCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "High Findings", a.HighCount, b.HighCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Medium Findings", a.MediumCount, b.MediumCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Low Findings", a.LowCount, b.LowCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Total Findings", a.Findings.Count, b.Findings.Count, higherIsBetter: false);

        return list;
    }

    private static List<CompareFindingRow> BuildFindingsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentFinding f) => $"{f.ServerName.Trim()}|{f.Area.Trim()}|{f.Finding.Trim()}".ToLowerInvariant();

        var baseDict = a.Findings
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Findings
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareFindingRow>();

        // New in Target
        foreach (var kvp in targetDict)
        {
            if (!baseDict.ContainsKey(kvp.Key))
            {
                var tf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "New",
                    Severity = tf.Severity,
                    ServerName = tf.ServerName,
                    Area = tf.Area,
                    Finding = tf.Finding,
                    Recommendation = tf.Recommendation
                });
            }
        }

        // Resolved (in Base, not in Target)
        foreach (var kvp in baseDict)
        {
            if (!targetDict.ContainsKey(kvp.Key))
            {
                var bf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "Resolved",
                    Severity = bf.Severity,
                    ServerName = bf.ServerName,
                    Area = bf.Area,
                    Finding = bf.Finding,
                    Recommendation = bf.Recommendation
                });
            }
        }

        // Ongoing (in both)
        foreach (var kvp in targetDict)
        {
            if (baseDict.ContainsKey(kvp.Key))
            {
                var tf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "Ongoing",
                    Severity = tf.Severity,
                    ServerName = tf.ServerName,
                    Area = tf.Area,
                    Finding = tf.Finding,
                    Recommendation = tf.Recommendation
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Resolved" ? 1 : 2;
        int SeverityOrder(string s) => s.ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
        };

        return result
            .OrderBy(r => StatusOrder(r.Status))
            .ThenBy(r => SeverityOrder(r.Severity))
            .ThenBy(r => r.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CompareServerRow> BuildServersDiff(AssessmentRun a, AssessmentRun b)
    {
        var baseDict = a.Servers
            .GroupBy(s => s.ServerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var targetDict = b.Servers
            .GroupBy(s => s.ServerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allServerNames = baseDict.Keys.Union(targetDict.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        var result = new List<CompareServerRow>();

        foreach (var name in allServerNames)
        {
            baseDict.TryGetValue(name, out var bs);
            targetDict.TryGetValue(name, out var ts);

            if (bs == null && ts != null)
            {
                result.Add(new CompareServerRow
                {
                    Status = "New",
                    ServerName = ts.ServerName,
                    TargetReachable = ts.Reachable,
                    TargetEdition = ts.Edition,
                    TargetVersion = ts.Version,
                    TargetSupportStatus = ts.SupportStatus,
                    TargetCpuCount = ts.CpuCount,
                    TargetMemoryMb = ts.MemoryMb,
                    TargetAllocatedGb = ts.AllocatedGb,
                    TargetUserDbCount = ts.UserDatabaseCount
                });
            }
            else if (bs != null && ts == null)
            {
                result.Add(new CompareServerRow
                {
                    Status = "Removed",
                    ServerName = bs.ServerName,
                    BaseReachable = bs.Reachable,
                    BaseEdition = bs.Edition,
                    BaseVersion = bs.Version,
                    BaseSupportStatus = bs.SupportStatus,
                    BaseCpuCount = bs.CpuCount,
                    BaseMemoryMb = bs.MemoryMb,
                    BaseAllocatedGb = bs.AllocatedGb,
                    BaseUserDbCount = bs.UserDatabaseCount
                });
            }
            else if (bs != null && ts != null)
            {
                var changes = new List<string>();
                if (bs.Reachable != ts.Reachable)
                    changes.Add($"Reachable: {(bs.Reachable ? "Yes" : "No")} \u2192 {(ts.Reachable ? "Yes" : "No")}");
                if (!string.Equals(bs.Edition, ts.Edition, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.Edition) || !string.IsNullOrWhiteSpace(ts.Edition)))
                    changes.Add($"Edition: {bs.Edition ?? "—"} \u2192 {ts.Edition ?? "—"}");
                if (!string.Equals(bs.Version, ts.Version, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.Version) || !string.IsNullOrWhiteSpace(ts.Version)))
                    changes.Add($"Version: {bs.Version ?? "—"} \u2192 {ts.Version ?? "—"}");
                if (!string.Equals(bs.SupportStatus, ts.SupportStatus, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.SupportStatus) || !string.IsNullOrWhiteSpace(ts.SupportStatus)))
                    changes.Add($"Support: {bs.SupportStatus ?? "—"} \u2192 {ts.SupportStatus ?? "—"}");
                if (bs.CpuCount != ts.CpuCount && (bs.CpuCount != null || ts.CpuCount != null))
                    changes.Add($"CPU Cores: {bs.CpuCount?.ToString() ?? "—"} \u2192 {ts.CpuCount?.ToString() ?? "—"}");
                if (bs.MemoryMb != ts.MemoryMb && (bs.MemoryMb != null || ts.MemoryMb != null))
                    changes.Add($"Memory MB: {bs.MemoryMb?.ToString("N0") ?? "—"} \u2192 {ts.MemoryMb?.ToString("N0") ?? "—"}");
                if (bs.AllocatedGb != ts.AllocatedGb && (bs.AllocatedGb != null || ts.AllocatedGb != null))
                    changes.Add($"Allocated GB: {bs.AllocatedGb?.ToString("N2") ?? "—"} \u2192 {ts.AllocatedGb?.ToString("N2") ?? "—"}");
                if (bs.UserDatabaseCount != ts.UserDatabaseCount && (bs.UserDatabaseCount != null || ts.UserDatabaseCount != null))
                    changes.Add($"Databases: {bs.UserDatabaseCount?.ToString() ?? "—"} \u2192 {ts.UserDatabaseCount?.ToString() ?? "—"}");

                var status = changes.Count > 0 ? "Changed" : "Unchanged";

                result.Add(new CompareServerRow
                {
                    Status = status,
                    ServerName = ts.ServerName,
                    BaseReachable = bs.Reachable,
                    TargetReachable = ts.Reachable,
                    BaseEdition = bs.Edition,
                    TargetEdition = ts.Edition,
                    BaseVersion = bs.Version,
                    TargetVersion = ts.Version,
                    BaseSupportStatus = bs.SupportStatus,
                    TargetSupportStatus = ts.SupportStatus,
                    BaseCpuCount = bs.CpuCount,
                    TargetCpuCount = ts.CpuCount,
                    BaseMemoryMb = bs.MemoryMb,
                    TargetMemoryMb = ts.MemoryMb,
                    BaseAllocatedGb = bs.AllocatedGb,
                    TargetAllocatedGb = ts.AllocatedGb,
                    BaseUserDbCount = bs.UserDatabaseCount,
                    TargetUserDbCount = ts.UserDatabaseCount,
                    Changes = changes
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Removed" ? 1 : s == "Changed" ? 2 : 3;
        return result.OrderBy(r => StatusOrder(r.Status)).ThenBy(r => r.ServerName).ToList();
    }

    private static List<CompareDatabaseRow> BuildDatabasesDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentDatabase d) => $"{d.ServerName.Trim()}|{d.Name.Trim()}".ToLowerInvariant();

        var baseDict = a.Databases
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Databases
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var allKeys = baseDict.Keys.Union(targetDict.Keys).ToList();
        var result = new List<CompareDatabaseRow>();

        foreach (var key in allKeys)
        {
            baseDict.TryGetValue(key, out var bd);
            targetDict.TryGetValue(key, out var td);

            if (bd == null && td != null)
            {
                result.Add(new CompareDatabaseRow
                {
                    Status = "New",
                    ServerName = td.ServerName,
                    DatabaseName = td.Name,
                    TargetState = td.State,
                    TargetRecoveryModel = td.RecoveryModel,
                    TargetCompatLevel = td.CompatibilityLevel,
                    TargetDataMb = td.DataMb,
                    TargetLogMb = td.LogMb
                });
            }
            else if (bd != null && td == null)
            {
                result.Add(new CompareDatabaseRow
                {
                    Status = "Removed",
                    ServerName = bd.ServerName,
                    DatabaseName = bd.Name,
                    BaseState = bd.State,
                    BaseRecoveryModel = bd.RecoveryModel,
                    BaseCompatLevel = bd.CompatibilityLevel,
                    BaseDataMb = bd.DataMb,
                    BaseLogMb = bd.LogMb
                });
            }
            else if (bd != null && td != null)
            {
                var changes = new List<string>();
                if (!string.Equals(bd.State, td.State, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bd.State) || !string.IsNullOrWhiteSpace(td.State)))
                    changes.Add($"State: {bd.State ?? "—"} \u2192 {td.State ?? "—"}");
                if (!string.Equals(bd.RecoveryModel, td.RecoveryModel, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bd.RecoveryModel) || !string.IsNullOrWhiteSpace(td.RecoveryModel)))
                    changes.Add($"Recovery: {bd.RecoveryModel ?? "—"} \u2192 {td.RecoveryModel ?? "—"}");
                if (bd.CompatibilityLevel != td.CompatibilityLevel && (bd.CompatibilityLevel != null || td.CompatibilityLevel != null))
                    changes.Add($"Compat: {bd.CompatibilityLevel?.ToString() ?? "—"} \u2192 {td.CompatibilityLevel?.ToString() ?? "—"}");

                var baseTotalMb = (bd.DataMb ?? 0) + (bd.LogMb ?? 0);
                var targetTotalMb = (td.DataMb ?? 0) + (td.LogMb ?? 0);
                var sizeDiffMb = targetTotalMb - baseTotalMb;

                if (Math.Abs(sizeDiffMb) >= 1m)
                {
                    var sign = sizeDiffMb > 0 ? "+" : "";
                    changes.Add($"Size: {baseTotalMb:N1} MB \u2192 {targetTotalMb:N1} MB ({sign}{sizeDiffMb:N1} MB)");
                }

                var status = changes.Count > 0 ? "Changed" : "Unchanged";

                result.Add(new CompareDatabaseRow
                {
                    Status = status,
                    ServerName = td.ServerName,
                    DatabaseName = td.Name,
                    BaseState = bd.State,
                    TargetState = td.State,
                    BaseRecoveryModel = bd.RecoveryModel,
                    TargetRecoveryModel = td.RecoveryModel,
                    BaseCompatLevel = bd.CompatibilityLevel,
                    TargetCompatLevel = td.CompatibilityLevel,
                    BaseDataMb = bd.DataMb,
                    TargetDataMb = td.DataMb,
                    BaseLogMb = bd.LogMb,
                    TargetLogMb = td.LogMb,
                    TotalMbDelta = sizeDiffMb,
                    Changes = changes
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Removed" ? 1 : s == "Changed" ? 2 : 3;
        return result.OrderBy(r => StatusOrder(r.Status)).ThenBy(r => r.ServerName).ThenBy(r => r.DatabaseName).ToList();
    }

    private static List<CompareBackupRow> BuildBackupsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentBackup bk) => $"{bk.ServerName.Trim()}|{bk.DatabaseName.Trim()}".ToLowerInvariant();

        var baseDict = a.Backups
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Backups
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var allKeys = baseDict.Keys.Union(targetDict.Keys).ToList();
        var result = new List<CompareBackupRow>();

        foreach (var key in allKeys)
        {
            baseDict.TryGetValue(key, out var bb);
            targetDict.TryGetValue(key, out var tb);

            if (bb == null && tb != null)
            {
                result.Add(new CompareBackupRow
                {
                    Status = "New",
                    ServerName = tb.ServerName,
                    DatabaseName = tb.DatabaseName,
                    TargetLastFullBackup = tb.LastFullBackup,
                    TargetLastDifferentialBackup = tb.LastDifferentialBackup,
                    TargetLastLogBackup = tb.LastLogBackup
                });
            }
            else if (bb != null && tb == null)
            {
                result.Add(new CompareBackupRow
                {
                    Status = "Removed",
                    ServerName = bb.ServerName,
                    DatabaseName = bb.DatabaseName,
                    BaseLastFullBackup = bb.LastFullBackup,
                    BaseLastDifferentialBackup = bb.LastDifferentialBackup,
                    BaseLastLogBackup = bb.LastLogBackup
                });
            }
            else if (bb != null && tb != null)
            {
                var updated = (tb.LastFullBackup > bb.LastFullBackup) ||
                              (tb.LastDifferentialBackup > bb.LastDifferentialBackup) ||
                              (tb.LastLogBackup > bb.LastLogBackup);

                result.Add(new CompareBackupRow
                {
                    Status = updated ? "Updated" : "Stale",
                    ServerName = tb.ServerName,
                    DatabaseName = tb.DatabaseName,
                    BaseLastFullBackup = bb.LastFullBackup,
                    TargetLastFullBackup = tb.LastFullBackup,
                    BaseLastDifferentialBackup = bb.LastDifferentialBackup,
                    TargetLastDifferentialBackup = tb.LastDifferentialBackup,
                    BaseLastLogBackup = bb.LastLogBackup,
                    TargetLastLogBackup = tb.LastLogBackup
                });
            }
        }

        return result.OrderBy(r => r.ServerName).ThenBy(r => r.DatabaseName).ToList();
    }

    private static List<CompareConfigRow> BuildConfigsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentConfiguration c) => $"{c.ServerName.Trim()}|{c.Name.Trim()}".ToLowerInvariant();

        var baseDict = a.Configurations
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Configurations
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareConfigRow>();
        foreach (var kvp in targetDict)
        {
            if (baseDict.TryGetValue(kvp.Key, out var bc))
            {
                var tc = kvp.Value;
                if (bc.RunValue != tc.RunValue)
                {
                    result.Add(new CompareConfigRow
                    {
                        ServerName = tc.ServerName,
                        ConfigName = tc.Name,
                        BaseRunValue = bc.RunValue,
                        TargetRunValue = tc.RunValue,
                        Status = "Changed"
                    });
                }
            }
        }

        return result.OrderBy(r => r.ServerName).ThenBy(r => r.ConfigName).ToList();
    }

    private static List<CompareLinkedServerRow> BuildLinkedServersDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentLinkedServer l) =>
            $"{l.ServerName.Trim()}|{l.LinkedServerName.Trim()}".ToLowerInvariant();

        var baseDict = a.LinkedServers
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.LinkedServers
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareLinkedServerRow>();

        foreach (var kvp in targetDict)
        {
            var tl = kvp.Value;

            if (!baseDict.TryGetValue(kvp.Key, out var bl))
            {
                result.Add(new CompareLinkedServerRow
                {
                    Status = "New",
                    ServerName = tl.ServerName,
                    LinkedServerName = tl.LinkedServerName,
                    TargetDataSource = tl.DataSource,
                    TargetProvider = tl.Provider,
                    TargetRemoteLoginEnabled = tl.IsRemoteLoginEnabled,
                    TargetRpcOutEnabled = tl.IsRpcOutEnabled
                });
                continue;
            }

            var changes = new List<string>();

            if (!string.Equals(bl.DataSource ?? "", tl.DataSource ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add($"Data source: {bl.DataSource ?? "-"} → {tl.DataSource ?? "-"}");

            if (!string.Equals(bl.Provider ?? "", tl.Provider ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add($"Provider: {bl.Provider ?? "-"} → {tl.Provider ?? "-"}");

            if (bl.IsRemoteLoginEnabled != tl.IsRemoteLoginEnabled)
                changes.Add($"Remote login: {(bl.IsRemoteLoginEnabled ? "Yes" : "No")} → {(tl.IsRemoteLoginEnabled ? "Yes" : "No")}");

            if (bl.IsRpcOutEnabled != tl.IsRpcOutEnabled)
                changes.Add($"RPC out: {(bl.IsRpcOutEnabled ? "Yes" : "No")} → {(tl.IsRpcOutEnabled ? "Yes" : "No")}");

            if (changes.Count == 0)
                continue;

            result.Add(new CompareLinkedServerRow
            {
                Status = "Changed",
                ServerName = tl.ServerName,
                LinkedServerName = tl.LinkedServerName,
                BaseDataSource = bl.DataSource,
                TargetDataSource = tl.DataSource,
                BaseProvider = bl.Provider,
                TargetProvider = tl.Provider,
                BaseRemoteLoginEnabled = bl.IsRemoteLoginEnabled,
                TargetRemoteLoginEnabled = tl.IsRemoteLoginEnabled,
                BaseRpcOutEnabled = bl.IsRpcOutEnabled,
                TargetRpcOutEnabled = tl.IsRpcOutEnabled,
                Changes = changes
            });
        }

        foreach (var kvp in baseDict)
        {
            if (targetDict.ContainsKey(kvp.Key))
                continue;

            var bl = kvp.Value;
            result.Add(new CompareLinkedServerRow
            {
                Status = "Removed",
                ServerName = bl.ServerName,
                LinkedServerName = bl.LinkedServerName,
                BaseDataSource = bl.DataSource,
                BaseProvider = bl.Provider,
                BaseRemoteLoginEnabled = bl.IsRemoteLoginEnabled,
                BaseRpcOutEnabled = bl.IsRpcOutEnabled
            });
        }

        return result
            .OrderBy(r => r.ServerName)
            .ThenBy(r => r.LinkedServerName)
            .ToList();
    }

    private static List<CompareSqlLoginRow> BuildSqlLoginsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentSqlLogin l) =>
            $"{l.ServerName.Trim()}|{l.LoginName.Trim()}".ToLowerInvariant();

        static string YesNo(bool v) => v ? "Yes" : "No";

        var baseDict = a.SqlLogins.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var targetDict = b.SqlLogins.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareSqlLoginRow>();

        foreach (var kvp in targetDict)
        {
            var tl = kvp.Value;

            if (!baseDict.TryGetValue(kvp.Key, out var bl))
            {
                result.Add(new CompareSqlLoginRow
                {
                    Status = "New",
                    ServerName = tl.ServerName,
                    LoginName = tl.LoginName,
                    TargetIsDisabled = tl.IsDisabled,
                    TargetIsSysadmin = tl.IsSysadmin,
                    TargetIsPolicyChecked = tl.IsPolicyChecked,
                    TargetIsExpirationChecked = tl.IsExpirationChecked
                });
                continue;
            }

            var changes = new List<string>();

            if (bl.IsDisabled != tl.IsDisabled)
                changes.Add($"Disabled: {YesNo(bl.IsDisabled)} → {YesNo(tl.IsDisabled)}");

            if (bl.IsSysadmin != tl.IsSysadmin)
                changes.Add($"Sysadmin: {YesNo(bl.IsSysadmin)} → {YesNo(tl.IsSysadmin)}");

            if (bl.IsPolicyChecked != tl.IsPolicyChecked)
                changes.Add($"Policy checked: {YesNo(bl.IsPolicyChecked)} → {YesNo(tl.IsPolicyChecked)}");

            if (bl.IsExpirationChecked != tl.IsExpirationChecked)
                changes.Add($"Expiration checked: {YesNo(bl.IsExpirationChecked)} → {YesNo(tl.IsExpirationChecked)}");

            if (changes.Count == 0)
                continue;

            result.Add(new CompareSqlLoginRow
            {
                Status = "Changed",
                ServerName = tl.ServerName,
                LoginName = tl.LoginName,
                BaseIsDisabled = bl.IsDisabled,
                TargetIsDisabled = tl.IsDisabled,
                BaseIsSysadmin = bl.IsSysadmin,
                TargetIsSysadmin = tl.IsSysadmin,
                BaseIsPolicyChecked = bl.IsPolicyChecked,
                TargetIsPolicyChecked = tl.IsPolicyChecked,
                BaseIsExpirationChecked = bl.IsExpirationChecked,
                TargetIsExpirationChecked = tl.IsExpirationChecked,
                Changes = changes
            });
        }

        foreach (var kvp in baseDict)
        {
            if (targetDict.ContainsKey(kvp.Key))
                continue;

            var bl = kvp.Value;
            result.Add(new CompareSqlLoginRow
            {
                Status = "Removed",
                ServerName = bl.ServerName,
                LoginName = bl.LoginName,
                BaseIsDisabled = bl.IsDisabled,
                BaseIsSysadmin = bl.IsSysadmin,
                BaseIsPolicyChecked = bl.IsPolicyChecked,
                BaseIsExpirationChecked = bl.IsExpirationChecked
            });
        }

        return result
            .OrderBy(r => r.ServerName)
            .ThenBy(r => r.LoginName)
            .ToList();
    }

    private static List<CompareAvailabilityGroupRow> BuildAvailabilityGroupsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentAvailabilityGroup g) =>
            $"{g.ServerName.Trim()}|{g.AgName.Trim()}|{(g.ReplicaServerName ?? string.Empty).Trim()}".ToLowerInvariant();

        var baseDict = a.AvailabilityGroups.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var targetDict = b.AvailabilityGroups.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareAvailabilityGroupRow>();

        foreach (var kvp in targetDict)
        {
            var tg = kvp.Value;

            if (!baseDict.TryGetValue(kvp.Key, out var bg))
            {
                result.Add(new CompareAvailabilityGroupRow
                {
                    Status = "New",
                    ServerName = tg.ServerName,
                    AgName = tg.AgName,
                    ReplicaServerName = tg.ReplicaServerName,
                    TargetRoleDesc = tg.RoleDesc,
                    TargetOperationalStateDesc = tg.OperationalStateDesc,
                    TargetConnectedStateDesc = tg.ConnectedStateDesc,
                    TargetSynchronizationHealthDesc = tg.SynchronizationHealthDesc
                });
                continue;
            }

            var changes = new List<string>();

            void Compare(string label, string? oldValue, string? newValue)
            {
                if (!string.Equals(oldValue ?? "", newValue ?? "", StringComparison.OrdinalIgnoreCase))
                    changes.Add($"{label}: {oldValue ?? "-"} → {newValue ?? "-"}");
            }

            Compare("Role", bg.RoleDesc, tg.RoleDesc);
            Compare("Operational state", bg.OperationalStateDesc, tg.OperationalStateDesc);
            Compare("Connected state", bg.ConnectedStateDesc, tg.ConnectedStateDesc);
            Compare("Synchronisation health", bg.SynchronizationHealthDesc, tg.SynchronizationHealthDesc);

            if (changes.Count == 0)
                continue;

            result.Add(new CompareAvailabilityGroupRow
            {
                Status = "Changed",
                ServerName = tg.ServerName,
                AgName = tg.AgName,
                ReplicaServerName = tg.ReplicaServerName,
                BaseRoleDesc = bg.RoleDesc,
                TargetRoleDesc = tg.RoleDesc,
                BaseOperationalStateDesc = bg.OperationalStateDesc,
                TargetOperationalStateDesc = tg.OperationalStateDesc,
                BaseConnectedStateDesc = bg.ConnectedStateDesc,
                TargetConnectedStateDesc = tg.ConnectedStateDesc,
                BaseSynchronizationHealthDesc = bg.SynchronizationHealthDesc,
                TargetSynchronizationHealthDesc = tg.SynchronizationHealthDesc,
                Changes = changes
            });
        }

        foreach (var kvp in baseDict)
        {
            if (targetDict.ContainsKey(kvp.Key))
                continue;

            var bg = kvp.Value;
            result.Add(new CompareAvailabilityGroupRow
            {
                Status = "Removed",
                ServerName = bg.ServerName,
                AgName = bg.AgName,
                ReplicaServerName = bg.ReplicaServerName,
                BaseRoleDesc = bg.RoleDesc,
                BaseOperationalStateDesc = bg.OperationalStateDesc,
                BaseConnectedStateDesc = bg.ConnectedStateDesc,
                BaseSynchronizationHealthDesc = bg.SynchronizationHealthDesc
            });
        }

        return result
            .OrderBy(r => r.ServerName)
            .ThenBy(r => r.AgName)
            .ThenBy(r => r.ReplicaServerName)
            .ToList();
    }

    private const int CertificateExpiryWarningDays = 90;

    private static List<CompareCertificateRow> BuildCertificatesDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentCertificate c) =>
            $"{c.ServerName.Trim()}|{c.CertificateName.Trim()}".ToLowerInvariant();

        static bool Expiring(AssessmentCertificate c) =>
            c.DaysToExpiry.HasValue && c.DaysToExpiry.Value <= CertificateExpiryWarningDays;

        var baseDict = a.Certificates.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var targetDict = b.Certificates.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareCertificateRow>();

        foreach (var kvp in targetDict)
        {
            var tc = kvp.Value;

            if (!baseDict.TryGetValue(kvp.Key, out var bc))
            {
                result.Add(new CompareCertificateRow
                {
                    Status = "New",
                    ServerName = tc.ServerName,
                    CertificateName = tc.CertificateName,
                    Thumbprint = tc.Thumbprint,
                    TargetExpiryDate = tc.ExpiryDate,
                    TargetDaysToExpiry = tc.DaysToExpiry,
                    TargetProtectedDatabases = tc.ProtectedDatabases,
                    IsExpiring = Expiring(tc)
                });
                continue;
            }

            var changes = new List<string>();

            if (bc.ExpiryDate != tc.ExpiryDate)
                changes.Add($"Expiry: {bc.ExpiryDate?.ToString("yyyy-MM-dd") ?? "-"} → {tc.ExpiryDate?.ToString("yyyy-MM-dd") ?? "-"}");

            if (!string.Equals(bc.Thumbprint ?? "", tc.Thumbprint ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add("Thumbprint changed (certificate was rotated)");

            if (!string.Equals(bc.ProtectedDatabases ?? "", tc.ProtectedDatabases ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add($"Protects: {bc.ProtectedDatabases ?? "-"} → {tc.ProtectedDatabases ?? "-"}");

            if (!string.Equals(bc.Subject ?? "", tc.Subject ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add($"Subject: {bc.Subject ?? "-"} → {tc.Subject ?? "-"}");

            // Surface a certificate that has crossed into the warning window even
            // when nothing about its definition changed.
            var crossedThreshold = !Expiring(bc) && Expiring(tc);

            if (changes.Count == 0 && !crossedThreshold)
                continue;

            if (crossedThreshold && changes.Count == 0)
                changes.Add($"Now {tc.DaysToExpiry} day(s) from expiry");

            result.Add(new CompareCertificateRow
            {
                Status = "Changed",
                ServerName = tc.ServerName,
                CertificateName = tc.CertificateName,
                Thumbprint = tc.Thumbprint,
                BaseExpiryDate = bc.ExpiryDate,
                TargetExpiryDate = tc.ExpiryDate,
                BaseDaysToExpiry = bc.DaysToExpiry,
                TargetDaysToExpiry = tc.DaysToExpiry,
                BaseProtectedDatabases = bc.ProtectedDatabases,
                TargetProtectedDatabases = tc.ProtectedDatabases,
                IsExpiring = Expiring(tc),
                Changes = changes
            });
        }

        foreach (var kvp in baseDict)
        {
            if (targetDict.ContainsKey(kvp.Key))
                continue;

            var bc = kvp.Value;
            result.Add(new CompareCertificateRow
            {
                Status = "Removed",
                ServerName = bc.ServerName,
                CertificateName = bc.CertificateName,
                Thumbprint = bc.Thumbprint,
                BaseExpiryDate = bc.ExpiryDate,
                BaseDaysToExpiry = bc.DaysToExpiry,
                BaseProtectedDatabases = bc.ProtectedDatabases
            });
        }

        return result
            .OrderBy(r => r.TargetDaysToExpiry ?? r.BaseDaysToExpiry ?? int.MaxValue)
            .ThenBy(r => r.ServerName)
            .ThenBy(r => r.CertificateName)
            .ToList();
    }
}

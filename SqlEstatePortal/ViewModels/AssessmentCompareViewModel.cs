using SqlEstatePortal.Models;

namespace SqlEstatePortal.ViewModels;

public class AssessmentCompareViewModel
{
    public int? BaseRunId { get; set; }
    public int? TargetRunId { get; set; }
    public AssessmentRun? BaseRun { get; set; }
    public AssessmentRun? TargetRun { get; set; }
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];

    // KPI Summary
    public List<CompareKpiRow> Kpis { get; set; } = [];

    // Findings Diff
    public List<CompareFindingRow> FindingsDiff { get; set; } = [];
    public int NewFindingsCount { get; set; }
    public int ResolvedFindingsCount { get; set; }
    public int OngoingFindingsCount { get; set; }

    // Server Diff
    public List<CompareServerRow> ServersDiff { get; set; } = [];
    public int NewServersCount { get; set; }
    public int RemovedServersCount { get; set; }
    public int ChangedServersCount { get; set; }
    public int UnchangedServersCount { get; set; }

    // Database Diff
    public List<CompareDatabaseRow> DatabasesDiff { get; set; } = [];
    public int NewDatabasesCount { get; set; }
    public int RemovedDatabasesCount { get; set; }
    public int ChangedDatabasesCount { get; set; }
    public int UnchangedDatabasesCount { get; set; }

    // Backup Diff
    public List<CompareBackupRow> BackupsDiff { get; set; } = [];

    // Configurations Diff
    public List<CompareConfigRow> ConfigsDiff { get; set; } = [];
    public int ChangedConfigsCount { get; set; }

    // Linked Servers Diff
    public List<CompareLinkedServerRow> LinkedServersDiff { get; set; } = [];
    public int NewLinkedServersCount { get; set; }
    public int RemovedLinkedServersCount { get; set; }
    public int ChangedLinkedServersCount { get; set; }

    // SQL Logins Diff
    public List<CompareSqlLoginRow> SqlLoginsDiff { get; set; } = [];
    public int NewSqlLoginsCount { get; set; }
    public int RemovedSqlLoginsCount { get; set; }
    public int ChangedSqlLoginsCount { get; set; }

    // Availability Groups Diff
    public List<CompareAvailabilityGroupRow> AvailabilityGroupsDiff { get; set; } = [];
    public int NewAvailabilityGroupsCount { get; set; }
    public int RemovedAvailabilityGroupsCount { get; set; }
    public int ChangedAvailabilityGroupsCount { get; set; }

    // Certificates Diff
    public List<CompareCertificateRow> CertificatesDiff { get; set; } = [];
    public int NewCertificatesCount { get; set; }
    public int RemovedCertificatesCount { get; set; }
    public int ChangedCertificatesCount { get; set; }
    public int ExpiringCertificatesCount { get; set; }
}

public class CompareKpiRow
{
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string BaseValue { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string Delta { get; set; } = string.Empty;
    /// <summary>
    /// "improved", "degraded", "neutral", "unchanged"
    /// </summary>
    public string Status { get; set; } = "neutral";
}

public class CompareFindingRow
{
    /// <summary>
    /// "New", "Resolved", "Ongoing"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class CompareServerRow
{
    /// <summary>
    /// "New", "Removed", "Changed", "Unchanged"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public bool? BaseReachable { get; set; }
    public bool? TargetReachable { get; set; }
    public string? BaseEdition { get; set; }
    public string? TargetEdition { get; set; }
    public string? BaseVersion { get; set; }
    public string? TargetVersion { get; set; }
    public string? BaseSupportStatus { get; set; }
    public string? TargetSupportStatus { get; set; }
    public int? BaseCpuCount { get; set; }
    public int? TargetCpuCount { get; set; }
    public decimal? BaseMemoryMb { get; set; }
    public decimal? TargetMemoryMb { get; set; }
    public decimal? BaseAllocatedGb { get; set; }
    public decimal? TargetAllocatedGb { get; set; }
    public int? BaseUserDbCount { get; set; }
    public int? TargetUserDbCount { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareDatabaseRow
{
    /// <summary>
    /// "New", "Removed", "Changed", "Unchanged"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? BaseState { get; set; }
    public string? TargetState { get; set; }
    public string? BaseRecoveryModel { get; set; }
    public string? TargetRecoveryModel { get; set; }
    public int? BaseCompatLevel { get; set; }
    public int? TargetCompatLevel { get; set; }
    public decimal? BaseDataMb { get; set; }
    public decimal? TargetDataMb { get; set; }
    public decimal? BaseLogMb { get; set; }
    public decimal? TargetLogMb { get; set; }
    public decimal? TotalMbDelta { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareBackupRow
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime? BaseLastFullBackup { get; set; }
    public DateTime? TargetLastFullBackup { get; set; }
    public DateTime? BaseLastDifferentialBackup { get; set; }
    public DateTime? TargetLastDifferentialBackup { get; set; }
    public DateTime? BaseLastLogBackup { get; set; }
    public DateTime? TargetLastLogBackup { get; set; }
    /// <summary>
    /// "Updated", "Stale", "New", "Removed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

public class CompareConfigRow
{
    public string ServerName { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public long? BaseRunValue { get; set; }
    public long? TargetRunValue { get; set; }
    public string Status { get; set; } = "Changed";
}

public class CompareLinkedServerRow
{
    /// <summary>
    /// "New", "Removed", "Changed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string LinkedServerName { get; set; } = string.Empty;
    public string? BaseDataSource { get; set; }
    public string? TargetDataSource { get; set; }
    public string? BaseProvider { get; set; }
    public string? TargetProvider { get; set; }
    public bool? BaseRemoteLoginEnabled { get; set; }
    public bool? TargetRemoteLoginEnabled { get; set; }
    public bool? BaseRpcOutEnabled { get; set; }
    public bool? TargetRpcOutEnabled { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareSqlLoginRow
{
    /// <summary>
    /// "New", "Removed", "Changed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public bool? BaseIsDisabled { get; set; }
    public bool? TargetIsDisabled { get; set; }
    public bool? BaseIsSysadmin { get; set; }
    public bool? TargetIsSysadmin { get; set; }
    public bool? BaseIsPolicyChecked { get; set; }
    public bool? TargetIsPolicyChecked { get; set; }
    public bool? BaseIsExpirationChecked { get; set; }
    public bool? TargetIsExpirationChecked { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareAvailabilityGroupRow
{
    /// <summary>
    /// "New", "Removed", "Changed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string AgName { get; set; } = string.Empty;
    public string? ReplicaServerName { get; set; }
    public string? BaseRoleDesc { get; set; }
    public string? TargetRoleDesc { get; set; }
    public string? BaseOperationalStateDesc { get; set; }
    public string? TargetOperationalStateDesc { get; set; }
    public string? BaseConnectedStateDesc { get; set; }
    public string? TargetConnectedStateDesc { get; set; }
    public string? BaseSynchronizationHealthDesc { get; set; }
    public string? TargetSynchronizationHealthDesc { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareCertificateRow
{
    /// <summary>
    /// "New", "Removed", "Changed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string CertificateName { get; set; } = string.Empty;
    public string? Thumbprint { get; set; }
    public DateTime? BaseExpiryDate { get; set; }
    public DateTime? TargetExpiryDate { get; set; }
    public int? BaseDaysToExpiry { get; set; }
    public int? TargetDaysToExpiry { get; set; }
    public string? BaseProtectedDatabases { get; set; }
    public string? TargetProtectedDatabases { get; set; }
    /// <summary>True when the certificate is expired or inside 90 days on the target run.</summary>
    public bool IsExpiring { get; set; }
    public List<string> Changes { get; set; } = [];
}

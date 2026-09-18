using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentDatabase
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? State { get; set; }

    [MaxLength(30)]
    public string? RecoveryModel { get; set; }

    public int? CompatibilityLevel { get; set; }

    [MaxLength(30)]
    public string? PageVerify { get; set; }

    public bool IsEncrypted { get; set; }
    public decimal? DataMb { get; set; }
    public decimal? LogMb { get; set; }

    [MaxLength(128)]
    public string? OwnerName { get; set; }

    public DateTime? LastGoodCheckDbTime { get; set; }

    [MaxLength(128)]
    public string? CollationName { get; set; }

    public DateTime? CreationDate { get; set; }
}

public class AssessmentVolume
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string MountPoint { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LogicalName { get; set; }

    public decimal? TotalGb { get; set; }
    public decimal? FreeGb { get; set; }
    public decimal? FreePct { get; set; }
}

public class AssessmentService
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ServiceName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? StartupType { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(200)]
    public string? ServiceAccount { get; set; }

    [MaxLength(10)]
    public string? InstantFileInitialization { get; set; }
}

public class AssessmentWait
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string WaitType { get; set; } = string.Empty;

    public long WaitingTasks { get; set; }
    public long WaitTimeMs { get; set; }
    public long SignalWaitTimeMs { get; set; }
    public decimal? WaitPct { get; set; }
}

public class AssessmentJob
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string JobName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [MaxLength(50)]
    public string? LastRunStatus { get; set; }

    public DateTime? LastRun { get; set; }
    public string? Message { get; set; }
}

public class AssessmentSysadmin
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TypeDesc { get; set; }

    public bool IsDisabled { get; set; }
    public DateTime? CreateDate { get; set; }
}

public class AssessmentConfiguration
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public long? Minimum { get; set; }
    public long? Maximum { get; set; }
    public long? ConfigValue { get; set; }
    public long? RunValue { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDynamic { get; set; }
    public bool IsAdvanced { get; set; }
}

public class AssessmentBackup
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DatabaseName { get; set; } = string.Empty;

    public DateTime? LastFullBackup { get; set; }
    public DateTime? LastDifferentialBackup { get; set; }
    public DateTime? LastLogBackup { get; set; }
}

public class AssessmentLinkedServer
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    /// <summary>The assessed SQL instance the linked server is defined on.</summary>
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>sys.servers.name - the name of the linked server entry.</summary>
    [MaxLength(200)]
    public string LinkedServerName { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? DataSource { get; set; }

    [MaxLength(128)]
    public string? Provider { get; set; }

    public bool IsRemoteLoginEnabled { get; set; }
    public bool IsRpcOutEnabled { get; set; }
}

public class AssessmentSqlLogin
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    /// <summary>The assessed SQL instance the login is defined on.</summary>
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>sys.sql_logins.name.</summary>
    [MaxLength(200)]
    public string LoginName { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }
    public bool IsPolicyChecked { get; set; }
    public bool IsExpirationChecked { get; set; }
    public bool IsSysadmin { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}

public class AssessmentAvailabilityGroup
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    /// <summary>The assessed SQL instance the AG was read from.</summary>
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AgName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ReplicaServerName { get; set; }

    [MaxLength(60)]
    public string? RoleDesc { get; set; }

    [MaxLength(60)]
    public string? OperationalStateDesc { get; set; }

    [MaxLength(60)]
    public string? ConnectedStateDesc { get; set; }

    [MaxLength(60)]
    public string? SynchronizationHealthDesc { get; set; }
}

public class AssessmentCertificate
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    /// <summary>The assessed SQL instance the certificate lives on.</summary>
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>The database the certificate was read from (master for instance-level certificates).</summary>
    [MaxLength(128)]
    public string? DatabaseName { get; set; }

    [MaxLength(256)]
    public string CertificateName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Subject { get; set; }

    [MaxLength(1000)]
    public string? IssuerName { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Days until expiry as measured on the collector at run time; negative once expired.</summary>
    public int? DaysToExpiry { get; set; }

    [MaxLength(200)]
    public string? PrivateKeyEncryption { get; set; }

    [MaxLength(200)]
    public string? Thumbprint { get; set; }

    /// <summary>Comma-separated databases whose encryption key this certificate protects.</summary>
    public string? ProtectedDatabases { get; set; }
}

public class AssessmentTlsCertificate
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? InstanceName { get; set; }

    /// <summary>Null when no certificate is configured and SQL Server self-signs.</summary>
    [MaxLength(200)]
    public string? Thumbprint { get; set; }

    [MaxLength(200)]
    public string? CertificateSource { get; set; }

    public bool ForceEncryption { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentRun
{
    public int Id { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Running";

    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    [MaxLength(500)]
    public string? ServerListPath { get; set; }

    [MaxLength(500)]
    public string? ReportHtmlPath { get; set; }

    [MaxLength(500)]
    public string? ReportJsonPath { get; set; }

    public string? HtmlContent { get; set; }

    public int ServerCount { get; set; }
    public int ReachableCount { get; set; }
    public int UnreachableCount { get; set; }
    public int EndOfSupportCount { get; set; }
    public decimal AllocatedStorageGb { get; set; }
    public int EstimatedLicensedCores { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int InfoCount { get; set; }

    public string? OutputLog { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<AssessmentFinding> Findings { get; set; } = new List<AssessmentFinding>();
    public ICollection<AssessmentServerSnapshot> Servers { get; set; } = new List<AssessmentServerSnapshot>();
    public ICollection<AssessmentDatabase> Databases { get; set; } = new List<AssessmentDatabase>();
    public ICollection<AssessmentVolume> Volumes { get; set; } = new List<AssessmentVolume>();
    public ICollection<AssessmentService> Services { get; set; } = new List<AssessmentService>();
    public ICollection<AssessmentWait> Waits { get; set; } = new List<AssessmentWait>();
    public ICollection<AssessmentJob> Jobs { get; set; } = new List<AssessmentJob>();
    public ICollection<AssessmentSysadmin> Sysadmins { get; set; } = new List<AssessmentSysadmin>();
    public ICollection<AssessmentConfiguration> Configurations { get; set; } = new List<AssessmentConfiguration>();
    public ICollection<AssessmentBackup> Backups { get; set; } = new List<AssessmentBackup>();
    public ICollection<AssessmentLinkedServer> LinkedServers { get; set; } = new List<AssessmentLinkedServer>();
    public ICollection<AssessmentSqlLogin> SqlLogins { get; set; } = new List<AssessmentSqlLogin>();
    public ICollection<AssessmentAvailabilityGroup> AvailabilityGroups { get; set; } = new List<AssessmentAvailabilityGroup>();
    public ICollection<AssessmentCertificate> Certificates { get; set; } = new List<AssessmentCertificate>();
    public ICollection<AssessmentTlsCertificate> TlsCertificates { get; set; } = new List<AssessmentTlsCertificate>();
}

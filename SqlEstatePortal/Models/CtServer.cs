using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

[Table("ct_servers")]
public class CtServer
{
    [Key]
    [Column("tx_id")]
    public int TxId { get; set; }

    [Column("server_name")]
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [Column("server_type")]
    [MaxLength(50)]
    public string? ServerType { get; set; }

    /// <summary>Critical or Non Critical. Mandatory on the register form.</summary>
    [Column("criticality_type")]
    [MaxLength(20)]
    public string? CriticalityType { get; set; }

    /// <summary>Windows Auth or MFA. Mandatory on the register form.</summary>
    [Column("auth_type")]
    [MaxLength(30)]
    public string? AuthType { get; set; }

    [Column("fqdn")]
    [MaxLength(255)]
    public string? Fqdn { get; set; }

    [Column("sql_version")]
    [MaxLength(150)]
    public string? SqlVersion { get; set; }

    [Column("sql_product")]
    [MaxLength(100)]
    public string? SqlProduct { get; set; }

    [Column("support_status")]
    [MaxLength(50)]
    public string? SupportStatus { get; set; }

    [Column("sql_edition")]
    [MaxLength(150)]
    public string? SqlEdition { get; set; }

    [Column("sql_started_at")]
    public DateTime? SqlStartedAt { get; set; }

    [Column("administrator_login")]
    [MaxLength(128)]
    public string? AdministratorLogin { get; set; }

    [Column("public_network_access")]
    [MaxLength(32)]
    public string? PublicNetworkAccess { get; set; }

    [Column("environment")]
    [MaxLength(100)]
    public string? Environment { get; set; }

    [Column("subscription")]
    [MaxLength(200)]
    public string? Subscription { get; set; }

    [Column("subscription_id")]
    [MaxLength(64)]
    public string? SubscriptionId { get; set; }

    [Column("azure_resource_id")]
    [MaxLength(512)]
    public string? AzureResourceId { get; set; }

    [Column("tower")]
    [MaxLength(100)]
    public string? Tower { get; set; }

    [Column("resource_group_name")]
    [MaxLength(200)]
    public string? ResourceGroupName { get; set; }

    [Column("data_centre_location")]
    [MaxLength(100)]
    public string? DataCentreLocation { get; set; }

    [Column("server_status")]
    [MaxLength(50)]
    public string? ServerStatus { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("azure_tags")]
    public string? AzureTags { get; set; }

    [Column("azure_synced_at")]
    public DateTime? AzureSyncedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("created_on")]
    public DateTime? CreatedOn { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("updated_on")]
    public DateTime? UpdatedOn { get; set; }

    [Column("status_checked_at")]
    public DateTime? StatusCheckedAt { get; set; }

    [Column("ip_address")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [Column("vm_cpu")]
    [MaxLength(20)]
    public string? VmCpu { get; set; }

    [Column("vm_ram")]
    [MaxLength(20)]
    public string? VmRam { get; set; }

    [Column("vm_storage_gb")]
    [MaxLength(20)]
    public string? VmStorageGb { get; set; }

    [Column("current_utilization_pct")]
    [MaxLength(20)]
    public string? CurrentUtilizationPct { get; set; }
}

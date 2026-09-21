using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

[Table("ct_database")]
public class CtDatabase
{
    [Key]
    [Column("tx_id")]
    public int TxId { get; set; }

    [Column("tower")]
    [MaxLength(100)]
    public string? Tower { get; set; }

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

    [Column("resource_group_name")]
    [MaxLength(200)]
    public string? ResourceGroupName { get; set; }

    [Column("data_centre_location")]
    [MaxLength(100)]
    public string? DataCentreLocation { get; set; }

    [Column("server_name")]
    [MaxLength(200)]
    public string? ServerName { get; set; }

    /// <summary>
    /// The real link to ct_servers. server_name is kept in step alongside it
    /// until every reader has moved over to the id.
    /// </summary>
    [Column("server_id")]
    public int? ServerId { get; set; }

    /// <summary>Inherited from the server; not set directly.</summary>
    [Column("criticality_type")]
    [MaxLength(20)]
    public string? CriticalityType { get; set; }

    [Column("elastic_pool_name")]
    [MaxLength(200)]
    public string? ElasticPoolName { get; set; }

    [Column("database_name")]
    [MaxLength(500)]
    public string DatabaseName { get; set; } = string.Empty;

    [Column("database_status")]
    [MaxLength(50)]
    public string? DatabaseStatus { get; set; }

    [Column("max_size_gb")]
    public int? MaxSizeGb { get; set; }

    [Column("max_size_mb")]
    public int? MaxSizeMb { get; set; }

    [Column("current_size_mb")]
    public int? CurrentSizeMb { get; set; }

    [Column("collation_name")]
    [MaxLength(128)]
    public string? CollationName { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [Column("license_type")]
    [MaxLength(64)]
    public string? LicenseType { get; set; }

    [Column("zone_redundant")]
    public bool? ZoneRedundant { get; set; }

    [Column("read_scale")]
    [MaxLength(32)]
    public string? ReadScale { get; set; }

    [Column("azure_tags")]
    public string? AzureTags { get; set; }

    [Column("azure_synced_at")]
    public DateTime? AzureSyncedAt { get; set; }

    [Column("database_edition")]
    [MaxLength(100)]
    public string? DatabaseEdition { get; set; }

    [Column("current_service_objective_name")]
    [MaxLength(100)]
    public string? CurrentServiceObjectiveName { get; set; }

    [Column("azure_sku_name")]
    [MaxLength(100)]
    public string? AzureSkuName { get; set; }

    [Column("azure_sku_capacity")]
    public int? AzureSkuCapacity { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("compatibility_level")]
    [MaxLength(20)]
    public string? CompatibilityLevel { get; set; }

    [Column("recovery_model")]
    [MaxLength(30)]
    public string? RecoveryModel { get; set; }

    [Column("free_space_mb")]
    public int? FreeSpaceMb { get; set; }

    [Column("backup_info")]
    [MaxLength(500)]
    public string? BackupInfo { get; set; }

    [Column("last_full_backup")]
    public DateTime? LastFullBackup { get; set; }

    [Column("last_differential_backup")]
    public DateTime? LastDifferentialBackup { get; set; }

    [Column("last_log_backup")]
    public DateTime? LastLogBackup { get; set; }

    [Column("database_owner")]
    [MaxLength(128)]
    public string? DatabaseOwner { get; set; }
}

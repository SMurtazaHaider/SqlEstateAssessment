using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

[Table("ct_applications")]
public class CtApplication
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("is_mapped")]
    public string? IsMapped { get; set; }

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("features")]
    public string? Features { get; set; }

    [Column("function")]
    public string? Function { get; set; }

    [Column("application_type")]
    public string? ApplicationType { get; set; }

    [Column("aquired_date")]
    public string? AquiredDate { get; set; }

    [Column("type_of_data")]
    public string? TypeOfData { get; set; }

    [Column("documentation")]
    public string? Documentation { get; set; }

    [Column("own_application_ip")]
    public string? OwnApplicationIp { get; set; }

    [Column("users")]
    public string? Users { get; set; }

    [Column("vendor")]
    public string? Vendor { get; set; }

    [Column("contract_renewal_date")]
    public string? ContractRenewalDate { get; set; }

    [Column("contract_type")]
    public string? ContractType { get; set; }

    [Column("contract_narrative")]
    public string? ContractNarrative { get; set; }

    [Column("business_criticality")]
    public string? BusinessCriticality { get; set; }

    /// <summary>
    /// Inherited from the servers this application touches; separate from the
    /// business-owned BusinessCriticality above.
    /// </summary>
    [Column("criticality_type")]
    [MaxLength(20)]
    public string? CriticalityType { get; set; }

    [Column("service_grade")]
    public string? ServiceGrade { get; set; }

    [Column("l1_support")]
    public string? L1Support { get; set; }

    [Column("l2_support")]
    public string? L2Support { get; set; }

    [Column("application_sme")]
    public string? ApplicationSme { get; set; }

    [Column("support_narrative")]
    public string? SupportNarrative { get; set; }

    [Column("support_partner_msp")]
    public string? SupportPartnerMsp { get; set; }

    [Column("service_owner")]
    public string? ServiceOwner { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("service_name")]
    public string? ServiceName { get; set; }

    [Column("service_type")]
    public string? ServiceType { get; set; }

    [Column("incidents_per_year")]
    public string? IncidentsPerYear { get; set; }

    [Column("incidents_per_user")]
    public string? IncidentsPerUser { get; set; }

    [Column("gartner_process")]
    public string? GartnerProcess { get; set; }

    [Column("has_docs")]
    public string? HasDocs { get; set; }

    [Column("time_roadmap")]
    public string? TimeRoadmap { get; set; }

    [Column("tech_grade")]
    public string? TechGrade { get; set; }

    [Column("age_of_tech")]
    public string? AgeOfTech { get; set; }

    [Column("technical_debt")]
    public string? TechnicalDebt { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("target_host_platform")]
    public string? TargetHostPlatform { get; set; }

    [Column("connected_systems")]
    public string? ConnectedSystems { get; set; }

    [Column("servers")]
    public string? Servers { get; set; }

    [Column("source_code_location")]
    public string? SourceCodeLocation { get; set; }

    [Column("tech_stack")]
    public string? TechStack { get; set; }

    [Column("review")]
    public string? Review { get; set; }

    [Column("roadmap")]
    public string? Roadmap { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("asset_tag")]
    public string? AssetTag { get; set; }

    [Column("compliance_grade")]
    public string? ComplianceGrade { get; set; }

    [Column("operating_region")]
    public string? OperatingRegion { get; set; }

    [Column("consumption")]
    public string? Consumption { get; set; }

    [Column("file_data_storage")]
    public string? FileDataStorage { get; set; }

    [Column("data_location")]
    public string? DataLocation { get; set; }

    [Column("data_classification")]
    public string? DataClassification { get; set; }

    [Column("disaster_recovery")]
    public string? DisasterRecovery { get; set; }

    [Column("last_dr_test")]
    public string? LastDrTest { get; set; }

    [Column("backed_up_data_location")]
    public string? BackedUpDataLocation { get; set; }

    [Column("authentication_type")]
    public string? AuthenticationType { get; set; }

    [Column("maintenance_revenue_impact")]
    public string? MaintenanceRevenueImpact { get; set; }

    [Column("maintenance_business_impact")]
    public string? MaintenanceBusinessImpact { get; set; }

    [Column("outtage_revenue_impact")]
    public string? OuttageRevenueImpact { get; set; }

    [Column("outtage_business_impact")]
    public string? OuttageBusinessImpact { get; set; }

    [Column("monitoring_grade")]
    public string? MonitoringGrade { get; set; }

    [Column("manual_alternative_process")]
    public string? ManualAlternativeProcess { get; set; }

    [Column("compliance_narrative")]
    public string? ComplianceNarrative { get; set; }

    [Column("external_url")]
    public string? ExternalUrl { get; set; }

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
}

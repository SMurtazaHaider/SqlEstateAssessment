using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8, ErrorMessage = "New password must be at least 8 characters."), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class TeamMemberFormViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Member Name")]
    public string MemberName { get; set; } = string.Empty;

    [Required, Display(Name = "Access Role")]
    public int AccessRoleId { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Team Name")]
    public int? TeamId { get; set; }

    public string? Designation { get; set; }

    [Required, Display(Name = "Admin Access")]
    public bool AdminAccess { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    [Display(Name = "Password (auto-generated)")]
    public string GeneratedPassword { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> RoleOptions { get; set; } = [];
    public IEnumerable<SelectListItem> TeamOptions { get; set; } = [];
    public IEnumerable<SelectListItem> StatusOptions { get; set; } =
    [
        new("Active", "Active"),
        new("Inactive", "Inactive")
    ];
}

public class EstateServerFormViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(200), Display(Name = "Server Name")]
    public string ServerName { get; set; } = string.Empty;

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class RolePermissionRow
{
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}

public class RoleFormViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RolePermissionRow> Permissions { get; set; } = [];
}

public class AssessmentDetailsViewModel
{
    public AssessmentRun Run { get; set; } = null!;
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];
    public int? SyncBatchId { get; set; }
    public string? SyncStatus { get; set; }
    public bool ShowSyncToRegister { get; set; }
    public bool ShowNoChangesFound { get; set; }

    /// <summary>
    /// Override rows per finding key, newest first. Empty for a finding that has
    /// never been moved. Drives both the "moved" marker and the hover history.
    /// </summary>
    public Dictionary<string, List<AssessmentFindingOverride>> FindingHistory { get; set; } = new();

    /// <summary>Whether the signed-in user may move a finding.</summary>
    public bool CanMoveFindings { get; set; }
}

public class AssessmentListItemViewModel
{
    public AssessmentRun Run { get; set; } = null!;
    public int? SyncBatchId { get; set; }
    public string? SyncStatus { get; set; }
    public bool ShowSyncToRegister { get; set; }
    public bool ShowNoChangesFound { get; set; }
}

public class DashboardViewModel
{
    public int TotalRuns { get; set; }
    public int SucceededRuns { get; set; }
    public int FailedRuns { get; set; }
    public int SelectedRunId { get; set; }
    public string? SelectedServer { get; set; }
    public int ServerCount { get; set; }
    public int ReachableCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int DatabaseCount { get; set; }
    public int FindingCount { get; set; }
    public decimal AllocatedStorageGb { get; set; }
    public AssessmentRunSummary? SelectedRun { get; set; }
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];
    public List<string> ServerNames { get; set; } = [];
    public List<AssessmentRunSummary> RecentRuns { get; set; } = [];
    public string ChartsJson { get; set; } = "{}";
}

public class DashboardCharts
{
    public ChartSlice[] FindingsBySeverity { get; set; } = [];
    public ChartSlice[] FindingsByArea { get; set; } = [];
    public ChartSlice[] FindingsByServer { get; set; } = [];
    public ChartSlice[] RecoveryModels { get; set; } = [];
    public ChartSlice[] SupportStatus { get; set; } = [];
    public ChartSlice[] Editions { get; set; } = [];
    public ChartSlice[] JobStatus { get; set; } = [];
    public ChartSlice[] ServiceStatus { get; set; } = [];
    public NamedValue[] VolumeFreePct { get; set; } = [];
    public NamedValue[] TopDatabasesMb { get; set; } = [];
    public NamedValue[] TopWaits { get; set; } = [];
    public HistoryPoint[] RunHistory { get; set; } = [];
}

public class ChartSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class NamedValue
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class HistoryPoint
{
    public string Label { get; set; } = string.Empty;
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
}

public class AssessmentRunSummary
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public int ServerCount { get; set; }
    public int ReachableCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

public class FindingSummary
{
    public string Severity { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
}

public class ServerSummary
{
    public string ServerName { get; set; } = string.Empty;
    public bool Reachable { get; set; }
    public string? Product { get; set; }
    public string? SupportStatus { get; set; }
    public string? Edition { get; set; }
}

public class ApplicationRegisterViewModel
{
    public string? Criticality { get; set; }
    public IReadOnlyList<string> CriticalityOptions { get; set; } = [];

    public string? Application { get; set; }
    public string? Status { get; set; }
    public string? Function { get; set; }
    public string? TimeRoadmap { get; set; }
    public string? TechGrade { get; set; }
    public string? Location { get; set; }
    public string? ComplianceGrade { get; set; }
    public string? TechnicalDebt { get; set; }
    public string? OperatingRegion { get; set; }
    public string? Monitoring { get; set; }
    public string? Vendor { get; set; }

    public int TotalCount { get; set; }

    public IReadOnlyList<string> ApplicationOptions { get; set; } = [];
    public IReadOnlyList<string> StatusOptions { get; set; } = [];
    public IReadOnlyList<string> FunctionOptions { get; set; } = [];
    public IReadOnlyList<string> TimeRoadmapOptions { get; set; } = [];
    public IReadOnlyList<string> TechGradeOptions { get; set; } = [];
    public IReadOnlyList<string> LocationOptions { get; set; } = [];
    public IReadOnlyList<string> ComplianceGradeOptions { get; set; } = [];
    public IReadOnlyList<string> TechnicalDebtOptions { get; set; } = [];
    public IReadOnlyList<string> OperatingRegionOptions { get; set; } = [];
    public IReadOnlyList<string> MonitoringOptions { get; set; } = [];
    public IReadOnlyList<string> VendorOptions { get; set; } = [];

    public IReadOnlyList<ApplicationRowViewModel> Applications { get; set; } = [];
}

public class ApplicationRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Function { get; set; }
    public string? ApplicationType { get; set; }
    public string? TimeRoadmap { get; set; }
    public string? TechGrade { get; set; }
    public string? ComplianceGrade { get; set; }
    public string? Location { get; set; }
    public string? BusinessCriticality { get; set; }
    /// <summary>Inherited from the linked servers.</summary>
    public string? CriticalityType { get; set; }
    public string? Tco { get; set; }
    public int LinkedDbCount { get; set; }
    public int LinkedServerCount { get; set; }
    public string? ServiceOwner { get; set; }
    public string? OperatingRegion { get; set; }
    public string? Vendor { get; set; }
    public string? TechnicalDebt { get; set; }
    public string? MonitoringGrade { get; set; }
}

public class DatabaseRegisterViewModel
{
    public string? Criticality { get; set; }
    public IReadOnlyList<string> CriticalityOptions { get; set; } = [];

    public string? DatabaseName { get; set; }
    public string? ServerName { get; set; }
    public string? Status { get; set; }
    public string? Environment { get; set; }
    public string? Edition { get; set; }
    public string? Location { get; set; }
    public string? Active { get; set; }

    public int TotalCount { get; set; }

    public IReadOnlyList<string> DatabaseNameOptions { get; set; } = [];
    public IReadOnlyList<string> ServerNameOptions { get; set; } = [];
    public IReadOnlyList<string> StatusOptions { get; set; } = [];
    public IReadOnlyList<string> EnvironmentOptions { get; set; } = [];
    public IReadOnlyList<string> EditionOptions { get; set; } = [];
    public IReadOnlyList<string> LocationOptions { get; set; } = [];

    public IReadOnlyList<DatabaseRowViewModel> Databases { get; set; } = [];
}

public class DatabaseRowViewModel
{
    public int TxId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public int? ServerId { get; set; }
    /// <summary>Inherited from the linked server.</summary>
    public string? CriticalityType { get; set; }
    public string? DatabaseStatus { get; set; }
    public string? DatabaseOwner { get; set; }
    public string? Environment { get; set; }
    public string? DatabaseEdition { get; set; }
    public string? ServiceObjective { get; set; }
    public string? DataCentreLocation { get; set; }
    public int? CurrentSizeMb { get; set; }
    public int? MaxSizeGb { get; set; }
    public int? FreeSpaceMb { get; set; }
    public bool IsActive { get; set; }
    public string? ElasticPoolName { get; set; }
    public string? CompatibilityLevel { get; set; }
    public string? RecoveryModel { get; set; }
    public int LinkedApplicationCount { get; set; }
}

public class ServerRegisterViewModel
{
    public string? ServerName { get; set; }
    public string? ServerType { get; set; }
    public string? Environment { get; set; }
    public string? Status { get; set; }
    public string? Subscription { get; set; }
    public string? DataCentre { get; set; }
    public string? Criticality { get; set; }
    public string? AuthType { get; set; }

    public int TotalCount { get; set; }
    /// <summary>Rows after filtering, so the heading can say what is actually on screen.</summary>
    public int FilteredCount { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }

    public IReadOnlyList<string> ServerNameOptions { get; set; } = [];
    public IReadOnlyList<string> ServerTypeOptions { get; set; } = [];
    public IReadOnlyList<string> EnvironmentOptions { get; set; } = [];
    public IReadOnlyList<string> StatusOptions { get; set; } = [];
    public IReadOnlyList<string> SubscriptionOptions { get; set; } = [];
    public IReadOnlyList<string> DataCentreOptions { get; set; } = [];
    public IReadOnlyList<string> CriticalityOptions { get; set; } = [];
    public IReadOnlyList<string> AuthTypeOptions { get; set; } = [];

    public IReadOnlyList<ServerRowViewModel> Servers { get; set; } = [];
}

public class ServerRowViewModel
{
    public int TxId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? ServerType { get; set; }
    public string? CriticalityType { get; set; }
    public string? AuthType { get; set; }
    public string? Environment { get; set; }
    public string? ServerStatus { get; set; }
    public string? SqlProduct { get; set; }
    public string? SupportStatus { get; set; }
    public string? SqlEdition { get; set; }
    public string? SqlVersion { get; set; }
    public string? Subscription { get; set; }
    public string? DataCentreLocation { get; set; }
    public int DatabaseCount { get; set; }
    public int LinkedApplicationCount { get; set; }
}

public class CostRegisterViewModel
{
    public string? Application { get; set; }
    public string? Service { get; set; }
    public string? Grade { get; set; }

    public int TotalCount { get; set; }
    public decimal TotalHosting { get; set; }
    public decimal TotalLicense { get; set; }
    public decimal TotalSupport { get; set; }
    public decimal TotalChange { get; set; }
    public decimal TotalTco { get; set; }

    public IReadOnlyList<string> ApplicationOptions { get; set; } = [];
    public IReadOnlyList<string> ServiceOptions { get; set; } = [];
    public IReadOnlyList<string> GradeOptions { get; set; } = [];

    public IReadOnlyList<CostRowViewModel> Costs { get; set; } = [];
}

public class CostRowViewModel
{
    public int Id { get; set; }
    public int? ApplicationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public string? CostGrade { get; set; }
    public decimal? HostingCost { get; set; }
    public decimal? LicenseCost { get; set; }
    public decimal? SupportCost { get; set; }
    public decimal? ChangeCost { get; set; }
    public decimal? Tco { get; set; }
    public string? TotalUsers { get; set; }
    public bool HasHosting { get; set; }
    public bool HasLicense { get; set; }
    public bool HasSupport { get; set; }
    public bool HasChange { get; set; }
}

public class ApplicationDetailsViewModel
{
    public CtApplication Application { get; set; } = null!;
    public IReadOnlyList<LinkedDatabaseItemViewModel> LinkedDatabases { get; set; } = [];
    public IReadOnlyList<LinkedServerItemViewModel> LinkedServers { get; set; } = [];
}

public class ServerDetailsViewModel
{
    public CtServer Server { get; set; } = null!;
    public IReadOnlyList<LinkedDatabaseItemViewModel> LinkedDatabases { get; set; } = [];
    public IReadOnlyList<LinkedApplicationItemViewModel> LinkedApplications { get; set; } = [];
}

public class DatabaseDetailsViewModel
{
    public CtDatabase Database { get; set; } = null!;
    public LinkedServerItemViewModel? LinkedServer { get; set; }
    public IReadOnlyList<LinkedApplicationItemViewModel> LinkedApplications { get; set; } = [];
}

public class LinkedDatabaseItemViewModel
{
    public int DatabaseId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public string? Environment { get; set; }
    public string? DatabaseStatus { get; set; }
    public string? DatabaseEdition { get; set; }
    public string? DataCentreLocation { get; set; }
}

public class LinkedServerItemViewModel
{
    public int? ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? Environment { get; set; }
    public string? ServerStatus { get; set; }
    public string? Tower { get; set; }
    public string? Subscription { get; set; }
    public string? DataCentreLocation { get; set; }
}

public class LinkedApplicationItemViewModel
{
    public int ApplicationId { get; set; }
    public string? ApplicationName { get; set; }
    public string? Status { get; set; }
    public string? Function { get; set; }
    public string? ApplicationType { get; set; }
    public string? Location { get; set; }
    public string? ServiceOwner { get; set; }
    public string? OperatingRegion { get; set; }
}

/// <summary>Add / edit form for a server in the inventory register.</summary>
public class ServerFormViewModel
{
    /// <summary>0 for a new server.</summary>
    public int TxId { get; set; }

    public bool IsNew => TxId == 0;

    [Required(ErrorMessage = "Server name is required."), MaxLength(200), Display(Name = "Server name")]
    public string ServerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type is required."), MaxLength(50), Display(Name = "Type")]
    public string ServerType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Criticality is required."), MaxLength(20), Display(Name = "Criticality")]
    public string CriticalityType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Auth type is required."), MaxLength(30), Display(Name = "Auth type")]
    public string AuthType { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Fqdn { get; set; }

    [MaxLength(50), Display(Name = "IP address")]
    public string? IpAddress { get; set; }

    [MaxLength(100)]
    public string? Environment { get; set; }

    [MaxLength(200)]
    public string? Subscription { get; set; }

    [MaxLength(100), Display(Name = "Data centre")]
    public string? DataCentreLocation { get; set; }

    [MaxLength(100)]
    public string? Tower { get; set; }

    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<string> ServerTypeOptions { get; set; } = [];
    public IReadOnlyList<string> CriticalityOptions { get; set; } = [];
    public IReadOnlyList<string> AuthTypeOptions { get; set; } = [];
    public IReadOnlyList<string> EnvironmentOptions { get; set; } = [];

    /// <summary>Databases currently pointing at this server.</summary>
    public IReadOnlyList<ServerDatabaseLinkViewModel> LinkedDatabases { get; set; } = [];

    /// <summary>Databases with no server assigned, offered for linking.</summary>
    public IReadOnlyList<ServerDatabaseLinkViewModel> AvailableDatabases { get; set; } = [];

    /// <summary>Database ids ticked to link on submit.</summary>
    public List<int> LinkDatabaseIds { get; set; } = [];

    /// <summary>Database ids ticked to delink on submit.</summary>
    public List<int> DelinkDatabaseIds { get; set; } = [];

    /// <summary>Applications explicitly linked to this server via ct_application_server.</summary>
    public IReadOnlyList<ServerApplicationLinkViewModel> LinkedApplications { get; set; } = [];

    /// <summary>
    /// Applications inferred through a linked database rather than an explicit row.
    /// Shown read-only: they follow from the database links and cannot be unlinked here.
    /// </summary>
    public IReadOnlyList<ServerApplicationLinkViewModel> InferredApplications { get; set; } = [];

    /// <summary>Every application not already explicitly linked, for the picker.</summary>
    public IReadOnlyList<ServerApplicationLinkViewModel> AvailableApplications { get; set; } = [];

    /// <summary>Application ids chosen to link on submit.</summary>
    public List<int> LinkApplicationIds { get; set; } = [];

    /// <summary>Application ids ticked to unlink on submit.</summary>
    public List<int> UnlinkApplicationIds { get; set; } = [];
}

public class ServerApplicationLinkViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Where an inferred link came from, e.g. the database that produced it.</summary>
    public string? SourceText { get; set; }
}

public class ServerDatabaseLinkViewModel
{
    public int TxId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public string? Environment { get; set; }
    public string? DatabaseStatus { get; set; }
}

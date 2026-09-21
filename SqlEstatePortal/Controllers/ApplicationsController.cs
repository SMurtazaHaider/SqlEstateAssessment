using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class ApplicationsController : Controller
{
    private readonly AppDbContext _db;

    public ApplicationsController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> Index(
        string? application,
        string? status,
        string? function,
        string? timeRoadmap,
        string? techGrade,
        string? location,
        string? complianceGrade,
        string? technicalDebt,
        string? operatingRegion,
        string? monitoring,
        string? vendor,
        string? criticality)
    {
        application = Norm(application);
        status = Norm(status);
        function = Norm(function);
        timeRoadmap = Norm(timeRoadmap);
        techGrade = Norm(techGrade);
        location = Norm(location);
        complianceGrade = Norm(complianceGrade);
        technicalDebt = Norm(technicalDebt);
        operatingRegion = Norm(operatingRegion);
        monitoring = Norm(monitoring);
        vendor = Norm(vendor);
        criticality = Norm(criticality);

        // Same rule as the Servers register: default to Critical on a plain page
        // load, honour an explicit choice (including "All") once the form posts.
        if (!Request.Query.ContainsKey("criticality"))
            criticality = CriticalityPropagation.Critical;

        var all = await _db.CtApplications.AsNoTracking().OrderBy(a => a.Name).ToListAsync();

        var tcoLookup = (await _db.Database.SqlQueryRaw<AppTcoRow>(
                """
                SELECT application_id AS ApplicationId, tco AS Tco
                FROM dbo.ct_costs
                WHERE application_id IS NOT NULL AND tco IS NOT NULL AND LTRIM(RTRIM(tco)) <> N''
                """).ToListAsync())
            .GroupBy(x => x.ApplicationId)
            .ToDictionary(g => g.Key, g => g.First().Tco);

        var linkedLookup = (await _db.Database.SqlQueryRaw<AppLinkCountRow>(
                """
                SELECT application_id AS ApplicationId, COUNT(*) AS [Count]
                FROM dbo.ct_application_database
                GROUP BY application_id
                """).ToListAsync())
            .ToDictionary(x => x.ApplicationId, x => x.Count);

        var linkedServerLookup = (await _db.Database.SqlQueryRaw<AppLinkCountRow>(
                """
                SELECT application_id AS ApplicationId, COUNT(*) AS [Count]
                FROM (
                    SELECT application_id, LOWER(LTRIM(RTRIM(server_name))) AS server_key
                    FROM dbo.ct_application_server
                    WHERE server_name IS NOT NULL AND LTRIM(RTRIM(server_name)) <> N''

                    UNION

                    SELECT l.application_id, LOWER(LTRIM(RTRIM(d.server_name))) AS server_key
                    FROM dbo.ct_application_database l
                    INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
                    WHERE d.server_name IS NOT NULL AND LTRIM(RTRIM(d.server_name)) <> N''
                ) x
                GROUP BY application_id
                """).ToListAsync())
            .ToDictionary(x => x.ApplicationId, x => x.Count);

        var filtered = all.AsEnumerable();
        if (application != null)
            filtered = filtered.Where(a => string.Equals(a.Name, application, StringComparison.OrdinalIgnoreCase));
        if (status != null)
            filtered = filtered.Where(a => string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase));
        if (function != null)
            filtered = filtered.Where(a => string.Equals(a.Function, function, StringComparison.OrdinalIgnoreCase));
        if (timeRoadmap != null)
            filtered = filtered.Where(a => string.Equals(a.TimeRoadmap, timeRoadmap, StringComparison.OrdinalIgnoreCase));
        if (techGrade != null)
            filtered = filtered.Where(a => string.Equals(a.TechGrade, techGrade, StringComparison.OrdinalIgnoreCase));
        if (location != null)
            filtered = filtered.Where(a => string.Equals(a.Location, location, StringComparison.OrdinalIgnoreCase));
        if (complianceGrade != null)
            filtered = filtered.Where(a => string.Equals(a.ComplianceGrade, complianceGrade, StringComparison.OrdinalIgnoreCase));
        if (technicalDebt != null)
            filtered = filtered.Where(a => string.Equals(a.TechnicalDebt, technicalDebt, StringComparison.OrdinalIgnoreCase));
        if (operatingRegion != null)
            filtered = filtered.Where(a => string.Equals(a.OperatingRegion, operatingRegion, StringComparison.OrdinalIgnoreCase));
        if (monitoring != null)
            filtered = filtered.Where(a => string.Equals(a.MonitoringGrade, monitoring, StringComparison.OrdinalIgnoreCase));
        if (vendor != null)
            filtered = filtered.Where(a => string.Equals(a.Vendor, vendor, StringComparison.OrdinalIgnoreCase));
        if (criticality != null)
            filtered = filtered.Where(a => string.Equals(a.CriticalityType, criticality, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        var vm = new ApplicationRegisterViewModel
        {
            Application = application,
            Status = status,
            Function = function,
            TimeRoadmap = timeRoadmap,
            TechGrade = techGrade,
            Location = location,
            ComplianceGrade = complianceGrade,
            TechnicalDebt = technicalDebt,
            OperatingRegion = operatingRegion,
            Monitoring = monitoring,
            Vendor = vendor,
            Criticality = criticality,
            TotalCount = all.Count,
            ApplicationOptions = DistinctSorted(all.Select(a => a.Name)),
            StatusOptions = DistinctSorted(all.Select(a => a.Status)),
            FunctionOptions = DistinctSorted(all.Select(a => a.Function)),
            TimeRoadmapOptions = DistinctSorted(all.Select(a => a.TimeRoadmap)),
            TechGradeOptions = DistinctSorted(all.Select(a => a.TechGrade)),
            LocationOptions = DistinctSorted(all.Select(a => a.Location)),
            ComplianceGradeOptions = DistinctSorted(all.Select(a => a.ComplianceGrade)),
            TechnicalDebtOptions = DistinctSorted(all.Select(a => a.TechnicalDebt)),
            OperatingRegionOptions = DistinctSorted(all.Select(a => a.OperatingRegion)),
            MonitoringOptions = DistinctSorted(all.Select(a => a.MonitoringGrade)),
            VendorOptions = DistinctSorted(all.Select(a => a.Vendor)),
            // The defaulted value must always be offered, even if nothing carries it yet.
            CriticalityOptions = DistinctSorted(all.Select(a => a.CriticalityType)
                .Concat([CriticalityPropagation.Critical, CriticalityPropagation.NonCritical])),
            Applications = list.Select(a => new ApplicationRowViewModel
            {
                Id = a.Id,
                Name = string.IsNullOrWhiteSpace(a.Name) ? $"(unnamed #{a.Id})" : a.Name!,
                Status = a.Status,
                Function = a.Function,
                ApplicationType = a.ApplicationType,
                TimeRoadmap = a.TimeRoadmap,
                TechGrade = a.TechGrade,
                ComplianceGrade = a.ComplianceGrade,
                Location = a.Location,
                BusinessCriticality = a.BusinessCriticality,
                CriticalityType = a.CriticalityType,
                Tco = tcoLookup.GetValueOrDefault(a.Id),
                LinkedDbCount = linkedLookup.GetValueOrDefault(a.Id),
                LinkedServerCount = linkedServerLookup.GetValueOrDefault(a.Id),
                ServiceOwner = a.ServiceOwner,
                OperatingRegion = a.OperatingRegion,
                Vendor = a.Vendor,
                TechnicalDebt = a.TechnicalDebt,
                MonitoringGrade = a.MonitoringGrade
            }).ToList()
        };

        return View(vm);
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var linkedDatabases = await _db.Database.SqlQueryRaw<LinkedDatabaseItemViewModel>(
            """
            SELECT
                d.tx_id AS DatabaseId,
                d.database_name AS DatabaseName,
                d.server_name AS ServerName,
                d.environment AS Environment,
                d.database_status AS DatabaseStatus,
                d.database_edition AS DatabaseEdition,
                d.data_centre_location AS DataCentreLocation
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
            WHERE l.application_id = {0}
            ORDER BY d.database_name
            """, id).ToListAsync();

        var linkedServers = await GetLinkedServersAsync(id);

        return View(new ApplicationDetailsViewModel
        {
            Application = app,
            LinkedDatabases = linkedDatabases,
            LinkedServers = linkedServers.Select(ToLinkedServerItem).ToList()
        });
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> LinkedDatabases(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var databases = await _db.Database.SqlQueryRaw<LinkedDatabaseDto>(
            """
            SELECT
                l.id AS LinkId,
                d.tx_id AS DatabaseId,
                d.database_name AS DatabaseName,
                d.server_name AS ServerName,
                d.environment AS Environment,
                d.database_status AS DatabaseStatus,
                d.database_edition AS DatabaseEdition,
                d.current_service_objective_name AS ServiceObjective,
                d.current_size_mb AS CurrentSizeMb,
                d.free_space_mb AS FreeSpaceMb,
                d.compatibility_level AS CompatibilityLevel,
                d.recovery_model AS RecoveryModel,
                d.data_centre_location AS Region
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
            WHERE l.application_id = {0}
            ORDER BY d.database_name
            """, id).ToListAsync();

        return Json(new
        {
            applicationId = id,
            applicationName = app.Name ?? $"#{id}",
            count = databases.Count,
            databases
        });
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> LinkedServers(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var servers = await GetLinkedServersAsync(id);

        return Json(new
        {
            applicationId = id,
            applicationName = app.Name ?? $"#{id}",
            count = servers.Count,
            servers
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Applications, "delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var app = await _db.CtApplications.FindAsync(id);
        if (app == null) return NotFound();

        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_application_database WHERE application_id = {0}", id);
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_application_server WHERE application_id = {0}", id);
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_costs WHERE application_id = {0}", id);

        _db.CtApplications.Remove(app);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Deleted {app.Name ?? ("#" + id)}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<LinkedServerDto>> GetLinkedServersAsync(int applicationId)
    {
        var rows = await _db.Database.SqlQueryRaw<LinkedServerDto>(
            """
            SELECT
                COALESCE(p.explicit_link_id, p.inferred_link_id, 0) AS LinkId,
                COALESCE(s.tx_id, p.server_id) AS ServerId,
                COALESCE(s.server_name, p.explicit_name, p.inferred_name) AS ServerName,
                COALESCE(p.explicit_source, p.inferred_source) AS SourceText,
                s.environment AS Environment,
                s.server_status AS ServerStatus,
                s.tower AS Tower,
                s.subscription AS Subscription,
                s.data_centre_location AS DataCentreLocation
            FROM (
                SELECT
                    LOWER(LTRIM(RTRIM(u.server_name))) AS server_key,
                    MAX(u.server_id) AS server_id,
                    MAX(CASE WHEN u.inferred = 0 THEN u.link_id END) AS explicit_link_id,
                    MAX(CASE WHEN u.inferred = 1 THEN u.link_id END) AS inferred_link_id,
                    MAX(CASE WHEN u.inferred = 0 THEN u.source_text END) AS explicit_source,
                    MAX(CASE WHEN u.inferred = 1 THEN u.source_text END) AS inferred_source,
                    MAX(CASE WHEN u.inferred = 0 THEN u.server_name END) AS explicit_name,
                    MAX(CASE WHEN u.inferred = 1 THEN u.server_name END) AS inferred_name
                FROM (
                    SELECT
                        l.id AS link_id,
                        l.server_id AS server_id,
                        LTRIM(RTRIM(l.server_name)) AS server_name,
                        l.source_text AS source_text,
                        0 AS inferred
                    FROM dbo.ct_application_server l
                    WHERE l.application_id = {0}
                      AND l.server_name IS NOT NULL AND LTRIM(RTRIM(l.server_name)) <> N''

                    UNION ALL

                    SELECT
                        MIN(l.id) AS link_id,
                        CAST(NULL AS int) AS server_id,
                        LTRIM(RTRIM(d.server_name)) AS server_name,
                        N'Linked database' AS source_text,
                        1 AS inferred
                    FROM dbo.ct_application_database l
                    INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
                    WHERE l.application_id = {0}
                      AND d.server_name IS NOT NULL AND LTRIM(RTRIM(d.server_name)) <> N''
                    GROUP BY LTRIM(RTRIM(d.server_name))
                ) u
                GROUP BY LOWER(LTRIM(RTRIM(u.server_name)))
            ) p
            OUTER APPLY (
                SELECT TOP (1)
                    inv.tx_id,
                    inv.server_name,
                    inv.environment,
                    inv.server_status,
                    inv.tower,
                    inv.subscription,
                    inv.data_centre_location
                FROM dbo.ct_servers inv
                WHERE (p.server_id IS NOT NULL AND inv.tx_id = p.server_id)
                   OR LOWER(LTRIM(RTRIM(inv.server_name))) = p.server_key
                ORDER BY CASE WHEN p.server_id IS NOT NULL AND inv.tx_id = p.server_id THEN 0 ELSE 1 END, inv.tx_id
            ) s
            """, applicationId).ToListAsync();

        return rows
            .OrderBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static LinkedServerItemViewModel ToLinkedServerItem(LinkedServerDto s) => new()
    {
        ServerId = s.ServerId,
        ServerName = s.ServerName,
        Environment = s.Environment,
        ServerStatus = s.ServerStatus,
        Tower = s.Tower,
        Subscription = s.Subscription,
        DataCentreLocation = s.DataCentreLocation
    };

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class AppTcoRow
    {
        public int ApplicationId { get; set; }
        public string? Tco { get; set; }
    }

    private sealed class AppLinkCountRow
    {
        public int ApplicationId { get; set; }
        public int Count { get; set; }
    }

    private sealed class LinkedDatabaseDto
    {
        public int LinkId { get; set; }
        public int DatabaseId { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string? ServerName { get; set; }
        public string? Environment { get; set; }
        public string? DatabaseStatus { get; set; }
        public string? DatabaseEdition { get; set; }
        public string? ServiceObjective { get; set; }
        public int? CurrentSizeMb { get; set; }
        public int? FreeSpaceMb { get; set; }
        public string? CompatibilityLevel { get; set; }
        public string? RecoveryModel { get; set; }
        public string? Region { get; set; }
    }

    private sealed class LinkedServerDto
    {
        public int LinkId { get; set; }
        public int? ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? Environment { get; set; }
        public string? ServerStatus { get; set; }
        public string? Tower { get; set; }
        public string? Subscription { get; set; }
        public string? DataCentreLocation { get; set; }
    }
}

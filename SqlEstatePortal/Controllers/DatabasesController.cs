using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class DatabasesController : Controller
{
    private readonly AppDbContext _db;

    public DatabasesController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> Index(
        string? databaseName,
        string? serverName,
        string? status,
        string? environment,
        string? edition,
        string? location,
        string? active,
        string? criticality)
    {
        databaseName = Norm(databaseName);
        serverName = Norm(serverName);
        status = Norm(status);
        environment = Norm(environment);
        edition = Norm(edition);
        location = Norm(location);
        active = Norm(active);
        criticality = Norm(criticality);

        // Same rule as the Servers register: default to Critical on a plain page
        // load, honour an explicit choice (including "All") once the form posts.
        if (!Request.Query.ContainsKey("criticality"))
            criticality = CriticalityPropagation.Critical;

        var all = await _db.CtDatabases.AsNoTracking()
            .OrderBy(d => d.ServerName)
            .ThenBy(d => d.DatabaseName)
            .ToListAsync();

        var appLinkCounts = await _db.Database.SqlQueryRaw<DbAppLinkCountRow>(
            """
            SELECT database_id AS DatabaseId, COUNT(*) AS Count
            FROM dbo.ct_application_database
            GROUP BY database_id
            """).ToListAsync();
        var appLinkLookup = appLinkCounts.ToDictionary(x => x.DatabaseId, x => x.Count);

        var servers = await _db.CtServers.AsNoTracking()
            .Select(s => new { s.TxId, s.ServerName })
            .ToListAsync();
        var serverLookup = servers
            .GroupBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TxId, StringComparer.OrdinalIgnoreCase);

        var filtered = all.AsEnumerable();
        if (databaseName != null)
            filtered = filtered.Where(d => string.Equals(d.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase));
        if (serverName != null)
            filtered = filtered.Where(d => string.Equals(d.ServerName, serverName, StringComparison.OrdinalIgnoreCase));
        if (status != null)
            filtered = filtered.Where(d => string.Equals(d.DatabaseStatus, status, StringComparison.OrdinalIgnoreCase));
        if (environment != null)
            filtered = filtered.Where(d => string.Equals(d.Environment, environment, StringComparison.OrdinalIgnoreCase));
        if (edition != null)
            filtered = filtered.Where(d => string.Equals(d.DatabaseEdition, edition, StringComparison.OrdinalIgnoreCase));
        if (location != null)
            filtered = filtered.Where(d => string.Equals(d.DataCentreLocation, location, StringComparison.OrdinalIgnoreCase));
        if (criticality != null)
            filtered = filtered.Where(d => string.Equals(d.CriticalityType, criticality, StringComparison.OrdinalIgnoreCase));
        if (active != null)
        {
            var wantActive = active is "1" or "true" or "yes" or "active";
            filtered = filtered.Where(d => d.IsActive == wantActive);
        }

        var list = filtered.ToList();

        var vm = new DatabaseRegisterViewModel
        {
            DatabaseName = databaseName,
            ServerName = serverName,
            Status = status,
            Environment = environment,
            Edition = edition,
            Location = location,
            Active = active,
            Criticality = criticality,
            TotalCount = all.Count,
            DatabaseNameOptions = DistinctSorted(all.Select(d => d.DatabaseName)),
            ServerNameOptions = DistinctSorted(all.Select(d => d.ServerName)),
            StatusOptions = DistinctSorted(all.Select(d => d.DatabaseStatus)),
            EnvironmentOptions = DistinctSorted(all.Select(d => d.Environment)),
            EditionOptions = DistinctSorted(all.Select(d => d.DatabaseEdition)),
            LocationOptions = DistinctSorted(all.Select(d => d.DataCentreLocation)),
            // The defaulted value must always be offered, even if nothing carries it yet.
            CriticalityOptions = DistinctSorted(all.Select(d => d.CriticalityType)
                .Concat([CriticalityPropagation.Critical, CriticalityPropagation.NonCritical])),
            Databases = list.Select(d =>
            {
                int? serverId = null;
                if (!string.IsNullOrWhiteSpace(d.ServerName) &&
                    serverLookup.TryGetValue(d.ServerName, out var sid))
                    serverId = sid;

                return new DatabaseRowViewModel
                {
                    TxId = d.TxId,
                    DatabaseName = d.DatabaseName,
                    ServerName = d.ServerName,
                    ServerId = serverId,
                    CriticalityType = d.CriticalityType,
                    DatabaseStatus = d.DatabaseStatus,
                    DatabaseOwner = d.DatabaseOwner,
                    Environment = d.Environment,
                    DatabaseEdition = d.DatabaseEdition,
                    ServiceObjective = d.CurrentServiceObjectiveName,
                    DataCentreLocation = d.DataCentreLocation,
                    CurrentSizeMb = d.CurrentSizeMb,
                    MaxSizeGb = d.MaxSizeGb,
                    FreeSpaceMb = d.FreeSpaceMb,
                    IsActive = d.IsActive,
                    ElasticPoolName = d.ElasticPoolName,
                    CompatibilityLevel = d.CompatibilityLevel,
                    RecoveryModel = d.RecoveryModel,
                    LinkedApplicationCount = appLinkLookup.GetValueOrDefault(d.TxId)
                };
            }).ToList()
        };

        return View(vm);
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var db = await _db.CtDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.TxId == id);
        if (db == null) return NotFound();

        LinkedServerItemViewModel? linkedServer = null;
        if (!string.IsNullOrWhiteSpace(db.ServerName))
        {
            linkedServer = await _db.CtServers.AsNoTracking()
                .Where(s => s.ServerName == db.ServerName)
                .Select(s => new LinkedServerItemViewModel
                {
                    ServerId = s.TxId,
                    ServerName = s.ServerName,
                    Environment = s.Environment,
                    ServerStatus = s.ServerStatus,
                    Tower = s.Tower,
                    Subscription = s.Subscription,
                    DataCentreLocation = s.DataCentreLocation
                })
                .FirstOrDefaultAsync();

            if (linkedServer == null)
            {
                linkedServer = new LinkedServerItemViewModel
                {
                    ServerName = db.ServerName
                };
            }
        }

        var linkedApplications = await _db.Database.SqlQueryRaw<LinkedApplicationItemViewModel>(
            """
            SELECT
                a.id AS ApplicationId,
                a.name AS ApplicationName,
                a.status AS Status,
                a.[function] AS [Function],
                a.application_type AS ApplicationType,
                a.location AS Location,
                a.service_owner AS ServiceOwner,
                a.operating_region AS OperatingRegion
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_applications a ON a.id = l.application_id
            WHERE l.database_id = {0}
            ORDER BY a.name
            """, id).ToListAsync();

        return View(new DatabaseDetailsViewModel
        {
            Database = db,
            LinkedServer = linkedServer,
            LinkedApplications = linkedApplications
        });
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> LinkedApplications(int id)
    {
        var db = await _db.CtDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.TxId == id);
        if (db == null) return NotFound();

        var applications = await _db.Database.SqlQueryRaw<LinkedApplicationItemViewModel>(
            """
            SELECT
                a.id AS ApplicationId,
                a.name AS ApplicationName,
                a.status AS Status,
                a.[function] AS [Function],
                a.application_type AS ApplicationType,
                a.location AS Location,
                a.service_owner AS ServiceOwner,
                a.operating_region AS OperatingRegion
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_applications a ON a.id = l.application_id
            WHERE l.database_id = {0}
            ORDER BY a.name
            """, id).ToListAsync();

        return Json(new
        {
            databaseId = id,
            databaseName = db.DatabaseName,
            count = applications.Count,
            applications
        });
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> LinkedServer(int id)
    {
        var db = await _db.CtDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.TxId == id);
        if (db == null) return NotFound();

        LinkedServerItemViewModel? server = null;
        if (!string.IsNullOrWhiteSpace(db.ServerName))
        {
            server = await _db.CtServers.AsNoTracking()
                .Where(s => s.ServerName == db.ServerName)
                .Select(s => new LinkedServerItemViewModel
                {
                    ServerId = s.TxId,
                    ServerName = s.ServerName,
                    Environment = s.Environment,
                    ServerStatus = s.ServerStatus,
                    Tower = s.Tower,
                    Subscription = s.Subscription,
                    DataCentreLocation = s.DataCentreLocation
                })
                .FirstOrDefaultAsync();

            if (server == null)
            {
                server = new LinkedServerItemViewModel
                {
                    ServerName = db.ServerName!
                };
            }
        }

        var servers = server == null ? Array.Empty<LinkedServerItemViewModel>() : new[] { server };

        return Json(new
        {
            databaseId = id,
            databaseName = db.DatabaseName,
            count = servers.Length,
            servers
        });
    }

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class DbAppLinkCountRow
    {
        public int DatabaseId { get; set; }
        public int Count { get; set; }
    }
}


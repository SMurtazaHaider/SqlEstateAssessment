using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.Services;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class InventoryServersController : Controller
{
    internal const string CriticalityCritical = "Critical";
    internal const string CriticalityNonCritical = "Non Critical";
    internal const string AuthWindows = "Windows Auth";
    internal const string AuthMfa = "MFA";

    private static readonly string[] CriticalityValues = [CriticalityCritical, CriticalityNonCritical];
    private static readonly string[] AuthTypeValues = [AuthWindows, AuthMfa];
    private static readonly string[] ServerTypeValues = ["SQL Servers", "APP Servers", "Others"];

    private readonly AppDbContext _db;
    private readonly ServerReachabilityService _reachability;
    private readonly PermissionService _permissions;

    public InventoryServersController(
        AppDbContext db,
        ServerReachabilityService reachability,
        PermissionService permissions)
    {
        _db = db;
        _reachability = reachability;
        _permissions = permissions;
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> Index(
        string? serverName,
        string? serverType,
        string? environment,
        string? status,
        string? subscription,
        string? dataCentre,
        string? criticality,
        string? authType)
    {
        serverName = Norm(serverName);
        serverType = Norm(serverType);
        environment = Norm(environment);
        status = Norm(status);
        subscription = Norm(subscription);
        dataCentre = Norm(dataCentre);
        criticality = Norm(criticality);
        authType = Norm(authType);

        // Default the Criticality filter to Critical when the page is opened
        // without an explicit filter submission (sidebar navigation, or RESET).
        // Once the form has been submitted the chosen value is honoured,
        // including "All", which posts an empty value and so is present in the
        // query string.
        if (!Request.Query.ContainsKey("criticality"))
            criticality = CriticalityCritical;

        var all = await _db.CtServers.AsNoTracking()
            .OrderBy(s => s.ServerName)
            .ToListAsync();

        var dbStats = await _db.Database.SqlQueryRaw<ServerDbStatRow>(
            """
            SELECT
                server_name AS ServerName,
                COUNT(*) AS DatabaseCount
            FROM dbo.ct_database
            WHERE server_name IS NOT NULL AND LTRIM(RTRIM(server_name)) <> N''
              AND is_active = 1
            GROUP BY server_name
            """).ToListAsync();

        var statsLookup = dbStats.ToDictionary(
            x => x.ServerName,
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var appLinkRows = await _db.Database.SqlQueryRaw<ServerAppLinkRow>(
            """
            SELECT server_id AS ServerId, server_name AS ServerName, application_id AS ApplicationId
            FROM dbo.ct_application_server

            UNION

            SELECT
                s.tx_id AS ServerId,
                LTRIM(RTRIM(d.server_name)) AS ServerName,
                l.application_id AS ApplicationId
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
            LEFT JOIN dbo.ct_servers s ON LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
            WHERE d.server_name IS NOT NULL AND LTRIM(RTRIM(d.server_name)) <> N''
            """).ToListAsync();

        var filtered = all.AsEnumerable();
        if (serverName != null)
            filtered = filtered.Where(s => string.Equals(s.ServerName, serverName, StringComparison.OrdinalIgnoreCase));
        if (serverType != null)
            filtered = filtered.Where(s => string.Equals(s.ServerType, serverType, StringComparison.OrdinalIgnoreCase));
        if (environment != null)
            filtered = filtered.Where(s => string.Equals(s.Environment, environment, StringComparison.OrdinalIgnoreCase));
        if (status != null)
            filtered = filtered.Where(s => string.Equals(s.ServerStatus, status, StringComparison.OrdinalIgnoreCase));
        if (subscription != null)
            filtered = filtered.Where(s => string.Equals(s.Subscription, subscription, StringComparison.OrdinalIgnoreCase));
        if (dataCentre != null)
            filtered = filtered.Where(s => string.Equals(s.DataCentreLocation, dataCentre, StringComparison.OrdinalIgnoreCase));
        if (criticality != null)
            filtered = filtered.Where(s => string.Equals(s.CriticalityType, criticality, StringComparison.OrdinalIgnoreCase));
        if (authType != null)
            filtered = filtered.Where(s => string.Equals(s.AuthType, authType, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        int LinkedAppCount(CtServer s) =>
            appLinkRows
                .Where(l =>
                    (l.ServerId.HasValue && l.ServerId.Value == s.TxId) ||
                    string.Equals(l.ServerName, s.ServerName, StringComparison.OrdinalIgnoreCase))
                .Select(l => l.ApplicationId)
                .Distinct()
                .Count();

        var vm = new ServerRegisterViewModel
        {
            ServerName = serverName,
            ServerType = serverType,
            Environment = environment,
            Status = status,
            Subscription = subscription,
            DataCentre = dataCentre,
            Criticality = criticality,
            AuthType = authType,
            TotalCount = all.Count,
            FilteredCount = list.Count,
            CanAdd = await _permissions.HasAsync(User, AppModules.InventoryServers, "insert"),
            CanEdit = await _permissions.HasAsync(User, AppModules.InventoryServers, "update"),
            ServerNameOptions = DistinctSorted(all.Select(s => s.ServerName)),
            ServerTypeOptions = DistinctSorted(all.Select(s => s.ServerType)),
            EnvironmentOptions = DistinctSorted(all.Select(s => s.Environment)),
            // Always offer both reachability states, so the default selection is
            // still shown even when no server currently carries that status.
            StatusOptions = DistinctSorted(all.Select(s => s.ServerStatus)
                .Concat([ServerReachabilityService.StatusReachable, ServerReachabilityService.StatusUnreachable])),
            // Same reasoning: the defaulted Criticality value must always be offered.
            CriticalityOptions = DistinctSorted(all.Select(s => s.CriticalityType).Concat(CriticalityValues)),
            AuthTypeOptions = DistinctSorted(all.Select(s => s.AuthType).Concat(AuthTypeValues)),
            SubscriptionOptions = DistinctSorted(all.Select(s => s.Subscription)),
            DataCentreOptions = DistinctSorted(all.Select(s => s.DataCentreLocation)),
            Servers = list.Select(s =>
            {
                statsLookup.TryGetValue(s.ServerName, out var stats);
                return new ServerRowViewModel
                {
                    TxId = s.TxId,
                    ServerName = s.ServerName,
                    ServerType = s.ServerType,
                    CriticalityType = s.CriticalityType,
                    AuthType = s.AuthType,
                    Environment = s.Environment,
                    ServerStatus = s.ServerStatus,
                    SqlProduct = s.SqlProduct,
                    SupportStatus = s.SupportStatus,
                    SqlEdition = s.SqlEdition,
                    SqlVersion = s.SqlVersion,
                    Subscription = s.Subscription,
                    DataCentreLocation = s.DataCentreLocation,
                    DatabaseCount = stats?.DatabaseCount ?? 0,
                    LinkedApplicationCount = LinkedAppCount(s)
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.InventoryServers, "update")]
    public async Task<IActionResult> CheckServerStatus(CancellationToken cancellationToken)
    {
        var result = await _reachability.CheckAllAsync(cancellationToken);
        var message =
            $"Server status checked for {result.Total} servers: {result.Reachable} Reachable, {result.Unreachable} UnReachable.";

        var wantsJson = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (wantsJson)
        {
            return Json(new
            {
                ok = true,
                message,
                total = result.Total,
                reachable = result.Reachable,
                unreachable = result.Unreachable,
                redirectUrl = Url.Action(nameof(Index))
            });
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var linkedDatabases = await _db.CtDatabases.AsNoTracking()
            .Where(d => d.ServerName == server.ServerName)
            .OrderBy(d => d.DatabaseName)
            .Select(d => new LinkedDatabaseItemViewModel
            {
                DatabaseId = d.TxId,
                DatabaseName = d.DatabaseName,
                ServerName = d.ServerName,
                Environment = d.Environment,
                DatabaseStatus = d.DatabaseStatus,
                DatabaseEdition = d.DatabaseEdition,
                DataCentreLocation = d.DataCentreLocation
            })
            .ToListAsync();

        var linkedApplications = (await GetLinkedApplicationsAsync(id, server.ServerName))
            .Select(ToLinkedApplicationItem)
            .ToList();

        return View(new ServerDetailsViewModel
        {
            Server = server,
            LinkedDatabases = linkedDatabases,
            LinkedApplications = linkedApplications
        });
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> ServerDatabases(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var databases = await _db.CtDatabases.AsNoTracking()
            .Where(d => d.ServerName == server.ServerName)
            .OrderBy(d => d.DatabaseName)
            .Select(d => new
            {
                databaseId = d.TxId,
                databaseName = d.DatabaseName,
                serverName = d.ServerName,
                environment = d.Environment,
                databaseStatus = d.DatabaseStatus,
                databaseEdition = d.DatabaseEdition,
                serviceObjective = d.CurrentServiceObjectiveName,
                currentSizeMb = d.CurrentSizeMb,
                freeSpaceMb = d.FreeSpaceMb,
                compatibilityLevel = d.CompatibilityLevel,
                recoveryModel = d.RecoveryModel,
                region = d.DataCentreLocation,
                elasticPoolName = d.ElasticPoolName
            })
            .ToListAsync();

        return Json(new
        {
            serverId = id,
            serverName = server.ServerName,
            count = databases.Count,
            databases
        });
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> LinkedApplications(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var applications = await GetLinkedApplicationsAsync(id, server.ServerName);

        return Json(new
        {
            serverId = id,
            serverName = server.ServerName,
            count = applications.Count,
            applications
        });
    }

    private async Task<List<LinkedApplicationDto>> GetLinkedApplicationsAsync(int serverId, string serverName)
    {
        var rows = await _db.Database.SqlQueryRaw<LinkedApplicationDto>(
            """
            SELECT
                a.id AS ApplicationId,
                a.name AS ApplicationName,
                a.status AS Status,
                a.[function] AS [Function],
                a.application_type AS ApplicationType,
                a.location AS Location,
                a.service_owner AS ServiceOwner,
                a.operating_region AS OperatingRegion,
                MAX(x.server_name) AS LinkedServerName
            FROM (
                SELECT l.application_id, LTRIM(RTRIM(l.server_name)) AS server_name
                FROM dbo.ct_application_server l
                WHERE l.server_id = {0}
                   OR LOWER(LTRIM(RTRIM(l.server_name))) = LOWER({1})

                UNION ALL

                SELECT l.application_id, LTRIM(RTRIM(d.server_name)) AS server_name
                FROM dbo.ct_application_database l
                INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
                WHERE LOWER(LTRIM(RTRIM(d.server_name))) = LOWER({1})
            ) x
            INNER JOIN dbo.ct_applications a ON a.id = x.application_id
            GROUP BY
                a.id, a.name, a.status, a.[function], a.application_type,
                a.location, a.service_owner, a.operating_region
            """, serverId, serverName).ToListAsync();

        return rows
            .OrderBy(a => a.ApplicationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static LinkedApplicationItemViewModel ToLinkedApplicationItem(LinkedApplicationDto a) => new()
    {
        ApplicationId = a.ApplicationId,
        ApplicationName = a.ApplicationName,
        Status = a.Status,
        Function = a.Function,
        ApplicationType = a.ApplicationType,
        Location = a.Location,
        ServiceOwner = a.ServiceOwner,
        OperatingRegion = a.OperatingRegion
    };

    [RequirePermission(AppModules.InventoryServers, "insert")]
    public async Task<IActionResult> Create()
    {
        var vm = new ServerFormViewModel
        {
            CriticalityType = CriticalityCritical,
            AuthType = AuthWindows,
            ServerType = "SQL Servers",
            IsActive = true
        };
        return View("Form", await BuildFormAsync(vm));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.InventoryServers, "insert")]
    public async Task<IActionResult> Create(ServerFormViewModel model, CancellationToken ct)
    {
        await ValidateAsync(model, null, ct);
        if (!ModelState.IsValid)
            return View("Form", await BuildFormAsync(model));

        var now = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "unknown";

        var server = new CtServer
        {
            ServerName = model.ServerName.Trim(),
            ServerType = model.ServerType.Trim(),
            CriticalityType = model.CriticalityType.Trim(),
            AuthType = model.AuthType.Trim(),
            Fqdn = Norm(model.Fqdn),
            IpAddress = Norm(model.IpAddress),
            Environment = Norm(model.Environment),
            Subscription = Norm(model.Subscription),
            DataCentreLocation = Norm(model.DataCentreLocation),
            Tower = Norm(model.Tower),
            Notes = Norm(model.Notes),
            IsActive = model.IsActive,
            ServerStatus = null,
            CreatedBy = actor,
            CreatedOn = now
        };

        _db.CtServers.Add(server);
        await _db.SaveChangesAsync(ct);

        var (linked, delinked) = await ApplyDatabaseLinksAsync(server, model, ct);
        var (appLinked, appUnlinked) = await ApplyApplicationLinksAsync(server, model, actor, now, ct);

        await CriticalityPropagation.ApplyAsync(_db, ct);

        TempData["Success"] = $"Added {server.ServerName}." +
            DescribeLinkChanges(linked, delinked) +
            DescribeAppLinkChanges(appLinked, appUnlinked);
        return RedirectToAction(nameof(Details), new { id = server.TxId });
    }

    [RequirePermission(AppModules.InventoryServers, "update")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id, ct);
        if (server == null) return NotFound();

        var vm = new ServerFormViewModel
        {
            TxId = server.TxId,
            ServerName = server.ServerName,
            ServerType = server.ServerType ?? string.Empty,
            CriticalityType = server.CriticalityType ?? CriticalityCritical,
            AuthType = server.AuthType ?? AuthWindows,
            Fqdn = server.Fqdn,
            IpAddress = server.IpAddress,
            Environment = server.Environment,
            Subscription = server.Subscription,
            DataCentreLocation = server.DataCentreLocation,
            Tower = server.Tower,
            Notes = server.Notes,
            IsActive = server.IsActive
        };

        return View("Form", await BuildFormAsync(vm));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.InventoryServers, "update")]
    public async Task<IActionResult> Edit(int id, ServerFormViewModel model, CancellationToken ct)
    {
        if (id != model.TxId) return BadRequest();

        var server = await _db.CtServers.FirstOrDefaultAsync(s => s.TxId == id, ct);
        if (server == null) return NotFound();

        await ValidateAsync(model, id, ct);
        if (!ModelState.IsValid)
            return View("Form", await BuildFormAsync(model));

        var now = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "unknown";
        var previousName = server.ServerName;
        var newName = model.ServerName.Trim();

        server.ServerName = newName;
        server.ServerType = model.ServerType.Trim();
        server.CriticalityType = model.CriticalityType.Trim();
        server.AuthType = model.AuthType.Trim();
        server.Fqdn = Norm(model.Fqdn);
        server.IpAddress = Norm(model.IpAddress);
        server.Environment = Norm(model.Environment);
        server.Subscription = Norm(model.Subscription);
        server.DataCentreLocation = Norm(model.DataCentreLocation);
        server.Tower = Norm(model.Tower);
        server.Notes = Norm(model.Notes);
        server.IsActive = model.IsActive;
        server.UpdatedBy = actor;
        server.UpdatedOn = now;

        // Databases point at a server by name, so a rename has to carry its
        // databases with it or they would silently become orphaned.
        var renamed = 0;
        if (!string.Equals(previousName, newName, StringComparison.OrdinalIgnoreCase))
        {
            var toRename = await _db.CtDatabases
                .Where(d => d.ServerName == previousName || d.ServerId == server.TxId)
                .ToListAsync(ct);
            foreach (var db in toRename)
            {
                db.ServerName = newName;
                db.ServerId = server.TxId;
            }
            renamed = toRename.Count;

            // ct_application_server is keyed on (application_id, server_name),
            // so its rows have to follow the rename or the links are orphaned.
            var linksToRename = await _db.CtApplicationServers
                .Where(l => l.ServerName == previousName)
                .ToListAsync(ct);
            foreach (var link in linksToRename)
            {
                link.ServerName = newName;
                link.ServerId = server.TxId;
            }
        }

        await _db.SaveChangesAsync(ct);

        var (linked, delinked) = await ApplyDatabaseLinksAsync(server, model, ct);
        var (appLinked, appUnlinked) = await ApplyApplicationLinksAsync(server, model, actor, now, ct);

        await CriticalityPropagation.ApplyAsync(_db, ct);

        var renameNote = renamed > 0
            ? $" Moved {renamed} database(s) from {previousName}."
            : string.Empty;

        TempData["Success"] = $"Updated {newName}." +
            DescribeLinkChanges(linked, delinked) +
            DescribeAppLinkChanges(appLinked, appUnlinked) +
            renameNote;
        return RedirectToAction(nameof(Details), new { id = server.TxId });
    }

    private async Task ValidateAsync(ServerFormViewModel model, int? existingId, CancellationToken ct)
    {
        var name = Norm(model.ServerName);
        if (name == null)
            return; // [Required] already reported it

        var clash = await _db.CtServers
            .AsNoTracking()
            .AnyAsync(s => s.ServerName == name && (existingId == null || s.TxId != existingId.Value), ct);

        if (clash)
            ModelState.AddModelError(nameof(model.ServerName), "A server with this name already exists.");

        if (Norm(model.CriticalityType) is string crit
            && !CriticalityValues.Contains(crit, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.CriticalityType), "Choose Critical or Non Critical.");

        if (Norm(model.AuthType) is string auth
            && !AuthTypeValues.Contains(auth, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.AuthType), "Choose Windows Auth or MFA.");
    }

    /// <summary>
    /// Applies the link / delink tick boxes. A database belongs to a server by
    /// name (ct_database.server_name), so linking sets that name and delinking
    /// clears it.
    /// </summary>
    private async Task<(int Linked, int Delinked)> ApplyDatabaseLinksAsync(
        CtServer server,
        ServerFormViewModel model,
        CancellationToken ct)
    {
        var linkIds = model.LinkDatabaseIds.Distinct().ToList();
        var delinkIds = model.DelinkDatabaseIds.Distinct().Except(linkIds).ToList();

        if (linkIds.Count == 0 && delinkIds.Count == 0)
            return (0, 0);

        var touched = await _db.CtDatabases
            .Where(d => linkIds.Contains(d.TxId) || delinkIds.Contains(d.TxId))
            .ToListAsync(ct);

        var linked = 0;
        var delinked = 0;

        foreach (var db in touched)
        {
            if (linkIds.Contains(db.TxId))
            {
                if (db.ServerId == server.TxId)
                    continue;
                // Both are written: the id is the real link, the name keeps the
                // readers that still match on it working.
                db.ServerId = server.TxId;
                db.ServerName = server.ServerName;
                linked++;
            }
            else
            {
                // Only clear a link that actually points at this server.
                if (db.ServerId != server.TxId
                    && !string.Equals(db.ServerName, server.ServerName, StringComparison.OrdinalIgnoreCase))
                    continue;
                db.ServerId = null;
                db.ServerName = null;
                delinked++;
            }
        }

        if (linked > 0 || delinked > 0)
            await _db.SaveChangesAsync(ct);

        return (linked, delinked);
    }

    /// <summary>
    /// Applications are many-to-many with servers, so links are rows in
    /// ct_application_server rather than a column. Both server_id and
    /// server_name are written: the unique key uses the name, while the id is
    /// what the register's join reads.
    /// </summary>
    private async Task<(int Linked, int Unlinked)> ApplyApplicationLinksAsync(
        CtServer server,
        ServerFormViewModel model,
        string actor,
        DateTime now,
        CancellationToken ct)
    {
        var linkIds = model.LinkApplicationIds.Distinct().ToList();
        var unlinkIds = model.UnlinkApplicationIds.Distinct().Except(linkIds).ToList();

        if (linkIds.Count == 0 && unlinkIds.Count == 0)
            return (0, 0);

        var existing = await _db.CtApplicationServers
            .Where(l => l.ServerName == server.ServerName)
            .ToListAsync(ct);

        var unlinked = 0;
        if (unlinkIds.Count > 0)
        {
            var toRemove = existing.Where(l => unlinkIds.Contains(l.ApplicationId)).ToList();
            _db.CtApplicationServers.RemoveRange(toRemove);
            unlinked = toRemove.Count;
        }

        var alreadyLinked = existing.Select(l => l.ApplicationId).ToHashSet();
        var linked = 0;
        foreach (var appId in linkIds)
        {
            if (alreadyLinked.Contains(appId))
                continue; // the unique key would reject a duplicate

            _db.CtApplicationServers.Add(new CtApplicationServer
            {
                ApplicationId = appId,
                ServerId = server.TxId,
                ServerName = server.ServerName,
                SourceText = "Linked in Server Register",
                CreatedBy = actor,
                CreatedOn = now
            });
            linked++;
        }

        if (linked > 0 || unlinked > 0)
            await _db.SaveChangesAsync(ct);

        return (linked, unlinked);
    }

    private static string DescribeAppLinkChanges(int linked, int unlinked)
    {
        var parts = new List<string>();
        if (linked > 0) parts.Add($"Linked {linked} application(s)");
        if (unlinked > 0) parts.Add($"unlinked {unlinked} application(s)");
        return parts.Count == 0 ? string.Empty : $" {string.Join(", ", parts)}.";
    }

    private static string DescribeLinkChanges(int linked, int delinked)
    {
        var parts = new List<string>();
        if (linked > 0) parts.Add($"Linked {linked} database(s)");
        if (delinked > 0) parts.Add($"delinked {delinked} database(s)");
        return parts.Count == 0 ? string.Empty : $" {string.Join(", ", parts)}.";
    }

    private async Task<ServerFormViewModel> BuildFormAsync(ServerFormViewModel model)
    {
        var servers = await _db.CtServers.AsNoTracking().ToListAsync();

        model.ServerTypeOptions = DistinctSorted(servers.Select(s => s.ServerType).Concat(ServerTypeValues));
        model.CriticalityOptions = CriticalityValues;
        model.AuthTypeOptions = AuthTypeValues;
        model.EnvironmentOptions = DistinctSorted(servers.Select(s => s.Environment));

        var name = Norm(model.ServerName);

        model.LinkedDatabases = model.TxId == 0 && name == null
            ? []
            : await _db.CtDatabases.AsNoTracking()
                .Where(d => (model.TxId != 0 && d.ServerId == model.TxId)
                            || (name != null && d.ServerName == name))
                .OrderBy(d => d.DatabaseName)
                .Select(d => new ServerDatabaseLinkViewModel
                {
                    TxId = d.TxId,
                    DatabaseName = d.DatabaseName,
                    ServerName = d.ServerName,
                    Environment = d.Environment,
                    DatabaseStatus = d.DatabaseStatus
                })
                .ToListAsync();

        model.AvailableDatabases = await _db.CtDatabases.AsNoTracking()
            .Where(d => d.ServerId == null && (d.ServerName == null || d.ServerName == ""))
            .OrderBy(d => d.DatabaseName)
            .Take(500)
            .Select(d => new ServerDatabaseLinkViewModel
            {
                TxId = d.TxId,
                DatabaseName = d.DatabaseName,
                ServerName = d.ServerName,
                Environment = d.Environment,
                DatabaseStatus = d.DatabaseStatus
            })
            .ToListAsync();

        await LoadApplicationLinksAsync(model, name);

        return model;
    }

    private async Task LoadApplicationLinksAsync(ServerFormViewModel model, string? serverName)
    {
        var apps = await _db.CtApplications.AsNoTracking()
            .Where(a => a.Name != null && a.Name != "")
            .Select(a => new { a.Id, Name = a.Name! })
            .ToListAsync();

        var appNames = apps.ToDictionary(a => a.Id, a => a.Name);
        var explicitIds = new HashSet<int>();

        if (serverName != null)
        {
            explicitIds = (await _db.CtApplicationServers.AsNoTracking()
                    .Where(l => l.ServerName == serverName)
                    .Select(l => l.ApplicationId)
                    .ToListAsync())
                .ToHashSet();

            // Applications reached through a database that sits on this server.
            // These follow from the database links, so they are read-only here.
            var inferredIds = await _db.Database.SqlQueryRaw<int>(
                SqlInferredApplicationIds, serverName).ToListAsync();

            model.LinkedApplications = explicitIds
                .Where(appNames.ContainsKey)
                .Select(id => new ServerApplicationLinkViewModel { Id = id, Name = appNames[id] })
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            model.InferredApplications = inferredIds
                .Where(id => !explicitIds.Contains(id) && appNames.ContainsKey(id))
                .Select(id => new ServerApplicationLinkViewModel
                {
                    Id = id,
                    Name = appNames[id],
                    SourceText = "via a linked database"
                })
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        model.AvailableApplications = apps
            .Where(a => !explicitIds.Contains(a.Id))
            .Select(a => new ServerApplicationLinkViewModel { Id = a.Id, Name = a.Name })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private const string SqlInferredApplicationIds = """
        SELECT DISTINCT l.application_id AS Value
        FROM dbo.ct_application_database l
        INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
        WHERE LOWER(LTRIM(RTRIM(d.server_name))) = LOWER(LTRIM(RTRIM({0})))
        """;

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class ServerDbStatRow
    {
        public string ServerName { get; set; } = string.Empty;
        public int DatabaseCount { get; set; }
    }

    private sealed class ServerAppLinkRow
    {
        public int? ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public int ApplicationId { get; set; }
    }

    private sealed class LinkedApplicationDto
    {
        public int ApplicationId { get; set; }
        public string? ApplicationName { get; set; }
        public string? Status { get; set; }
        public string? Function { get; set; }
        public string? ApplicationType { get; set; }
        public string? Location { get; set; }
        public string? ServiceOwner { get; set; }
        public string? OperatingRegion { get; set; }
        public string? LinkedServerName { get; set; }
    }
}

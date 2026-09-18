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
public class AssessmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly AssessmentRunnerService _runner;
    private readonly ServerReachabilityService _reachability;
    private readonly InventorySyncService _inventorySync;
    private readonly AssessmentCompareService _compareService;
    private readonly ServerQaCompareService _qaCompareService;

    public AssessmentsController(
        AppDbContext db,
        AssessmentRunnerService runner,
        ServerReachabilityService reachability,
        InventorySyncService inventorySync,
        AssessmentCompareService compareService,
        ServerQaCompareService qaCompareService)
    {
        _db = db;
        _runner = runner;
        _reachability = reachability;
        _inventorySync = inventorySync;
        _compareService = compareService;
        _qaCompareService = qaCompareService;
    }

    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Index()
    {
        var runs = await _db.AssessmentRuns
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .ToListAsync();

        var runIds = runs.Select(r => r.Id).ToList();
        var syncBatches = await _db.InventorySyncBatches.AsNoTracking()
            .Where(b => runIds.Contains(b.AssessmentRunId))
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync();

        // Latest batch per assessment run
        var syncByRun = syncBatches
            .GroupBy(b => b.AssessmentRunId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<AssessmentListItemViewModel>();
        foreach (var r in runs)
        {
            syncByRun.TryGetValue(r.Id, out var batch);
            var syncStatus = batch?.Status;
            var syncEligible = string.Equals(r.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                && (syncStatus == null
                    || string.Equals(syncStatus, InventorySyncService.StatusPending, StringComparison.OrdinalIgnoreCase));

            var hasChanges = false;
            if (syncEligible)
            {
                if (batch != null
                    && string.Equals(syncStatus, InventorySyncService.StatusPending, StringComparison.OrdinalIgnoreCase))
                {
                    hasChanges = batch.NewCount + batch.ChangedCount + batch.RemovedCount > 0;
                }
                else
                {
                    hasChanges = await _inventorySync.HasChangesAsync(r.Id);
                }
            }

            rows.Add(new AssessmentListItemViewModel
            {
                Run = r,
                SyncBatchId = batch?.Id,
                SyncStatus = syncStatus,
                ShowSyncToRegister = syncEligible && hasChanges,
                ShowNoChangesFound = syncEligible && !hasChanges
            });
        }

        return View(rows);
    }

    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var run = await _db.AssessmentRuns
            .Include(x => x.Findings)
            .Include(x => x.Servers)
            .Include(x => x.Databases)
            .Include(x => x.Volumes)
            .Include(x => x.Services)
            .Include(x => x.Waits)
            .Include(x => x.Jobs)
            .Include(x => x.Sysadmins)
            .Include(x => x.Configurations)
            .Include(x => x.Backups)
            .Include(x => x.LinkedServers)
            .Include(x => x.SqlLogins)
            .Include(x => x.AvailabilityGroups)
            .Include(x => x.Certificates)
            .Include(x => x.TlsCertificates)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (run == null) return NotFound();

        var available = await _db.AssessmentRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .Select(r => new AssessmentRunSummary
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                Status = r.Status
            })
            .ToListAsync();

        var syncBatch = await _db.InventorySyncBatches.AsNoTracking()
            .Where(b => b.AssessmentRunId == id)
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync();

        var syncStatus = syncBatch?.Status;
        var syncEligible = string.Equals(run.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            && (syncStatus == null
                || string.Equals(syncStatus, InventorySyncService.StatusPending, StringComparison.OrdinalIgnoreCase));

        var hasChanges = false;
        if (syncEligible)
        {
            if (syncBatch != null
                && string.Equals(syncStatus, InventorySyncService.StatusPending, StringComparison.OrdinalIgnoreCase))
            {
                hasChanges = syncBatch.NewCount + syncBatch.ChangedCount + syncBatch.RemovedCount > 0;
            }
            else
            {
                hasChanges = await _inventorySync.HasChangesAsync(id);
            }
        }

        return View(new AssessmentDetailsViewModel
        {
            Run = run,
            AvailableRuns = available,
            SyncBatchId = syncBatch?.Id,
            SyncStatus = syncStatus,
            ShowSyncToRegister = syncEligible && hasChanges,
            ShowNoChangesFound = syncEligible && !hasChanges
        });
    }

    [HttpGet]
    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> ReachableServers()
    {
        var servers = await _db.CtServers.AsNoTracking()
            .Where(s => s.ServerStatus == ServerReachabilityService.StatusReachable &&
                       (s.ServerType == "SQL Servers" || s.ServerType == "SQL" || (string.IsNullOrEmpty(s.ServerType) && s.ServerName.Contains("SQL"))))
            .OrderBy(s => s.ServerName)
            .Select(s => new
            {
                id = s.TxId,
                name = s.ServerName,
                environment = s.Environment,
                status = s.ServerStatus
            })
            .ToListAsync();

        return Json(new { count = servers.Count, servers });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Assessments, "insert")]
    public async Task<IActionResult> Run(string[]? servers, CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "unknown";
        var selected = (servers ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        AssessmentRun run;
        try
        {
            run = await _runner.RunAsync(username, selected, cancellationToken);
        }
        catch (Exception ex)
        {
            var wantsJsonError = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);
            if (wantsJsonError)
                return BadRequest(new { ok = false, message = ex.Message });

            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        var succeeded = run.Status == "Succeeded";
        var message = succeeded
            ? $"Assessment #{run.Id} completed."
            : $"Assessment #{run.Id} failed: {run.ErrorMessage}";

        var wantsJson = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (wantsJson)
        {
            return Json(new
            {
                ok = succeeded,
                id = run.Id,
                status = run.Status,
                message,
                redirectUrl = Url.Action(nameof(Details), new { id = run.Id })
            });
        }

        TempData[succeeded ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Details), new { id = run.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Assessments, "insert")]
    public async Task<IActionResult> CheckServerStatus(CancellationToken cancellationToken)
    {
        var result = await _reachability.CheckAllAsync(cancellationToken);
        var message =
            $"Server status checked for {result.Total} servers: {result.Reachable} Reachable, {result.Unreachable} UnReachable.";
        TempData["Success"] = message;

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
                unreachable = result.Unreachable
            });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Compare(int? baseRunId, int? targetRunId, CancellationToken ct = default)
    {
        var available = await _db.AssessmentRuns
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .Select(r => new AssessmentRunSummary
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                Status = r.Status,
                ReachableCount = r.ReachableCount,
                ServerCount = r.ServerCount,
                CriticalCount = r.CriticalCount,
                HighCount = r.HighCount,
                MediumCount = r.MediumCount,
                LowCount = r.LowCount
            })
            .ToListAsync(ct);

        if (available.Count == 0)
        {
            return View(new AssessmentCompareViewModel { AvailableRuns = available });
        }

        // Default selection: if not provided, pick latest as target, and prior as base
        if (!targetRunId.HasValue && available.Count > 0)
        {
            targetRunId = available[0].Id;
        }

        if (!baseRunId.HasValue)
        {
            if (available.Count > 1)
            {
                baseRunId = available[1].Id;
            }
            else if (available.Count > 0)
            {
                baseRunId = available[0].Id;
            }
        }

        if (!baseRunId.HasValue || !targetRunId.HasValue)
        {
            return View(new AssessmentCompareViewModel
            {
                BaseRunId = baseRunId,
                TargetRunId = targetRunId,
                AvailableRuns = available
            });
        }

        var baseRun = await _db.AssessmentRuns
            .Include(x => x.Findings)
            .Include(x => x.Servers)
            .Include(x => x.Databases)
            .Include(x => x.Backups)
            .Include(x => x.Configurations)
            .Include(x => x.LinkedServers)
            .Include(x => x.SqlLogins)
            .Include(x => x.AvailabilityGroups)
            .Include(x => x.Certificates)
            .Include(x => x.TlsCertificates)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == baseRunId.Value, ct);

        var targetRun = await _db.AssessmentRuns
            .Include(x => x.Findings)
            .Include(x => x.Servers)
            .Include(x => x.Databases)
            .Include(x => x.Backups)
            .Include(x => x.Configurations)
            .Include(x => x.LinkedServers)
            .Include(x => x.SqlLogins)
            .Include(x => x.AvailabilityGroups)
            .Include(x => x.Certificates)
            .Include(x => x.TlsCertificates)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == targetRunId.Value, ct);

        if (baseRun == null || targetRun == null)
        {
            TempData["Error"] = "One or both selected assessment runs could not be found.";
            return View(new AssessmentCompareViewModel
            {
                BaseRunId = baseRunId,
                TargetRunId = targetRunId,
                AvailableRuns = available
            });
        }

        var model = _compareService.Compare(baseRun, targetRun, available);
        return View(model);
    }

    [HttpGet]
    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> CompareServers(
        string? server1,
        string? server2,
        int? run1,
        int? run2,
        CancellationToken ct = default)
    {
        var availableRuns = await _qaCompareService.GetAvailableRunsAsync(ct);

        var servers1 = run1.HasValue
            ? await _qaCompareService.GetServersForRunAsync(run1.Value, ct)
            : [];
        var servers2 = run2.HasValue
            ? await _qaCompareService.GetServersForRunAsync(run2.Value, ct)
            : [];

        var vm = new ServerQaCompareViewModel
        {
            Server1 = server1,
            Server2 = server2,
            Server1RunId = run1,
            Server2RunId = run2,
            AvailableRuns = availableRuns,
            AvailableServers1 = servers1,
            AvailableServers2 = servers2
        };

        if (string.IsNullOrWhiteSpace(server1) || string.IsNullOrWhiteSpace(server2) || !run1.HasValue || !run2.HasValue)
            return View(vm);

        if (string.Equals(server1.Trim(), server2.Trim(), StringComparison.OrdinalIgnoreCase)
            && run1.Value == run2.Value)
        {
            TempData["Error"] = "Select two different servers, or the same server with two different assessment dates.";
            return View(vm);
        }

        var left = await _qaCompareService.LoadAsync(server1.Trim(), run1, ct);
        var right = await _qaCompareService.LoadAsync(server2.Trim(), run2, ct);
        if (left == null || right == null)
        {
            TempData["Error"] = "Could not load assessment data for one or both selected dates/servers.";
            return View(vm);
        }

        return View(_qaCompareService.Compare(left, right, availableRuns, servers1, servers2));
    }

    [HttpGet]
    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> AssessmentRunServers(int? runId, CancellationToken ct = default)
    {
        if (!runId.HasValue)
            return Json(Array.Empty<object>());

        var servers = await _qaCompareService.GetServersForRunAsync(runId.Value, ct);
        return Json(servers.Select(name => new { name }));
    }
}

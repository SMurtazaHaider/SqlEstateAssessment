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
    private readonly FindingOverrideService _findingOverrides;
    private readonly PermissionService _permissions;

    public AssessmentsController(
        AppDbContext db,
        AssessmentRunnerService runner,
        ServerReachabilityService reachability,
        InventorySyncService inventorySync,
        AssessmentCompareService compareService,
        ServerQaCompareService qaCompareService,
        FindingOverrideService findingOverrides,
        PermissionService permissions)
    {
        _db = db;
        _runner = runner;
        _reachability = reachability;
        _inventorySync = inventorySync;
        _compareService = compareService;
        _qaCompareService = qaCompareService;
        _findingOverrides = findingOverrides;
        _permissions = permissions;
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
            ShowNoChangesFound = syncEligible && !hasChanges,
            // Scoped to this run: the hover history shows the moves made on the
            // assessment being looked at, not moves made on an earlier one.
            FindingHistory = await _findingOverrides.GetHistoryAsync(id, run.Findings),
            CanMoveFindings = await _permissions.HasAsync(User, AppModules.Assessments, "update")
        });
    }

    /// <summary>
    /// Moves one finding to a different severity, with a mandatory comment.
    ///
    /// The comment is validated here and not only in the browser - the form can
    /// be posted directly, and a blank comment would leave an unexplained change
    /// in an audit trail whose whole purpose is to explain changes.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Assessments, "update")]
    public async Task<IActionResult> MoveFinding(
        int id, int findingId, string toSeverity, string comment, CancellationToken ct = default)
    {
        var finding = await _db.AssessmentFindings
            .FirstOrDefaultAsync(f => f.Id == findingId && f.AssessmentRunId == id, ct);

        if (finding == null)
        {
            TempData["FindingMoveError"] = "That finding no longer exists on this assessment.";
            return RedirectToAction(nameof(Details), new { id });
        }

        comment = (comment ?? string.Empty).Trim();

        if (comment.Length == 0)
        {
            TempData["FindingMoveError"] = "A comment is required when moving a finding.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (comment.Length > FindingOverrideService.MaxCommentLength)
        {
            TempData["FindingMoveError"] =
                $"The comment is too long (maximum {FindingOverrideService.MaxCommentLength} characters).";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!FindingOverrideService.IsValidTarget(toSeverity))
        {
            TempData["FindingMoveError"] = "Choose a severity to move the finding to.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var actor = User.Identity?.Name ?? "Unknown";
        var from = finding.Severity;
        var row = await _findingOverrides.MoveAsync(finding, toSeverity, comment, actor, id, ct);

        if (row == null)
        {
            TempData["FindingMoveError"] = $"That finding is already {finding.Severity}.";
        }
        else
        {
            // The run's tiles are stored counts, so they have to be recomputed
            // here or the Summary and Findings tabs disagree until the next
            // restart.
            await RecountAsync(id, ct);
            TempData["FindingMoveOk"] = $"Moved from {from} to {row.ToSeverity}.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Recomputes one run's severity tiles after a finding has been moved.
    ///
    /// Set-based on purpose. Loading the run with .Include(r => r.Findings) looks
    /// obvious and is a trap: AssessmentRuns carries HtmlContent as
    /// nvarchar(max) - the whole stored HTML report - and the join repeats it
    /// once per finding. On a run with 500 findings and a few MB of report that
    /// is gigabytes over the wire, and the request simply hangs. The same
    /// mistake hung application startup twice; see Data/SeverityRerankPass.
    /// </summary>
    private async Task RecountAsync(int runId, CancellationToken ct)
    {
        // Plain string with a {0} placeholder rather than an interpolated one:
        // ExecuteSqlRawAsync raises EF1002 for interpolation, and this way runId
        // travels as a real parameter.
        const string sql = @"
            UPDATE r
               SET CriticalCount = x.Critical,
                   HighCount     = x.High,
                   MediumCount   = x.Medium,
                   LowCount      = x.Low,
                   InfoCount     = x.Info
              FROM AssessmentRuns AS r
             CROSS APPLY (
                -- ISNULL matters: SUM over a run with no findings returns NULL,
                -- and these are NOT NULL int columns.
                SELECT
                    ISNULL(SUM(CASE WHEN f.Severity = N'Critical' THEN 1 ELSE 0 END), 0) AS Critical,
                    ISNULL(SUM(CASE WHEN f.Severity = N'High'     THEN 1 ELSE 0 END), 0) AS High,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Medium'   THEN 1 ELSE 0 END), 0) AS Medium,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Low'      THEN 1 ELSE 0 END), 0) AS Low,
                    ISNULL(SUM(CASE WHEN f.Severity = N'Info'     THEN 1 ELSE 0 END), 0) AS Info
                FROM AssessmentFindings AS f
                WHERE f.AssessmentRunId = r.Id
             ) AS x
             WHERE r.Id = {0};";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { runId }, ct);
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

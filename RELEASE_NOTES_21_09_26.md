# Release Notes - September 21, 2026

## 1. New assessment sections: linked servers, SQL logins, availability groups
- The collector had always queried **linked servers**, **SQL logins** and **availability groups**, but `AssessmentRunnerService.ImportJsonAsync` read only eight sections, so all three were parsed and discarded on every run. They are now persisted and surfaced.
- New tables `AssessmentLinkedServers`, `AssessmentSqlLogins`, `AssessmentAvailabilityGroups`, each cascade-deleting with their run.
- New **Linked servers**, **SQL logins** and **Availability groups** tabs on assessment details, with the usual column filters and server filter.
- **Assessment Compare** gains New / Removed / Changed diffs for all three, keyed on server + name (+ replica for AGs), with per-field `old -> new` change lists. Tabs appear only when there is something to show.
- Data appears from the next assessment run onwards; earlier runs discarded it at import time.

## 2. Certificates
- **New collector queries.** Database certificates come from `master.sys.certificates`, mapped to the databases they protect via `sys.dm_database_encryption_keys`. The DMV read sits in its own TRY/CATCH, so an instance without `VIEW SERVER STATE` still returns the certificate inventory, just without the Protects column.
- **TLS / connection certificates** are read from the instance's `SuperSocketNetLib` registry keys (thumbprint and `ForceEncryption`), also guarded. A blank thumbprint is reported as self-signed.
- **Expiry findings** in area `Encryption`: **Critical** when already expired, **High** within 30 days, **Medium** within 90, naming the databases each certificate protects. A **Low** is raised when an instance has no configured TLS certificate. These flow into the existing severity tabs, dashboard counts and findings diff.
- New tables `AssessmentCertificates` and `AssessmentTlsCertificates`, a **Certificates** tab on assessment details, and a **Certificates Diff** tab on Compare. The diff also flags a certificate that has merely crossed into the 90-day window between two runs, which no field-level comparison would catch.

## 3. SQL QA Compare covers the new sections
- `ServerQaCompareService.Flatten` knew about six data sets; it now also flattens linked servers, SQL logins, availability groups, certificates and TLS certificates.
- **Linked servers** and **SQL logins** are compared as *presence*: one row per item, Key = its name, Value = Yes / No. A server that has the item and one that does not now produce a single row with an explicit **No**, rather than the "-" placeholder used for genuinely absent data.
- Certificate **thumbprints are deliberately not compared** - they differ between any two servers by design, so every row would score No and drag the match percentage down for something that is not a parity defect. Expiry, private-key encryption and protected databases are compared instead.

## 4. Server register: add and edit
- **Add server** button and per-row **Edit**, gated on the InventoryServers insert / update permissions.
- Mandatory fields: **name**, **Type**, **Criticality**, **Auth Type**. Criticality and Auth Type are validated server-side against their allowed values rather than trusting the dropdown, and the server name is checked for uniqueness.
- New `ct_servers.criticality_type` (Critical / Non Critical) and `ct_servers.auth_type` (Windows Auth / MFA). Existing rows are backfilled to Critical / Windows Auth.
- **Criticality filter, defaulting to Critical.** The Status filter default reverts to All, so only one filter is pre-applied. "All" remains selectable: the default applies only when the page is opened without an explicit filter submission.
- Grid trimmed to Server, Type, Criticality, Auth Type, Environment, Status, Databases, Linked Apps, Actions. Product, Support, Edition, Version, Subscription and Data Centre remain on the details page and as filters.
- Heading now reads "Showing *n* of *N*" so a filtered view cannot be mistaken for an empty register.

## 5. Linking databases and applications from the server form
- **Databases**: linked databases are listed with a Delink tick box, unassigned databases with a Link tick box (capped at 500).
- **Applications**: explicit links from `ct_application_server` with an Unlink tick box, plus a multi-select picker for the rest. Applications reached *through* a linked database are listed read-only, since they follow from the database link and cannot be unlinked here.
- **Renaming a server now carries its links.** Databases point at a server by name and `ct_application_server` is keyed on `(application_id, server_name)`, so a rename would previously have orphaned both. The Edit action reassigns them and reports how many moved.

## 6. Databases now link to servers by id
- New `ct_database.server_id` with `FK_ct_database_server` referencing `ct_servers(tx_id)` `ON DELETE SET NULL`, plus an index and a backfill from the existing name match.
- `server_name` is still written alongside it, so every existing reader keeps working. The backfill re-runs on each startup because `InventorySyncService` still writes only the name.
- Delink is no longer name-dependent, and "unassigned" now requires both a null id and an empty name, so a database cannot be offered for linking to a second server.

## 7. Criticality inherited from servers
- New `ct_database.criticality_type` and `ct_applications.criticality_type`, recomputed by `Data/CriticalityPropagation.cs` at startup and after every server save.
- A database takes its server's rating. An application is **Critical if any server it touches is Critical**, counting explicit links and servers reached through a linked database. An application with no server link is left alone - there is no basis to rate it.
- The whole estate is recomputed in one pass rather than patched incrementally: at this scale it costs almost nothing and removes the drift that incremental updates would introduce whenever a link changed or a sync rewrote a name.
- **Criticality filters, defaulting to Critical**, added to the Database Register and Application Register, matching the Servers register. A Criticality column is shown on both.
- `ct_applications.business_criticality` is **not** touched. An interim build wrote into it; this release restores the original values from a snapshot column and then drops that column. See section 5 of the schema script.

## 8. Reachability now probes the SQL port
- `ServerReachabilityService` tried ICMP ping only, so managed endpoints that answer on 1433 while dropping ICMP - Azure SQL, load balancers, hardened hosts - were permanently marked UnReachable.
- It now attempts a TCP connect on the SQL port first (honouring `host,port`), falling back to ping for named instances on dynamic ports and hosts that block 1433 from the portal.

## Database changes
All schema changes apply automatically on startup. `Scripts/release-2026-09-21-schema.sql` contains the same statements for review or manual pre-application.

| Table | Change |
|---|---|
| `AssessmentLinkedServers` | new |
| `AssessmentSqlLogins` | new |
| `AssessmentAvailabilityGroups` | new |
| `AssessmentCertificates` | new |
| `AssessmentTlsCertificates` | new |
| `ct_servers` | `criticality_type`, `auth_type` (+ backfill) |
| `ct_database` | `server_id` (+ FK, index, backfill), `criticality_type` |
| `ct_applications` | `criticality_type`; `business_criticality` restored, snapshot column dropped |

`Scripts/insert_ct_servers_sql_servers.sql` adds the 14 SQL servers to `ct_servers`; it skips names already present.

## Known issues and follow-ups
- **Certificate query times out on some instances.** `CTUCTA03VMSQL02` exceeds the collector's 60-second query timeout, so certificates come back empty for it. The fix is to split the certificate read and the encryption-key map into separate round trips, so a stalled DMV costs the Protects column rather than the whole section.
- **`sys.dm_os_host_info` does not exist on SQL Server 2016**, so the Host section is empty across most of the estate and logs a warning per server each run. Pre-existing; needs a version guard falling back to `sys.dm_os_windows_info`.
- **`gamarinesqlsrvprd.database.windows.net` requires MFA** and cannot be assessed. Interactive MFA is incompatible with unattended collection; a service principal would be needed, which in turn requires moving the collector from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`. Separately, most collector queries do not apply to Azure SQL Database. Servers marked **MFA** could be skipped by the runner to stop the per-run failures.
- **Manual database links can be overwritten** by the next inventory sync, which writes `ct_database.server_name` from assessment results. Application links are unaffected, being real rows.

---

# Release Notes - September 18, 2026

## Server-wise SQL QA Compare
- **New page**: Added **SQL QA Compare** (`/Assessments/CompareServers`) to compare configuration and inventory parameters between two SQL Server instances, modeled on the legacy MigratePlus server-wise QA report.
- **Date-first selection**: Choose **Assessment date #1** then **Server Instance #1**, and independently **Assessment date #2** then **Server Instance #2**, so you can compare Server A on date X with Server B on date Y (or the same server across two dates).
- **Match scoring**: Rows are scored **Yes** (exact), **Close** (near match via PHP-compatible `similar_text`), or **No** (different or missing on one side).
- **Compared areas**: Server properties, `sp_configure`, services, databases, volumes, and sysadmins from the selected assessment runs (no new database tables).
- **Match summary**: Visual bar and clickable Yes / Close / No / Total tiles above the grid; tiles filter the results table.
- **Export**: CSV export of the current filtered compare rows.
- **Navigation**: Sidebar entry plus shortcuts from Dashboard, Assessments list, assessment details, and Compare Assessments.

---

# Release Notes - September 3, 2026

## Linked Apps / Servers via Databases
- **Application Register**: Linked server counts, detail lists, and popup JSON now include servers inferred from linked databases (`ct_application_database` → `ct_database.server_name`), merged with explicit `ct_application_server` links and deduplicated by server name.
- **Server Register**: Linked application counts, detail lists, and popup JSON now include applications inferred the same way (apps whose linked databases sit on that server), with distinct application IDs so counts stay accurate.
- **Shared query helpers**: `GetLinkedServersAsync` / `GetLinkedApplicationsAsync` centralize the union logic for details views and Linked popups.

---

# Release Notes - August 28, 2026

## 1. Assessment Comparison Feature
- **Side-by-Side Assessment Diff**: Added comprehensive assessment comparison tool (`AssessmentCompareService`, `Assessments/Compare.cshtml`) allowing side-by-side analysis between any two assessment runs.
- **Executive KPI Summary**: Displays deltas with contextual improved/degraded badges for Total Servers, Reachable Servers, Total Databases, Storage (Allocated/Data/Log), Backup coverage, and High/Medium/Low findings.
- **Findings Diff**: Categorizes findings into **New**, **Resolved**, and **Ongoing** issues across runs.
- **Granular Infrastructure Diff**: Highlights server, database, backup timestamp, and SQL configuration changes with clean `Old → New` visual indicators (only shown when values changed).
- **Navigation Shortcuts**: Added "Compare Assessments" buttons across Dashboard, Assessments list, and Assessment detail views.

## 2. Inventory Synchronization & Approval Workflow
- **Maker-Checker vs. Auto-Direct Modes**: Configurable `InventorySync:Mode` (`MakerChecker` or `AutoDirect`) in `appsettings.json` with status badge on Sync History.
- **Enforced Maker-Checker Protection**: Removed direct startup database updates for backup times and database owners to ensure all changes flow through the approval review workflow.
- **Intelligent Change Detection**: "Sync to register" button only displays when differences exist; displays "No changes found" otherwise.
- **Large Batch Dual-Protection Fix**:
  - Configured global ASP.NET Core `FormOptions` and action attributes (`[RequestFormLimits]`, `[RequestSizeLimit]`) to eliminate HTTP 400 Bad Request errors on batches with thousands of fields.
  - Implemented client-side JSON serialization on form submit for high-speed, lightweight payload delivery.
- **Progress Overlay on Sync Actions**: Animated loading bar and live elapsed duration timer displayed during "Approve & apply" and "Save for later".
- **Formatting Fix**: Resolved date-parser issue ensuring decimal numbers (e.g. storage GB, CPU counts) are preserved accurately as numbers.

## 3. Server Type Categorization & Assessment Scoping
- **`server_type` Column**: Added `server_type` to `ct_servers` with automatic categorization (`APP Servers`, `SQL Servers`, `Others`).
- **Server Register Grid & Filter**: Added searchable "Type" filter and sortable "Type" column on the Servers register and details views.
- **Targeted SQL Operations**: Restricted "Check Server Status" reachability pings and assessment execution to SQL servers only.

## 4. Navigation & Layout Improvements
- **Sidebar Menu Button**: Placed dedicated menu toggle button inside sidebar header adjacent to brand logo.
- **Responsive Drawer**: Added backdrop overlay, click-outside dismissal, Escape key closing, and auto-dismissal when clicking links on mobile/narrow viewports.

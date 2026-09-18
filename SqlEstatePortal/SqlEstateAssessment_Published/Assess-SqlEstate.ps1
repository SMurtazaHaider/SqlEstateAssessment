#Requires -Version 5.1
<#
.SYNOPSIS
    Read-only SQL Server estate assessment. Never modifies data, schemas, or configuration.

.DESCRIPTION
    Connects to each SQL Server in the list using SELECT/DMV monitoring queries only.
    Collects status, cost drivers, alerts, performance, security, licensing,
    standards, supportability, and backup/SLA signals, then writes a Single Source
    of Truth report (HTML, JSON, Markdown).

.PARAMETER Servers
    SQL Server names (HOST, HOST\INSTANCE, HOST,port).

.PARAMETER ServerListPath
    Text file with one server per line. Lines starting with # are ignored.

.PARAMETER OutputDirectory
    Folder for reports. Created if missing. Default: .\reports

.PARAMETER Credential
    SQL authentication. Omit for Windows authentication.

.PARAMETER ConnectionTimeout
    Seconds to wait for a connection. Default: 15

.PARAMETER QueryTimeout
    Seconds per query. Default: 60

.PARAMETER TrustServerCertificate
    Set when connecting with Encrypt and a self-signed certificate.

.PARAMETER SampleSeconds
    Seconds between two performance counter samples (for per-sec rates). Default: 5

.PARAMETER FullBackupSlaHours
    Full backup older than this is an SLA breach. Default: 24

.PARAMETER LogBackupSlaMinutes
    Full/Bulk-logged databases with log backup older than this is an SLA breach. Default: 60

.PARAMETER StorageUsdPerGbMonth
    Optional storage cost rate used in the cost section. 0 skips dollar estimates.

.PARAMETER StandardCoreLicenseUsd
    Optional Standard edition core license unit cost. 0 skips dollar estimates.

.PARAMETER EnterpriseCoreLicenseUsd
    Optional Enterprise edition core license unit cost. 0 skips dollar estimates.

.EXAMPLE
    .\Assess-SqlEstate.ps1 -Servers 'SQLPROD01','SQLPROD02\INST2'

.EXAMPLE
    .\Assess-SqlEstate.ps1 -ServerListPath .\servers.txt -TrustServerCertificate

.EXAMPLE
    .\Assess-SqlEstate.ps1 -Servers 'SQLPROD01' -Credential (Get-Credential)
#>
[CmdletBinding(DefaultParameterSetName = 'File')]
param(
    [Parameter(ParameterSetName = 'Inline', Mandatory = $true, Position = 0)]
    [string[]]$Servers,

    [Parameter(ParameterSetName = 'File')]
    [string]$ServerListPath,

    [string]$OutputDirectory,

    [pscredential]$Credential,

    [int]$ConnectionTimeout = 15,

    [int]$QueryTimeout = 60,

    [switch]$TrustServerCertificate,

    [ValidateRange(1, 30)]
    [int]$SampleSeconds = 5,

    [int]$FullBackupSlaHours = 24,

    [int]$LogBackupSlaMinutes = 60,

    [decimal]$StorageUsdPerGbMonth = 0,

    [decimal]$StandardCoreLicenseUsd = 0,

    [decimal]$EnterpriseCoreLicenseUsd = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot -and $PSCommandPath) {
    $scriptRoot = Split-Path -Parent $PSCommandPath
}
if (-not $scriptRoot -and $MyInvocation.MyCommand.Path) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if (-not $scriptRoot -and $psISE -and $psISE.CurrentFile.FullPath) {
    $scriptRoot = Split-Path -Parent $psISE.CurrentFile.FullPath
}
if (-not $scriptRoot) {
    $scriptRoot = (Get-Location).Path
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $scriptRoot 'reports'
}
if ($PSCmdlet.ParameterSetName -eq 'File' -and -not $ServerListPath) {
    $ServerListPath = Join-Path $scriptRoot 'servers.example.txt'
}

# --- helpers -----------------------------------------------------------------

function Get-ServerList {
    if ($PSCmdlet.ParameterSetName -eq 'File') {
        if (-not (Test-Path -LiteralPath $ServerListPath)) {
            throw "Server list not found: $ServerListPath"
        }
        $items = Get-Content -LiteralPath $ServerListPath |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and ($_ -notmatch '^\s*#') }
        if (-not $items) { throw "No servers found in $ServerListPath" }
        return @($items)
    }
    return @($Servers)
}

function Get-SqlConnectionString {
    param(
        [string]$ServerName,
        [bool]$Encrypt,
        [bool]$TrustCert
    )

    $parts = New-Object System.Collections.Generic.List[string]
    [void]$parts.Add("Data Source=$ServerName")
    [void]$parts.Add('Initial Catalog=master')
    [void]$parts.Add("Connect Timeout=$ConnectionTimeout")
    [void]$parts.Add('Application Name=SqlEstateAssessment-ReadOnly')
    [void]$parts.Add("Workstation ID=$($env:COMPUTERNAME)")
    [void]$parts.Add('Pooling=false')
    [void]$parts.Add("Encrypt=$(if ($Encrypt) { 'true' } else { 'false' })")
    if ($TrustCert) {
        [void]$parts.Add('TrustServerCertificate=true')
    }
    if ($Credential) {
        $user = $Credential.UserName.Replace(';', '')
        $pwd = $Credential.GetNetworkCredential().Password.Replace(';', '')
        [void]$parts.Add('Integrated Security=false')
        [void]$parts.Add("User ID=$user")
        [void]$parts.Add("Password=$pwd")
    }
    else {
        [void]$parts.Add('Integrated Security=true')
    }
    return ($parts -join ';')
}

function New-SqlConnection {
    param([string]$ServerName)

    $attempts = @(
        @{ Encrypt = $true;  TrustCert = [bool]$TrustServerCertificate },
        @{ Encrypt = $true;  TrustCert = $true },
        @{ Encrypt = $false; TrustCert = $true }
    )

    $lastError = $null
    foreach ($attempt in $attempts) {
        if ($TrustServerCertificate -and -not $attempt.TrustCert) { continue }
        $conn = $null
        try {
            $cs = Get-SqlConnectionString -ServerName $ServerName -Encrypt $attempt.Encrypt -TrustCert $attempt.TrustCert
            $conn = New-Object System.Data.SqlClient.SqlConnection $cs
            $conn.Open()
            return $conn
        }
        catch {
            $lastError = $_
            if ($null -ne $conn) { $conn.Dispose() }
        }
    }
    if ($null -eq $lastError) {
        throw "Could not open a read-only connection to $ServerName."
    }
    throw $lastError.Exception
}

function New-ResultObject {
    param([hashtable]$Properties)
    $obj = New-Object PSObject
    foreach ($key in $Properties.Keys) {
        $obj | Add-Member -NotePropertyName $key -NotePropertyValue $Properties[$key]
    }
    return $obj
}

function Convert-DataTable {
    param($Table)
    if ($null -eq $Table -or $Table.Rows.Count -eq 0) { return @() }
    $rows = foreach ($row in $Table.Rows) {
        $obj = New-Object PSObject
        foreach ($col in $Table.Columns) {
            $value = $row[$col]
            if ($value -is [DBNull]) { $value = $null }
            $obj | Add-Member -NotePropertyName $col.ColumnName -NotePropertyValue $value
        }
        $obj
    }
    return @($rows)
}

function Invoke-ReadOnlyQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Name,
        [string]$Sql,
        [int]$Timeout = $QueryTimeout
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = $Timeout
    $cmd.CommandType = [System.Data.CommandType]::Text

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $set = New-Object System.Data.DataSet
    try {
        [void]$adapter.Fill($set)
        $tables = @()
        foreach ($table in $set.Tables) {
            $tables += , (Convert-DataTable $table)
        }
        if ($tables.Count -eq 1) { return $tables[0] }
        return $tables
    }
    catch {
        Write-Warning ("{0}: query '{1}' failed: {2}" -f $Connection.DataSource, $Name, $_.Exception.Message)
        return @()
    }
    finally {
        $adapter.Dispose()
        $cmd.Dispose()
        $set.Dispose()
    }
}

function Add-Finding {
    param(
        [System.Collections.IList]$List,
        [string]$Server,
        [ValidateSet('Critical', 'High', 'Medium', 'Low', 'Info')]
        [string]$Severity,
        [string]$Area,
        [string]$Finding,
        [string]$Recommendation
    )
    $List.Add([pscustomobject]@{
            Server           = $Server
            Severity         = $Severity
            Area             = $Area
            Finding          = $Finding
            Recommendation   = $Recommendation
        }) | Out-Null
}

function Get-MajorVersion {
    param($ProductVersion)
    if ([string]::IsNullOrWhiteSpace([string]$ProductVersion)) { return 0 }
    $part = ([string]$ProductVersion).Split('.')[0]
    $n = 0
    [void][int]::TryParse($part, [ref]$n)
    return $n
}

function Get-Supportability {
    param([string]$ProductVersion)

    $major = Get-MajorVersion $ProductVersion
    $map = @{
        16 = @{ Product = 'SQL Server 2022'; MainstreamEnd = '2028-01-11'; ExtendedEnd = '2033-01-11'; Status = 'Supported' }
        15 = @{ Product = 'SQL Server 2019'; MainstreamEnd = '2025-02-28'; ExtendedEnd = '2030-01-08'; Status = 'Extended support' }
        14 = @{ Product = 'SQL Server 2017'; MainstreamEnd = '2022-10-11'; ExtendedEnd = '2027-10-12'; Status = 'Extended support' }
        13 = @{ Product = 'SQL Server 2016'; MainstreamEnd = '2021-07-13'; ExtendedEnd = '2026-07-14'; Status = 'End of support' }
        12 = @{ Product = 'SQL Server 2014'; MainstreamEnd = '2019-07-09'; ExtendedEnd = '2024-07-09'; Status = 'End of support' }
        11 = @{ Product = 'SQL Server 2012'; MainstreamEnd = '2017-07-11'; ExtendedEnd = '2022-07-12'; Status = 'End of support' }
        10 = @{ Product = 'SQL Server 2008/R2'; MainstreamEnd = '2014-07-08'; ExtendedEnd = '2019-07-09'; Status = 'End of support' }
    }
    if ($map.ContainsKey($major)) {
        return New-ResultObject $map[$major]
    }
    if ($major -ge 17) {
        return New-ResultObject @{ Product = "SQL Server (version $major)"; MainstreamEnd = 'Unknown'; ExtendedEnd = 'Unknown'; Status = 'Supported (verify lifecycle)' }
    }
    return New-ResultObject @{ Product = "Unknown ($ProductVersion)"; MainstreamEnd = 'Unknown'; ExtendedEnd = 'Unknown'; Status = 'Unknown' }
}

function Get-LicensedCoreCount {
    param([int]$CpuCount)
    if ($CpuCount -le 0) { return 0 }
    $cores = [Math]::Max(4, $CpuCount)
    if ($cores % 2 -eq 1) { $cores++ }
    return $cores
}

function HtmlEncode {
    param($Value)
    if ($null -eq $Value) { return '' }
    return [System.Net.WebUtility]::HtmlEncode([string]$Value)
}

function Format-Cell {
    param($Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [datetime]) { return $Value.ToString('yyyy-MM-dd HH:mm') }
    if ($Value -is [bool]) { return $(if ($Value) { 'Yes' } else { 'No' }) }
    if ($Value -is [double] -or $Value -is [decimal] -or $Value -is [float]) {
        return ('{0:N2}' -f $Value)
    }
    return [string]$Value
}

function ConvertTo-HtmlTable {
    param(
        [object[]]$Rows,
        [string[]]$Columns
    )
    $rowList = @($Rows)
    if ($rowList.Count -eq 0) {
        return '<p class="muted">None</p>'
    }
    if (-not $Columns) {
        $Columns = @($rowList[0].PSObject.Properties.Name)
    }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<table>')
    [void]$sb.Append('<thead><tr>')
    foreach ($c in $Columns) { [void]$sb.Append("<th>$(HtmlEncode $c)</th>") }
    [void]$sb.AppendLine('</tr></thead><tbody>')
    foreach ($row in $rowList) {
        [void]$sb.Append('<tr>')
        foreach ($c in $Columns) {
            $val = $row.$c
            $cls = ''
            if ($c -eq 'Severity') {
                $cls = ' class="sev-' + ([string]$val).ToLowerInvariant() + '"'
            }
            [void]$sb.Append("<td$cls>$(HtmlEncode (Format-Cell $val))</td>")
        }
        [void]$sb.AppendLine('</tr>')
    }
    [void]$sb.AppendLine('</tbody></table>')
    return $sb.ToString()
}

# --- SQL (SELECT / monitoring only) ------------------------------------------

$QueryInstance = @'
SELECT
    CAST(SERVERPROPERTY('MachineName') AS nvarchar(128))              AS MachineName,
    CAST(SERVERPROPERTY('ServerName') AS nvarchar(128))               AS ServerName,
    CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128))             AS InstanceName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))           AS ProductVersion,
    CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(128))             AS ProductLevel,
    CAST(SERVERPROPERTY('ProductUpdateLevel') AS nvarchar(128))       AS ProductUpdateLevel,
    CAST(SERVERPROPERTY('ProductUpdateReference') AS nvarchar(128))   AS ProductUpdateReference,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128))                  AS Edition,
    CAST(SERVERPROPERTY('EngineEdition') AS int)                      AS EngineEdition,
    CAST(SERVERPROPERTY('LicenseType') AS nvarchar(128))              AS LicenseType,
    CAST(SERVERPROPERTY('NumLicenses') AS int)                        AS NumLicenses,
    CAST(SERVERPROPERTY('IsHadrEnabled') AS int)                      AS IsHadrEnabled,
    CAST(SERVERPROPERTY('IsClustered') AS int)                        AS IsClustered,
    CAST(SERVERPROPERTY('Collation') AS nvarchar(128))                AS Collation,
    CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int)           AS IsIntegratedSecurityOnly,
    i.sqlserver_start_time                                            AS SqlServerStartTime,
    i.cpu_count                                                       AS CpuCount,
    i.hyperthread_ratio                                               AS HyperthreadRatio,
    CAST(i.physical_memory_kb / 1024.0 AS decimal(18,2))              AS PhysicalMemoryMB,
    i.virtual_machine_type_desc                                       AS VirtualMachineType,
    @@VERSION                                                         AS VersionString
FROM sys.dm_os_sys_info AS i;
'@

$QueryConfig = @'
-- Full sp_configure catalogue (incl. advanced). Uses sys.configurations so we do not
-- need to run RECONFIGURE / change show advanced options on the target instance.
SELECT
    c.name,
    CAST(c.minimum AS bigint) AS minimum,
    CAST(c.maximum AS bigint) AS maximum,
    CAST(c.value AS bigint) AS config_value,
    CAST(c.value_in_use AS bigint) AS run_value,
    CAST(c.value_in_use AS bigint) AS value_in_use,
    c.description,
    CAST(c.is_dynamic AS bit) AS is_dynamic,
    CAST(c.is_advanced AS bit) AS is_advanced
FROM sys.configurations AS c
ORDER BY c.name;
'@

$QueryDatabases = @'
SELECT
    d.database_id,
    d.name,
    d.state_desc,
    d.recovery_model_desc,
    d.compatibility_level,
    d.collation_name,
    d.is_read_only,
    d.is_auto_close_on,
    d.is_auto_shrink_on,
    d.page_verify_option_desc,
    d.is_encrypted,
    d.is_trustworthy_on,
    d.is_auto_create_stats_on,
    d.is_auto_update_stats_on,
    d.user_access_desc,
    d.log_reuse_wait_desc,
    d.create_date,
    sp.name AS owner_name,
    CAST(DATABASEPROPERTYEX(d.name, 'LastGoodCheckDbTime') AS datetime) AS LastGoodCheckDbTime,
    CAST((SELECT SUM(mf.size) * 8.0 / 1024
          FROM sys.master_files AS mf
          WHERE mf.database_id = d.database_id AND mf.type = 0) AS decimal(18,2)) AS DataMB,
    CAST((SELECT SUM(mf.size) * 8.0 / 1024
          FROM sys.master_files AS mf
          WHERE mf.database_id = d.database_id AND mf.type = 1) AS decimal(18,2)) AS LogMB
FROM sys.databases AS d
LEFT JOIN sys.server_principals AS sp
    ON d.owner_sid = sp.sid;
'@

$QueryBackups = @'
SELECT
    d.name AS database_name,
    d.name AS DatabaseName,
    MAX(CASE WHEN bs.type = 'D' THEN bs.backup_finish_date END) AS LastFull,
    MAX(CASE WHEN bs.type = 'D' THEN bs.backup_finish_date END) AS LastFullBackup,
    MAX(CASE WHEN bs.type = 'I' THEN bs.backup_finish_date END) AS LastDiff,
    MAX(CASE WHEN bs.type = 'I' THEN bs.backup_finish_date END) AS LastDifferentialBackup,
    MAX(CASE WHEN bs.type = 'L' THEN bs.backup_finish_date END) AS LastLog,
    MAX(CASE WHEN bs.type = 'L' THEN bs.backup_finish_date END) AS LastLogBackup
FROM sys.databases AS d
LEFT JOIN msdb.dbo.backupset AS bs
    ON d.name = bs.database_name
GROUP BY d.name
ORDER BY d.name;
'@

$QueryJobs = @'
SELECT
    j.name AS JobName,
    j.enabled,
    h.run_status,
    CASE h.run_status
        WHEN 0 THEN 'Failed'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Retry'
        WHEN 3 THEN 'Canceled'
        WHEN 4 THEN 'In progress'
        ELSE 'Unknown'
    END AS LastRunStatus,
    CASE
        WHEN h.run_date IS NULL THEN NULL
        ELSE DATETIMEFROMPARTS(
            h.run_date / 10000,
            (h.run_date / 100) % 100,
            h.run_date % 100,
            h.run_time / 10000,
            (h.run_time / 100) % 100,
            h.run_time % 100,
            0)
    END AS LastRun,
    LEFT(h.message, 400) AS Message
FROM msdb.dbo.sysjobs AS j
OUTER APPLY (
    SELECT TOP (1) h2.run_status, h2.run_date, h2.run_time, h2.message
    FROM msdb.dbo.sysjobhistory AS h2
    WHERE h2.job_id = j.job_id AND h2.step_id = 0
    ORDER BY h2.run_date DESC, h2.run_time DESC
) AS h;
'@

$QuerySysadmins = @'
SELECT
    p.name,
    p.type_desc,
    p.is_disabled,
    p.create_date
FROM sys.server_principals AS p
WHERE IS_SRVROLEMEMBER(N'sysadmin', p.name) = 1
  AND p.name IS NOT NULL;
'@

$QuerySqlLogins = @'
SELECT
    l.name,
    l.is_disabled,
    l.is_policy_checked,
    l.is_expiration_checked,
    IS_SRVROLEMEMBER(N'sysadmin', l.name) AS is_sysadmin,
    l.create_date,
    l.modify_date
FROM sys.sql_logins AS l
WHERE l.name NOT LIKE N'##%';
'@

$QueryServices = @'
SELECT servicename, startup_type_desc, status_desc, service_account,
       instant_file_initialization_enabled
FROM sys.dm_server_services;
'@

$QueryVolumes = @'
SELECT DISTINCT
    vs.volume_mount_point,
    vs.logical_volume_name,
    CAST(vs.total_bytes / 1024.0 / 1024 / 1024 AS decimal(18,2))     AS TotalGB,
    CAST(vs.available_bytes / 1024.0 / 1024 / 1024 AS decimal(18,2)) AS FreeGB,
    CAST(100.0 * vs.available_bytes / NULLIF(vs.total_bytes, 0) AS decimal(5,2)) AS FreePct
FROM sys.master_files AS mf
CROSS APPLY sys.dm_os_volume_stats(mf.database_id, mf.file_id) AS vs;
'@

$QueryWaits = @'
SELECT TOP (15)
    wait_type,
    waiting_tasks_count,
    wait_time_ms,
    signal_wait_time_ms,
    CAST(100.0 * wait_time_ms / NULLIF(SUM(wait_time_ms) OVER (), 0) AS decimal(5,2)) AS WaitPct
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (
    N'BROKER_EVENTHANDLER', N'BROKER_RECEIVE_WAITFOR', N'BROKER_TASK_STOP',
    N'BROKER_TO_FLUSH', N'BROKER_TRANSMITTER', N'CHECKPOINT_QUEUE',
    N'CHKPT', N'CLR_AUTO_EVENT', N'CLR_MANUAL_EVENT', N'CLR_SEMAPHORE',
    N'CXCONSUMER', N'DBMIRROR_DBM_EVENT', N'DBMIRROR_EVENTS_QUEUE',
    N'DBMIRROR_WORKER_QUEUE', N'DBMIRRORING_CMD', N'DIRTY_PAGE_POLL',
    N'DISPATCHER_QUEUE_SEMAPHORE', N'EXECSYNC', N'FSAGENT',
    N'FT_IFTS_SCHEDULER_IDLE_WAIT', N'FT_IFTSHC_MUTEX', N'HADR_CLUSAPI_CALL',
    N'HADR_FILESTREAM_IOMGR_IOCOMPLETION', N'HADR_LOGCAPTURE_WAIT',
    N'HADR_NOTIFICATION_DEQUEUE', N'HADR_TIMER_TASK', N'HADR_WORK_QUEUE',
    N'KSOURCE_WAKEUP', N'LAZYWRITER_SLEEP', N'LOGMGR_QUEUE',
    N'MEMORY_ALLOCATION_EXT', N'ONDEMAND_TASK_QUEUE',
    N'PARALLEL_REDO_DRAIN_WORKER', N'PARALLEL_REDO_LOG_POSITION',
    N'PARALLEL_REDO_TRAN_LIST', N'PARALLEL_REDO_WORKER_SYNC',
    N'PARALLEL_REDO_WORKER_WAIT_WORK', N'PREEMPTIVE_OS_FLUSHFILEBUFFERS',
    N'PREEMPTIVE_XE_GETTARGETSTATE', N'PWAIT_ALL_COMPONENTS_INITIALIZED',
    N'PWAIT_DIRECTLOGCONSUMER_GETNEXT', N'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP',
    N'QDS_ASYNC_QUEUE', N'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
    N'QDS_SHUTDOWN_QUEUE', N'REDO_THREAD_PENDING_WORK',
    N'REQUEST_FOR_DEADLOCK_SEARCH', N'RESOURCE_QUEUE', N'SERVER_IDLE_CHECK',
    N'SLEEP_BPOOL_FLUSH', N'SLEEP_DBSTARTUP', N'SLEEP_DCOMSTARTUP',
    N'SLEEP_MASTERDBREADY', N'SLEEP_MASTERMDREADY', N'SLEEP_MASTERUPGRADED',
    N'SLEEP_MSDBSTARTUP', N'SLEEP_SYSTEMTASK', N'SLEEP_TASK',
    N'SLEEP_TEMPDBSTARTUP', N'SNI_HTTP_ACCEPT', N'SOS_WORK_DISPATCHER',
    N'SP_SERVER_DIAGNOSTICS_SLEEP', N'SQLTRACE_BUFFER_FLUSH',
    N'SQLTRACE_INCREMENTAL_FLUSH_SLEEP', N'SQLTRACE_WAIT_ENTRIES',
    N'STARTUP_DEPENDENCY_MANAGER', N'WAIT_FOR_RESULTS', N'WAITFOR',
    N'WAITFOR_TASKSHUTDOWN', N'WAIT_XTP_CKPT_CLOSE', N'WAIT_XTP_HOST_WAIT',
    N'WAIT_XTP_OFFLINE_CKPT_NEW_LOG', N'WAIT_XTP_RECOVERY',
    N'XE_DISPATCHER_WAIT', N'XE_TIMER_EVENT', N'XE_LIVE_TARGET_TVF'
)
AND wait_type NOT LIKE N'SLEEP%'
AND wait_type NOT LIKE N'%IDLE%'
ORDER BY wait_time_ms DESC;
'@

$QueryCounters = @'
SELECT RTRIM(object_name) AS object_name,
       RTRIM(counter_name) AS counter_name,
       RTRIM(instance_name) AS instance_name,
       cntr_value,
       cntr_type
FROM sys.dm_os_performance_counters
WHERE
    (object_name LIKE N'%Buffer Manager%' AND counter_name IN (N'Page life expectancy', N'Buffer cache hit ratio', N'Page lookups/sec', N'Page reads/sec', N'Page writes/sec'))
 OR (object_name LIKE N'%SQL Statistics%' AND counter_name IN (N'Batch Requests/sec', N'SQL Compilations/sec', N'SQL Re-Compilations/sec'))
 OR (object_name LIKE N'%Memory Manager%' AND counter_name IN (N'Total Server Memory (KB)', N'Target Server Memory (KB)', N'Memory Grants Pending'))
 OR (object_name LIKE N'%General Statistics%' AND counter_name IN (N'User Connections', N'Processes blocked'))
 OR (object_name LIKE N'%Locks%' AND instance_name = N'_Total' AND counter_name IN (N'Number of Deadlocks/sec', N'Lock Wait Time (ms)'))
 OR (object_name LIKE N'%Access Methods%' AND counter_name IN (N'Full Scans/sec', N'Index Searches/sec', N'Page Splits/sec'));
'@

$QueryAg = @'
SELECT
    ag.name AS AgName,
    ar.replica_server_name,
    ars.role_desc,
    ars.operational_state_desc,
    ars.connected_state_desc,
    ars.synchronization_health_desc
FROM sys.availability_groups AS ag
JOIN sys.availability_replicas AS ar
    ON ag.group_id = ar.group_id
JOIN sys.dm_hadr_availability_replica_states AS ars
    ON ar.replica_id = ars.replica_id;
'@

$QuerySuspectPages = @'
SELECT DB_NAME(database_id) AS database_name, file_id, page_id, event_type, error_count, last_update_date
FROM msdb.dbo.suspect_pages;
'@

$QueryLinkedServers = @'
SELECT name, data_source, provider, is_linked, is_remote_login_enabled, is_rpc_out_enabled
FROM sys.servers
WHERE is_linked = 1;
'@

$QueryEnterpriseFeatures = @'
SELECT d.name AS database_name, f.feature_name
FROM sys.databases AS d
CROSS APPLY sys.dm_db_persisted_sku_features AS f
WHERE d.database_id = DB_ID();
'@

$QueryHost = @'
SELECT host_platform, host_distribution, host_release
FROM sys.dm_os_host_info;
'@

$QueryTraceFlags = 'DBCC TRACESTATUS(-1) WITH NO_INFOMSGS;'

function Get-CounterMap {
    param($Rows)
    $map = @{}
    foreach ($r in @($Rows)) {
        $key = '{0}|{1}|{2}' -f $r.counter_name.Trim(), $r.instance_name, $r.object_name
        $map[$key] = $r
        $short = $r.counter_name.Trim()
        if (-not $map.ContainsKey($short)) { $map[$short] = $r }
    }
    return $map
}

function Get-CounterValue {
    param($Map, [string]$CounterName)
    if ($Map.ContainsKey($CounterName)) { return [int64]$Map[$CounterName].cntr_value }
    return $null
}

function Assess-Server {
    param([string]$ServerName)

    $result = New-ResultObject @{
        Server             = $ServerName
        Reachable          = $false
        CollectedUtc       = [datetime]::UtcNow
        Error              = $null
        Instance           = $null
        Support            = $null
        Host               = @()
        Configuration      = @()
        Databases          = @()
        Backups            = @()
        Jobs               = @()
        Sysadmins          = @()
        SqlLogins          = @()
        Services           = @()
        Volumes            = @()
        Waits              = @()
        Performance        = $null
        AvailabilityGroups = @()
        SuspectPages       = @()
        LinkedServers      = @()
        TraceFlags         = @()
        Cost               = $null
        Findings           = New-Object System.Collections.Generic.List[object]
    }

    $conn = $null
    try {
        $conn = New-SqlConnection -ServerName $ServerName
        $result.Reachable = $true
    }
    catch {
        $result.Error = $_.Exception.Message
        Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Status' `
            -Finding "Instance unreachable: $($_.Exception.Message)" `
            -Recommendation 'Verify service state, network, firewall, and that this account can connect as a read-only monitor login.'
        return $result
    }

    try {
        $instanceRows = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Instance' -Sql $QueryInstance)
        $instance = $null
        if ($instanceRows.Count -gt 0) { $instance = $instanceRows[0] }
        $result.Instance = $instance

        $support = $null
        if ($instance) {
            $support = Get-Supportability -ProductVersion $instance.ProductVersion
            $result.Support = $support
            if ($support.Status -eq 'End of support') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Supportability' `
                    -Finding "$($support.Product) extended support ended $($support.ExtendedEnd)." `
                    -Recommendation 'Upgrade to a supported SQL Server version (2019/2022/2025) or isolate and accept the security/compliance risk.'
            }
            elseif ($support.Status -eq 'Extended support') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Supportability' `
                    -Finding "$($support.Product) is on extended support (ends $($support.ExtendedEnd))." `
                    -Recommendation 'Plan upgrade before extended support ends; extended support typically excludes new security engineering beyond patches.'
            }
        }

        $result.Host = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Host' -Sql $QueryHost)
        $configs = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Config' -Sql $QueryConfig)
        $result.Configuration = $configs
        $cfg = @{}
        foreach ($c in $configs) { $cfg[$c.name] = [int64]$c.value_in_use }

        if ($cfg.ContainsKey('xp_cmdshell') -and $cfg['xp_cmdshell'] -ne 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Security' `
                -Finding 'xp_cmdshell is enabled.' `
                -Recommendation 'Disable xp_cmdshell unless a documented exception exists; use SQL Agent or external orchestration instead.'
        }
        if ($cfg.ContainsKey('Ole Automation Procedures') -and $cfg['Ole Automation Procedures'] -ne 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Security' `
                -Finding 'Ole Automation Procedures is enabled.' `
                -Recommendation 'Disable Ole Automation Procedures if unused.'
        }
        if ($cfg.ContainsKey('clr enabled') -and $cfg['clr enabled'] -ne 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'Security' `
                -Finding 'CLR is enabled.' `
                -Recommendation 'Confirm CLR assemblies are required, signed, and reviewed; disable if unused.'
        }
        if ($cfg.ContainsKey('optimize for ad hoc workloads') -and $cfg['optimize for ad hoc workloads'] -eq 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Low -Area 'Standards' `
                -Finding 'optimize for ad hoc workloads is OFF.' `
                -Recommendation 'Enable this on most OLTP systems to reduce plan-cache bloat from one-shot queries.'
        }
        if ($cfg.ContainsKey('backup compression default') -and $cfg['backup compression default'] -eq 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Low -Area 'Cost' `
                -Finding 'Backup compression is not the instance default.' `
                -Recommendation 'Enable backup compression default to reduce backup storage and duration (test CPU impact).'
        }
        if ($instance -and $cfg.ContainsKey('max server memory (MB)')) {
            $phys = [decimal]$instance.PhysicalMemoryMB
            $maxMem = [int64]$cfg['max server memory (MB)']
            if ($phys -gt 0 -and ($maxMem -le 0 -or $maxMem -ge ($phys - 512) -or $maxMem -eq 2147483647)) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'Performance' `
                    -Finding "max server memory is $maxMem MB vs $([int]$phys) MB physical." `
                    -Recommendation 'Cap max server memory to leave RAM for the OS (typically 4 GB+ on dedicated hosts, more with other services).'
            }
        }

        $dbs = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Databases' -Sql $QueryDatabases)
        $result.Databases = $dbs
        $backups = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Backups' -Sql $QueryBackups)
        $result.Backups = $backups
        $backupMap = @{}
        foreach ($b in $backups) { $backupMap[$b.database_name] = $b }

        $now = Get-Date
        $userDbs = @($dbs | Where-Object { $_.name -notin @('master', 'model', 'msdb', 'tempdb') })
        foreach ($db in $dbs) {
            if ($db.state_desc -ne 'ONLINE' -and $db.name -ne 'tempdb') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Status' `
                    -Finding "Database '$($db.name)' state is $($db.state_desc)." `
                    -Recommendation 'Investigate database state, error log, and restore/repair options.'
            }
            if ($db.is_auto_shrink_on) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Standards' `
                    -Finding "Database '$($db.name)' has AUTO_SHRINK on." `
                    -Recommendation 'Disable AUTO_SHRINK; it causes fragmentation and CPU/IO spikes.'
            }
            if ($db.is_auto_close_on -and $db.name -notin @('master', 'tempdb')) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'Standards' `
                    -Finding "Database '$($db.name)' has AUTO_CLOSE on." `
                    -Recommendation 'Disable AUTO_CLOSE on server databases; it causes reopen latency.'
            }
            if ($db.page_verify_option_desc -ne 'CHECKSUM' -and $db.name -ne 'tempdb') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Standards' `
                    -Finding "Database '$($db.name)' PAGE_VERIFY is $($db.page_verify_option_desc)." `
                    -Recommendation 'Set PAGE_VERIFY CHECKSUM for corruption detection.'
            }
            if ($db.is_trustworthy_on -and $db.name -ne 'msdb') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Security' `
                    -Finding "Database '$($db.name)' is TRUSTWORTHY." `
                    -Recommendation 'Turn TRUSTWORTHY off unless required; prefer module signing.'
            }
            if ($instance -and $db.compatibility_level) {
                $expected = (Get-MajorVersion $instance.ProductVersion) * 10
                if ($expected -ge 90 -and [int]$db.compatibility_level -lt ($expected - 20) -and $db.name -notin @('master', 'msdb', 'tempdb')) {
                    Add-Finding -List $result.Findings -Server $ServerName -Severity Low -Area 'Standards' `
                        -Finding "Database '$($db.name)' compatibility $($db.compatibility_level) is well below instance $expected." `
                        -Recommendation 'Test and raise compatibility level after upgrade to unlock optimizer improvements.'
                }
            }
            $checkDbTime = $null
            if ($db.LastGoodCheckDbTime) {
                $checkDbTime = [datetime]$db.LastGoodCheckDbTime
                if ($checkDbTime.Year -lt 1990) { $checkDbTime = $null }
            }
            if ($checkDbTime) {
                $ageDays = ($now - $checkDbTime).TotalDays
                if ($ageDays -gt 7 -and $db.name -ne 'tempdb') {
                    Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'SLA' `
                        -Finding "Database '$($db.name)' last known good CHECKDB is $([int]$ageDays) days old." `
                        -Recommendation 'Run DBCC CHECKDB on a schedule (or on a restored copy) and alert on failures.'
                }
            }
            elseif ($db.name -ne 'tempdb') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'SLA' `
                    -Finding "Database '$($db.name)' has no LastGoodCheckDbTime." `
                    -Recommendation 'Run DBCC CHECKDB and capture last-known-good; property is unavailable on some older builds.'
            }

            if ($db.name -eq 'tempdb') { continue }
            $b = $null
            if ($backupMap.ContainsKey($db.name)) { $b = $backupMap[$db.name] }
            $lastFull = if ($b) { $b.LastFull } else { $null }
            if (-not $lastFull) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'SLA' `
                    -Finding "Database '$($db.name)' has no full backup recorded in msdb." `
                    -Recommendation 'Take a full backup immediately and enroll the database in the estate backup SLA.'
            }
            else {
                $fullAgeHrs = ($now - [datetime]$lastFull).TotalHours
                if ($fullAgeHrs -gt $FullBackupSlaHours) {
                    Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'SLA' `
                        -Finding "Database '$($db.name)' last full backup is $([int]$fullAgeHrs) hours old (SLA $($FullBackupSlaHours)h)." `
                        -Recommendation 'Fix the backup job and verify backups are restorable off-box.'
                }
            }
            if ($db.recovery_model_desc -in @('FULL', 'BULK_LOGGED') -and $db.name -notin @('master', 'msdb', 'model')) {
                $lastLog = if ($b) { $b.LastLog } else { $null }
                if (-not $lastLog) {
                    Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'SLA' `
                        -Finding "Database '$($db.name)' is $($db.recovery_model_desc) but has no log backup." `
                        -Recommendation 'Start log backups or switch to SIMPLE if point-in-time recovery is not required.'
                }
                else {
                    $logAgeMin = ($now - [datetime]$lastLog).TotalMinutes
                    if ($logAgeMin -gt $LogBackupSlaMinutes) {
                        Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'SLA' `
                            -Finding "Database '$($db.name)' last log backup is $([int]$logAgeMin) minutes old (SLA $($LogBackupSlaMinutes)m)." `
                            -Recommendation 'Increase log backup frequency to meet RPO.'
                    }
                }
            }
        }

        $jobs = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Jobs' -Sql $QueryJobs)
        $result.Jobs = $jobs
        foreach ($job in $jobs) {
            if ($job.LastRunStatus -eq 'Failed' -and $job.enabled -eq $true) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Alerts' `
                    -Finding "SQL Agent job '$($job.JobName)' last run failed." `
                    -Recommendation 'Inspect job history, fix the failure, and alert on Agent job failures.'
            }
        }

        $sysadmins = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Sysadmins' -Sql $QuerySysadmins)
        $result.Sysadmins = $sysadmins
        $enabledAdmins = @($sysadmins | Where-Object { $_.is_disabled -eq $false })
        if ($enabledAdmins.Count -gt 8) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'Security' `
                -Finding "$($enabledAdmins.Count) enabled sysadmin principals." `
                -Recommendation 'Reduce sysadmin membership; use least-privilege roles for daily operations.'
        }
        $builtinAdmin = @($sysadmins | Where-Object { $_.name -eq 'BUILTIN\Administrators' })
        if ($builtinAdmin.Count -gt 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Security' `
                -Finding 'BUILTIN\Administrators is a sysadmin.' `
                -Recommendation 'Remove BUILTIN\Administrators from sysadmin and grant named DBA groups instead.'
        }

        $logins = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'SqlLogins' -Sql $QuerySqlLogins)
        $result.SqlLogins = $logins
        foreach ($login in $logins) {
            if ($login.is_disabled -eq $false -and $login.is_policy_checked -eq $false) {
                $sev = if ($login.is_sysadmin) { 'High' } else { 'Medium' }
                Add-Finding -List $result.Findings -Server $ServerName -Severity $sev -Area 'Security' `
                    -Finding "SQL login '$($login.name)' has CHECK_POLICY off." `
                    -Recommendation 'Enable CHECK_POLICY (and CHECK_EXPIRATION for interactive logins).'
            }
        }
        if ($instance -and $instance.IsIntegratedSecurityOnly -eq 0) {
            $enabledSql = @($logins | Where-Object { $_.is_disabled -eq $false }).Count
            Add-Finding -List $result.Findings -Server $ServerName -Severity Info -Area 'Security' `
                -Finding "Mixed authentication is enabled ($enabledSql enabled SQL logins)." `
                -Recommendation 'Prefer Windows authentication only where possible.'
        }

        $result.Services = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Services' -Sql $QueryServices)
        foreach ($svc in @($result.Services)) {
            if ($svc.servicename -like '*SQL Server (*' -and $svc.service_account -match '^NT (AUTHORITY|Service)\\') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Low -Area 'Security' `
                    -Finding "Engine service account is $($svc.service_account)." `
                    -Recommendation 'Use a Group Managed Service Account (gMSA) for production instances.'
            }
            if ($svc.servicename -like '*SQL Server (*' -and $svc.instant_file_initialization_enabled -eq $false) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Low -Area 'Performance' `
                    -Finding 'Instant file initialization is not enabled.' `
                    -Recommendation 'Grant Perform volume maintenance tasks to the service account to speed data-file growths.'
            }
        }

        $volumes = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Volumes' -Sql $QueryVolumes)
        $result.Volumes = $volumes
        foreach ($v in $volumes) {
            if ($v.FreePct -lt 10) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Status' `
                    -Finding "Volume $($v.volume_mount_point) has $($v.FreePct)% free ($($v.FreeGB) GB)." `
                    -Recommendation 'Free space or expand the volume before autogrowth and backups fail.'
            }
            elseif ($v.FreePct -lt 20) {
                Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Status' `
                    -Finding "Volume $($v.volume_mount_point) has $($v.FreePct)% free ($($v.FreeGB) GB)." `
                    -Recommendation 'Plan capacity; keep headroom for growth, snapshots, and index rebuilds.'
            }
        }

        $result.Waits = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Waits' -Sql $QueryWaits)
        $sample1 = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Counters1' -Sql $QueryCounters)
        Start-Sleep -Seconds $SampleSeconds
        $sample2 = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Counters2' -Sql $QueryCounters)
        $map1 = Get-CounterMap $sample1
        $map2 = Get-CounterMap $sample2

        function Get-Rate([string]$Name) {
            $a = Get-CounterValue $map1 $Name
            $b = Get-CounterValue $map2 $Name
            if ($null -eq $a -or $null -eq $b) { return $null }
            return [math]::Round(($b - $a) / [double]$SampleSeconds, 2)
        }

        $ple = Get-CounterValue $map2 'Page life expectancy'
        $grants = Get-CounterValue $map2 'Memory Grants Pending'
        $blocked = Get-CounterValue $map2 'Processes blocked'
        $userConn = Get-CounterValue $map2 'User Connections'
        $totalMem = Get-CounterValue $map2 'Total Server Memory (KB)'
        $targetMem = Get-CounterValue $map2 'Target Server Memory (KB)'

        $perf = [pscustomobject]@{
            PageLifeExpectancySec = $ple
            MemoryGrantsPending   = $grants
            ProcessesBlocked      = $blocked
            UserConnections       = $userConn
            TotalServerMemoryMB   = if ($totalMem) { [math]::Round($totalMem / 1024.0, 1) } else { $null }
            TargetServerMemoryMB  = if ($targetMem) { [math]::Round($targetMem / 1024.0, 1) } else { $null }
            BatchRequestsPerSec   = Get-Rate 'Batch Requests/sec'
            CompilationsPerSec    = Get-Rate 'SQL Compilations/sec'
            RecompilationsPerSec  = Get-Rate 'SQL Re-Compilations/sec'
            PageReadsPerSec       = Get-Rate 'Page reads/sec'
            PageWritesPerSec      = Get-Rate 'Page writes/sec'
            DeadlocksPerSec       = Get-Rate 'Number of Deadlocks/sec'
            SampleSeconds         = $SampleSeconds
        }
        $result.Performance = $perf

        if ($ple -ne $null -and $ple -lt 300) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Performance' `
                -Finding "Page life expectancy is ${ple}s." `
                -Recommendation 'Investigate memory pressure, oversized plans, and whether max server memory / host RAM is adequate. PLE is instance-wide and can mislead on NUMA.'
        }
        if ($grants -ne $null -and $grants -gt 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Performance' `
                -Finding "Memory Grants Pending = $grants." `
                -Recommendation 'Find large memory-grant queries; consider grants feedback, stats, or more memory.'
        }
        if ($blocked -ne $null -and $blocked -gt 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Alerts' `
                -Finding "Processes blocked = $blocked at sample time." `
                -Recommendation 'Capture blocking (whoisactive / Query Store) and review long transactions.'
        }

        $ags = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'AG' -Sql $QueryAg)
        $result.AvailabilityGroups = $ags
        foreach ($ag in $ags) {
            if ($ag.synchronization_health_desc -and $ag.synchronization_health_desc -ne 'HEALTHY') {
                Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Status' `
                    -Finding "AG '$($ag.AgName)' replica $($ag.replica_server_name) health is $($ag.synchronization_health_desc)." `
                    -Recommendation 'Check AG dashboard, replica connectivity, and send/redo queues.'
            }
        }

        $suspect = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'SuspectPages' -Sql $QuerySuspectPages)
        $result.SuspectPages = $suspect
        if ($suspect.Count -gt 0) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Alerts' `
                -Finding "$($suspect.Count) suspect page row(s) in msdb.dbo.suspect_pages." `
                -Recommendation 'Investigate IO subsystem and restore from known-good backups; do not ignore suspect_pages.'
        }

        $result.LinkedServers = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'Linked' -Sql $QueryLinkedServers)
        $result.TraceFlags = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'TraceFlags' -Sql $QueryTraceFlags)

        $sku = @(Invoke-ReadOnlyQuery -Connection $conn -Name 'SkuFeatures' -Sql $QueryEnterpriseFeatures)
        if ($instance -and $instance.Edition -like '*Standard*' -and $sku.Count -gt 0) {
            $names = ($sku | Select-Object -ExpandProperty feature_name) -join ', '
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Licensing' `
                -Finding "Standard edition database master is using persisted SKU feature(s): $names." `
                -Recommendation 'Confirm edition/feature compliance; Enterprise-only features on Standard can block restore/upgrade paths.'
        }

        $dataGb = 0d
        foreach ($db in $dbs) {
            if ($db.DataMB) { $dataGb += [decimal]$db.DataMB / 1024 }
            if ($db.LogMB) { $dataGb += [decimal]$db.LogMB / 1024 }
        }
        $licensedCores = 0
        if ($instance) { $licensedCores = Get-LicensedCoreCount ([int]$instance.CpuCount) }
        $edition = if ($instance) { [string]$instance.Edition } else { '' }
        $coreUsd = 0d
        if ($edition -like '*Enterprise*' -and $EnterpriseCoreLicenseUsd -gt 0) {
            $coreUsd = $licensedCores * $EnterpriseCoreLicenseUsd
        }
        elseif ($edition -like '*Standard*' -and $StandardCoreLicenseUsd -gt 0) {
            $coreUsd = $licensedCores * $StandardCoreLicenseUsd
        }
        $storageUsd = 0d
        if ($StorageUsdPerGbMonth -gt 0) { $storageUsd = [math]::Round($dataGb * $StorageUsdPerGbMonth, 2) }

        $result.Cost = [pscustomobject]@{
            AllocatedDataAndLogGB     = [math]::Round($dataGb, 2)
            UserDatabaseCount         = $userDbs.Count
            CpuCount                  = if ($instance) { $instance.CpuCount } else { $null }
            EstimatedLicensedCores    = $licensedCores
            LicenseType               = if ($instance) { $instance.LicenseType } else { $null }
            NumLicensesProperty       = if ($instance) { $instance.NumLicenses } else { $null }
            EstimatedCoreLicenseUsd   = $coreUsd
            EstimatedStorageUsdMonth  = $storageUsd
            Note                      = 'Dollar figures are estimates from the rates you passed. LicenseType/NumLicenses from SERVERPROPERTY is often unpopulated for core licensing.'
        }

        if ($instance -and $instance.Edition -like '*Express*') {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Medium -Area 'Licensing' `
                -Finding 'Express edition detected (resource limits apply).' `
                -Recommendation 'Confirm Express is appropriate; it is not sized for most production OLTP estates.'
        }
        if ($instance -and $instance.Edition -like '*Evaluation*') {
            Add-Finding -List $result.Findings -Server $ServerName -Severity Critical -Area 'Licensing' `
                -Finding 'Evaluation edition detected; it will expire.' `
                -Recommendation 'Convert to a licensed edition before the evaluation period ends.'
        }
        if ($instance -and $instance.Edition -like '*Developer*' ) {
            Add-Finding -List $result.Findings -Server $ServerName -Severity High -Area 'Licensing' `
                -Finding 'Developer edition detected.' `
                -Recommendation 'Developer edition is not licensed for production workloads.'
        }
    }
    finally {
        if ($conn) {
            $conn.Close()
            $conn.Dispose()
        }
    }

    return $result
}

function Get-SeverityOrder {
    param([string]$Severity)
    switch ($Severity) {
        'Critical' { 1 }
        'High'     { 2 }
        'Medium'   { 3 }
        'Low'      { 4 }
        default    { 5 }
    }
}

function Write-Reports {
    param(
        [object[]]$Estate,
        [string]$Directory
    )

    if (-not (Test-Path -LiteralPath $Directory)) {
        New-Item -ItemType Directory -Path $Directory | Out-Null
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $estateList = @($Estate)
    $allFindings = @(@($estateList | ForEach-Object {
            if ($null -ne $_.Findings) { $_.Findings }
        }) | Sort-Object @{ Expression = { Get-SeverityOrder $_.Severity } }, Server, Area)
    $reachable = @($estateList | Where-Object { $_.Reachable }).Count
    $critical = @($allFindings | Where-Object { $_.Severity -eq 'Critical' }).Count
    $high = @($allFindings | Where-Object { $_.Severity -eq 'High' }).Count
    $medium = @($allFindings | Where-Object { $_.Severity -eq 'Medium' }).Count
    $low = @($allFindings | Where-Object { $_.Severity -eq 'Low' }).Count
    $eol = @($estateList | Where-Object { $_.Support -and $_.Support.Status -eq 'End of support' }).Count
    $totalGb = 0d
    $totalCores = 0
    foreach ($s in $estateList) {
        if ($s.Cost) {
            $totalGb += [decimal]$s.Cost.AllocatedDataAndLogGB
            $totalCores += [int]$s.Cost.EstimatedLicensedCores
        }
    }

    $summary = [pscustomobject]@{
        GeneratedLocal       = Get-Date
        GeneratedUtc         = [datetime]::UtcNow
        ServerCount          = $estateList.Count
        ReachableCount       = $reachable
        UnreachableCount     = $estateList.Count - $reachable
        EndOfSupportCount    = $eol
        AllocatedStorageGB   = [math]::Round($totalGb, 2)
        EstimatedLicensedCores = $totalCores
        FindingCounts        = [pscustomobject]@{
            Critical = $critical
            High     = $high
            Medium   = $medium
            Low      = $low
            Total    = $allFindings.Count
        }
        FullBackupSlaHours   = $FullBackupSlaHours
        LogBackupSlaMinutes  = $LogBackupSlaMinutes
    }

    $payload = [pscustomobject]@{
        Title            = 'Charles Taylor SQL Estate Management - Single Source of Truth'
        Mode             = 'Read-only (SELECT and monitoring queries only)'
        ExecutiveSummary = $summary
        Findings         = $allFindings
        Servers          = $estateList
    }

    $jsonPath = Join-Path $Directory "sql-estate-$stamp.json"
    $mdPath = Join-Path $Directory "sql-estate-$stamp.md"
    $htmlPath = Join-Path $Directory "sql-estate-$stamp.html"

    $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

    $md = New-Object System.Text.StringBuilder
    [void]$md.AppendLine('# Charles Taylor SQL Estate Management')
    [void]$md.AppendLine()
    [void]$md.AppendLine("Generated: $($summary.GeneratedLocal)")
    [void]$md.AppendLine()
    [void]$md.AppendLine('## Executive summary')
    [void]$md.AppendLine()
    [void]$md.AppendLine("- Servers assessed: $($summary.ServerCount) (reachable $($summary.ReachableCount), unreachable $($summary.UnreachableCount))")
    [void]$md.AppendLine("- End of support: $($summary.EndOfSupportCount)")
    [void]$md.AppendLine("- Allocated data+log: $($summary.AllocatedStorageGB) GB")
    [void]$md.AppendLine("- Estimated licensed cores: $($summary.EstimatedLicensedCores)")
    [void]$md.AppendLine("- Findings: Critical $critical, High $high, Medium $medium, Low $low")
    [void]$md.AppendLine()
    [void]$md.AppendLine('## Findings')
    [void]$md.AppendLine()
    foreach ($f in $allFindings) {
        [void]$md.AppendLine("- **$($f.Severity)** / $($f.Area) / $($f.Server): $($f.Finding) -- $($f.Recommendation)")
    }
    [void]$md.AppendLine()
    $md.ToString() | Set-Content -LiteralPath $mdPath -Encoding UTF8

    $execRows = @(
        [pscustomobject]@{ Metric = 'Servers assessed'; Value = $summary.ServerCount }
        [pscustomobject]@{ Metric = 'Reachable'; Value = $summary.ReachableCount }
        [pscustomobject]@{ Metric = 'Unreachable'; Value = $summary.UnreachableCount }
        [pscustomobject]@{ Metric = 'End of support'; Value = $summary.EndOfSupportCount }
        [pscustomobject]@{ Metric = 'Allocated data+log (GB)'; Value = $summary.AllocatedStorageGB }
        [pscustomobject]@{ Metric = 'Estimated licensed cores'; Value = $summary.EstimatedLicensedCores }
        [pscustomobject]@{ Metric = 'Critical findings'; Value = $critical }
        [pscustomobject]@{ Metric = 'High findings'; Value = $high }
        [pscustomobject]@{ Metric = 'Medium findings'; Value = $medium }
        [pscustomobject]@{ Metric = 'Low findings'; Value = $low }
    )

    $instanceRows = foreach ($s in $estateList) {
        $i = $s.Instance
        [pscustomobject]@{
            Server          = $s.Server
            Reachable       = $s.Reachable
            Product         = if ($s.Support) { $s.Support.Product } else { '' }
            Support         = if ($s.Support) { $s.Support.Status } else { $(if ($s.Error) { 'Unreachable' } else { '' }) }
            Edition         = if ($i) { $i.Edition } else { '' }
            Version         = if ($i) { "$($i.ProductVersion) $($i.ProductLevel) $($i.ProductUpdateLevel)" } else { '' }
            Cpu             = if ($i) { $i.CpuCount } else { '' }
            MemoryMB        = if ($i) { $i.PhysicalMemoryMB } else { '' }
            Started         = if ($i) { $i.SqlServerStartTime } else { '' }
            Error           = $s.Error
        }
    }

    $generatedHtml = HtmlEncode $summary.GeneratedLocal
    $execHtml = ConvertTo-HtmlTable $execRows @('Metric','Value')
    $findingsHtml = ConvertTo-HtmlTable $allFindings @('Severity','Area','Server','Finding','Recommendation')
    $statusHtml = ConvertTo-HtmlTable $instanceRows @('Server','Reachable','Product','Support','Edition','Version','Cpu','MemoryMB','Started','Error')
    $slaNote = "Backup SLA used: full $($FullBackupSlaHours)h, log $($LogBackupSlaMinutes)m. No data, schema, configuration, or infrastructure was modified."

    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<title>Charles Taylor SQL Estate Management</title>
<style>
  body { margin: 0; font-family: "Segoe UI", Calibri, sans-serif; color: #1c2430; background: #f3f6f9; }
  header { padding: 32px 40px 16px; }
  header h1 { margin: 0 0 6px; font-size: 28px; color: #1f4e79; }
  header p { margin: 0; color: #5c6b7a; }
  main { padding: 0 40px 48px; }
  h2 { margin: 28px 0 10px; font-size: 18px; border-bottom: 2px solid #1f4e79; padding-bottom: 4px; }
  h3 { margin: 18px 0 8px; font-size: 15px; }
  table { border-collapse: collapse; width: 100%; background: #fff; margin: 8px 0 16px; }
  th, td { border: 1px solid #d5dde6; padding: 6px 8px; text-align: left; vertical-align: top; font-size: 13px; }
  th { background: #e8eef4; }
  .muted { color: #5c6b7a; }
  .sev-critical { color: #8b1e1e; font-weight: 600; }
  .sev-high { color: #9a4a00; font-weight: 600; }
  .sev-medium { color: #6b5a00; }
  .sev-low { color: #2b5d34; }
  .note { font-size: 12px; color: #5c6b7a; }
</style>
</head>
<body>
<header>
  <h1>Charles Taylor SQL Estate Management</h1>
  <p>Single Source of Truth - read-only collection - $generatedHtml</p>
</header>
<main>
  <h2>Executive summary</h2>
  $execHtml
  <p class="note">$slaNote</p>

  <h2>Findings, risks, and recommendations</h2>
  $findingsHtml

  <h2>Estate status</h2>
  $statusHtml
"@

    foreach ($s in $estateList) {
        $html += "<h2>$(HtmlEncode $s.Server)</h2>"
        if ($s.Cost) {
            $html += "<h3>Cost drivers</h3>"
            $html += ConvertTo-HtmlTable @($s.Cost)
        }
        if ($s.Performance) {
            $html += "<h3>Performance snapshot</h3>"
            $html += ConvertTo-HtmlTable @($s.Performance)
        }
        $html += "<h3>Volumes</h3>" + (ConvertTo-HtmlTable @($s.Volumes))
        $html += "<h3>Databases</h3>" + (ConvertTo-HtmlTable @($s.Databases) @('name','state_desc','recovery_model_desc','compatibility_level','page_verify_option_desc','is_encrypted','DataMB','LogMB','LastGoodCheckDbTime','owner_name'))
        $html += "<h3>Services</h3>" + (ConvertTo-HtmlTable @($s.Services))
        $html += "<h3>Top waits</h3>" + (ConvertTo-HtmlTable @($s.Waits) @('wait_type','waiting_tasks_count','wait_time_ms','signal_wait_time_ms','WaitPct'))
        $html += "<h3>Sysadmins</h3>" + (ConvertTo-HtmlTable @($s.Sysadmins))
        $html += "<h3>Last backups</h3>" + (ConvertTo-HtmlTable @($s.Backups) @('DatabaseName','LastFullBackup','LastDifferentialBackup','LastLogBackup'))
        $html += "<h3>sp_configure</h3>" + (ConvertTo-HtmlTable @($s.Configuration) @('name','minimum','maximum','config_value','run_value','is_dynamic','is_advanced','description'))
        $html += "<h3>Availability groups</h3>" + (ConvertTo-HtmlTable @($s.AvailabilityGroups))
        $html += "<h3>SQL Agent jobs</h3>" + (ConvertTo-HtmlTable @($s.Jobs) @('JobName','enabled','LastRunStatus','LastRun','Message'))
    }

    $html += @"
</main>
</body>
</html>
"@

    $html | Set-Content -LiteralPath $htmlPath -Encoding UTF8

    return [pscustomobject]@{
        Html = $htmlPath
        Json = $jsonPath
        Markdown = $mdPath
        Summary = $summary
    }
}

# --- main --------------------------------------------------------------------

$serverList = @(Get-ServerList)
Write-Host "Charles Taylor SQL Estate Management (read-only) - $($serverList.Count) server(s)" -ForegroundColor Cyan

$estate = @(foreach ($name in $serverList) {
    Write-Host "Assessing $name ..."
    try {
        Assess-Server -ServerName $name
    }
    catch {
        Write-Warning ("Assessment failed for {0}: {1}" -f $name, $_.Exception.Message)
        if ($_.Exception.InnerException) {
            Write-Warning ("Inner: {0}" -f $_.Exception.InnerException.Message)
        }
        Write-Warning $_.ScriptStackTrace
        [pscustomobject]@{
            Server             = $name
            Reachable          = $false
            CollectedUtc       = [datetime]::UtcNow
            Error              = $_.Exception.ToString()
            Instance           = $null
            Support            = $null
            Host               = $null
            Configuration      = @()
            Databases          = @()
            Backups            = @()
            Jobs               = @()
            Sysadmins          = @()
            SqlLogins          = @()
            Services           = @()
            Volumes            = @()
            Waits              = @()
            Performance        = $null
            AvailabilityGroups = @()
            SuspectPages       = @()
            LinkedServers      = @()
            TraceFlags         = @()
            Cost               = $null
            Findings           = @(
                [pscustomobject]@{
                    Server         = $name
                    Severity       = 'Critical'
                    Area           = 'Status'
                    Finding        = "Collector error: $($_.Exception.Message)"
                    Recommendation = 'See the console warning for the stack trace; rerun after the script fix.'
                }
            )
        }
    }
})

$report = Write-Reports -Estate @($estate) -Directory $OutputDirectory

Write-Host ""
Write-Host "Reachable: $($report.Summary.ReachableCount)/$($report.Summary.ServerCount)" -ForegroundColor Green
Write-Host "Findings: Critical $($report.Summary.FindingCounts.Critical), High $($report.Summary.FindingCounts.High), Medium $($report.Summary.FindingCounts.Medium), Low $($report.Summary.FindingCounts.Low)"
Write-Host "HTML: $($report.Html)"
Write-Host "JSON: $($report.Json)"
Write-Host "Markdown: $($report.Markdown)"

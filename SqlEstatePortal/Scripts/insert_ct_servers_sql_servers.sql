/*
    Insert SQL estate servers into dbo.ct_servers
    ------------------------------------------------------
    Target   : SqlEstatePortal database
    Adds     : 14 servers, server_type = 'SQL Servers'
    Behaviour: idempotent - skips any server_name already present
               (ct_servers has no unique constraint on server_name,
                so duplicates would otherwise be possible)

    Note: server_status is left at the table default 'Online'.
          Run "Check Server Status" in the portal to set it to
          Reachable / UnReachable - assessments only pick up
          servers marked 'Reachable'.
*/

SET NOCOUNT ON;

DECLARE @now datetime2    = SYSUTCDATETIME();
DECLARE @actor nvarchar(100) = N'Manual insert';

DECLARE @incoming TABLE (server_name nvarchar(200) PRIMARY KEY);

INSERT INTO @incoming (server_name) VALUES
    (N'CTPEAW01VMSQL01'),
    (N'CTPCTA04VMSQL03'),
    (N'CTDTRX01VMSQL01'),
    (N'gamarinesqlsrvprd.database.windows.net'),
    (N'CTPCTA04VMSQL02'),
    (N'CTPCTA04VMSQL01'),
    (N'CTPTRX01VMSQL01'),
    (N'CTPFIN01VMSQL01'),
    (N'CTPCTA01SQL01'),
    (N'CTUCTA03VMSQL02'),
    (N'CTDCTA01SQL02'),
    (N'CTDCTA01SQL01'),
    (N'CTUCMA01VMSQL02'),
    (N'CTUCTA03VMSQL01');

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.ct_servers
        (server_name, server_type, is_active, created_by, created_on)
    SELECT
        i.server_name,
        N'SQL Servers',
        1,
        @actor,
        @now
    FROM @incoming AS i
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.ct_servers AS s
        WHERE s.server_name = i.server_name
    );

    DECLARE @inserted int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    PRINT CONCAT(N'Inserted ', @inserted, N' of ', (SELECT COUNT(*) FROM @incoming),
                 N' servers (the rest already existed).');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Verify
SELECT tx_id, server_name, server_type, server_status, is_active, created_by, created_on
FROM dbo.ct_servers
WHERE server_name IN (SELECT server_name FROM @incoming)
ORDER BY server_name;

/*
    Certificate collection diagnostic
    ---------------------------------
    Run this in SSMS against one of the assessed instances (e.g. itdshd01vmsql01)
    using the SAME login the portal's app pool / your dotnet run session uses.

    It runs the exact queries the collector runs, plus permission checks, so we can
    see whether the sections come back empty because there is genuinely nothing
    there, or because a query is failing and being swallowed.

    Paste back the output of all five steps.
*/

SET NOCOUNT ON;

PRINT '=== 1. Who am I and what can I see ===';
SELECT
    SUSER_SNAME()                                              AS login_name,
    IS_SRVROLEMEMBER('sysadmin')                               AS is_sysadmin,
    HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE')         AS has_view_server_state,
    HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DEFINITION')       AS has_view_any_definition,
    DB_NAME()                                                  AS current_database,
    @@VERSION                                                  AS version;

PRINT '';
PRINT '=== 2. Raw certificate count (no joins, no TRY/CATCH) ===';
-- If this returns 0 rows, the instance genuinely has no certificates in master
-- and the empty tab is correct. Most instances have ##MS_...## system certificates.
SELECT COUNT(*) AS certificate_count FROM master.sys.certificates;

SELECT name, subject, issuer_name, start_date, expiry_date
FROM master.sys.certificates
ORDER BY name;

PRINT '';
PRINT '=== 3. Encryption keys DMV on its own ===';
-- Needs VIEW SERVER STATE. If this errors, the collector still expects to return
-- certificates (the DMV read is wrapped in TRY/CATCH) - so an error here alone
-- should NOT produce an empty Certificates tab.
BEGIN TRY
    SELECT database_id, DB_NAME(database_id) AS database_name, encryptor_thumbprint
    FROM sys.dm_database_encryption_keys;
    PRINT 'dm_database_encryption_keys: OK';
END TRY
BEGIN CATCH
    PRINT 'dm_database_encryption_keys FAILED: ' + ERROR_MESSAGE();
END CATCH;

PRINT '';
PRINT '=== 4. The collector''s certificate query, verbatim ===';
BEGIN TRY
    DECLARE @deks TABLE (thumbprint varbinary(20), database_name sysname);

    BEGIN TRY
        INSERT INTO @deks (thumbprint, database_name)
        SELECT dek.encryptor_thumbprint, DB_NAME(dek.database_id)
        FROM sys.dm_database_encryption_keys AS dek;
    END TRY
    BEGIN CATCH
        DELETE FROM @deks;
    END CATCH;

    SELECT
        DB_NAME()                                   AS database_name,
        c.name                                      AS certificate_name,
        c.subject,
        c.issuer_name,
        c.start_date,
        c.expiry_date,
        c.pvt_key_encryption_type_desc              AS pvt_key_encryption,
        CONVERT(nvarchar(200), c.thumbprint, 1)     AS thumbprint,
        STUFF((
            SELECT N', ' + d.database_name
            FROM @deks AS d
            WHERE d.thumbprint = c.thumbprint
            ORDER BY d.database_name
            FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS protected_databases,
        DATEDIFF(day, GETDATE(), c.expiry_date)     AS days_to_expiry
    FROM master.sys.certificates AS c
    ORDER BY c.expiry_date;

    PRINT 'collector certificate query: OK';
END TRY
BEGIN CATCH
    PRINT 'collector certificate query FAILED: ' + ERROR_MESSAGE();
END CATCH;

PRINT '';
PRINT '=== 5. The collector''s TLS query, verbatim ===';
-- xp_instance_regread normally requires sysadmin. Note how many result sets
-- come back here: if you see TWO grids instead of one, that is the bug.
BEGIN TRY
    DECLARE @instance     sysname = CONVERT(sysname, SERVERPROPERTY(N'InstanceName'));
    DECLARE @thumbprint   nvarchar(200);
    DECLARE @forceEncrypt int;

    IF @instance IS NULL SET @instance = N'MSSQLSERVER';

    BEGIN TRY
        EXEC master.dbo.xp_instance_regread
            N'HKEY_LOCAL_MACHINE',
            N'Software\Microsoft\MSSQLServer\MSSQLServer\SuperSocketNetLib',
            N'Certificate',
            @thumbprint OUTPUT;

        EXEC master.dbo.xp_instance_regread
            N'HKEY_LOCAL_MACHINE',
            N'Software\Microsoft\MSSQLServer\MSSQLServer\SuperSocketNetLib',
            N'ForceEncryption',
            @forceEncrypt OUTPUT;

        PRINT 'xp_instance_regread: OK';
    END TRY
    BEGIN CATCH
        SET @thumbprint = NULL;
        PRINT 'xp_instance_regread FAILED: ' + ERROR_MESSAGE();
    END CATCH;

    SELECT
        @instance                                   AS instance_name,
        NULLIF(LTRIM(RTRIM(@thumbprint)), N'')      AS thumbprint,
        CASE WHEN ISNULL(@forceEncrypt, 0) = 1 THEN 1 ELSE 0 END AS force_encryption,
        CASE WHEN NULLIF(LTRIM(RTRIM(@thumbprint)), N'') IS NULL
             THEN N'Self-signed (auto-generated)'
             ELSE N'Configured certificate'
        END                                         AS certificate_source;

    PRINT 'collector TLS query: OK';
END TRY
BEGIN CATCH
    PRINT 'collector TLS query FAILED: ' + ERROR_MESSAGE();
END CATCH;

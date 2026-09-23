/*
================================================================================
  SQL Estate Portal - release 2026-09-21 schema script
================================================================================
  Target : the SqlEstatePortal database (NOT the assessed estate)

  You do not have to run this. The portal applies every statement below itself
  on startup, from Data/AssessmentSchema.cs, Data/CtInventorySchema.cs and
  Data/CriticalityPropagation.cs. This file exists so a DBA can review the
  change, pre-apply it, or run it where automatic schema changes are not
  wanted.

  Every statement is idempotent and safe to re-run, with ONE exception that is
  called out in section 5 - read it before running this on an environment that
  has already started the new build.

  Run order matters: sections 1-4 create the columns that sections 5-6 depend on.
================================================================================
*/

SET NOCOUNT ON;
GO

PRINT '=== 1. New assessment detail tables ===============================';
GO

-- AssessmentLinkedServers
IF OBJECT_ID('AssessmentLinkedServers','U') IS NULL
              CREATE TABLE AssessmentLinkedServers (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                LinkedServerName nvarchar(200) NOT NULL,
                DataSource nvarchar(400) NULL,
                Provider nvarchar(128) NULL,
                IsRemoteLoginEnabled bit NOT NULL CONSTRAINT DF_AssessmentLinkedServers_RemoteLogin DEFAULT 0,
                IsRpcOutEnabled bit NOT NULL CONSTRAINT DF_AssessmentLinkedServers_RpcOut DEFAULT 0
              );
GO

-- AssessmentSqlLogins
IF OBJECT_ID('AssessmentSqlLogins','U') IS NULL
              CREATE TABLE AssessmentSqlLogins (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                LoginName nvarchar(200) NOT NULL,
                IsDisabled bit NOT NULL CONSTRAINT DF_AssessmentSqlLogins_Disabled DEFAULT 0,
                IsPolicyChecked bit NOT NULL CONSTRAINT DF_AssessmentSqlLogins_Policy DEFAULT 0,
                IsExpirationChecked bit NOT NULL CONSTRAINT DF_AssessmentSqlLogins_Expiration DEFAULT 0,
                IsSysadmin bit NOT NULL CONSTRAINT DF_AssessmentSqlLogins_Sysadmin DEFAULT 0,
                CreateDate datetime2 NULL,
                ModifyDate datetime2 NULL
              );
GO

-- AssessmentAvailabilityGroups
IF OBJECT_ID('AssessmentAvailabilityGroups','U') IS NULL
              CREATE TABLE AssessmentAvailabilityGroups (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                AgName nvarchar(200) NOT NULL,
                ReplicaServerName nvarchar(200) NULL,
                RoleDesc nvarchar(60) NULL,
                OperationalStateDesc nvarchar(60) NULL,
                ConnectedStateDesc nvarchar(60) NULL,
                SynchronizationHealthDesc nvarchar(60) NULL
              );
GO

-- AssessmentCertificates
IF OBJECT_ID('AssessmentCertificates','U') IS NULL
              CREATE TABLE AssessmentCertificates (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                DatabaseName nvarchar(128) NULL,
                CertificateName nvarchar(256) NOT NULL,
                Subject nvarchar(1000) NULL,
                IssuerName nvarchar(1000) NULL,
                StartDate datetime2 NULL,
                ExpiryDate datetime2 NULL,
                DaysToExpiry int NULL,
                PrivateKeyEncryption nvarchar(200) NULL,
                Thumbprint nvarchar(200) NULL,
                ProtectedDatabases nvarchar(max) NULL
              );
GO

-- AssessmentTlsCertificates
IF OBJECT_ID('AssessmentTlsCertificates','U') IS NULL
              CREATE TABLE AssessmentTlsCertificates (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                InstanceName nvarchar(200) NULL,
                Thumbprint nvarchar(200) NULL,
                CertificateSource nvarchar(200) NULL,
                ForceEncryption bit NOT NULL CONSTRAINT DF_AssessmentTlsCertificates_ForceEncryption DEFAULT 0
              );
GO


PRINT '=== 2. ct_servers: criticality and auth type ======================';
GO

IF COL_LENGTH('ct_servers','criticality_type') IS NULL ALTER TABLE dbo.ct_servers ADD criticality_type nvarchar(20) NULL;
GO

IF COL_LENGTH('ct_servers','auth_type') IS NULL ALTER TABLE dbo.ct_servers ADD auth_type nvarchar(30) NULL;
GO

IF COL_LENGTH('ct_servers','criticality_type') IS NOT NULL
              UPDATE dbo.ct_servers SET criticality_type = N'Critical'
              WHERE criticality_type IS NULL OR criticality_type = '';
GO

IF COL_LENGTH('ct_servers','auth_type') IS NOT NULL
              UPDATE dbo.ct_servers SET auth_type = N'Windows Auth'
              WHERE auth_type IS NULL OR auth_type = '';
GO


PRINT '=== 3. ct_database: real link to ct_servers =======================';
GO

/*
  ct_database has always pointed at its server by name, which breaks on rename
  and allows orphans. server_id is the real relationship. server_name is kept
  in step for now so nothing reading it breaks; it is dropped in a later
  release once every reader has moved over.
*/
GO

IF COL_LENGTH('ct_database','server_id') IS NULL ALTER TABLE dbo.ct_database ADD server_id int NULL;
GO

IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              UPDATE d
                 SET d.server_id = s.tx_id
                FROM dbo.ct_database AS d
               INNER JOIN dbo.ct_servers AS s
                  ON LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
               WHERE d.server_id IS NULL
                 AND d.server_name IS NOT NULL
                 AND LTRIM(RTRIM(d.server_name)) <> N'';
GO

IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ct_database_server_id' AND object_id = OBJECT_ID(N'dbo.ct_database'))
              CREATE NONCLUSTERED INDEX [IX_ct_database_server_id] ON dbo.[ct_database] ([server_id]);
GO

IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ct_database_server')
              ALTER TABLE dbo.ct_database WITH CHECK
                ADD CONSTRAINT [FK_ct_database_server]
                FOREIGN KEY ([server_id]) REFERENCES dbo.[ct_servers] ([tx_id])
                ON DELETE SET NULL;
GO


PRINT '=== 4. Criticality columns on databases and applications ==========';
GO

IF COL_LENGTH('ct_applications','criticality_type') IS NULL ALTER TABLE dbo.ct_applications ADD criticality_type nvarchar(20) NULL;
GO

IF COL_LENGTH('ct_database','criticality_type') IS NULL ALTER TABLE dbo.ct_database ADD criticality_type nvarchar(20) NULL;
GO


PRINT '=== 5. One-time repair of ct_applications.business_criticality ====';
GO
/*
  READ THIS BEFORE RUNNING.

  An interim build of this release wrote server-derived criticality straight
  into business_criticality, having first snapshotted the original values into
  business_criticality_original. The final release moved that to its own
  criticality_type column and puts the business values back.

  Statement 2 uses a heuristic: a row whose snapshot is NULL was empty before
  propagation touched it, so a current value of exactly 'Critical' or
  'Non Critical' can only have come from propagation and is cleared.

  The repair is deliberately self-limiting - it ends by dropping the snapshot
  column, so it cannot run twice and cannot freeze the column against later
  edits. That also makes it the ONE part of this script that is not
  re-runnable. If business_criticality_original is already gone, all three
  statements correctly do nothing.
*/
GO

IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
  EXEC sp_executesql N'
      UPDATE dbo.ct_applications
         SET business_criticality = business_criticality_original
       WHERE business_criticality_original IS NOT NULL;';
GO

IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
  EXEC sp_executesql N'
      UPDATE dbo.ct_applications
         SET business_criticality = NULL
       WHERE business_criticality_original IS NULL
         AND business_criticality IN (N''Critical'', N''Non Critical'');';
GO

IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
  EXEC sp_executesql N'ALTER TABLE dbo.ct_applications DROP COLUMN business_criticality_original;';
GO


PRINT '=== 6. Recompute criticality across the estate ====================';
GO
/*
  Criticality is owned by the server and inherited downwards:
    - a database takes its server's rating
    - an application is Critical when ANY server it touches is Critical,
      counting both explicit ct_application_server links and servers reached
      through a linked database
    - an application with no server link is left alone - there is no basis to
      rate it, and overwriting would destroy business data for nothing

  The portal runs this same pass on startup and after every server save, so
  running it here is optional.
*/
GO

UPDATE d
   SET d.criticality_type = s.criticality_type
  FROM dbo.ct_database AS d
 INNER JOIN dbo.ct_servers AS s ON s.tx_id = d.server_id;
GO

UPDATE d
   SET d.criticality_type = s.criticality_type
  FROM dbo.ct_database AS d
 INNER JOIN dbo.ct_servers AS s
    ON LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
 WHERE d.server_id IS NULL;
GO

UPDATE dbo.ct_database
   SET criticality_type = NULL
 WHERE server_id IS NULL
   AND (server_name IS NULL OR LTRIM(RTRIM(server_name)) = N'');
GO

WITH app_server_criticality AS (
    SELECT l.application_id AS app_id, s.criticality_type AS crit
    FROM dbo.ct_application_server AS l
    INNER JOIN dbo.ct_servers AS s
        ON s.tx_id = l.server_id
        OR LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(l.server_name)))

    UNION ALL

    SELECT l.application_id AS app_id, s.criticality_type AS crit
    FROM dbo.ct_application_database AS l
    INNER JOIN dbo.ct_database AS d ON d.tx_id = l.database_id
    INNER JOIN dbo.ct_servers AS s
        ON s.tx_id = d.server_id
        OR LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
),
agg AS (
    SELECT app_id,
           MAX(CASE WHEN crit = N'Critical' THEN 1 ELSE 0 END) AS any_critical
    FROM app_server_criticality
    GROUP BY app_id
)
UPDATE a
   SET a.criticality_type =
       CASE WHEN agg.any_critical = 1 THEN N'Critical' ELSE N'Non Critical' END
  FROM dbo.ct_applications AS a
 INNER JOIN agg ON agg.app_id = a.id;
GO


PRINT '=== Done ==========================================================';
GO

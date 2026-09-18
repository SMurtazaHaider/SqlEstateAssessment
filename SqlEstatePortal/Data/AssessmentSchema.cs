using Microsoft.EntityFrameworkCore;

namespace SqlEstatePortal.Data;

public static class AssessmentSchema
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        var statements = new[]
        {
            "IF COL_LENGTH('AssessmentRuns','UnreachableCount') IS NULL ALTER TABLE AssessmentRuns ADD UnreachableCount int NOT NULL CONSTRAINT DF_AR_Unreachable DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','EndOfSupportCount') IS NULL ALTER TABLE AssessmentRuns ADD EndOfSupportCount int NOT NULL CONSTRAINT DF_AR_Eos DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','AllocatedStorageGb') IS NULL ALTER TABLE AssessmentRuns ADD AllocatedStorageGb decimal(18,2) NOT NULL CONSTRAINT DF_AR_Gb DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','EstimatedLicensedCores') IS NULL ALTER TABLE AssessmentRuns ADD EstimatedLicensedCores int NOT NULL CONSTRAINT DF_AR_Cores DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','InfoCount') IS NULL ALTER TABLE AssessmentRuns ADD InfoCount int NOT NULL CONSTRAINT DF_AR_Info DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','HtmlContent') IS NULL ALTER TABLE AssessmentRuns ADD HtmlContent nvarchar(max) NULL;",
            @"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID('AssessmentFindings') AND c.name = 'Finding' AND t.name = N'nvarchar' AND c.max_length <> -1)
              ALTER TABLE AssessmentFindings ALTER COLUMN Finding nvarchar(max) NOT NULL;",
            @"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID('AssessmentFindings') AND c.name = 'Recommendation' AND t.name = N'nvarchar' AND c.max_length <> -1)
              ALTER TABLE AssessmentFindings ALTER COLUMN Recommendation nvarchar(max) NOT NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','UserDatabaseCount') IS NULL ALTER TABLE AssessmentServerSnapshots ADD UserDatabaseCount int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','StartedAt') IS NULL ALTER TABLE AssessmentServerSnapshots ADD StartedAt datetime2 NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','UserConnections') IS NULL ALTER TABLE AssessmentServerSnapshots ADD UserConnections int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','PageLifeExpectancySec') IS NULL ALTER TABLE AssessmentServerSnapshots ADD PageLifeExpectancySec int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','BatchRequestsPerSec') IS NULL ALTER TABLE AssessmentServerSnapshots ADD BatchRequestsPerSec decimal(18,2) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','HostPlatform') IS NULL ALTER TABLE AssessmentServerSnapshots ADD HostPlatform nvarchar(80) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','HostDistribution') IS NULL ALTER TABLE AssessmentServerSnapshots ADD HostDistribution nvarchar(120) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','Collation') IS NULL ALTER TABLE AssessmentServerSnapshots ADD Collation nvarchar(80) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','VirtualMachineType') IS NULL ALTER TABLE AssessmentServerSnapshots ADD VirtualMachineType nvarchar(50) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','LicenseType') IS NULL ALTER TABLE AssessmentServerSnapshots ADD LicenseType nvarchar(50) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','Error') IS NOT NULL ALTER TABLE AssessmentServerSnapshots ALTER COLUMN Error nvarchar(max) NULL;",
            @"IF OBJECT_ID('AssessmentDatabases','U') IS NULL
              CREATE TABLE AssessmentDatabases (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                Name nvarchar(128) NOT NULL,
                State nvarchar(60) NULL,
                RecoveryModel nvarchar(30) NULL,
                CompatibilityLevel int NULL,
                PageVerify nvarchar(30) NULL,
                IsEncrypted bit NOT NULL,
                DataMb decimal(18,2) NULL,
                LogMb decimal(18,2) NULL,
                OwnerName nvarchar(128) NULL,
                LastGoodCheckDbTime datetime2 NULL
              );",
            @"IF OBJECT_ID('AssessmentVolumes','U') IS NULL
              CREATE TABLE AssessmentVolumes (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                MountPoint nvarchar(200) NOT NULL,
                LogicalName nvarchar(100) NULL,
                TotalGb decimal(18,2) NULL,
                FreeGb decimal(18,2) NULL,
                FreePct decimal(8,2) NULL
              );",
            @"IF OBJECT_ID('AssessmentServices','U') IS NULL
              CREATE TABLE AssessmentServices (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                ServiceName nvarchar(200) NOT NULL,
                StartupType nvarchar(50) NULL,
                Status nvarchar(50) NULL,
                ServiceAccount nvarchar(200) NULL,
                InstantFileInitialization nvarchar(10) NULL
              );",
            @"IF OBJECT_ID('AssessmentWaits','U') IS NULL
              CREATE TABLE AssessmentWaits (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                WaitType nvarchar(120) NOT NULL,
                WaitingTasks bigint NOT NULL,
                WaitTimeMs bigint NOT NULL,
                SignalWaitTimeMs bigint NOT NULL,
                WaitPct decimal(8,2) NULL
              );",
            @"IF OBJECT_ID('AssessmentJobs','U') IS NULL
              CREATE TABLE AssessmentJobs (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                JobName nvarchar(200) NOT NULL,
                Enabled bit NOT NULL,
                LastRunStatus nvarchar(50) NULL,
                LastRun datetime2 NULL,
                Message nvarchar(max) NULL
              );",
            @"IF OBJECT_ID('AssessmentSysadmins','U') IS NULL
              CREATE TABLE AssessmentSysadmins (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                Name nvarchar(200) NOT NULL,
                TypeDesc nvarchar(50) NULL,
                IsDisabled bit NOT NULL,
                CreateDate datetime2 NULL
              );",
            @"IF OBJECT_ID('AssessmentConfigurations','U') IS NULL
              CREATE TABLE AssessmentConfigurations (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                Name nvarchar(128) NOT NULL,
                Minimum bigint NULL,
                Maximum bigint NULL,
                ConfigValue bigint NULL,
                RunValue bigint NULL,
                Description nvarchar(500) NULL,
                IsDynamic bit NOT NULL CONSTRAINT DF_AssessmentConfigurations_IsDynamic DEFAULT 0,
                IsAdvanced bit NOT NULL CONSTRAINT DF_AssessmentConfigurations_IsAdvanced DEFAULT 0
              );",
            @"IF OBJECT_ID('AssessmentBackups','U') IS NULL
              CREATE TABLE AssessmentBackups (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                DatabaseName nvarchar(128) NOT NULL,
                LastFullBackup datetime2 NULL,
                LastDifferentialBackup datetime2 NULL,
                LastLogBackup datetime2 NULL
              );",
            @"IF OBJECT_ID('AssessmentLinkedServers','U') IS NULL
              CREATE TABLE AssessmentLinkedServers (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                LinkedServerName nvarchar(200) NOT NULL,
                DataSource nvarchar(400) NULL,
                Provider nvarchar(128) NULL,
                IsRemoteLoginEnabled bit NOT NULL CONSTRAINT DF_AssessmentLinkedServers_RemoteLogin DEFAULT 0,
                IsRpcOutEnabled bit NOT NULL CONSTRAINT DF_AssessmentLinkedServers_RpcOut DEFAULT 0
              );",
            @"IF OBJECT_ID('AssessmentSqlLogins','U') IS NULL
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
              );",
            @"IF OBJECT_ID('AssessmentAvailabilityGroups','U') IS NULL
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
              );",
            @"IF OBJECT_ID('AssessmentCertificates','U') IS NULL
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
              );",
            @"IF OBJECT_ID('AssessmentTlsCertificates','U') IS NULL
              CREATE TABLE AssessmentTlsCertificates (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                InstanceName nvarchar(200) NULL,
                Thumbprint nvarchar(200) NULL,
                CertificateSource nvarchar(200) NULL,
                ForceEncryption bit NOT NULL CONSTRAINT DF_AssessmentTlsCertificates_ForceEncryption DEFAULT 0
              );",
            "IF COL_LENGTH('AssessmentDatabases','CollationName') IS NULL ALTER TABLE AssessmentDatabases ADD CollationName nvarchar(128) NULL;",
            "IF COL_LENGTH('AssessmentDatabases','CreationDate') IS NULL ALTER TABLE AssessmentDatabases ADD CreationDate datetime2 NULL;",
            @"IF OBJECT_ID('InventorySyncBatches','U') IS NULL
              CREATE TABLE InventorySyncBatches (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id),
                Status nvarchar(40) NOT NULL,
                CreatedBy nvarchar(100) NULL,
                CreatedAtUtc datetime2 NOT NULL,
                ApprovedBy nvarchar(100) NULL,
                ApprovedAtUtc datetime2 NULL,
                RejectedBy nvarchar(100) NULL,
                RejectedAtUtc datetime2 NULL,
                Notes nvarchar(1000) NULL,
                NewCount int NOT NULL CONSTRAINT DF_ISB_New DEFAULT 0,
                ChangedCount int NOT NULL CONSTRAINT DF_ISB_Changed DEFAULT 0,
                RemovedCount int NOT NULL CONSTRAINT DF_ISB_Removed DEFAULT 0,
                UnchangedCount int NOT NULL CONSTRAINT DF_ISB_Unchanged DEFAULT 0
              );",
            @"IF OBJECT_ID('InventorySyncItems','U') IS NULL
              CREATE TABLE InventorySyncItems (
                Id int IDENTITY PRIMARY KEY,
                BatchId int NOT NULL REFERENCES InventorySyncBatches(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                EntityType nvarchar(20) NOT NULL CONSTRAINT DF_ISI_EntityType DEFAULT N'Database',
                DatabaseName nvarchar(500) NOT NULL,
                ChangeType nvarchar(20) NOT NULL,
                CtDatabaseId int NULL,
                CtServerId int NULL,
                Selected bit NOT NULL CONSTRAINT DF_ISI_Selected DEFAULT 0,
                Applied bit NOT NULL CONSTRAINT DF_ISI_Applied DEFAULT 0,
                OldSnapshotJson nvarchar(max) NULL,
                NewSnapshotJson nvarchar(max) NULL
              );",
            "IF COL_LENGTH('InventorySyncItems','EntityType') IS NULL ALTER TABLE InventorySyncItems ADD EntityType nvarchar(20) NOT NULL CONSTRAINT DF_ISI_EntityType DEFAULT N'Database';",
            "IF COL_LENGTH('InventorySyncItems','CtServerId') IS NULL ALTER TABLE InventorySyncItems ADD CtServerId int NULL;",
            @"IF OBJECT_ID('InventorySyncFields','U') IS NULL
              CREATE TABLE InventorySyncFields (
                Id int IDENTITY PRIMARY KEY,
                ItemId int NOT NULL REFERENCES InventorySyncItems(Id) ON DELETE CASCADE,
                FieldName nvarchar(80) NOT NULL,
                OldValue nvarchar(max) NULL,
                NewValue nvarchar(max) NULL,
                Selected bit NOT NULL CONSTRAINT DF_ISF_Selected DEFAULT 0
              );",
            @"IF OBJECT_ID('InventorySyncAudits','U') IS NULL
              CREATE TABLE InventorySyncAudits (
                Id int IDENTITY PRIMARY KEY,
                BatchId int NOT NULL REFERENCES InventorySyncBatches(Id) ON DELETE CASCADE,
                ItemId int NULL,
                EventType nvarchar(40) NOT NULL,
                Actor nvarchar(100) NOT NULL,
                OccurredAtUtc datetime2 NOT NULL,
                DetailJson nvarchar(max) NULL
              );",
            @"IF OBJECT_ID('EstateServers','U') IS NULL
              CREATE TABLE EstateServers (
                Id int IDENTITY PRIMARY KEY,
                ServerName nvarchar(200) NOT NULL,
                Enabled bit NOT NULL CONSTRAINT DF_EstateServers_Enabled DEFAULT 1,
                Notes nvarchar(500) NULL,
                CreatedAt datetime2 NOT NULL,
                UpdatedAt datetime2 NULL
              );",
            @"IF OBJECT_ID('EstateServers','U') IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_EstateServers_ServerName' AND object_id = OBJECT_ID('EstateServers'))
              CREATE UNIQUE INDEX IX_EstateServers_ServerName ON EstateServers(ServerName);"
        };

        foreach (var sql in statements)
            await db.Database.ExecuteSqlRawAsync(sql);
    }
}

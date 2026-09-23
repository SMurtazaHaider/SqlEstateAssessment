using Microsoft.EntityFrameworkCore;

namespace SqlEstatePortal.Data;

/// <summary>
/// Ensures Charles Taylor inventory tables (ct_*) exist.
/// Converted from MySQL dump u927457459_m_localhost.sql (CREATE TABLE + key/AUTO_INCREMENT alters).
/// </summary>
public static class CtInventorySchema
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        var statements = new[]
        {
            @"IF OBJECT_ID(N'dbo.ct_applications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_applications] (
    [id] int IDENTITY(1,1) NOT NULL,
    [name] nvarchar(max) NULL,
    [status] nvarchar(max) NULL,
    [is_mapped] nvarchar(max) NULL,
    [summary] nvarchar(max) NULL,
    [features] nvarchar(max) NULL,
    [function] nvarchar(max) NULL,
    [application_type] nvarchar(max) NULL,
    [aquired_date] nvarchar(max) NULL,
    [type_of_data] nvarchar(max) NULL,
    [documentation] nvarchar(max) NULL,
    [own_application_ip] nvarchar(max) NULL,
    [users] nvarchar(max) NULL,
    [vendor] nvarchar(max) NULL,
    [contract_renewal_date] nvarchar(max) NULL,
    [contract_type] nvarchar(max) NULL,
    [contract_narrative] nvarchar(max) NULL,
    [business_criticality] nvarchar(max) NULL,
    [service_grade] nvarchar(max) NULL,
    [l1_support] nvarchar(max) NULL,
    [l2_support] nvarchar(max) NULL,
    [application_sme] nvarchar(max) NULL,
    [support_narrative] nvarchar(max) NULL,
    [support_partner_msp] nvarchar(max) NULL,
    [service_owner] nvarchar(max) NULL,
    [type] nvarchar(max) NULL,
    [service_name] nvarchar(max) NULL,
    [service_type] nvarchar(max) NULL,
    [incidents_per_year] nvarchar(max) NULL,
    [incidents_per_user] nvarchar(max) NULL,
    [gartner_process] nvarchar(max) NULL,
    [has_docs] nvarchar(max) NULL,
    [time_roadmap] nvarchar(max) NULL,
    [tech_grade] nvarchar(max) NULL,
    [age_of_tech] nvarchar(max) NULL,
    [technical_debt] nvarchar(max) NULL,
    [location] nvarchar(max) NULL,
    [target_host_platform] nvarchar(max) NULL,
    [connected_systems] nvarchar(max) NULL,
    [servers] nvarchar(max) NULL,
    [source_code_location] nvarchar(max) NULL,
    [tech_stack] nvarchar(max) NULL,
    [review] nvarchar(max) NULL,
    [roadmap] nvarchar(max) NULL,
    [code] nvarchar(max) NULL,
    [asset_tag] nvarchar(max) NULL,
    [compliance_grade] nvarchar(max) NULL,
    [operating_region] nvarchar(max) NULL,
    [consumption] nvarchar(max) NULL,
    [file_data_storage] nvarchar(max) NULL,
    [data_location] nvarchar(max) NULL,
    [data_classification] nvarchar(max) NULL,
    [disaster_recovery] nvarchar(max) NULL,
    [last_dr_test] nvarchar(max) NULL,
    [backed_up_data_location] nvarchar(max) NULL,
    [authentication_type] nvarchar(max) NULL,
    [maintenance_revenue_impact] nvarchar(max) NULL,
    [maintenance_business_impact] nvarchar(max) NULL,
    [outtage_revenue_impact] nvarchar(max) NULL,
    [outtage_business_impact] nvarchar(max) NULL,
    [monitoring_grade] nvarchar(max) NULL,
    [manual_alternative_process] nvarchar(max) NULL,
    [compliance_narrative] nvarchar(max) NULL,
    [external_url] nvarchar(max) NULL,
    [created_by] nvarchar(100) NULL,
    [created_on] datetime2 NULL,
    [updated_by] nvarchar(100) NULL,
    [updated_on] datetime2 NULL,
    CONSTRAINT [PK_ct_applications] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_application_database', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_application_database] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_id] int NOT NULL,
    [database_id] int NOT NULL,
    [created_on] datetime2 NULL CONSTRAINT [DF_ct_application_database_created_on] DEFAULT (SYSUTCDATETIME()),
    [created_by] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_application_database] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [UQ_ct_application_database_uq_application_database] UNIQUE ([application_id], [database_id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_application_id' AND object_id = OBJECT_ID(N'dbo.ct_application_database'))
        CREATE NONCLUSTERED INDEX [idx_application_id] ON dbo.[ct_application_database] ([application_id]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_database_id' AND object_id = OBJECT_ID(N'dbo.ct_application_database'))
        CREATE NONCLUSTERED INDEX [idx_database_id] ON dbo.[ct_application_database] ([database_id]);
END;",
            @"IF OBJECT_ID(N'dbo.ct_application_server', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_application_server] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_id] int NOT NULL,
    [server_id] int NULL,
    [server_name] nvarchar(200) NOT NULL,
    [source_text] nvarchar(500) NULL,
    [created_on] datetime2 NULL CONSTRAINT [DF_ct_application_server_created_on] DEFAULT (SYSUTCDATETIME()),
    [created_by] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_application_server] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [UQ_ct_application_server_app_name] UNIQUE ([application_id], [server_name])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ct_application_server_application_id' AND object_id = OBJECT_ID(N'dbo.ct_application_server'))
        CREATE NONCLUSTERED INDEX [idx_ct_application_server_application_id] ON dbo.[ct_application_server] ([application_id]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ct_application_server_server_id' AND object_id = OBJECT_ID(N'dbo.ct_application_server'))
        CREATE NONCLUSTERED INDEX [idx_ct_application_server_server_id] ON dbo.[ct_application_server] ([server_id]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ct_application_server_server_name' AND object_id = OBJECT_ID(N'dbo.ct_application_server'))
        CREATE NONCLUSTERED INDEX [idx_ct_application_server_server_name] ON dbo.[ct_application_server] ([server_name]);
END;",
            @"IF OBJECT_ID(N'dbo.ct_application_database_history', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_application_database_history] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_id] int NOT NULL,
    [database_id] int NOT NULL,
    [action_type] nvarchar(20) NOT NULL,
    [comment_text] nvarchar(max) NOT NULL,
    [created_by] nvarchar(100) NOT NULL,
    [created_on] datetime2 NOT NULL CONSTRAINT [DF_ct_application_database_history_created_on] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ct_application_database_history] PRIMARY KEY CLUSTERED ([id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_app_db' AND object_id = OBJECT_ID(N'dbo.ct_application_database_history'))
        CREATE NONCLUSTERED INDEX [idx_app_db] ON dbo.[ct_application_database_history] ([application_id], [database_id]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_app_created' AND object_id = OBJECT_ID(N'dbo.ct_application_database_history'))
        CREATE NONCLUSTERED INDEX [idx_app_created] ON dbo.[ct_application_database_history] ([application_id], [created_on]);
END;",
            @"IF OBJECT_ID(N'dbo.ct_application_types', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_application_types] (
    [id] int IDENTITY(1,1) NOT NULL,
    [function_name] nvarchar(255) NULL,
    [application_type] nvarchar(255) NULL,
    CONSTRAINT [PK_ct_application_types] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_azure_sync_log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_azure_sync_log] (
    [id] int IDENTITY(1,1) NOT NULL,
    [synced_at] datetime2 NOT NULL,
    [user_id] int NULL,
    [user_name] nvarchar(128) NULL,
    [sync_mode] nvarchar(32) NOT NULL CONSTRAINT [DF_ct_azure_sync_log_sync_mode] DEFAULT (N'incremental'),
    [summary_json] nvarchar(max) NULL,
    [created_on] datetime2 NOT NULL CONSTRAINT [DF_ct_azure_sync_log_created_on] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ct_azure_sync_log] PRIMARY KEY CLUSTERED ([id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_azure_sync_synced_at' AND object_id = OBJECT_ID(N'dbo.ct_azure_sync_log'))
        CREATE NONCLUSTERED INDEX [idx_azure_sync_synced_at] ON dbo.[ct_azure_sync_log] ([synced_at]);
END;",
            @"IF OBJECT_ID(N'dbo.ct_business_apps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_business_apps] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_name] nvarchar(max) NULL,
    [status] nvarchar(max) NULL,
    [service_name] nvarchar(max) NULL,
    [criticality] nvarchar(max) NULL,
    [valid_business_unit] nvarchar(max) NULL,
    [business_unit_name] nvarchar(max) NULL,
    [users] nvarchar(max) NULL,
    [stakeholder] nvarchar(max) NULL,
    [business_time_roadmap] nvarchar(max) NULL,
    [business_grade] nvarchar(max) NULL,
    [business_sentiment] nvarchar(max) NULL,
    [business_roadmap] nvarchar(max) NULL,
    [business_target_host_platform] nvarchar(max) NULL,
    [last_review] nvarchar(max) NULL,
    CONSTRAINT [PK_ct_business_apps] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_business_process', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_business_process] (
    [id] int IDENTITY(1,1) NOT NULL,
    [business_process] nvarchar(100) NULL,
    [description] nvarchar(max) NULL,
    [code] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_business_process] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_business_units', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_business_units] (
    [id] int IDENTITY(1,1) NOT NULL,
    [colour] nvarchar(max) NULL,
    [description] nvarchar(max) NULL,
    [head] nvarchar(max) NULL,
    [url] nvarchar(max) NULL,
    [region] nvarchar(max) NULL,
    [name] nvarchar(max) NULL,
    [alias] nvarchar(max) NULL,
    [businessunitcode] nvarchar(max) NULL,
    [companycode] nvarchar(max) NULL,
    [businesstype] nvarchar(max) NULL,
    [heademail] nvarchar(max) NULL,
    [financialcontroller] nvarchar(max) NULL,
    [imageurl] nvarchar(max) NULL,
    [parentbusinessunitcode] nvarchar(max) NULL,
    [businessunittype] nvarchar(max) NULL,
    [rechargecostcentre] nvarchar(max) NULL,
    [rechargebusinessunitcode] nvarchar(max) NULL,
    [rechargebusinessunitname] nvarchar(max) NULL,
    [majorbusinessunitcode] nvarchar(max) NULL,
    [majorbusinessunitname] nvarchar(max) NULL,
    [rechargebusinessunitshortname] nvarchar(max) NULL,
    [majorbusinessunitshortname] nvarchar(max) NULL,
    [financebusinesspartner] nvarchar(max) NULL,
    [regionname] nvarchar(max) NULL,
    [companyname] nvarchar(max) NULL,
    [shortname] nvarchar(max) NULL,
    [sumisactive] nvarchar(max) NULL,
    [sumheadcount] nvarchar(max) NULL,
    [sumprofitcentre] nvarchar(max) NULL,
    [sumrechargelevel] nvarchar(max) NULL,
    [colour_2] nvarchar(max) NULL,
    CONSTRAINT [PK_ct_business_units] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_consumption', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_consumption] (
    [id] int IDENTITY(1,1) NOT NULL,
    [consumption] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_consumption] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_costs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_costs] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_id] int NULL,
    [name] nvarchar(max) NULL,
    [l1_support] nvarchar(max) NULL,
    [service_name] nvarchar(max) NULL,
    [cost_grade] nvarchar(max) NULL,
    [cost_narrative] nvarchar(max) NULL,
    [estimated_revenue] nvarchar(max) NULL,
    [hosting_cost] nvarchar(max) NULL,
    [azure_hosting_cost] decimal(15,2) NULL,
    [azure_cost_period] nvarchar(32) NULL,
    [azure_cost_synced_at] datetime2 NULL,
    [azure_cost_currency] nvarchar(8) NULL,
    [license_cost] nvarchar(max) NULL,
    [support_cost] nvarchar(max) NULL,
    [change_cost] nvarchar(max) NULL,
    [tco] nvarchar(max) NULL,
    [total_users] nvarchar(max) NULL,
    [cost_per_head] nvarchar(max) NULL,
    CONSTRAINT [PK_ct_costs] PRIMARY KEY CLUSTERED ([id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ct_costs_application_id' AND object_id = OBJECT_ID(N'dbo.ct_costs'))
        CREATE NONCLUSTERED INDEX [idx_ct_costs_application_id] ON dbo.[ct_costs] ([application_id]);
END;",
            @"IF OBJECT_ID(N'dbo.ct_location', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_location] (
    [id] int IDENTITY(1,1) NOT NULL,
    [location] nvarchar(255) NULL,
    [platform] nvarchar(255) NULL,
    CONSTRAINT [PK_ct_location] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_monitoring_grade', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_monitoring_grade] (
    [id] int IDENTITY(1,1) NOT NULL,
    [grade] nvarchar(100) NULL,
    [description] nvarchar(max) NULL,
    CONSTRAINT [PK_ct_monitoring_grade] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_roadmap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_roadmap] (
    [id] int IDENTITY(1,1) NOT NULL,
    [time_roadmap] nvarchar(100) NULL,
    [value] int NULL,
    [colour] nvarchar(20) NULL,
    CONSTRAINT [PK_ct_roadmap] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_service', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_service] (
    [id] int IDENTITY(1,1) NOT NULL,
    [department] nvarchar(255) NULL,
    [business_unit] nvarchar(255) NULL,
    [service_type] nvarchar(255) NULL,
    [service_model] nvarchar(255) NULL,
    CONSTRAINT [PK_ct_service] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_status', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_status] (
    [id] int IDENTITY(1,1) NOT NULL,
    [application_status] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_status] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_support_status', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_support_status] (
    [id] int IDENTITY(1,1) NOT NULL,
    [support_status] nvarchar(100) NULL,
    CONSTRAINT [PK_ct_support_status] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_technical_debt', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_technical_debt] (
    [id] int IDENTITY(1,1) NOT NULL,
    [technical_debt] nvarchar(100) NULL,
    [value] int NULL,
    CONSTRAINT [PK_ct_technical_debt] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_workers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_workers] (
    [id] int IDENTITY(1,1) NOT NULL,
    [empid] nvarchar(max) NULL,
    [positiontitle] nvarchar(max) NULL,
    [rechargebusinessunitshortname] nvarchar(max) NULL,
    [workertype] nvarchar(max) NULL,
    [businessunitshortname] nvarchar(max) NULL,
    [linemanagername] nvarchar(max) NULL,
    [workeremploymentenddate] nvarchar(max) NULL,
    [workerprimarycontactemail] nvarchar(max) NULL,
    [workeridentityemail] nvarchar(max) NULL,
    [workeroriginalemployer_groups] nvarchar(max) NULL,
    [workeroriginalemployer] nvarchar(max) NULL,
    [workerregion] nvarchar(max) NULL,
    [workeradname] nvarchar(max) NULL,
    [workercountry] nvarchar(max) NULL,
    [workerlocation] nvarchar(max) NULL,
    [workerfullname] nvarchar(max) NULL,
    [m0] nvarchar(max) NULL,
    CONSTRAINT [PK_ct_workers] PRIMARY KEY CLUSTERED ([id])
    );
END;",
            @"IF OBJECT_ID(N'dbo.ct_servers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_servers] (
    [tx_id] int IDENTITY(1,1) NOT NULL,
    [server_name] nvarchar(200) NOT NULL,
    [server_type] nvarchar(50) NULL,
    [fqdn] nvarchar(255) NULL,
    [sql_version] nvarchar(150) NULL,
    [sql_product] nvarchar(100) NULL,
    [support_status] nvarchar(50) NULL,
    [sql_edition] nvarchar(150) NULL,
    [sql_started_at] datetime2 NULL,
    [administrator_login] nvarchar(128) NULL,
    [public_network_access] nvarchar(32) NULL,
    [environment] nvarchar(100) NULL,
    [subscription] nvarchar(200) NULL,
    [subscription_id] nvarchar(64) NULL,
    [azure_resource_id] nvarchar(512) NULL,
    [tower] nvarchar(100) NULL,
    [resource_group_name] nvarchar(200) NULL,
    [data_centre_location] nvarchar(100) NULL,
    [server_status] nvarchar(50) NULL CONSTRAINT [DF_ct_servers_server_status] DEFAULT (N'Online'),
    [notes] nvarchar(max) NULL,
    [azure_tags] nvarchar(max) NULL,
    [azure_synced_at] datetime2 NULL,
    [is_active] bit NOT NULL CONSTRAINT [DF_ct_servers_is_active] DEFAULT (1),
    [created_by] nvarchar(100) NULL,
    [created_on] datetime2 NULL,
    [updated_by] nvarchar(100) NULL,
    [updated_on] datetime2 NULL,
    [status_checked_at] datetime2 NULL,
    [ip_address] nvarchar(50) NULL,
    [vm_cpu] nvarchar(20) NULL,
    [vm_ram] nvarchar(20) NULL,
    [vm_storage_gb] nvarchar(20) NULL,
    [current_utilization_pct] nvarchar(20) NULL,
    CONSTRAINT [PK_ct_servers] PRIMARY KEY CLUSTERED ([tx_id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ct_servers_server_name' AND object_id = OBJECT_ID(N'dbo.ct_servers'))
        CREATE NONCLUSTERED INDEX [IX_ct_servers_server_name] ON dbo.[ct_servers] ([server_name]);
END;",
            "IF COL_LENGTH('ct_servers','server_type') IS NULL ALTER TABLE dbo.ct_servers ADD server_type nvarchar(50) NULL;",
            @"IF COL_LENGTH('ct_servers','server_type') IS NOT NULL
              UPDATE dbo.ct_servers
              SET server_type = CASE 
                  WHEN UPPER(server_name) LIKE '%APP%' THEN 'APP Servers'
                  WHEN UPPER(server_name) LIKE '%SQL%' THEN 'SQL Servers'
                  ELSE 'Others'
              END
              WHERE server_type IS NULL OR server_type = '';",
            "IF COL_LENGTH('ct_servers','criticality_type') IS NULL ALTER TABLE dbo.ct_servers ADD criticality_type nvarchar(20) NULL;",
            "IF COL_LENGTH('ct_servers','auth_type') IS NULL ALTER TABLE dbo.ct_servers ADD auth_type nvarchar(30) NULL;",
            @"IF COL_LENGTH('ct_servers','criticality_type') IS NOT NULL
              UPDATE dbo.ct_servers SET criticality_type = N'Critical'
              WHERE criticality_type IS NULL OR criticality_type = '';",
            @"IF COL_LENGTH('ct_servers','auth_type') IS NOT NULL
              UPDATE dbo.ct_servers SET auth_type = N'Windows Auth'
              WHERE auth_type IS NULL OR auth_type = '';",
            "IF COL_LENGTH('ct_servers','sql_product') IS NULL ALTER TABLE dbo.ct_servers ADD sql_product nvarchar(100) NULL;",
            "IF COL_LENGTH('ct_servers','support_status') IS NULL ALTER TABLE dbo.ct_servers ADD support_status nvarchar(50) NULL;",
            "IF COL_LENGTH('ct_servers','sql_edition') IS NULL ALTER TABLE dbo.ct_servers ADD sql_edition nvarchar(150) NULL;",
            "IF COL_LENGTH('ct_servers','sql_started_at') IS NULL ALTER TABLE dbo.ct_servers ADD sql_started_at datetime2 NULL;",
            "IF COL_LENGTH('ct_servers','status_checked_at') IS NULL ALTER TABLE dbo.ct_servers ADD status_checked_at datetime2 NULL;",
            @"IF COL_LENGTH('ct_servers','status_checked_at') IS NOT NULL
              UPDATE dbo.ct_servers
              SET status_checked_at = updated_on
              WHERE status_checked_at IS NULL
                AND updated_by = N'Check Server Status'
                AND updated_on IS NOT NULL;",
            @"IF COL_LENGTH('ct_servers','sql_version') IS NOT NULL
              AND COL_LENGTH('ct_servers','sql_version') < 300
              ALTER TABLE dbo.ct_servers ALTER COLUMN sql_version nvarchar(150) NULL;",
            @"IF OBJECT_ID(N'dbo.ct_database', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_database] (
    [tx_id] int IDENTITY(1,1) NOT NULL,
    [tower] nvarchar(100) NULL,
    [environment] nvarchar(100) NULL,
    [subscription] nvarchar(200) NULL,
    [subscription_id] nvarchar(64) NULL,
    [azure_resource_id] nvarchar(512) NULL,
    [resource_group_name] nvarchar(200) NULL,
    [data_centre_location] nvarchar(100) NULL,
    [server_name] nvarchar(200) NULL,
    [elastic_pool_name] nvarchar(200) NULL,
    [database_name] nvarchar(500) NOT NULL,
    [database_status] nvarchar(50) NULL,
    [max_size_gb] int NULL,
    [max_size_mb] int NULL,
    [current_size_mb] int NULL,
    [collation_name] nvarchar(128) NULL,
    [creation_date] datetime2 NULL,
    [license_type] nvarchar(64) NULL,
    [zone_redundant] bit NULL CONSTRAINT [DF_ct_database_zone_redundant] DEFAULT (0),
    [read_scale] nvarchar(32) NULL,
    [azure_tags] nvarchar(max) NULL,
    [azure_synced_at] datetime2 NULL,
    [database_edition] nvarchar(100) NULL,
    [current_service_objective_name] nvarchar(100) NULL,
    [azure_sku_name] nvarchar(100) NULL,
    [azure_sku_capacity] int NULL,
    [is_active] bit NOT NULL CONSTRAINT [DF_ct_database_is_active] DEFAULT (1),
    [created_at] datetime2 NOT NULL CONSTRAINT [DF_ct_database_created_at] DEFAULT (SYSUTCDATETIME()),
    [compatibility_level] nvarchar(20) NULL,
    [recovery_model] nvarchar(30) NULL,
    [free_space_mb] int NULL,
    [backup_info] nvarchar(500) NULL,
    [last_full_backup] datetime2 NULL,
    [last_differential_backup] datetime2 NULL,
    [last_log_backup] datetime2 NULL,
    [database_owner] nvarchar(128) NULL,
    CONSTRAINT [PK_ct_database] PRIMARY KEY CLUSTERED ([tx_id])
    );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ct_database_server_name' AND object_id = OBJECT_ID(N'dbo.ct_database'))
        CREATE NONCLUSTERED INDEX [IX_ct_database_server_name] ON dbo.[ct_database] ([server_name]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ct_database_database_name' AND object_id = OBJECT_ID(N'dbo.ct_database'))
        CREATE NONCLUSTERED INDEX [IX_ct_database_database_name] ON dbo.[ct_database] ([database_name]);
END;",
            "IF COL_LENGTH('ct_database','last_full_backup') IS NULL ALTER TABLE dbo.ct_database ADD last_full_backup datetime2 NULL;",
            // ct_database has always pointed at its server by name, which breaks on
            // rename and allows orphans. server_id is the real relationship; the
            // name is kept in step until every reader has moved over.
            "IF COL_LENGTH('ct_applications','criticality_type') IS NULL ALTER TABLE dbo.ct_applications ADD criticality_type nvarchar(20) NULL;",
            // Server-derived criticality briefly lived in business_criticality. Put the
            // business values back, then retire the snapshot column so this runs once.
            //
            // These MUST go through sp_executesql. SQL Server compiles a whole batch
            // before running it, and a reference to a column that does not exist is a
            // compile-time error - the IF never gets the chance to skip it. (Deferred
            // name resolution covers missing tables, not missing columns.) On a
            // database that never ran the interim build the column is absent, so the
            // statement has to stay uncompiled until we know it is there.
            @"IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
              EXEC sp_executesql N'
                  UPDATE dbo.ct_applications
                     SET business_criticality = business_criticality_original
                   WHERE business_criticality_original IS NOT NULL;';",
            @"IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
              EXEC sp_executesql N'
                  UPDATE dbo.ct_applications
                     SET business_criticality = NULL
                   WHERE business_criticality_original IS NULL
                     AND business_criticality IN (N''Critical'', N''Non Critical'');';",
            @"IF COL_LENGTH('ct_applications','business_criticality_original') IS NOT NULL
              EXEC sp_executesql N'ALTER TABLE dbo.ct_applications DROP COLUMN business_criticality_original;';",
            "IF COL_LENGTH('ct_database','criticality_type') IS NULL ALTER TABLE dbo.ct_database ADD criticality_type nvarchar(20) NULL;",
            "IF COL_LENGTH('ct_database','server_id') IS NULL ALTER TABLE dbo.ct_database ADD server_id int NULL;",
            @"IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              UPDATE d
                 SET d.server_id = s.tx_id
                FROM dbo.ct_database AS d
               INNER JOIN dbo.ct_servers AS s
                  ON LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
               WHERE d.server_id IS NULL
                 AND d.server_name IS NOT NULL
                 AND LTRIM(RTRIM(d.server_name)) <> N'';",
            @"IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ct_database_server_id' AND object_id = OBJECT_ID(N'dbo.ct_database'))
              CREATE NONCLUSTERED INDEX [IX_ct_database_server_id] ON dbo.[ct_database] ([server_id]);",
            @"IF COL_LENGTH('ct_database','server_id') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ct_database_server')
              ALTER TABLE dbo.ct_database WITH CHECK
                ADD CONSTRAINT [FK_ct_database_server]
                FOREIGN KEY ([server_id]) REFERENCES dbo.[ct_servers] ([tx_id])
                ON DELETE SET NULL;",

            "IF COL_LENGTH('ct_database','last_differential_backup') IS NULL ALTER TABLE dbo.ct_database ADD last_differential_backup datetime2 NULL;",
            "IF COL_LENGTH('ct_database','last_log_backup') IS NULL ALTER TABLE dbo.ct_database ADD last_log_backup datetime2 NULL;",
            "IF COL_LENGTH('ct_database','database_owner') IS NULL ALTER TABLE dbo.ct_database ADD database_owner nvarchar(128) NULL;"
        };

        foreach (var sql in statements)
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        await MigrateLegacyOwnerFromBackupInfoAsync(db);

        // Criticality is inherited from servers, so it is recomputed once the
        // columns and links are in place.
        await CriticalityPropagation.ApplyAsync(db);
    }

    /// <summary>
    /// Older syncs stored "Owner=sa" inside backup_info. Move that into database_owner
    /// and leave backup_info for backup timestamps only.
    /// </summary>
    private static async Task MigrateLegacyOwnerFromBackupInfoAsync(AppDbContext db)
    {
        var rows = await db.CtDatabases
            .Where(d => d.BackupInfo != null && d.BackupInfo.Contains("Owner="))
            .ToListAsync();
        if (rows.Count == 0) return;

        foreach (var row in rows)
        {
            string? owner = null;
            var kept = new List<string>();
            foreach (var part in row.BackupInfo!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith("Owner=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part["Owner=".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        owner = value;
                    continue;
                }
                kept.Add(part);
            }

            if (string.IsNullOrWhiteSpace(row.DatabaseOwner) && !string.IsNullOrWhiteSpace(owner))
                row.DatabaseOwner = owner;
            row.BackupInfo = kept.Count == 0 ? null : string.Join("; ", kept);
        }

        await db.SaveChangesAsync();
    }
}

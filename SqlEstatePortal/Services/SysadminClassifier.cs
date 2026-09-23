using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

/// <summary>
/// Sorts sysadmin role members into the five classes the Sysadmins tab shows.
///
/// The point of the split is that most rows are noise. SQL Server setup puts its
/// own service SIDs into sysadmin on every instance, so their presence carries no
/// information - on a fourteen-server estate they are the large majority of rows
/// and they bury the handful that a security review actually cares about.
///
/// They are classified rather than discarded, because their *absence* is a
/// finding (no NT SERVICE\SQLSERVERAGENT means somebody has been tidying up
/// security and Agent jobs will misbehave), and because an auditor asking for
/// every sysadmin wants the complete list without a re-run.
///
/// The distinction that matters most is BuiltInService vs UnknownService. A
/// blanket "hide NT SERVICE\*" rule would also have hidden accounts that setup
/// never created - the Azure SQL IaaS Agent extension's
/// NT Service\SQLIaaSExtensionQuery, third-party backup and monitoring agents -
/// which are exactly the privileged accounts worth noticing.
/// </summary>
public static class SysadminClassifier
{
    public enum AccountClass
    {
        WindowsUser,
        WindowsGroup,
        SqlLogin,
        UnknownService,
        BuiltInService
    }

    /// <summary>
    /// Service accounts SQL Server setup itself provisions into sysadmin.
    ///
    /// Matched as a prefix, not an exact name, because a named instance gets
    /// NT SERVICE\MSSQL$PAYROLL and NT SERVICE\SQLAgent$PAYROLL rather than the
    /// default-instance names - an exact list would classify every named
    /// instance's own engine account as unrecognised.
    /// </summary>
    private static readonly string[] BuiltInServicePrefixes =
    {
        @"NT SERVICE\MSSQLSERVER",      // database engine, default instance
        @"NT SERVICE\MSSQL$",           // database engine, named instance
        @"NT SERVICE\SQLSERVERAGENT",   // Agent, default instance
        @"NT SERVICE\SQLAgent$",        // Agent, named instance
        @"NT SERVICE\SQLWriter",        // VSS writer, used by backup software
        @"NT SERVICE\Winmgmt",          // WMI, used by Configuration Manager
        @"NT SERVICE\MSSQLFDLauncher",  // full-text daemon
        @"NT SERVICE\ReportServer",     // Reporting Services
        @"NT AUTHORITY\SYSTEM"          // added by setup on older versions
    };

    public static AccountClass Classify(AssessmentSysadmin account)
    {
        var name = (account.Name ?? string.Empty).Trim();
        var type = (account.TypeDesc ?? string.Empty).Trim();

        // A service SID is only "expected" when it is also a Windows principal.
        // A SQL login that someone has named NT SERVICE\... is not the real thing.
        var isWindows = type.StartsWith("WINDOWS", StringComparison.OrdinalIgnoreCase);

        if (isWindows && IsBuiltInService(name)) return AccountClass.BuiltInService;
        if (isWindows && IsServiceAccount(name)) return AccountClass.UnknownService;

        if (type.Equals("WINDOWS_GROUP", StringComparison.OrdinalIgnoreCase))
            return AccountClass.WindowsGroup;
        if (type.Equals("WINDOWS_LOGIN", StringComparison.OrdinalIgnoreCase))
            return AccountClass.WindowsUser;

        // SQL_LOGIN, and anything else (certificate- or asymmetric-key-mapped
        // logins) - none of them are Windows principals, so they belong with the
        // SQL side rather than being silently dropped.
        return AccountClass.SqlLogin;
    }

    private static bool IsBuiltInService(string name) =>
        BuiltInServicePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static bool IsServiceAccount(string name) =>
        name.StartsWith(@"NT SERVICE\", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(@"NT AUTHORITY\", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<AssessmentSysadmin> InClass(
        IEnumerable<AssessmentSysadmin> accounts, AccountClass wanted) =>
        accounts.Where(a => Classify(a) == wanted);
}

using Microsoft.EntityFrameworkCore;

namespace SqlEstatePortal.Data;

/// <summary>
/// Criticality is owned by the server and inherited downwards: a database takes
/// its server's rating, and an application is Critical when any server it touches
/// is Critical.
///
/// The whole estate is recomputed in one pass rather than patched incrementally.
/// At this size (tens of servers, hundreds of databases) that costs almost
/// nothing and removes the drift that incremental updates would introduce every
/// time a link changed or an inventory sync rewrote a server name.
/// </summary>
public static class CriticalityPropagation
{
    public const string Critical = "Critical";
    public const string NonCritical = "Non Critical";

    /// <summary>Databases linked by id take that server's rating.</summary>
    private const string DatabasesById = """
        UPDATE d
           SET d.criticality_type = s.criticality_type
          FROM dbo.ct_database AS d
         INNER JOIN dbo.ct_servers AS s ON s.tx_id = d.server_id;
        """;

    /// <summary>Databases still linked only by name take theirs the same way.</summary>
    private const string DatabasesByName = """
        UPDATE d
           SET d.criticality_type = s.criticality_type
          FROM dbo.ct_database AS d
         INNER JOIN dbo.ct_servers AS s
            ON LOWER(LTRIM(RTRIM(s.server_name))) = LOWER(LTRIM(RTRIM(d.server_name)))
         WHERE d.server_id IS NULL;
        """;

    /// <summary>A database on no server has nothing to inherit from.</summary>
    private const string DatabasesUnassigned = """
        UPDATE dbo.ct_database
           SET criticality_type = NULL
         WHERE server_id IS NULL
           AND (server_name IS NULL OR LTRIM(RTRIM(server_name)) = N'');
        """;

    /// <summary>
    /// An application is Critical when any server it reaches is Critical, counting
    /// both explicit ct_application_server links and servers reached through a
    /// linked database. Applications with no server at all are left untouched:
    /// there is no basis to rate them. This writes criticality_type, which is
    /// separate from the business-owned business_criticality field.
    /// </summary>
    private const string Applications = """
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
        """;

    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct = default)
    {
        var statements = new[]
        {
            DatabasesById,
            DatabasesByName,
            DatabasesUnassigned,
            Applications
        };

        foreach (var sql in statements)
            await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}

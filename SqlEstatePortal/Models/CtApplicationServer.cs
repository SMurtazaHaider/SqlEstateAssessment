using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

/// <summary>
/// Explicit application-to-server link. Applications genuinely run on several
/// servers and a server hosts several applications, so unlike databases this
/// relationship is many-to-many and has a real link table.
/// </summary>
[Table("ct_application_server")]
public class CtApplicationServer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("application_id")]
    public int ApplicationId { get; set; }

    /// <summary>Nullable in the schema; populated whenever the server is in the register.</summary>
    [Column("server_id")]
    public int? ServerId { get; set; }

    /// <summary>Part of the unique key with application_id, so it must be kept in step on rename.</summary>
    [Column("server_name")]
    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [Column("source_text")]
    [MaxLength(500)]
    public string? SourceText { get; set; }

    [Column("created_on")]
    public DateTime? CreatedOn { get; set; }

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
}

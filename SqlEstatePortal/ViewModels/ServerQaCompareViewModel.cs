namespace SqlEstatePortal.ViewModels;

public class ServerQaCompareViewModel
{
    public string? Server1 { get; set; }
    public string? Server2 { get; set; }

    public int? Server1RunId { get; set; }
    public int? Server2RunId { get; set; }
    public DateTime? Server1AssessedAt { get; set; }
    public DateTime? Server2AssessedAt { get; set; }

    /// <summary>All assessment runs for the date dropdowns (pick date first).</summary>
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];

    /// <summary>Servers present in the selected assessment for side #1.</summary>
    public List<string> AvailableServers1 { get; set; } = [];

    /// <summary>Servers present in the selected assessment for side #2.</summary>
    public List<string> AvailableServers2 { get; set; } = [];

    public bool HasResult { get; set; }
    public List<ServerQaCompareRow> Rows { get; set; } = [];

    public int YesCount { get; set; }
    public int CloseCount { get; set; }
    public int NoCount { get; set; }
    public int TotalCount { get; set; }

    public int YesPercent => TotalCount == 0 ? 0 : (int)Math.Round(100.0 * YesCount / TotalCount, MidpointRounding.AwayFromZero);
    public int ClosePercent => TotalCount == 0 ? 0 : (int)Math.Round(100.0 * CloseCount / TotalCount, MidpointRounding.AwayFromZero);
    public int NoPercent => TotalCount == 0 ? 0 : (int)Math.Round(100.0 * NoCount / TotalCount, MidpointRounding.AwayFromZero);
}

public class ServerQaCompareRow
{
    public string Parameter { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Server1Name { get; set; } = string.Empty;
    public string Server1Value { get; set; } = string.Empty;
    public string Server2Name { get; set; } = string.Empty;
    public string Server2Value { get; set; } = string.Empty;
    /// <summary>Yes, Close, or No.</summary>
    public string Match { get; set; } = "No";
    public int MatchPercent { get; set; }
}

public class ServerQaSnapshot
{
    public string ServerName { get; set; } = string.Empty;
    public int AssessmentRunId { get; set; }
    public DateTime AssessedAt { get; set; }
    public List<QaParameterItem> Items { get; set; } = [];
}

public class QaParameterItem
{
    public string Parameter { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

public class ServerReachabilityResult
{
    public int Total { get; set; }
    public int Reachable { get; set; }
    public int Unreachable { get; set; }
}

public class ServerReachabilityService
{
    public const string StatusReachable = "Reachable";
    public const string StatusUnreachable = "UnReachable";

    private readonly AppDbContext _db;
    private readonly ILogger<ServerReachabilityService> _logger;

    public ServerReachabilityService(AppDbContext db, ILogger<ServerReachabilityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ServerReachabilityResult> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _db.CtServers
            .Where(s => s.ServerType == "SQL Servers"
                     || s.ServerType == "SQL"
                     || (string.IsNullOrEmpty(s.ServerType) && s.ServerName.Contains("SQL")))
            .ToListAsync(cancellationToken);
        var result = new ServerReachabilityResult { Total = servers.Count };
        if (servers.Count == 0)
            return result;

        var updates = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();

        await Parallel.ForEachAsync(
            servers,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 16,
                CancellationToken = cancellationToken
            },
            async (server, ct) =>
            {
                var endpoint = ResolveEndpoint(server);
                var reachable = await ProbeAsync(endpoint, ct);
                updates[server.TxId] = reachable;
            });

        var now = DateTime.UtcNow;
        foreach (var server in servers)
        {
            var reachable = updates.TryGetValue(server.TxId, out var ok) && ok;
            server.ServerStatus = reachable ? StatusReachable : StatusUnreachable;
            server.UpdatedOn = now;
            server.UpdatedBy = "Check Server Status";
            server.StatusCheckedAt = now;
            if (reachable) result.Reachable++;
            else result.Unreachable++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Server reachability check finished. Total={Total}, Reachable={Reachable}, UnReachable={Unreachable}",
            result.Total, result.Reachable, result.Unreachable);
        return result;
    }

    /// <summary>The address and TCP port a probe should target, plus whether a named instance was specified.</summary>
    internal readonly record struct ServerEndpoint(string? Host, int Port, bool NamedInstance);

    internal const int DefaultSqlPort = 1433;

    internal static ServerEndpoint ResolveEndpoint(CtServer server)
    {
        var raw = FirstUsable(server.IpAddress, server.Fqdn, server.ServerName);
        if (raw == null)
            return new ServerEndpoint(null, DefaultSqlPort, false);

        var value = raw.Trim();
        var port = DefaultSqlPort;

        // "host,1433" - an explicit port always wins.
        var comma = value.IndexOf(',');
        if (comma >= 0)
        {
            var portText = value[(comma + 1)..].Trim();
            if (int.TryParse(portText, out var parsed) && parsed is > 0 and <= 65535)
                port = parsed;
            value = value[..comma].Trim();
        }

        // "host\INSTANCE" - a named instance listens on a dynamic port brokered by
        // SQL Browser, so a 1433 probe proves nothing; remember that for the fallback.
        var namedInstance = false;
        var slash = value.IndexOf('\\');
        if (slash >= 0)
        {
            namedInstance = comma < 0;
            value = value[..slash].Trim();
        }

        return new ServerEndpoint(value, port, namedInstance);
    }

    private static string? FirstUsable(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Decides whether the estate can actually reach a SQL instance.
    /// A TCP connect on the SQL port is tried first, because that is what an assessment
    /// needs and because managed endpoints (Azure SQL, load balancers, hardened hosts)
    /// answer on 1433 while silently dropping ICMP. Ping is kept as a fallback for named
    /// instances on dynamic ports and for hosts that block 1433 from the portal.
    /// </summary>
    private static async Task<bool> ProbeAsync(ServerEndpoint endpoint, CancellationToken cancellationToken)
    {
        var host = endpoint.Host;
        if (string.IsNullOrWhiteSpace(host))
            return false;

        // Skip obvious non-host values
        if (host.Contains(' ') || host.Contains('/') || host.Contains('(') || host.StartsWith('.'))
            return false;

        if (await TryTcpConnectAsync(host, endpoint.Port, TcpTimeoutMs, cancellationToken))
            return true;

        return await TryPingAsync(host, PingTimeoutMs, cancellationToken);
    }

    private const int TcpTimeoutMs = 3000;
    private const int PingTimeoutMs = 2000;

    private static async Task<bool> TryTcpConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);

            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Timeout, refused, DNS failure - all mean "not reachable on this port".
            return false;
        }
    }

    private static async Task<bool> TryPingAsync(string host, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

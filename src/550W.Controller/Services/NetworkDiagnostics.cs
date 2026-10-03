using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace Controller550W.Controller.Services;

public sealed class NetworkDiagnostics
{
    public static NetworkInterface[] Adapters() => NetworkInterface.GetAllNetworkInterfaces();
    public async Task RunAsync(NetworkSettings config, Action<DiagnosticRow> update, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(config.TimeoutMs);
        var adapters = Array.Empty<NetworkInterface>();
        try
        {
            adapters = Adapters().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
            update(new("local", "LOCAL NETWORK", adapters.Length > 0 ? $"LINK UP · {adapters.Length} ADAPTER(S)" : "OFFLINE", adapters.Length > 0 ? "online" : "offline", "适配器链路状态，不等于互联网可用"));
        }
        catch { update(new("local", "LOCAL NETWORK", "UNAVAILABLE", "unavailable")); }
        var vpn = config.VpnMode switch
        {
            VpnMode.Manual => adapters.FirstOrDefault(x => x.Id == config.VpnAdapterId),
            VpnMode.Auto => adapters.FirstOrDefault(x => new[] { "wintun", "wireguard", "openvpn", "tap", "tun", "vpn" }.Any(n => (x.Name + " " + x.Description).Contains(n, StringComparison.OrdinalIgnoreCase))),
            _ => null
        };
        using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseDefaultCredentials = false };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("550W-Controller/1.0");
        var dns = DnsCheck(config.DnsHost, update, deadline.Token);
        var domestic = Route(client, config.DomesticEndpoints, config.TimeoutMs, deadline.Token);
        var external = Route(client, config.ExternalEndpoints, config.TimeoutMs, deadline.Token);
        var d = await domestic; update(new("domestic", "DOMESTIC ROUTE", d.Display, d.Status, Detail(d)));
        var e = await external; update(new("external", "EXTERNAL ROUTE", e.Display, e.Status, Detail(e)));
        if (config.VpnMode == VpnMode.Disabled) update(new("vpn", "VPN LINK", "DISABLED", "disabled"));
        else if (vpn == null) update(new("vpn", "VPN ADAPTER", config.VpnMode == VpnMode.Manual ? "NOT ACTIVE" : "NOT IDENTIFIED", "unknown", "仅外部连通性已检测；不把代理连通误报成 VPN"));
        else update(new("vpn", "VPN LINK", $"{e.Status.ToUpperInvariant()} · ADAPTER UP", e.Status, "检测到活动 VPN 类型适配器，状态取自三次外网采样；不证明请求一定经过该适配器"));
        await dns;
    }
    static string Detail(RouteSummary r) => $"成功 {r.Successes}/{r.Attempts}; median {r.MedianMs:0} ms; jitter(max-min) {r.JitterMs:0} ms";
    static async Task DnsCheck(string host, Action<DiagnosticRow> update, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, token);
            update(new("dns", "DNS RESOLUTION", addresses.Length > 0 ? $"{watch.ElapsedMilliseconds} ms · RESOLVED" : "UNAVAILABLE", addresses.Length > 0 ? "online" : "unavailable"));
        }
        catch (OperationCanceledException) { update(new("dns", "DNS RESOLUTION", "TIMEOUT", "timeout")); }
        catch { update(new("dns", "DNS RESOLUTION", "UNAVAILABLE", "unavailable")); }
    }
    static async Task<RouteSummary> Route(HttpClient client, string[] endpoints, int timeout, CancellationToken token)
    {
        if (endpoints.Length == 0) return new("unknown", 0, 0, null, null);
        var results = await Task.WhenAll(endpoints.Select(async endpoint =>
        {
            var samples = new List<double?>();
            for (var i = 0; i < 3; i++)
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token); attempt.CancelAfter(Math.Max(100, timeout / 3));
                var watch = Stopwatch.StartNew();
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attempt.Token);
                    samples.Add((int)response.StatusCode is >= 200 and < 400 ? watch.Elapsed.TotalMilliseconds : null);
                }
                catch { samples.Add(null); }
            }
            return RouteSummary.FromSamples(samples);
        }));
        return results.OrderByDescending(x => x.Successes).ThenBy(x => x.JitterMs ?? double.MaxValue).First();
    }
}

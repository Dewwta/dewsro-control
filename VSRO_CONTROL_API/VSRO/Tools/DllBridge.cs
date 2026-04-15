using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

public class DllBridge : IDisposable
{
    public static readonly DllBridge Instance = new();

    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private static readonly ConcurrentDictionary<string, StreamWriter> _clients = new();

    private DllBridge() { } // private so nobody calls new

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        await Task.Run(() => ListenAsync(_cts.Token));
        Logger.Info(this, "Listening on port 9001...");
    }
    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        Logger.Info(this, "Stopped.");
    }

    public void Dispose() => Stop();

    public void SendToDll(string accountName, string eventType, object payload)
    {
        if (!_clients.TryGetValue(accountName, out var writer)) return;
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(new { type = eventType, data = payload }));
        }
        catch { _clients.TryRemove(accountName, out _); }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, 9001);
        _listener.Start();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Logger.Info(this, $"Listener error: {ex.Message}"); }
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        var reader = new StreamReader(tcp.GetStream());
        var writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };
        string accountName = null;

        try
        {
            while (!ct.IsCancellationRequested && tcp.Connected)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                var type = ExtractStr(line, "type");
                if (type == "auth")
                {
                    accountName = ExtractStr(line, "user");
                    _clients[accountName] = writer;
                    Logger.Info(this, $"Authenticated: {accountName}");
                }
                else
                {
                    Logger.Info(this, $"{accountName ?? "unknown"}: {line}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Logger.Info(this, $"Client error: {ex.Message}"); }
        finally
        {
            if (accountName != null) _clients.TryRemove(accountName, out _);
            tcp.Dispose();
        }
    }

    private string ExtractStr(string json, string key)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty(key).GetString() ?? "";
        }
        catch { return ""; }
    }
}

// TODO: Implement handlers for responses, define types
using CoreLib.Models;
using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

public class DllBridge : IDisposable
{
    public static readonly DllBridge Instance = new();
    private readonly Dictionary<string, Func<string, JsonElement, Task>> _handlers = new();
    public void RegisterHandler(string type, Func<string, JsonElement, Task> handler)
    => _handlers[type] = handler;

    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private static readonly ConcurrentDictionary<string, StreamWriter> _clients = new();

    private DllBridge() { } // private so nobody calls new

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(_cts.Token));
        Logger.Debug(this, "Listening on port 9001...");
        await Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        Logger.Info(this, "Stopped.");
    }

    public void Dispose() => Stop();

    public void RemoveClient(string accName)
    {
        if (!_clients.TryRemove(accName, out _))
        {
            Logger.Debug(this, $"Couldnt remove account after disconnect! Not an error.");
        }
        
    }

    public void SendToDll(string accountName, string eventType, object payload)
    {
        if (accountName.Contains("PartyBot"))
            return;
        
        //Logger.Debug(this, $"SendToDll called: account='{accountName}' type='{eventType}'");

        if (!_clients.TryGetValue(accountName.ToLowerInvariant(), out var writer))
        {
            Logger.Warn(this, $"No connected DLL client for account '{accountName}' — dropping '{eventType}'");
            return;
        }
        
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadDoc = JsonDocument.Parse(payloadJson);

            var merged = new Dictionary<string, object> { ["type"] = eventType };
            foreach (var prop in payloadDoc.RootElement.EnumerateObject())
                merged[prop.Name] = prop.Value.Clone();  // clone so it survives doc disposal

            writer.WriteLine(JsonSerializer.Serialize(merged));
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
            catch (SocketException) { }    // listener closed mid-accept
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

                var doc = JsonDocument.Parse(line);
                var type = doc.RootElement.GetProperty("type").GetString();

                // Intercept auth to bind the writer.
                if (type == "auth")
                {
                    accountName = doc.RootElement.GetProperty("user").GetString();
                    _clients[accountName.ToLowerInvariant()] = writer;
                    Logger.Debug("DllAuth", $"Sending ack for user {accountName}");
                    DllBridge.Instance.SendToDll(accountName!, "login_ack", new { });
                    Logger.Info(this, $"Registered connection for {accountName}");
                }

                if (_handlers.TryGetValue(type!, out var handler))
                    await handler(accountName ?? "unknown", doc.RootElement);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }        // normal TCP drop / stream closed
        catch (SocketException) { }    // connection reset by peer
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

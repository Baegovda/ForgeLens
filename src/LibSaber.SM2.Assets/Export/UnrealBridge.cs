using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Export;

/// <summary>oneclick: result of one Direct-to-Unreal import.</summary>
public sealed class UnrealImportResult
{
  public bool Success { get; set; }
  /// <summary>"EngineWire", "RemoteExecution" or "Headless".</summary>
  public string Mode { get; set; } = "";
  public string Summary { get; set; } = "";
  public JsonObject? Result { get; set; }
  public string? LogPath { get; set; }
}

/// <summary>
///   oneclick: EngineWire / MCP Automation Bridge client (native MCP Streamable HTTP, http://127.0.0.1:3000/mcp; same
///   protocol as tools/ue/enginewire_call.py): initialize → notifications/initialized → tools/call system_control
///   execute_python. Token: env ENGINEWIRE_TOKEN, else %USERPROFILE%\.enginewire_token (X-MCP-Capability-Token).
/// </summary>
public sealed class EngineWireClient : IDisposable
{
  private readonly HttpClient _http;
  private readonly Uri _url;
  private readonly string? _token;
  private string? _session;
  private int _id;

  public EngineWireClient(int port = 3000, string? token = null, TimeSpan? timeout = null)
  {
    _url = new Uri($"http://127.0.0.1:{port}/mcp");
    _token = token ?? Token();
    _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromHours(2) };
  }

  public static string? Token()
  {
    var env = Environment.GetEnvironmentVariable("ENGINEWIRE_TOKEN");
    if (!string.IsNullOrWhiteSpace(env))
      return env.Trim();
    var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".enginewire_token");
    return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
  }

  public static bool PortOpen(int port, int timeoutMs = 400)
  {
    try
    {
      using var client = new TcpClient();
      var task = client.ConnectAsync(IPAddress.Loopback, port);
      return task.Wait(timeoutMs) && client.Connected;
    }
    catch
    {
      return false;
    }
  }

  private async Task<JsonNode?> Rpc(string method, JsonObject? @params, bool notification, CancellationToken ct)
  {
    var body = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
    var id = ++_id;
    if (!notification)
      body["id"] = id;
    if (@params is not null)
      body["params"] = @params;
    using var req = new HttpRequestMessage(HttpMethod.Post, _url) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    req.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
    if (!string.IsNullOrEmpty(_token))
      req.Headers.TryAddWithoutValidation("X-MCP-Capability-Token", _token);
    if (_session is not null)
      req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _session);
    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    if (resp.Headers.TryGetValues("Mcp-Session-Id", out var sid))
      _session = sid.FirstOrDefault() ?? _session;
    if (resp.StatusCode == HttpStatusCode.Unauthorized)
      throw new InvalidOperationException("EngineWire refused the token (HTTP 401). Set ENGINEWIRE_TOKEN or %USERPROFILE%\\.enginewire_token.");
    if (notification)
      return null;
    resp.EnsureSuccessStatusCode();
    var text = await resp.Content.ReadAsStringAsync(ct);
    var msg = McpResponse.Find(text, id, resp.Content.Headers.ContentType?.MediaType);
    if (msg is null)
      throw new InvalidOperationException("EngineWire sent no JSON-RPC response for " + method + ".");
    if (msg["error"] is { } err)
      throw new InvalidOperationException("EngineWire " + method + ": " + err.ToJsonString());
    return msg["result"];
  }

  public async Task Initialize(CancellationToken ct)
  {
    await Rpc("initialize", new JsonObject
    {
      ["protocolVersion"] = "2025-03-26",
      ["capabilities"] = new JsonObject(),
      ["clientInfo"] = new JsonObject { ["name"] = "ForgeLens", ["version"] = "1" },
    }, false, ct);
    await Rpc("notifications/initialized", null, true, ct);
  }

  /// <summary>Runs Python in the editor; returns (ok, captured output, error text).</summary>
  public async Task<(bool Ok, string Output, string Error)> ExecutePython(string code, CancellationToken ct)
  {
    var result = await Rpc("tools/call", new JsonObject
    {
      ["name"] = "system_control",
      ["arguments"] = new JsonObject { ["action"] = "execute_python", ["code"] = code },
    }, false, ct);
    var sc = result?["structuredContent"] as JsonObject;
    var output = sc?["output"]?.GetValue<string>() ?? string.Join("\n", (result?["content"] as JsonArray ?? new JsonArray()).Select(c => c?["text"]?.GetValue<string>() ?? ""));
    var error = sc?["error"]?.ToString() ?? "";
    var isError = result?["isError"]?.GetValue<bool>() == true;
    return (!isError && error.Length == 0, output, error);
  }

  public void Dispose()
  {
    try
    {
      if (_session is not null)
      {
        using var req = new HttpRequestMessage(HttpMethod.Delete, _url);
        req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _session);
        if (!string.IsNullOrEmpty(_token))
          req.Headers.TryAddWithoutValidation("X-MCP-Capability-Token", _token);
        _http.Send(req);
      }
    }
    catch
    {
      // session expires on its own
    }

    _http.Dispose();
  }
}

/// <summary>oneclick: picks the JSON-RPC response out of a JSON or text/event-stream (SSE) body.</summary>
public static class McpResponse
{
  public static JsonObject? Find(string body, int id, string? mediaType = null)
  {
    var candidates = new List<string>();
    if (mediaType == "text/event-stream" || body.TrimStart().StartsWith("event:") || body.TrimStart().StartsWith("data:"))
    {
      var data = new StringBuilder();
      foreach (var raw in body.Replace("\r\n", "\n").Split('\n'))
      {
        if (raw.StartsWith("data:"))
          data.Append(raw.AsSpan(5).TrimStart()).Append('\n');
        else if (raw.Length == 0 && data.Length > 0)
        {
          candidates.Add(data.ToString());
          data.Clear();
        }
      }

      if (data.Length > 0)
        candidates.Add(data.ToString());
    }
    else
      candidates.Add(body);

    foreach (var c in candidates)
    {
      JsonNode? node;
      try { node = JsonNode.Parse(c); }
      catch { continue; }
      foreach (var msg in node is JsonArray arr ? arr.OfType<JsonObject>() : node is JsonObject o ? new[] { o } : Array.Empty<JsonObject>())
        if (msg["id"] is { } mid && mid.ToString() == id.ToString() && (msg.ContainsKey("result") || msg.ContainsKey("error")))
          return msg;
    }

    return null;
  }
}

/// <summary>
///   oneclick: Unreal's Python Remote Execution (PythonScriptPlugin bRemoteExecution; protocol of the engine's
///   remote_execution.py): UDP multicast 239.0.0.1:6766 (bind 127.0.0.1) ping/pong discovery, open_connection to a
///   TCP command server we host, then a "command" message (ExecuteFile) and its "command_result".
/// </summary>
public sealed class PythonRemoteExecution
{
  private const string Magic = "ue_py";
  private static readonly IPAddress Group = IPAddress.Parse("239.0.0.1");
  private const int Port = 6766;
  private readonly string _node = Guid.NewGuid().ToString();

  public sealed record Node(string Id, string ProjectRoot, string ProjectName);

  private static byte[] Message(string type, string source, string? dest = null, JsonObject? data = null)
  {
    var o = new JsonObject { ["version"] = 1, ["magic"] = Magic, ["type"] = type, ["source"] = source };
    if (dest is not null)
      o["dest"] = dest;
    if (data is not null)
      o["data"] = data;
    return Encoding.UTF8.GetBytes(o.ToJsonString());
  }

  private static UdpClient Socket()
  {
    var udp = new UdpClient(AddressFamily.InterNetwork);
    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, Port));
    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Loopback.GetAddressBytes());
    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 0);
    udp.JoinMulticastGroup(Group, IPAddress.Loopback);
    return udp;
  }

  /// <summary>Editors answering a ping within <paramref name="timeoutMs" />.</summary>
  public async Task<List<Node>> Discover(int timeoutMs, CancellationToken ct)
  {
    var nodes = new List<Node>();
    using var udp = Socket();
    var ping = Message("ping", _node);
    await udp.SendAsync(ping, ping.Length, new IPEndPoint(Group, Port));
    var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (DateTime.UtcNow < end)
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      cts.CancelAfter(end - DateTime.UtcNow);
      UdpReceiveResult r;
      try { r = await udp.ReceiveAsync(cts.Token); }
      catch (OperationCanceledException) { break; }
      try
      {
        var msg = JsonNode.Parse(r.Buffer) as JsonObject;
        if (msg?["magic"]?.ToString() != Magic || msg["type"]?.ToString() != "pong" || msg["dest"]?.ToString() != _node)
          continue;
        var id = msg["source"]!.ToString();
        if (nodes.All(n => n.Id != id))
          nodes.Add(new Node(id, msg["data"]?["project_root"]?.ToString() ?? "", msg["data"]?["project_name"]?.ToString() ?? ""));
      }
      catch
      {
        // not ours
      }
    }

    return nodes;
  }

  /// <summary>Runs a .py file in the editor of <paramref name="node" />.</summary>
  public async Task<(bool Ok, string Output)> ExecuteFile(Node node, string file, CancellationToken ct)
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    using var udp = Socket();
    try
    {
      var open = Message("open_connection", _node, node.Id, new JsonObject { ["command_ip"] = "127.0.0.1", ["command_port"] = port });
      await udp.SendAsync(open, open.Length, new IPEndPoint(Group, Port));
      using var accept = CancellationTokenSource.CreateLinkedTokenSource(ct);
      accept.CancelAfter(TimeSpan.FromSeconds(10));
      using var tcp = await listener.AcceptTcpClientAsync(accept.Token);
      var stream = tcp.GetStream();
      var cmd = Message("command", _node, node.Id, new JsonObject
      {
        ["command"] = file.Replace('\\', '/'),
        ["unattended"] = true,
        ["exec_mode"] = "ExecuteFile",
      });
      await stream.WriteAsync(cmd, ct);
      var buffer = new MemoryStream();
      var chunk = new byte[65536];
      while (true)
      {
        var n = await stream.ReadAsync(chunk, ct);
        if (n <= 0)
          throw new InvalidOperationException("Remote Execution closed the connection before the result.");
        buffer.Write(chunk, 0, n);
        JsonObject? msg;
        try { msg = JsonNode.Parse(buffer.ToArray()) as JsonObject; }
        catch (JsonException) { continue; }
        if (msg?["type"]?.ToString() != "command_result")
          continue;
        var data = msg["data"];
        var output = string.Join("\n", (data?["output"] as JsonArray ?? new JsonArray()).Select(o => o?["output"]?.ToString() ?? ""));
        var ok = data?["success"]?.GetValue<bool>() == true;
        if (!ok && data?["result"] is { } res)
          output += "\n" + res;
        return (ok, output);
      }
    }
    finally
    {
      try
      {
        var close = Message("close_connection", _node, node.Id);
        await udp.SendAsync(close, close.Length, new IPEndPoint(Group, Port));
      }
      catch
      {
        // best effort
      }

      listener.Stop();
    }
  }
}

/// <summary>
///   oneclick: runs the Direct-to-Unreal import. Order: EngineWire (open editor, port 3000) → Python Remote Execution
///   (open editor whose project matches) → headless UnrealEditor-Cmd.exe -run=pythonscript (only when no Unreal Editor is
///   running, so a second process never saves packages under an open editor). Env SM2_DIRECT_BRIDGE=enginewire|remote|headless
///   forces one route (QA; "headless" then also runs next to an open editor, so only use it with a new content folder).
/// </summary>
public static class UnrealDirectImporter
{
  public static string? ForcedBridge() => Environment.GetEnvironmentVariable("SM2_DIRECT_BRIDGE")?.Trim().ToLowerInvariant() is { Length: > 0 } b && b != "auto" ? b : null;

  public static async Task<UnrealImportResult> Run(UnrealDirectManifest.Paths paths, UnrealDirectChoice choice, IProgress<string> log, CancellationToken ct,
    int engineWirePort = 3000)
  {
    var result = new UnrealImportResult { LogPath = paths.Log };
    var projectDir = Path.GetDirectoryName(Path.GetFullPath(choice.Project)) ?? "";
    using var tail = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var tailTask = Tail(paths.Log, log, tail.Token);
    try
    {
      string? editorOutput = null;
      var done = false;
      var forced = ForcedBridge();
      if (forced is not null)
        log.Report("Bridge forced by SM2_DIRECT_BRIDGE=" + forced + ".");
      if (forced is null or "enginewire" && EngineWireClient.PortOpen(engineWirePort))
      {
        log.Report($"Editor bridge found (EngineWire :{engineWirePort}).");
        try
        {
          using var ew = new EngineWireClient(engineWirePort);
          await ew.Initialize(ct);
          var probe = await ew.ExecutePython("import unreal\nprint('FORGEPROJECT ' + unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()))", ct);
          var running = probe.Output.Split('\n').FirstOrDefault(l => l.StartsWith("FORGEPROJECT "))?["FORGEPROJECT ".Length..].Trim() ?? "";
          if (!SameDir(running, projectDir))
            log.Report($"EngineWire editor has another project open ({running}); not using it.");
          else
          {
            result.Mode = "EngineWire";
            log.Report("Importing through EngineWire…");
            var code = "exec(compile(open(" + JsonSerializer.Serialize(paths.Runner.Replace('\\', '/')) + ", encoding='utf-8').read(), 'run_import.py', 'exec'), {'__name__': '__main__'})";
            var r = await ew.ExecutePython(code, ct);
            editorOutput = r.Output + (r.Error.Length > 0 ? "\n" + r.Error : "");
            done = true;
          }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          log.Report("EngineWire failed: " + ex.Message);
        }
      }

      if (!done && forced is null or "remote")
      {
        try
        {
          var rex = new PythonRemoteExecution();
          var nodes = await rex.Discover(1500, ct);
          var node = nodes.FirstOrDefault(n => SameDir(n.ProjectRoot, projectDir));
          if (node is not null)
          {
            result.Mode = "RemoteExecution";
            log.Report($"Importing through Python Remote Execution ({node.ProjectName})…");
            var r = await rex.ExecuteFile(node, paths.Runner, ct);
            editorOutput = r.Output;
            done = true;
          }
          else if (nodes.Count > 0)
            log.Report("Remote Execution editors found, but none has this project open: " + string.Join(", ", nodes.Select(n => n.ProjectName)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          log.Report("Remote Execution not available: " + ex.Message);
        }
      }

      if (!done && forced is "enginewire" or "remote")
      {
        result.Summary = "The forced bridge (" + forced + ") did not answer for this project.";
        return result;
      }

      if (!done)
      {
        if (forced != "headless" && EditorRunning())
        {
          result.Summary = "An Unreal Editor is running, but neither EngineWire (:" + engineWirePort + ") nor Python Remote Execution answered for this project. " +
                           "Close the editor (headless import) or enable a bridge, then export again. The files are in the export folder.";
          return result;
        }

        var cmd = UnrealEditorLocator.FindEditorCmd(choice.Project);
        if (cmd is null)
        {
          result.Summary = "UnrealEditor-Cmd.exe not found for " + choice.Project + ".";
          return result;
        }

        result.Mode = "Headless";
        log.Report("No editor open: headless import with " + cmd);
        var psi = new ProcessStartInfo(cmd, UnrealEditorLocator.HeadlessArguments(choice.Project, paths.Runner))
        {
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
        };
        psi.Environment["SM2_FORGE_HEADLESS"] = "1";
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("could not start " + cmd);
        try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* not allowed */ }
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) =>
        {
          if (e.Data is null) return;
          lock (sb) sb.AppendLine(e.Data);
          if (e.Data.Contains("Error:", StringComparison.Ordinal) && e.Data.Contains("LogPython", StringComparison.Ordinal))
            log.Report(e.Data.Trim());
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (sb) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        editorOutput = sb.ToString();
        log.Report($"UnrealEditor-Cmd exited with code {p.ExitCode}.");
        File.WriteAllText(Path.ChangeExtension(paths.Log, ".headless.txt"), editorOutput);
      }

      await Task.Delay(300, CancellationToken.None);
      JsonObject? json = null;
      if (File.Exists(paths.Result))
        json = JsonNode.Parse(File.ReadAllText(paths.Result)) as JsonObject;
      else if (editorOutput?.Split('\n').LastOrDefault(l => l.Contains("FORGEJSON ")) is { } line)
        json = JsonNode.Parse(line[(line.IndexOf("FORGEJSON ", StringComparison.Ordinal) + 10)..]) as JsonObject;
      var noResult = json is null;
      json ??= new JsonObject
      {
        ["ok"] = false,
        ["models"] = new JsonArray(),
        ["errors"] = new JsonArray(Summarize(null, editorOutput)),
      };
      {
        json["mode"] = result.Mode;
        try { File.WriteAllText(paths.Result, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); } catch (IOException) { }
      }

      result.Result = json;
      result.Success = json?["ok"]?.GetValue<bool>() == true;
      result.Summary = Summarize(noResult ? null : json, editorOutput);
      return result;
    }
    finally
    {
      await Task.Delay(400, CancellationToken.None);
      tail.Cancel();
      try { await tailTask; } catch (OperationCanceledException) { }
    }
  }

  public static string Summarize(JsonObject? json, string? output)
  {
    if (json is null)
    {
      var lines = output?.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.Contains("[Callstack]")).ToList() ?? new List<string>();
      // A crash prints "Assertion failed" / "Fatal error" before its callstack: report that, not the stack.
      var crash = lines.FirstOrDefault(l => l.Contains("Assertion failed", StringComparison.Ordinal) || l.Contains("Fatal error", StringComparison.Ordinal)
                                            || l.Contains("Unhandled Exception", StringComparison.Ordinal));
      var err = crash is not null
        ? new[] { "Unreal crashed: " + crash }
        : lines.Where(l => (l.Contains("Error", StringComparison.OrdinalIgnoreCase) && !l.EndsWith("Error:", StringComparison.Ordinal)) || l.Contains("Traceback")).TakeLast(4);
      return "The import did not finish (no result). " + string.Join(" ", err).Trim();
    }

    var parts = new List<string>();
    foreach (var m in (json["models"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
    {
      var errs = (m["errors"] as JsonArray)?.Count ?? 0;
      var clips = (m["clips"] as JsonArray)?.Count ?? 0;
      var manny = (m["mannyClips"] as JsonArray)?.Count ?? 0;
      var s = $"{m["name"]} → {m["folder"]}: {m["assets"] ?? 0} assets" + (clips > 0 ? $", {clips} clips" : "") + (manny > 0 ? $", {manny} Manny clips" : "")
              + (m["materialInstances"] is { } mi ? $", {mi} MIs" : "") + (m["backup"] is { } b ? $", old copy {b}" : "")
              + (errs > 0 ? ", errors: " + string.Join("; ", (m["errors"] as JsonArray)!.Select(e => e?.ToString())) : "");
      parts.Add(s);
    }

    return (json["ok"]?.GetValue<bool>() == true ? "Imported. " : "Import FAILED. ") + string.Join(" | ", parts);
  }

  private static bool SameDir(string a, string b)
  {
    static string N(string p) => p.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
    return a.Length > 0 && N(a) == N(b);
  }

  private static bool EditorRunning()
  {
    try
    {
      return Process.GetProcesses().Any(p =>
      {
        try { return p.ProcessName.StartsWith("UnrealEditor", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
      });
    }
    catch
    {
      return false;
    }
  }

  /// <summary>Streams new lines of the importer's log file.</summary>
  private static async Task Tail(string path, IProgress<string> log, CancellationToken ct)
  {
    long pos = 0;
    var rest = "";
    while (true)
    {
      try
      {
        if (File.Exists(path))
        {
          using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
          if (fs.Length > pos)
          {
            fs.Seek(pos, SeekOrigin.Begin);
            var bytes = new byte[fs.Length - pos];
            var n = await fs.ReadAsync(bytes, CancellationToken.None);
            pos += n;
            var text = rest + Encoding.UTF8.GetString(bytes, 0, n);
            var lines = text.Split('\n');
            rest = lines[^1];
            foreach (var l in lines[..^1])
              log.Report(l.TrimEnd('\r'));
          }
        }
      }
      catch (IOException)
      {
        // retry
      }

      if (ct.IsCancellationRequested)
      {
        if (rest.Length > 0)
          log.Report(rest);
        return;
      }

      try { await Task.Delay(250, ct); }
      catch (OperationCanceledException) { /* one last read */ }
    }
  }
}

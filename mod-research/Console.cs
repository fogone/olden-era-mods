using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OldenEra.Modding.Research;

// The console: the same commands answered at once, on this computer only (loopback), for local development.
//   http://127.0.0.1:8766/?c=<command>        one command
//   POST with one command per line            several (lines starting with # are left out)
// The request waits while the game runs the commands on its main thread, then the answer is their output as text.
// Every request carries the key the console writes at its start into BepInEx\research-token.txt, in the header
// X-Research-Token: a web page in a browser can neither read the file nor send that header to this address (the
// console allows no cross-origin request), so a page cannot run commands here. A request with an Origin (a browser's)
// or another Host (a name made to point here) is refused too.
//   curl -s -H "X-Research-Token: $(cat BepInEx/research-token.txt)" "http://127.0.0.1:8766/?c=eval%20hero.cqei.cwfa"
//   curl -s -H "X-Research-Token: ..." --data-binary @commands.txt http://127.0.0.1:8766/
public partial class Plugin
{
    const int Port = 8766;
    const int MaxBody = 1 << 20;
    const string TokenHeader = "X-Research-Token";

    static TcpListener listener;
    static string token;
    static readonly ConcurrentQueue<Job> jobs = new();

    sealed class Job
    {
        public string[] Lines;
        public readonly StringBuilder Output = new();
        public readonly ManualResetEventSlim Done = new();
    }

    static void StartConsole()
    {
        try
        {
            token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            File.WriteAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "research-token.txt"), token);
            var on = new TcpListener(IPAddress.Loopback, Port);
            on.Start();
            listener = on;
            new Thread(() => Serve(on)) { IsBackground = true, Name = "OE research console" }.Start();
            logger.LogInfo($"Console at http://127.0.0.1:{Port}/?c=<command>, the key in BepInEx\\research-token.txt (header {TokenHeader})");
        }
        catch (Exception e)
        {
            logger.LogWarning($"Console not started on port {Port}: {e.Message}");
        }
    }

    // The port is freed for the next load (the dev loader reloads the plugin on a rebuild).
    static void StopConsole()
    {
        listener?.Stop();
        listener = null;
    }

    // On the main thread, every frame: the commands that came in.
    static void RunConsoleJobs()
    {
        while (jobs.TryDequeue(out var job))
        {
            foreach (var line in job.Lines)
                commands.Run(line, text => job.Output.Append(text).Append('\n'));
            job.Done.Set();
        }
    }

    static void Serve(TcpListener on)
    {
        while (true)
        {
            TcpClient client;
            try { client = on.AcceptTcpClient(); }
            catch (Exception) { return; }   // stopped
            // Each on its own: one waiting for the main thread does not hold up the others.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Answer(client); }
                catch (Exception) { /* the client went away */ }
                finally { client.Close(); }
            });
        }
    }

    // A minimal HTTP exchange; the answer waits for the main thread (at most a minute).
    static void Answer(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var request = reader.ReadLine() ?? "";
        var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string line;
        while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            if (line.IndexOf(':') is var colon and > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        if (Refused(headers) is { } refusal)
        {
            Reply(stream, "403 Forbidden", refusal + "\n");
            return;
        }
        var length = headers.TryGetValue("Content-Length", out var value) && int.TryParse(value, out var n) ? n : 0;
        if (length < 0 || length > MaxBody)
        {
            Reply(stream, "413 Payload Too Large", $"at most {MaxBody} bytes\n");
            return;
        }
        var body = new char[length];
        for (var read = 0; read < length;)
        {
            var got = reader.Read(body, read, length - read);
            if (got <= 0)
                break;
            read += got;
        }
        var target = request.Split(' ').Skip(1).FirstOrDefault() ?? "/";
        var query = target.Contains("?c=") ? Uri.UnescapeDataString(target[(target.IndexOf("?c=") + 3)..].Replace('+', ' ')) : "";
        var lines = (query + "\n" + new string(body)).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
        var job = new Job { Lines = lines.Length > 0 ? lines : new[] { "help" } };
        jobs.Enqueue(job);
        Reply(stream, "200 OK", job.Done.Wait(TimeSpan.FromSeconds(60)) ? job.Output.ToString() : "timed out (is the game paused or loading?)\n");
    }

    // Why the request may not run commands: no key or a wrong one, a browser's (Origin), not to this computer's name.
    static string Refused(System.Collections.Generic.Dictionary<string, string> headers)
    {
        if (headers.ContainsKey("Origin"))
            return "refused: a request from a web page";
        if (!headers.TryGetValue("Host", out var host) || (host != $"127.0.0.1:{Port}" && host != $"localhost:{Port}"))
            return "refused: not to 127.0.0.1";
        if (token == null || !headers.TryGetValue(TokenHeader, out var key) || key != token)
            return $"refused: no key (header {TokenHeader}, the key in BepInEx\\research-token.txt)";
        return null;
    }

    static void Reply(Stream stream, string status, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        stream.Write(head, 0, head.Length);
        stream.Write(bytes, 0, bytes.Length);
    }
}

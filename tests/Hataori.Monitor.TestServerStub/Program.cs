using System.Net;
using System.Net.Sockets;
using System.Text;

// Monitorのテストが「Hataori.Server.exe」として起動する代用Server。
// - HATAORI_STUB_PORT があれば 127.0.0.1 の指定ポートで GET /health に {"status":"ok"} を返す。
// - 標準入力が閉じられると終了する（Monitorの正常停止と同じ）。HATAORI_STUB_IGNORE_STDIN=1 なら終了せず待ち続ける。
var port = int.TryParse(Environment.GetEnvironmentVariable("HATAORI_STUB_PORT"), out var value) ? value : 0;
var ignoreStdin = Environment.GetEnvironmentVariable("HATAORI_STUB_IGNORE_STDIN") == "1";
using var stop = new CancellationTokenSource();

if (port > 0)
{
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    _ = Task.Run(async () =>
    {
        while (!stop.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(stop.Token);
            await using var stream = client.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer, stop.Token);
            var body = "{\"status\":\"ok\",\"name\":\"Hataori\",\"version\":\"0.0.0-stub\"}";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response), stop.Token);
        }
    });
}

if (ignoreStdin)
{
    await Task.Delay(Timeout.Infinite);
}

await Console.In.ReadToEndAsync();
stop.Cancel();
return 0;

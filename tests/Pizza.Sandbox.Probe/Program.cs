using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// Argumentos: --in <handle> --out <handle> --forbidden <arquivo>; ou --stdio --forbidden <arquivo>.
var stdio = args.Contains("--stdio");
var forbidden = args[Array.IndexOf(args, "--forbidden") + 1];

using Stream input = stdio ? Stream.Null : OpenHandle(args[Array.IndexOf(args, "--in") + 1], FileAccess.Read);
using Stream output = stdio ? Console.OpenStandardOutput() : OpenHandle(args[Array.IndexOf(args, "--out") + 1], FileAccess.Write);
using var reader = new StreamReader(input);
using var writer = new StreamWriter(output) { AutoFlush = true };

OpenProcessToken(GetCurrentProcess(), 8 /* TOKEN_QUERY */, out var token);
GetTokenInformation(token, 29 /* TokenIsAppContainer */, out var isAppContainer, sizeof(int), out _);
writer.WriteLine("is_appcontainer=" + (isAppContainer != 0));

Attempt("read_forbidden", () => File.ReadAllText(forbidden));
Attempt("list_forbidden_folder", () => Directory.GetFiles(Path.GetDirectoryName(forbidden)!));
Attempt("write_forbidden_folder", () => File.WriteAllText(forbidden + ".escrito", "x"));
Attempt("spawn_process", () => Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { CreateNoWindow = true, UseShellExecute = false })!.WaitForExit());
Attempt("dns", () => Dns.GetHostAddresses("example.com"));
Attempt("connect", () =>
{
    using var client = new TcpClient();
    if (!client.ConnectAsync(IPAddress.Parse("1.1.1.1"), 443).Wait(TimeSpan.FromSeconds(4)))
    {
        throw new TimeoutException();
    }
});

// Depois das tentativas proibidas, o caminho legítimo: ler pelo handle que o residente entregou.
var line = reader.ReadLine();
if (line is not null && line.StartsWith("src=", StringComparison.Ordinal))
{
    Attempt("read_handle", () =>
    {
        using var source = OpenHandle(line["src=".Length..], FileAccess.Read);
        using var sourceReader = new StreamReader(source);
        writer.WriteLine("handle_text=" + sourceReader.ReadToEnd());
    });
}

writer.WriteLine("end=1");
return 0;

void Attempt(string name, Action action)
{
    try
    {
        action();
        writer.WriteLine(name + "=allowed");
    }
    catch (Exception)
    {
        writer.WriteLine(name + "=denied");
    }
}

static FileStream OpenHandle(string value, FileAccess access) =>
    new(new SafeFileHandle(nint.Parse(value, System.Globalization.CultureInfo.InvariantCulture), ownsHandle: true), access, 0, false);

[DllImport("kernel32.dll")]
static extern nint GetCurrentProcess();

[DllImport("advapi32.dll")]
static extern bool OpenProcessToken(nint process, uint access, out nint token);

[DllImport("advapi32.dll")]
static extern bool GetTokenInformation(nint token, int infoClass, out int value, int size, out int returned);

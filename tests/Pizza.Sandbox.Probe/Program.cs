using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

// Argumentos: --in <handle> --out <handle> --forbidden <arquivo>; ou --stdio --forbidden <arquivo>.
// --protect: liga a mesma proteção contra dumps do residente (PRIV-05), antes de qualquer outra coisa.
if (args.Contains("--protect"))
{
    Pizza.Core.Diagnostics.CrashReporting.Suppress();
}

var stdio = args.Contains("--stdio");
var forbidden = args[Array.IndexOf(args, "--forbidden") + 1];

using Stream input = stdio ? Stream.Null : OpenHandle(args[Array.IndexOf(args, "--in") + 1], FileAccess.Read);
using Stream output = stdio ? Console.OpenStandardOutput() : OpenHandle(args[Array.IndexOf(args, "--out") + 1], FileAccess.Write);
using var reader = new StreamReader(input);
using var writer = new StreamWriter(output) { AutoFlush = true };

var act = Array.IndexOf(args, "--act");
if (act >= 0)
{
    // Worker sabotado, para testar como o residente reage.
    var request = reader.ReadLine() ?? "";
    var jobId = Regex.Match(request, "\"jobId\":\"([0-9a-f]{12})\"").Groups[1].Value;
    switch (args[act + 1])
    {
        case "quality-offer":
            var suggestedIndex = Array.IndexOf(args, "--suggested");
            var suggested = int.Parse(args[suggestedIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
            writer.Write("{\"v\":1,\"type\":\"qualityoffer\",\"jobId\":\"" + jobId + "\",\"bitrate\":" + suggested + "}\n");
            return 0;
        case "valid-bundle":
            using (var destination = OpenHandle(Regex.Match(request, "\"dstHandle\":(\\d+)").Groups[1].Value, FileAccess.Write))
            {
                var bundle = new Pizza.Core.Output.OutputBundle.Writer(destination, 2);
                bundle.Add("%PDF-primeiro"u8);
                bundle.Add("%PDF-segundo"u8);
                bundle.Finish();
            }

            writer.Write(Done(jobId));
            return 0;
        case "oversized-output":
        case "oversized-bundle":
        case "oversized-after-done":
            var mode = args[act + 1];
            if (mode == "oversized-after-done")
            {
                writer.Write(Done(jobId));
            }

            using (var destination = OpenHandle(Regex.Match(request, "\"dstHandle\":(\\d+)").Groups[1].Value, FileAccess.Write))
            {
                // Comprimento lógico grande, com poucos bytes físicos: o teste nunca grava 2 GiB.
                if (!DeviceIoControl(destination.SafeFileHandle, 0x000900C4 /* FSCTL_SET_SPARSE */, 0, 0, 0, 0, out _, 0))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                var length = Pizza.Core.Output.OutputBudget.MaxBytes + 1;
                if (mode == "oversized-bundle")
                {
                    destination.Write("PZB1"u8);
                    destination.Write(BitConverter.GetBytes(1));
                    destination.Write(BitConverter.GetBytes(length));
                    destination.Write("%PDF-"u8);
                    length += 16;
                }

                destination.SetLength(length);
            }

            if (mode == "oversized-after-done")
            {
                break;
            }

            writer.Write(Done(jobId));
            return 0;
        case "bad-bundle":
            // Worker comprometido no Dividir: anuncia duas partes e entrega uma, que nem é PDF.
            using (var destination = OpenHandle(Regex.Match(request, "\"dstHandle\":(\\d+)").Groups[1].Value, FileAccess.Write))
            {
                destination.Write("PZB1"u8);
                destination.Write(BitConverter.GetBytes(2));
                destination.Write(BitConverter.GetBytes(4L));
                destination.Write("MZ.."u8);
            }

            writer.Write(Done(jobId));
            return 0;
        case "bad-markdown":
            // Worker comprometido no Markdown (ADR-038): imagem válida, mas o texto cita a imagem 2, que não existe.
            using (var destination = OpenHandle(Regex.Match(request, "\"dstHandle\":(\\d+)").Groups[1].Value, FileAccess.Write))
            {
                var markdown = System.Text.Encoding.UTF8.GetBytes("![[2]]");
                destination.Write("PZB2"u8);
                destination.Write(BitConverter.GetBytes(2));
                destination.WriteByte(1);
                destination.Write(BitConverter.GetBytes((long)markdown.Length));
                destination.Write(markdown);
                destination.WriteByte(2);
                destination.Write(BitConverter.GetBytes(5L));
                destination.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 1]);
            }

            writer.Write(Done(jobId));
            return 0;
        case "exit":
            return 1;
        case "crash":
            // TST-20: travamento de verdade, como um decodificador que estoura a memória.
            Marshal.WriteInt32((nint)0x10, 1);
            break;
        case "failfast":
            Environment.FailFast("TST-20");
            break;
        case "hang-after-done":
            writer.Write(Done(jobId));
            break;
        case "wrong-job":
            writer.Write(Done("ffffffffffff"));
            return 0;
        case "flood-progress":
            // Worker comprometido: avisos repetidos e voltando, para encher a fila da interface.
            for (var i = 0; i < 20000; i++)
            {
                writer.Write(Progress(jobId, 50));
            }

            writer.Write(Progress(jobId, 30));
            writer.Write(Progress(jobId, 100));
            writer.Write(Done(jobId));
            return 0;
    }

    Thread.Sleep(Timeout.Infinite);
}

OpenProcessToken(GetCurrentProcess(), 8 /* TOKEN_QUERY */, out var token);
GetTokenInformation(token, 29 /* TokenIsAppContainer */, out var isAppContainer, sizeof(int), out _);
writer.WriteLine("is_appcontainer=" + (isAppContainer != 0));
writer.WriteLine("error_mode=" + GetErrorMode());

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

static string Progress(string jobId, int percent) => "{\"v\":1,\"type\":\"progress\",\"jobId\":\"" + jobId + "\",\"percent\":" + percent + "}\n";

static string Done(string jobId) => "{\"v\":1,\"type\":\"done\",\"jobId\":\"" + jobId + "\",\"elapsedMs\":1}\n";

static FileStream OpenHandle(string value, FileAccess access) =>
    new(new SafeFileHandle(nint.Parse(value, System.Globalization.CultureInfo.InvariantCulture), ownsHandle: true), access, 0, false);

[DllImport("kernel32.dll")]
static extern nint GetCurrentProcess();

[DllImport("kernel32.dll")]
static extern uint GetErrorMode();

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool DeviceIoControl(SafeFileHandle file, uint code, nint input, uint inputSize, nint output, uint outputSize, out uint returned, nint overlapped);

[DllImport("advapi32.dll")]
static extern bool OpenProcessToken(nint process, uint access, out nint token);

[DllImport("advapi32.dll")]
static extern bool GetTokenInformation(nint token, int infoClass, out int value, int size, out int returned);

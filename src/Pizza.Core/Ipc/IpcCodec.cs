using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pizza.Core.Ipc;

public enum IpcType
{
    Warmup,
    Convert,
    Cancel,
    Progress,
    Done,
    Error,
}

/// <summary>Mensagem do canal entre residente e Worker (SPECS §10.3). Não existe campo para caminho de arquivo (SEC-15).</summary>
public sealed record IpcMessage(IpcType Type, string JobId)
{
    public string? Category { get; init; }

    public string? ConverterId { get; init; }

    public long? SrcHandle { get; init; }

    public long? DstHandle { get; init; }

    public IReadOnlyDictionary<string, string>? Options { get; init; }

    public int? Percent { get; init; }

    public long? ElapsedMs { get; init; }

    public string? Code { get; init; }
}

public sealed class IpcProtocolException(string message) : Exception(message);

/// <summary>Lê e grava mensagens em JSON estrito: qualquer coisa fora do esquema é recusada (SEC-13).</summary>
public static partial class IpcCodec
{
    public const int MaxMessageBytes = 64 * 1024;

    private const int Version = 1;

    private static readonly string[] Categories = ["image", "pdf"];

    private static readonly string[] ErrorCodes =
    [
        "CORRUPTED", "LOCKED", "NO_SPACE", "NO_PERMISSION", "PASSWORD", "LIMIT_EXCEEDED",
        "TIMEOUT", "UNSUPPORTED", "WORKER_CRASHED", "OFFICE_UNAVAILABLE",
    ];

    private static readonly Dictionary<IpcType, string[]> Fields = new()
    {
        [IpcType.Warmup] = ["category"],
        [IpcType.Convert] = ["converterId", "srcHandle", "dstHandle", "options"],
        [IpcType.Cancel] = [],
        [IpcType.Progress] = ["percent"],
        [IpcType.Done] = ["elapsedMs"],
        [IpcType.Error] = ["code"],
    };

    public static IpcMessage Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxMessageBytes)
        {
            throw new IpcProtocolException("Mensagem acima do limite.");
        }

        Dictionary<string, object> values;
        try
        {
            values = ReadObject(payload);
        }
        catch (JsonException)
        {
            throw new IpcProtocolException("JSON inválido.");
        }

        if (!values.Remove("v", out var version) || version is not long number || number != Version)
        {
            throw new IpcProtocolException("Versão inválida.");
        }

        var jobId = TakeString(values, "jobId");
        if (!JobIdFormat().IsMatch(jobId))
        {
            throw new IpcProtocolException("jobId inválido.");
        }

        var type = ParseType(TakeString(values, "type"));
        if (values.Keys.Except(Fields[type]).Any() || Fields[type].Except(values.Keys).Any())
        {
            throw new IpcProtocolException("Campos não correspondem ao tipo.");
        }

        return Build(type, jobId, values);
    }

    public static byte[] Serialize(IpcMessage message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", Version);
            writer.WriteString("type", TypeName(message.Type));
            writer.WriteString("jobId", message.JobId);
            WriteFields(writer, message);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    [GeneratedRegex("^[0-9a-f]{12}$")]
    private static partial Regex JobIdFormat();

    [GeneratedRegex("^[a-z]+\\.[a-z0-9]+$")]
    private static partial Regex ConverterIdFormat();

    private static Dictionary<string, object> ReadObject(ReadOnlySpan<byte> payload)
    {
        var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = 3 });
        var values = new Dictionary<string, object>();

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new IpcProtocolException("Esperava um objeto.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            if (!reader.Read() || !values.TryAdd(name, ReadValue(ref reader)))
            {
                throw new IpcProtocolException("Campo repetido.");
            }
        }

        if (reader.Read())
        {
            throw new IpcProtocolException("Conteúdo depois do objeto.");
        }

        return values;
    }

    private static object ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString()!;

            case JsonTokenType.Number when reader.TryGetInt64(out var number):
                return number;

            case JsonTokenType.StartObject:
                var options = new Dictionary<string, string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var name = reader.GetString()!;
                    if (!reader.Read() || reader.TokenType != JsonTokenType.String || !options.TryAdd(name, reader.GetString()!))
                    {
                        throw new IpcProtocolException("Opção inválida.");
                    }
                }

                return options;

            default:
                throw new IpcProtocolException("Tipo de valor não aceito.");
        }
    }

    private static IpcMessage Build(IpcType type, string jobId, Dictionary<string, object> values) => type switch
    {
        IpcType.Warmup => new IpcMessage(type, jobId) { Category = OneOf(values, "category", Categories) },
        IpcType.Convert => new IpcMessage(type, jobId)
        {
            ConverterId = ConverterId(values),
            SrcHandle = Handle(values, "srcHandle"),
            DstHandle = Handle(values, "dstHandle"),
            Options = values["options"] as Dictionary<string, string> ?? throw new IpcProtocolException("options inválido."),
        },
        IpcType.Progress => new IpcMessage(type, jobId) { Percent = (int)Number(values, "percent", 0, 100) },
        IpcType.Done => new IpcMessage(type, jobId) { ElapsedMs = Number(values, "elapsedMs", 0, long.MaxValue) },
        IpcType.Error => new IpcMessage(type, jobId) { Code = OneOf(values, "code", ErrorCodes) },
        _ => new IpcMessage(type, jobId),
    };

    private static string ConverterId(Dictionary<string, object> values)
    {
        var value = TakeString(values, "converterId");
        return ConverterIdFormat().IsMatch(value) ? value : throw new IpcProtocolException("converterId inválido.");
    }

    private static long Handle(Dictionary<string, object> values, string name) => Number(values, name, 1, uint.MaxValue);

    private static long Number(Dictionary<string, object> values, string name, long minimum, long maximum)
    {
        if (!values.TryGetValue(name, out var value) || value is not long number || number < minimum || number > maximum)
        {
            throw new IpcProtocolException($"{name} inválido.");
        }

        return number;
    }

    private static string OneOf(Dictionary<string, object> values, string name, string[] allowed)
    {
        var value = TakeString(values, name);
        return allowed.Contains(value) ? value : throw new IpcProtocolException($"{name} inválido.");
    }

    private static string TakeString(Dictionary<string, object> values, string name)
    {
        if (!values.Remove(name, out var value) || value is not string text)
        {
            throw new IpcProtocolException($"{name} inválido.");
        }

        return text;
    }

    private static IpcType ParseType(string name) => name switch
    {
        "warmup" => IpcType.Warmup,
        "convert" => IpcType.Convert,
        "cancel" => IpcType.Cancel,
        "progress" => IpcType.Progress,
        "done" => IpcType.Done,
        "error" => IpcType.Error,
        _ => throw new IpcProtocolException("Tipo desconhecido."),
    };

    private static string TypeName(IpcType type) => type.ToString().ToLowerInvariant();

    private static void WriteFields(Utf8JsonWriter writer, IpcMessage message)
    {
        switch (message.Type)
        {
            case IpcType.Warmup:
                writer.WriteString("category", message.Category);
                break;

            case IpcType.Convert:
                writer.WriteString("converterId", message.ConverterId);
                writer.WriteNumber("srcHandle", message.SrcHandle!.Value);
                writer.WriteNumber("dstHandle", message.DstHandle!.Value);
                writer.WriteStartObject("options");
                foreach (var option in message.Options!)
                {
                    writer.WriteString(option.Key, option.Value);
                }

                writer.WriteEndObject();
                break;

            case IpcType.Progress:
                writer.WriteNumber("percent", message.Percent!.Value);
                break;

            case IpcType.Done:
                writer.WriteNumber("elapsedMs", message.ElapsedMs!.Value);
                break;

            case IpcType.Error:
                writer.WriteString("code", message.Code);
                break;
        }
    }
}

/// <summary>Uma mensagem por linha, com tamanho máximo aplicado durante a leitura.</summary>
public static class IpcFraming
{
    private const byte NewLine = (byte)'\n';

    /// <summary>Lê a próxima mensagem. Devolve null quando o outro lado fechou o canal entre mensagens.</summary>
    public static byte[]? ReadFrame(Stream stream)
    {
        using var frame = new MemoryStream();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                return frame.Length == 0 ? null : throw new IpcProtocolException("Mensagem cortada.");
            }

            if (value == NewLine)
            {
                return frame.ToArray();
            }

            if (frame.Length == IpcCodec.MaxMessageBytes)
            {
                throw new IpcProtocolException("Mensagem acima do limite.");
            }

            frame.WriteByte((byte)value);
        }
    }

    public static void WriteFrame(Stream stream, byte[] payload)
    {
        stream.Write(payload);
        stream.WriteByte(NewLine);
        stream.Flush();
    }
}

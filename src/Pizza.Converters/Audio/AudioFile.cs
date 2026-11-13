namespace Pizza.Converters.Audio;

/// <summary>Duração (ms), taxa de amostragem, canais e bits por amostra (0 quando o formato comprimido não informa).</summary>
internal readonly record struct AudioInfo(long DurationMs, int SampleRate, int Channels, int Bits);

/// <summary>Formato de origem reconhecido pelo conteúdo (ADR-035, equivalente da SEC-45 para áudio).</summary>
internal enum AudioSource
{
    Unknown,
    Mp3,
    Wave,
    Flac,
    Mpeg4,
    Adts,
}

/// <summary>Leitura de áudio pelo Media Foundation, só a partir de um <see cref="Stream"/> já aberto.</summary>
internal static class AudioFile
{
    private const int HeaderBytes = 12;

    // Subtipos que o Pizza aceita de origem; qualquer outro (WMA, AC-3, vídeo…) é recusado.
    private static readonly Guid[] AcceptedSubtypes =
        [MediaFoundation.Pcm, MediaFoundation.Float, MediaFoundation.Mp3, MediaFoundation.Aac, MediaFoundation.Alac, MediaFoundation.Flac];

    /// <summary>Se este Windows tem o Media Foundation (as edições N vêm sem ele).</summary>
    public static bool Available { get; } = Probe();

    /// <summary>O formato real, pelas primeiras bytes; a extensão não conta.</summary>
    public static AudioSource Sniff(ReadOnlySpan<byte> head)
    {
        if (head.Length < HeaderBytes)
        {
            return AudioSource.Unknown;
        }

        if (head.StartsWith("ID3"u8) || (head[0] == 0xFF && (head[1] & 0xE6) == 0xE2))
        {
            return AudioSource.Mp3; // etiqueta ID3, ou sincronia de quadro MPEG camada III
        }

        if (head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8))
        {
            return AudioSource.Wave;
        }

        if (head.StartsWith("fLaC"u8))
        {
            return AudioSource.Flac;
        }

        if (head[4..8].SequenceEqual("ftyp"u8))
        {
            return AudioSource.Mpeg4;
        }

        return head[0] == 0xFF && (head[1] & 0xF6) == 0xF0 ? AudioSource.Adts : AudioSource.Unknown;
    }

    /// <summary>Lê só os dados do áudio, sem decodificar. Null quando não é um áudio aceito.</summary>
    public static AudioInfo? Inspect(Stream source)
    {
        if (!Available)
        {
            return null;
        }

        MediaFoundation.Startup();
        try
        {
            using var reader = AudioReader.Open(source);
            return reader?.Info;
        }
        catch (MediaFoundationException)
        {
            return null;
        }
        finally
        {
            MediaFoundation.Shutdown();
        }
    }

    internal static bool Accepts(Guid subtype) => AcceptedSubtypes.Contains(subtype);

    private static bool Probe()
    {
        try
        {
            MediaFoundation.Startup();
            MediaFoundation.Shutdown();
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MediaFoundationException)
        {
            return false;
        }
    }
}

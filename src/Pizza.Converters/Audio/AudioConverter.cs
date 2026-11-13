using System.Globalization;

namespace Pizza.Converters.Audio;

/// <summary>
/// Aba Áudio (ADR-035): decodifica a origem para PCM e grava no formato escolhido, tudo pelo Media Foundation do
/// Windows e só por handles. Canais, taxa e profundidade são convertidos pelo próprio leitor do MF.
/// </summary>
public sealed class AudioConverter(string id) : IConverter
{
    private const string Original = "original";
    private const int ProgressSteps = 100;

    // Os codificadores de MP3 e AAC do Windows só trabalham em 44,1 e 48 kHz e em até dois canais.
    private static readonly uint[] CompressedRates = [44100, 48000];

    public string Id => id;

    private string Format => id["audio.".Length..];

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (!AudioFile.Available)
        {
            return "MEDIA_UNAVAILABLE";
        }

        MediaFoundation.Startup();
        try
        {
            using var reader = AudioReader.Open(source);
            return reader is null ? "UNSUPPORTED" : Transcode(reader, destination, options, progress);
        }
        catch (MediaFoundationException)
        {
            return "CORRUPTED";
        }
        finally
        {
            MediaFoundation.Shutdown();
        }
    }

    private string? Transcode(AudioReader reader, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress)
    {
        var lossless = Format is "flac" or "wav" || options["bitrate"] == "lossless";
        var (rate, channels, bits) = Target(reader.Info, options, lossless);

        // O leitor entrega PCM já no formato da saída: ele mesmo reamostra e mistura os canais.
        var pcm = MediaFoundation.CreateMediaType();
        nint current = 0, writer = 0, outStream = 0, outBytes = 0;
        try
        {
            SetPcm(pcm, rate, channels, bits);
            MediaFoundation.SetCurrentMediaType(reader.Handle, MediaFoundation.FirstAudioStream, pcm);
            current = MediaFoundation.CurrentMediaType(reader.Handle, MediaFoundation.FirstAudioStream);

            outStream = HandleStream.Create(destination);
            outBytes = MediaFoundation.ByteStream(outStream);
            if (OpenWriter(outBytes, current, options, rate, channels, bits) is not { } opened)
            {
                return "MEDIA_UNAVAILABLE";
            }

            (writer, var stream) = opened;

            MediaFoundation.BeginWriting(writer);
            if (Copy(reader, writer, stream, progress) is { } error)
            {
                return error;
            }

            MediaFoundation.FinalizeWriting(writer);
            progress?.Report(ProgressSteps);
            return null;
        }
        finally
        {
            MediaFoundation.Release(writer);
            MediaFoundation.Release(outBytes);
            MediaFoundation.Release(outStream);
            MediaFoundation.Release(current);
            MediaFoundation.Release(pcm);
        }
    }

    /// <summary>Taxa, canais e bits da saída, a partir da origem e das opções (ADR-035 e adendo).</summary>
    private (uint Rate, uint Channels, uint Bits) Target(AudioInfo source, IReadOnlyDictionary<string, string> options, bool lossless)
    {
        var compressed = Format is "mp3" || (Format is "m4a" && !lossless);
        var rate = options["rate"] == Original ? (uint)source.SampleRate : uint.Parse(options["rate"], CultureInfo.InvariantCulture);
        if (compressed && !CompressedRates.Contains(rate))
        {
            rate = 48000;
        }

        var channels = options["channels"] switch
        {
            "mono" => 1u,
            "stereo" => 2u,
            _ => (uint)Math.Max(1, source.Channels),
        };
        if (compressed || Format is "m4a")
        {
            channels = Math.Min(channels, 2u);
        }

        var depth = options.GetValueOrDefault("depth", Original);
        var bits = !lossless || Format is "m4a" && source.Bits != 24 ? 16u
            : depth == Original ? (source.Bits is 16 or 8 ? 16u : 24u)
            : uint.Parse(depth, CultureInfo.InvariantCulture);
        return (rate, channels, bits);
    }

    private Guid Container() => Format switch
    {
        "mp3" => MediaFoundation.Mp3Container,
        "m4a" => MediaFoundation.Mpeg4Container,
        "flac" => MediaFoundation.FlacContainer,
        _ => MediaFoundation.WaveContainer,
    };

    /// <summary>
    /// Gravador com o fluxo de saída já ligado ao codificador. Se o codificador recusar a taxa pedida para esta
    /// combinação de canais (mono não chega a 320 kbps, por exemplo), tenta o valor aceito logo abaixo, com um gravador
    /// novo: nada é escrito antes de BeginWriting. Null quando o Windows não tem o codificador.
    /// </summary>
    private (nint Writer, uint Stream)? OpenWriter(nint byteStream, nint input, IReadOnlyDictionary<string, string> options, uint rate, uint channels, uint bits)
    {
        foreach (var kbps in Bitrates(options))
        {
            var output = MediaFoundation.CreateMediaType();
            var attributes = MediaFoundation.CreateAttributes();
            nint writer = 0;
            try
            {
                MediaFoundation.SetGuid(attributes, MediaFoundation.ContainerType, Container());
                writer = MediaFoundation.SinkWriter(byteStream, attributes);
                MediaFoundation.SetGuid(output, MediaFoundation.MajorType, MediaFoundation.Audio);
                MediaFoundation.SetUInt32(output, MediaFoundation.SampleRate, rate);
                MediaFoundation.SetUInt32(output, MediaFoundation.Channels, channels);
                switch (Format, kbps)
                {
                    case ("mp3", _):
                        MediaFoundation.SetGuid(output, MediaFoundation.SubType, MediaFoundation.Mp3);
                        MediaFoundation.SetUInt32(output, MediaFoundation.AverageBytes, kbps * 1000 / 8);
                        break;
                    case ("m4a", 0):
                        MediaFoundation.SetGuid(output, MediaFoundation.SubType, MediaFoundation.Alac);
                        MediaFoundation.SetUInt32(output, MediaFoundation.BitsPerSample, bits);
                        break;
                    case ("m4a", _):
                        MediaFoundation.SetGuid(output, MediaFoundation.SubType, MediaFoundation.Aac);
                        MediaFoundation.SetUInt32(output, MediaFoundation.BitsPerSample, 16);
                        MediaFoundation.SetUInt32(output, MediaFoundation.AverageBytes, kbps * 1000 / 8);
                        break;
                    case ("flac", _):
                        MediaFoundation.SetGuid(output, MediaFoundation.SubType, MediaFoundation.Flac);
                        MediaFoundation.SetUInt32(output, MediaFoundation.BitsPerSample, bits);
                        break;
                    default:
                        SetPcm(output, rate, channels, bits);
                        break;
                }

                var stream = MediaFoundation.AddStream(writer, output);
                var hr = MediaFoundation.SetInputMediaType(writer, stream, input);
                if (hr >= 0)
                {
                    (var opened, writer) = (writer, 0);
                    return (opened, stream);
                }

                if (hr != MediaFoundation.InvalidMediaType)
                {
                    MediaFoundation.Check(hr);
                }
            }
            catch (MediaFoundationException exception) when (exception.Code == MediaFoundation.InvalidMediaType)
            {
                // Combinação recusada pelo codificador: tenta o próximo valor da lista.
            }
            finally
            {
                MediaFoundation.Release(writer);
                MediaFoundation.Release(attributes);
                MediaFoundation.Release(output);
            }
        }

        return null;
    }

    /// <summary>A taxa pedida e as menores, da maior para a menor; 0 é o "Sem perda" do M4A e o formato sem taxa.</summary>
    private IEnumerable<uint> Bitrates(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("bitrate", out var chosen) || chosen == "lossless")
        {
            return [0u];
        }

        var asked = uint.Parse(chosen, CultureInfo.InvariantCulture);
        var all = Core.Conversions.ConverterCatalog.Find(Id)!.Options.First(option => option.Key == "bitrate").Choices
            .Select(choice => choice.Value).Where(value => value.All(char.IsAsciiDigit)).Select(value => uint.Parse(value, CultureInfo.InvariantCulture));
        return all.Where(value => value <= asked).OrderDescending();
    }

    /// <summary>Lê e grava amostra por amostra; a porcentagem vem da posição sobre a duração (ADR-029).</summary>
    private static string? Copy(AudioReader reader, nint writer, uint stream, IProgress<int>? progress)
    {
        var duration = reader.Info.DurationMs * 10_000;
        while (true)
        {
            var hr = MediaFoundation.ReadSample(reader.Handle, out var flags, out var time, out var sample);
            if (hr < 0 || (flags & MediaFoundation.ReadError) != 0)
            {
                MediaFoundation.Release(sample);
                return "CORRUPTED";
            }

            if (sample != 0)
            {
                try
                {
                    MediaFoundation.WriteSample(writer, stream, sample);
                }
                finally
                {
                    MediaFoundation.Release(sample);
                }

                if (duration > 0)
                {
                    progress?.Report((int)Math.Clamp(time * (ProgressSteps - 1) / duration, 0, ProgressSteps - 1));
                }
            }

            if ((flags & MediaFoundation.EndOfStream) != 0)
            {
                return null;
            }
        }
    }

    private static void SetPcm(nint type, uint rate, uint channels, uint bits)
    {
        MediaFoundation.SetGuid(type, MediaFoundation.MajorType, MediaFoundation.Audio);
        MediaFoundation.SetGuid(type, MediaFoundation.SubType, MediaFoundation.Pcm);
        MediaFoundation.SetUInt32(type, MediaFoundation.SampleRate, rate);
        MediaFoundation.SetUInt32(type, MediaFoundation.Channels, channels);
        MediaFoundation.SetUInt32(type, MediaFoundation.BitsPerSample, bits);
        MediaFoundation.SetUInt32(type, MediaFoundation.BlockAlignment, channels * bits / 8);
        MediaFoundation.SetUInt32(type, MediaFoundation.AverageBytes, rate * channels * bits / 8);
    }
}

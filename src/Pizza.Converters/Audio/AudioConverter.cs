using System.Globalization;

namespace Pizza.Converters.Audio;

/// <summary>
/// Aba Áudio (ADR-035): decodifica a origem para PCM e grava no formato escolhido, tudo pelo Media Foundation do
/// Windows e só por handles. Canais, taxa e profundidade são convertidos pelo próprio leitor do MF.
/// </summary>
public sealed class AudioConverter(string id) : IConverter
{
    private const int ProgressSteps = 100;

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

    // Amostra do probe: os primeiros 5 s (em unidades de 100 ns) viram FLAC na memória do Worker.
    private const long SampleLength = 5 * 10_000_000L;

    private static readonly Dictionary<string, string> OriginalLossless = new() { ["depth"] = "original", ["channels"] = "original", ["rate"] = "original" };

    /// <summary>
    /// Adendo do ADR-035: duração e formato da origem, para o tamanho previsto, e quanto uma amostra do próprio arquivo
    /// comprime sem perda (o FLAC e o ALAC variam muito com o conteúdo). Null se não for um áudio aceito.
    /// </summary>
    public static Core.Ipc.MediaInfo? Probe(Stream source)
    {
        if (AudioFile.Inspect(source) is not { } info)
        {
            return null;
        }

        source.Position = 0;
        return new Core.Ipc.MediaInfo(info.DurationMs, info.SampleRate, info.Channels, info.Bits, LosslessPercent(source));
    }

    /// <summary>Tamanho do FLAC da amostra sobre o PCM dela, de 1 a 100; 0 se não deu para medir (o painel usa uma média).</summary>
    private static int LosslessPercent(Stream source)
    {
        MediaFoundation.Startup();
        nint pcm = 0, current = 0, outStream = 0, outBytes = 0, writer = 0;
        try
        {
            using var reader = AudioReader.Open(source);
            if (reader is null)
            {
                return 0;
            }

            var flac = new AudioConverter("audio.flac");
            var target = Core.Conversions.AudioPlan.Target("flac", OriginalLossless, reader.Info.SampleRate, reader.Info.Channels, reader.Info.Bits);
            pcm = MediaFoundation.CreateMediaType();
            SetPcm(pcm, (uint)target.Rate, (uint)target.Channels, (uint)target.Bits);
            MediaFoundation.SetCurrentMediaType(reader.Handle, MediaFoundation.FirstAudioStream, pcm);
            current = MediaFoundation.CurrentMediaType(reader.Handle, MediaFoundation.FirstAudioStream);

            using var memory = new MemoryStream();
            outStream = HandleStream.Create(memory);
            outBytes = MediaFoundation.ByteStream(outStream);
            if (flac.OpenWriter(outBytes, current, OriginalLossless, (uint)target.Rate, (uint)target.Channels, (uint)target.Bits) is not { } opened)
            {
                return 0;
            }

            (writer, var stream) = opened;
            MediaFoundation.BeginWriting(writer);
            long raw = 0;
            if (Pass(reader, null, 0, 0, sample =>
            {
                raw += MediaFoundation.TotalLength(sample);
                MediaFoundation.WriteSample(writer, stream, sample);
            }, SampleLength) is not null || raw == 0)
            {
                return 0;
            }

            MediaFoundation.FinalizeWriting(writer);
            return (int)Math.Clamp(Math.Round(memory.Length * 100.0 / raw), 1, 100);
        }
        catch (MediaFoundationException)
        {
            return 0;
        }
        finally
        {
            MediaFoundation.Release(writer);
            MediaFoundation.Release(outBytes);
            MediaFoundation.Release(outStream);
            MediaFoundation.Release(current);
            MediaFoundation.Release(pcm);
            MediaFoundation.Shutdown();
        }
    }

    private string? Transcode(AudioReader reader, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress)
    {
        // A mesma regra que o painel usa para o tamanho previsto (adendo do ADR-035).
        var target = Core.Conversions.AudioPlan.Target(Format, options, reader.Info.SampleRate, reader.Info.Channels, reader.Info.Bits);
        var (rate, channels, bits) = ((uint)target.Rate, (uint)target.Channels, (uint)target.Bits);

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
            var format = new Pcm((int)rate, (int)channels, (int)bits);
            var volume = new VolumeChain(options, format.Rate);
            var half = ProgressSteps / 2;
            if (volume.Target is { } loudness)
            {
                // Normalizar (adendo do ADR-035): mede o volume já amplificado e comprimido, volta ao começo e aplica.
                var meter = new Loudness(format.Channels, format.Rate);
                var measuring = volume;
                if (Pass(reader, progress, 0, half, sample => Edit(sample, format, samples =>
                {
                    measuring.Before(samples, format.Channels);
                    meter.Add(samples);
                }, write: false)) is { } measureError)
                {
                    return measureError;
                }

                MediaFoundation.Rewind(reader.Handle);
                var measured = meter.Result;
                volume = new VolumeChain(options, format.Rate) { Normalize = double.IsFinite(measured) ? Math.Pow(10, (loudness - measured) / 20) : 1 };
            }

            var applying = volume;
            if (Pass(reader, progress, volume.Target is null ? 0 : half, ProgressSteps - 1, sample =>
            {
                if (applying.Active)
                {
                    Edit(sample, format, samples => applying.Apply(samples, format.Channels), write: true);
                }

                MediaFoundation.WriteSample(writer, stream, sample);
            }) is { } error)
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

    /// <summary>
    /// Uma passada pelo arquivo, amostra por amostra; a porcentagem vai de <paramref name="from"/> a
    /// <paramref name="to"/> pela posição sobre a duração (ADR-029).
    /// </summary>
    private static string? Pass(AudioReader reader, IProgress<int>? progress, int from, int to, Action<nint> handle, long stopAt = long.MaxValue)
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
                    handle(sample);
                }
                finally
                {
                    MediaFoundation.Release(sample);
                }

                if (duration > 0)
                {
                    progress?.Report(from + (int)Math.Clamp(time * (to - from) / duration, 0, to - from));
                }
            }

            if ((flags & MediaFoundation.EndOfStream) != 0 || time >= stopAt)
            {
                return null;
            }
        }
    }

    private readonly record struct Pcm(int Rate, int Channels, int Bits);

    private delegate void SampleWork(Span<float> samples);

    /// <summary>Converte o PCM inteiro do buffer da amostra para -1 a 1, aplica o trabalho e, se pedido, grava de volta.</summary>
    private static void Edit(nint sample, Pcm format, SampleWork work, bool write)
    {
        var buffer = MediaFoundation.ContiguousBuffer(sample);
        try
        {
            var bytes = MediaFoundation.Lock(buffer);
            try
            {
                var size = format.Bits / 8;
                var count = bytes.Length / size;
                var samples = System.Buffers.ArrayPool<float>.Shared.Rent(count);
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        samples[i] = size == 2
                            ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes[(2 * i)..]) / 32768f
                            : ((bytes[(3 * i) + 2] << 24) | (bytes[(3 * i) + 1] << 16) | (bytes[3 * i] << 8)) / 2147483648f;
                    }

                    work(samples.AsSpan(0, count));
                    if (write)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            var value = Math.Clamp(samples[i], -1f, 1f);
                            if (size == 2)
                            {
                                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes[(2 * i)..], (short)Math.Clamp(MathF.Round(value * 32768f), short.MinValue, short.MaxValue));
                            }
                            else
                            {
                                var integer = (int)Math.Clamp(MathF.Round(value * 8388608f), -8388608, 8388607);
                                bytes[3 * i] = (byte)integer;
                                bytes[(3 * i) + 1] = (byte)(integer >> 8);
                                bytes[(3 * i) + 2] = (byte)(integer >> 16);
                            }
                        }
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<float>.Shared.Return(samples);
                }
            }
            finally
            {
                MediaFoundation.Unlock(buffer);
            }
        }
        finally
        {
            MediaFoundation.Release(buffer);
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

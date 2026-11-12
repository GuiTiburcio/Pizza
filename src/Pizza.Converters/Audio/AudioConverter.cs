using System.Globalization;
using System.Runtime.InteropServices;
using static Pizza.Converters.Audio.MediaFoundation;

namespace Pizza.Converters.Audio;

/// <summary>Áudio do Windows, em fluxo, só dentro do Worker (ADR-035). Sem URL, caminho ou codec empacotado.</summary>
public sealed class AudioConverter(string id) : IConverter
{
    private const int InvalidStream = unchecked((int)0xC00D36B3);
    private const long MaximumInput = 2L * 1024 * 1024 * 1024;
    public string Id => id;

    public string? Convert(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress = null)
    {
        if (!OperatingSystem.IsWindows()) return "MEDIA_UNAVAILABLE";
        if (id is not ("audio.mp3" or "audio.m4a" or "audio.flac" or "audio.wav")) return "UNSUPPORTED";
        if (!source.CanSeek || !source.CanRead || !destination.CanSeek || !destination.CanWrite) return "UNSUPPORTED";
        try
        {
            if (source.Length > MaximumInput) return "LIMIT_EXCEEDED";
            if (!HasAudioSignature(source)) return "UNSUPPORTED";
            return Transcode(source, destination, options, progress);
        }
        catch (DllNotFoundException) { return "MEDIA_UNAVAILABLE"; }
        catch (EntryPointNotFoundException) { return "MEDIA_UNAVAILABLE"; }
        catch (COMException error)
        {
            return error.HResult switch
            {
                unchecked((int)0xC00D36C4) or unchecked((int)0xC00D36B4) or InvalidStream => "UNSUPPORTED",
                unchecked((int)0xC00D5212) => "MEDIA_UNAVAILABLE",
                _ => "CORRUPTED",
            };
        }
        catch (IOException) { return "CORRUPTED"; }
        catch (ArgumentException) { return "UNSUPPORTED"; }
        catch (OverflowException) { return "LIMIT_EXCEEDED"; }
    }

    private string? Transcode(Stream source, Stream destination, IReadOnlyDictionary<string, string> options, IProgress<int>? progress)
    {
        var initialized = CoInitializeEx(0, 0);
        // RPC_E_CHANGED_MODE: COM já está inicializado com outro modelo nesta thread.
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) Check(initialized);
        var started = false;
        nint inputBytes = 0, outputBytes = 0, reader = 0, writer = 0, attributes = 0, native = 0, pcm = 0, encoded = 0;
        using var input = new ComStream(source);
        using var output = new ComStream(destination);
        try
        {
            if (MFStartup(0x00020070, 1) < 0) return "MEDIA_UNAVAILABLE"; // MFSTARTUP_NOSOCKET.
            started = true;
            source.Position = 0;
            Check(MFCreateMFByteStreamOnStream(input.Pointer, out inputBytes));
            Check(MFCreateSourceReaderFromByteStream(inputBytes, 0, out reader));
            var stream = FindAudio(reader, out native);
            if (stream == uint.MaxValue) return "UNSUPPORTED";
            var nativeRate = Get(native, Rate);
            var nativeChannels = Get(native, Channels);
            var nativeBits = Get(native, Bits, 16);
            if (nativeRate is < 8000 or > 384000 || nativeChannels is < 1 or > 8) return "UNSUPPORTED";

            var compressed = id == "audio.mp3" || (id == "audio.m4a" && options.GetValueOrDefault("bitrate") != "lossless");
            var rate = options.GetValueOrDefault("sampleRate", "original") switch
            {
                "original" => compressed && nativeRate is not (44100 or 48000) ? 48000u : nativeRate,
                "44100" => 44100u,
                "48000" => 48000u,
                _ => 0u,
            };
            var channels = options.GetValueOrDefault("channels", "original") switch
            {
                "original" => nativeChannels,
                "mono" => 1u,
                "stereo" => 2u,
                _ => 0u,
            };
            var depth = options.GetValueOrDefault("depth", "original") switch
            {
                "original" => nativeBits <= 16 ? 16u : 24u,
                "16" => 16u,
                "24" => 24u,
                _ => 0u,
            };
            if (rate == 0 || channels == 0 || depth == 0 || (compressed && channels > 2)) return "UNSUPPORTED";
            // Os codificadores MP3/AAC aceitam PCM de 16 bits. FLAC/ALAC/WAV preservam a profundidade escolhida.
            if (compressed) depth = 16;
            SelectStream(reader, 0xFFFFFFFE, false);
            SelectStream(reader, stream, true);
            pcm = Type(AudioSubtype(1), rate, channels, depth, rate * channels * (depth / 8));
            ReaderType(reader, stream, pcm);
            // Confere o formato efetivamente negociado antes de entregar amostras ao escritor.
            var actual = CurrentType(reader, stream);
            try
            {
                if (GetGuid(actual, Subtype) != AudioSubtype(1) || Get(actual, Rate) != rate ||
                    Get(actual, Channels) != channels || Get(actual, Bits) != depth) return "UNSUPPORTED";
            }
            finally { Release(actual); }

            var outputSubtype = id switch
            {
                "audio.mp3" => AudioSubtype(0x55),
                "audio.m4a" => AudioSubtype(options.GetValueOrDefault("bitrate") == "lossless" ? 0x6C61u : 0x1610u),
                "audio.flac" => AudioSubtype(0xF1AC),
                _ => AudioSubtype(1),
            };
            uint average;
            if (compressed)
            {
                var fallback = id == "audio.mp3" ? "192" : "160";
                if (!uint.TryParse(options.GetValueOrDefault("bitrate", fallback), NumberStyles.None, CultureInfo.InvariantCulture, out var kbps)) return "UNSUPPORTED";
                uint[] supported = id == "audio.mp3" ? [96, 112, 128, 160, 192, 224, 256, 320] : [64, 96, 128, 160, 192, 256, 320];
                if (!supported.Contains(kbps)) return "UNSUPPORTED";
                average = kbps * 1000 / 8;
            }
            else average = rate * channels * (depth / 8);
            encoded = Type(outputSubtype, rate, channels, depth, average, compressed);
            Check(MFCreateAttributes(out attributes, 1));
            Set(attributes, Container, id switch { "audio.mp3" => Mp3, "audio.m4a" => Mp4, "audio.flac" => Flac, _ => Wave });
            Check(MFCreateMFByteStreamOnStream(output.Pointer, out outputBytes));
            Check(MFCreateSinkWriterFromURL(0, outputBytes, attributes, out writer));
            var target = AddStream(writer, encoded);
            InputType(writer, target, pcm);
            Begin(writer);

            var duration = GetDuration(reader);
            long? first = null;
            var empty = 0;
            progress?.Report(0);
            while (true)
            {
                var sample = Read(reader, stream, out var flags, out var timestamp);
                try
                {
                    // Não aceitar mudança de formato sem renegociar o escritor.
                    if ((flags & (1u | 16u | 32u)) != 0) return "CORRUPTED";
                    if (sample != 0)
                    {
                        empty = 0;
                        first ??= timestamp;
                        if (timestamp < first.Value) return "CORRUPTED";
                        SampleTime(sample, checked(timestamp - first.Value));
                        Write(writer, target, sample);
                        if (duration > 0) progress?.Report((int)Math.Clamp(timestamp / (double)duration * 100, 0, 99));
                    }
                    else if (++empty > 1024) return "CORRUPTED";
                    if ((flags & 2) != 0) break;
                }
                finally { Release(sample); }
            }
            Finish(writer);
            progress?.Report(100);
            return null;
        }
        finally
        {
            Release(writer);
            Release(reader);
            Release(encoded);
            Release(pcm);
            Release(native);
            Release(attributes);
            Release(outputBytes);
            Release(inputBytes);
            if (started) MFShutdown();
            if (initialized >= 0) CoUninitialize();
        }
    }

    private static nint Type(Guid subtype, uint rate, uint channels, uint bits, uint average, bool compressed = false)
    {
        Check(MFCreateMediaType(out var type));
        try
        {
            Set(type, MajorType, AudioMajor);
            Set(type, Subtype, subtype);
            Set(type, Rate, rate);
            Set(type, Channels, channels);
            Set(type, Bits, bits);
            Set(type, Alignment, compressed ? 1u : channels * (bits / 8));
            Set(type, AverageBytes, average);
            Set(type, Independent, 1);
            return type;
        }
        catch { Release(type); throw; }
    }

    private static uint FindAudio(nint reader, out nint native)
    {
        native = 0;
        var selected = uint.MaxValue;
        try
        {
            // Não carregar um vídeo por acidente e não confiar apenas no cabeçalho/extensão.
            for (uint index = 0; index < 32; index++)
            {
                var hr = NativeType(reader, index, out var type);
                if (hr == InvalidStream) return selected;
                Check(hr);
                try
                {
                    if (GetGuid(type, MajorType) != AudioMajor) { Release(native); native = 0; return uint.MaxValue; }
                    var subtype = GetGuid(type, Subtype);
                    if (subtype != AudioSubtype(1) && subtype != AudioSubtype(3) && subtype != AudioSubtype(0x55) &&
                        subtype != AudioSubtype(0x1610) && subtype != AudioSubtype(0x1600) && subtype != AudioSubtype(0xFF) &&
                        subtype != AudioSubtype(0x6C61) && subtype != AudioSubtype(0xF1AC))
                    { Release(native); native = 0; return uint.MaxValue; }
                    if (selected == uint.MaxValue)
                    {
                        selected = index;
                        native = type;
                        type = 0;
                    }
                }
                finally { Release(type); }
            }
            Release(native);
            native = 0;
            return uint.MaxValue; // Limite explícito para contêineres hostis.
        }
        catch { Release(native); native = 0; throw; }
    }

    private static bool HasAudioSignature(Stream source)
    {
        source.Position = 0;
        Span<byte> bytes = stackalloc byte[32];
        var count = source.ReadAtLeast(bytes, 12, throwOnEndOfStream: false);
        source.Position = 0;
        if (count < 4) return false;
        return bytes[..3].SequenceEqual("ID3"u8) || bytes[..4].SequenceEqual("fLaC"u8) ||
            (count >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WAVE"u8)) ||
            (count >= 12 && bytes[4..8].SequenceEqual("ftyp"u8)) ||
            (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0);
    }
}

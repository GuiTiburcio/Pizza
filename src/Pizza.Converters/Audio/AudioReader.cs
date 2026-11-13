namespace Pizza.Converters.Audio;

/// <summary>
/// O leitor do Media Foundation sobre o handle da origem, já conferido: formato reconhecido pelo conteúdo, só áudio
/// (nenhum fluxo de vídeo) e codec da lista aceita (ADR-035). O MF precisa estar iniciado.
/// </summary>
internal sealed class AudioReader : IDisposable
{
    private readonly nint _stream;
    private readonly nint _byteStream;

    private AudioReader(nint stream, nint byteStream, nint reader, AudioInfo info)
    {
        _stream = stream;
        _byteStream = byteStream;
        Handle = reader;
        Info = info;
    }

    public nint Handle { get; }

    public AudioInfo Info { get; }

    /// <summary>Null quando o conteúdo não é um áudio aceito; o chamador responde UNSUPPORTED.</summary>
    public static AudioReader? Open(Stream source)
    {
        Span<byte> head = stackalloc byte[12];
        var start = source.Position;
        var read = source.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        source.Position = start;
        if (AudioFile.Sniff(head[..read]) == AudioSource.Unknown)
        {
            return null;
        }

        var stream = HandleStream.Create(source);
        nint byteStream = 0, reader = 0;
        try
        {
            byteStream = MediaFoundation.ByteStream(stream);
            reader = MediaFoundation.SourceReader(byteStream);
            if (Describe(reader) is not { } info)
            {
                Free(stream, byteStream, reader);
                return null;
            }

            MediaFoundation.SelectStream(reader, MediaFoundation.AllStreams, false);
            MediaFoundation.SelectStream(reader, MediaFoundation.FirstAudioStream, true);
            return new AudioReader(stream, byteStream, reader, info);
        }
        catch (MediaFoundationException exception) when (exception.Code is MediaFoundation.UnsupportedByteStream or MediaFoundation.InvalidMediaType)
        {
            Free(stream, byteStream, reader);
            return null;
        }
        catch
        {
            Free(stream, byteStream, reader);
            throw;
        }
    }

    public void Dispose() => Free(_stream, _byteStream, Handle);

    /// <summary>Confere todos os fluxos: nenhum pode ser vídeo, e o primeiro áudio precisa ser de um codec aceito.</summary>
    private static AudioInfo? Describe(nint reader)
    {
        AudioInfo? info = null;
        for (uint index = 0; ; index++)
        {
            var hr = MediaFoundation.NativeMediaType(reader, index, 0, out var type);
            if (hr == MediaFoundation.InvalidStreamNumber)
            {
                return info;
            }

            MediaFoundation.Check(hr);
            try
            {
                if (MediaFoundation.GetGuid(type, MediaFoundation.MajorType) != MediaFoundation.Audio)
                {
                    return null;
                }

                if (info is null)
                {
                    if (!AudioFile.Accepts(MediaFoundation.GetGuid(type, MediaFoundation.SubType)))
                    {
                        return null;
                    }

                    var duration = MediaFoundation.DurationOf(reader);
                    info = new AudioInfo(duration / 10_000, (int)UInt(type, MediaFoundation.SampleRate), (int)UInt(type, MediaFoundation.Channels), (int)UInt(type, MediaFoundation.BitsPerSample));
                }
            }
            finally
            {
                MediaFoundation.Release(type);
            }
        }
    }

    private static uint UInt(nint type, Guid key) => MediaFoundation.GetUInt32(type, key, out var value) < 0 ? 0 : value;

    private static void Free(nint stream, nint byteStream, nint reader)
    {
        MediaFoundation.Release(reader);
        MediaFoundation.Release(byteStream);
        MediaFoundation.Release(stream);
    }
}

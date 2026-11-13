using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>
/// O pouco do Media Foundation que a aba Áudio usa (ADR-035), chamado pela tabela de funções de cada objeto COM.
/// Nada de caminho de arquivo chega aqui: o MF lê e grava por um <see cref="HandleStream"/> sobre o handle (SEC-15).
/// </summary>
internal static unsafe partial class MediaFoundation
{
    public const uint FirstAudioStream = 0xFFFFFFFD;
    public const uint AllStreams = 0xFFFFFFFE;
    public const uint MediaSource = 0xFFFFFFFF;
    public const uint EndOfStream = 0x2;
    public const uint ReadError = 0x1;

    public const int UnsupportedByteStream = unchecked((int)0xC00D36C4); // MF_E_UNSUPPORTED_BYTESTREAM_TYPE
    public const int InvalidMediaType = unchecked((int)0xC00D36B4); // MF_E_INVALIDMEDIATYPE
    public const int InvalidStreamNumber = unchecked((int)0xC00D36B3); // MF_E_INVALIDSTREAMNUMBER
    public const int NoMoreTypes = unchecked((int)0xC00D36B9); // MF_E_NO_MORE_TYPES

    private const uint Version = 0x00020070; // MF_VERSION
    private const uint NoSocket = 0x1; // MFSTARTUP_NOSOCKET: sem a parte de rede (SEC-20)

    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid SubType = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid Channels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid SampleRate = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid AverageBytes = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    public static readonly Guid BitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    public static readonly Guid BlockAlignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    public static readonly Guid Duration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a"); // MF_PD_DURATION
    public static readonly Guid ContainerType = new("150ff23f-4abc-478b-ac4f-e1916fba1cca");

    public static readonly Guid Audio = new("73647561-0000-0010-8000-00aa00389b71");
    public static readonly Guid Pcm = Wave(0x0001);
    public static readonly Guid Float = Wave(0x0003);
    public static readonly Guid Mp3 = Wave(0x0055);
    public static readonly Guid Aac = Wave(0x1610);
    public static readonly Guid Alac = Wave(0x6C61);
    public static readonly Guid Flac = Wave(0xF1AC);

    public static readonly Guid Mpeg4Container = new("dc6cd05d-b9d0-40ef-bd35-fa622c1ab28a");
    public static readonly Guid Mp3Container = new("e438b912-83f1-4de6-9e3a-9ffbc6dd24d1");
    public static readonly Guid FlacContainer = new("31344aa3-05a9-42b5-901b-8e9d4257f75e");
    public static readonly Guid WaveContainer = new("64c3453c-0f26-4741-be63-87bdf8bb935b");

    private static Guid Wave(ushort tag) => new(tag, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

    public static void Startup() => Check(MFStartup(Version, NoSocket));

    public static void Shutdown() => MFShutdown();

    public static nint CreateMediaType()
    {
        Check(MFCreateMediaType(out var type));
        return type;
    }

    public static nint CreateAttributes()
    {
        Check(MFCreateAttributes(out var attributes, 1));
        return attributes;
    }

    public static nint ByteStream(nint stream)
    {
        Check(MFCreateMFByteStreamOnStream(stream, out var byteStream));
        return byteStream;
    }

    public static nint SourceReader(nint byteStream)
    {
        Check(MFCreateSourceReaderFromByteStream(byteStream, 0, out var reader));
        return reader;
    }

    public static nint SinkWriter(nint byteStream, nint attributes)
    {
        Check(MFCreateSinkWriterFromURL(null, byteStream, attributes, out var writer));
        return writer;
    }

    // IUnknown.
    public static void Release(nint value)
    {
        if (value != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(value, 2))(value);
        }
    }

    // IMFAttributes.
    public static int GetUInt32(nint attributes, Guid key, out uint value)
    {
        uint result;
        var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, uint*, int>)Slot(attributes, 7))(attributes, &key, &result);
        value = result;
        return hr;
    }

    public static Guid GetGuid(nint attributes, Guid key)
    {
        Guid value;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(attributes, 10))(attributes, &key, &value));
        return value;
    }

    public static void SetUInt32(nint attributes, Guid key, uint value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Slot(attributes, 21))(attributes, &key, value));

    public static void SetGuid(nint attributes, Guid key, Guid value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(attributes, 24))(attributes, &key, &value));

    // IMFSourceReader.
    public static void SelectStream(nint reader, uint stream, bool selected) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)Slot(reader, 4))(reader, stream, selected ? 1 : 0));

    public static int NativeMediaType(nint reader, uint stream, uint index, out nint type)
    {
        nint result;
        var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)Slot(reader, 5))(reader, stream, index, &result);
        type = hr < 0 ? 0 : result;
        return hr;
    }

    public static nint CurrentMediaType(nint reader, uint stream)
    {
        nint type;
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(reader, 6))(reader, stream, &type));
        return type;
    }

    public static void SetCurrentMediaType(nint reader, uint stream, nint type) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, uint*, nint, int>)Slot(reader, 7))(reader, stream, null, type));

    public static int ReadSample(nint reader, out uint flags, out long time, out nint sample)
    {
        uint actual, readFlags;
        long stamp;
        nint result;
        var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, uint*, uint*, long*, nint*, int>)Slot(reader, 9))(reader, FirstAudioStream, 0, &actual, &readFlags, &stamp, &result);
        flags = readFlags;
        time = stamp;
        sample = hr < 0 ? 0 : result;
        return hr;
    }

    /// <summary>Volta ao começo, para a segunda passada do Normalizar.</summary>
    public static void Rewind(nint reader)
    {
        var variant = stackalloc byte[24];
        new Span<byte>(variant, 24).Clear();
        *(ushort*)variant = 20; // VT_I8, posição 0
        var format = Guid.Empty;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, byte*, int>)Slot(reader, 8))(reader, &format, variant));
    }

    // IMFSample e IMFMediaBuffer: as amostras PCM, para o Volume trabalhar no próprio buffer.
    public static nint ContiguousBuffer(nint sample)
    {
        nint buffer;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(sample, 41))(sample, &buffer));
        return buffer;
    }

    public static Span<byte> Lock(nint buffer)
    {
        byte* data;
        uint maximum, length;
        Check(((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, &maximum, &length));
        return new Span<byte>(data, (int)length);
    }

    public static void Unlock(nint buffer) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(buffer, 4))(buffer));

    /// <summary>Duração em unidades de 100 ns, ou 0 quando o arquivo não informa.</summary>
    public static long DurationOf(nint reader)
    {
        // PROPVARIANT: tipo nos 2 primeiros bytes; o valor (VT_UI8) a partir do byte 8.
        var variant = stackalloc byte[24];
        new Span<byte>(variant, 24).Clear();
        var key = Duration;
        var hr = ((delegate* unmanaged[Stdcall]<nint, uint, Guid*, byte*, int>)Slot(reader, 12))(reader, MediaSource, &key, variant);
        return hr < 0 || *(ushort*)variant != 21 /* VT_UI8 */ ? 0 : *(long*)(variant + 8);
    }

    // IMFSinkWriter.
    public static uint AddStream(nint writer, nint type)
    {
        uint index;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Slot(writer, 3))(writer, type, &index));
        return index;
    }

    public static int SetInputMediaType(nint writer, uint stream, nint type) =>
        ((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Slot(writer, 4))(writer, stream, type, 0);

    public static void BeginWriting(nint writer) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 5))(writer));

    public static void WriteSample(nint writer, uint stream, nint sample) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(writer, 6))(writer, stream, sample));

    public static void FinalizeWriting(nint writer) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 11))(writer));

    public static void Check(int hr)
    {
        if (hr < 0)
        {
            throw new MediaFoundationException(hr);
        }
    }

    private static nint Slot(nint value, int index) => (*(nint**)value)[index];

    [LibraryImport("mfplat.dll")]
    private static partial int MFStartup(uint version, uint flags);

    [LibraryImport("mfplat.dll")]
    private static partial int MFShutdown();

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMediaType(out nint type);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateAttributes(out nint attributes, uint size);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMFByteStreamOnStream(nint stream, out nint byteStream);

    [LibraryImport("mfreadwrite.dll")]
    private static partial int MFCreateSourceReaderFromByteStream(nint byteStream, nint attributes, out nint reader);

    [LibraryImport("mfreadwrite.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MFCreateSinkWriterFromURL(string? url, nint byteStream, nint attributes, out nint writer);
}

/// <summary>Falha de uma chamada do Media Foundation. Leva só o código: nunca nome de arquivo nem conteúdo (PRIV-16).</summary>
internal sealed class MediaFoundationException(int hr) : Exception($"0x{hr:X8}")
{
    public int Code { get; } = hr;
}

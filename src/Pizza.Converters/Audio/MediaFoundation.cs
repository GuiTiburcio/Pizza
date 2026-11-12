using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>API/slots dos headers Windows SDK mfapi.h, mfobjects.h e mfreadwrite.h, sem COM dinâmico.</summary>
internal static unsafe partial class MediaFoundation
{
    internal static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    internal static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    internal static readonly Guid Channels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    internal static readonly Guid Rate = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    internal static readonly Guid Bits = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    internal static readonly Guid Alignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    internal static readonly Guid AverageBytes = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    internal static readonly Guid Independent = new("c9173739-5e56-461c-b713-46fb995cb95f");
    internal static readonly Guid AudioMajor = new("73647561-0000-0010-8000-00aa00389b71");
    internal static readonly Guid Container = new("150ff23f-4abc-478b-ac4f-e1916fba1cca");
    internal static readonly Guid Mp4 = new("dc6cd05d-b9d0-40ef-bd35-fa622c1ab28a");
    internal static readonly Guid Mp3 = new("e438b912-83f1-4de6-9e3a-9ffbc6dd24d1");
    internal static readonly Guid Flac = new("31344aa3-05a9-42b5-901b-8e9d4257f75e");
    internal static readonly Guid Wave = new("64c3453c-0f26-4741-be63-87bdf8bb935b");
    internal static readonly Guid Duration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a");
    internal static Guid AudioSubtype(uint format) => new(format, 0, 0x10, 0x80, 0, 0, 0xaa, 0, 0x38, 0x9b, 0x71);

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct Variant
    {
        [FieldOffset(0)] internal ushort Type;
        [FieldOffset(8)] internal long Value;
    }

    [LibraryImport("ole32.dll")] internal static partial int CoInitializeEx(nint reserved, uint flags);
    [LibraryImport("ole32.dll")] internal static partial void CoUninitialize();
    [LibraryImport("ole32.dll")] internal static partial int PropVariantClear(Variant* value);
    [LibraryImport("mfplat.dll")] internal static partial int MFStartup(uint version, uint flags);
    [LibraryImport("mfplat.dll")] internal static partial int MFShutdown();
    [LibraryImport("mfplat.dll")] internal static partial int MFCreateMediaType(out nint type);
    [LibraryImport("mfplat.dll")] internal static partial int MFCreateAttributes(out nint attributes, uint size);
    [LibraryImport("mfplat.dll")] internal static partial int MFCreateMFByteStreamOnStream(nint stream, out nint byteStream);
    [LibraryImport("mfreadwrite.dll")] internal static partial int MFCreateSourceReaderFromByteStream(nint stream, nint attributes, out nint reader);
    [LibraryImport("mfreadwrite.dll")] internal static partial int MFCreateSinkWriterFromURL(nint url, nint stream, nint attributes, out nint writer);

    internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    internal static nint* Table(nint pointer) => *(nint**)pointer;
    internal static void Release(nint pointer)
    {
        if (pointer != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Table(pointer)[2])(pointer);
    }

    internal static void Set(nint attributes, Guid key, uint value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Table(attributes)[21])(attributes, &key, value));
    internal static void Set(nint attributes, Guid key, Guid value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Table(attributes)[24])(attributes, &key, &value));
    internal static uint Get(nint attributes, Guid key, uint fallback = 0)
    {
        uint value;
        return ((delegate* unmanaged[Stdcall]<nint, Guid*, uint*, int>)Table(attributes)[7])(attributes, &key, &value) >= 0 ? value : fallback;
    }
    internal static Guid GetGuid(nint attributes, Guid key)
    {
        Guid value;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Table(attributes)[10])(attributes, &key, &value));
        return value;
    }

    internal static int NativeType(nint reader, uint stream, out nint type)
    {
        nint value = 0;
        var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)Table(reader)[5])(reader, stream, 0, &value);
        type = value;
        return hr;
    }
    internal static void SelectStream(nint reader, uint stream, bool selected) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)Table(reader)[4])(reader, stream, selected ? 1 : 0));
    internal static void ReaderType(nint reader, uint stream, nint type) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, uint*, nint, int>)Table(reader)[7])(reader, stream, null, type));
    internal static nint CurrentType(nint reader, uint stream)
    {
        nint result;
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Table(reader)[6])(reader, stream, &result));
        return result;
    }

    internal static long GetDuration(nint reader)
    {
        var key = Duration;
        Variant value = default;
        try
        {
            var hr = ((delegate* unmanaged[Stdcall]<nint, uint, Guid*, Variant*, int>)Table(reader)[12])(reader, uint.MaxValue, &key, &value);
            return hr >= 0 && value.Type is 20 or 21 && value.Value > 0 ? value.Value : 0;
        }
        finally { PropVariantClear(&value); }
    }

    internal static uint AddStream(nint writer, nint type)
    {
        uint index;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Table(writer)[3])(writer, type, &index));
        return index;
    }
    internal static void InputType(nint writer, uint index, nint type) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Table(writer)[4])(writer, index, type, 0));
    internal static void Begin(nint writer) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Table(writer)[5])(writer));
    internal static void Finish(nint writer) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Table(writer)[11])(writer));
    internal static void Write(nint writer, uint index, nint sample) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Table(writer)[6])(writer, index, sample));
    internal static nint Read(nint reader, uint index, out uint flags, out long timestamp)
    {
        uint status;
        long time;
        nint sample;
        Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, uint*, uint*, long*, nint*, int>)Table(reader)[9])(reader, index, 0, null, &status, &time, &sample));
        flags = status;
        timestamp = time;
        return sample;
    }
    internal static void SampleTime(nint sample, long timestamp) =>
        Check(((delegate* unmanaged[Stdcall]<nint, long, int>)Table(sample)[36])(sample, timestamp));
}

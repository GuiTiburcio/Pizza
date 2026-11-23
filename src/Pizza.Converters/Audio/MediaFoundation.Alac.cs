using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

internal static unsafe partial class MediaFoundation
{
    internal static uint AlacLayoutTag(nint type)
    {
        var key = new Guid("b6bc765f-4c3b-40a4-bd51-2535b66fe09d"); // MF_MT_USER_DATA
        var data = stackalloc byte[48];
        uint length;
        var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, byte*, uint, uint*, int>)Slot(type, 15))(type, &key, data, 48, &length);
        if (hr < 0 || length != 48) return 0;
        var cookie = new ReadOnlySpan<byte>(data, 48);
        if (BinaryPrimitives.ReadUInt32BigEndian(cookie[24..]) != 24 || !cookie.Slice(28, 4).SequenceEqual("chan"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(cookie[32..]) != 0
            || BinaryPrimitives.ReadUInt64BigEndian(cookie[40..]) != 0) return 0;
        return BinaryPrimitives.ReadUInt32BigEndian(cookie[36..]);
    }

    private static nint CreateResampler()
    {
        var classId = new Guid("f447b69e-1884-4a7e-8055-346f74d6edb3");
        var interfaceId = new Guid("bf94c121-5b05-4e6f-8000-ba598961414d"); // IMFTransform
        Check(CoCreateInstance(&classId, 0, 1, &interfaceId, out var transform));
        return transform;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* classId, nint outer, uint context, Guid* interfaceId, out nint result);
}

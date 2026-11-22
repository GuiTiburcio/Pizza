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

    /// <summary>
    /// O decoder ALAC do Windows entrega a ordem do cookie, não a ordem WAVE. Insere uma permutação
    /// antes de outro resampler do Windows fazer a mistura/reamostragem pedida. Não cria DSP próprio.
    /// </summary>
    internal static void CorrectAlacOrder(nint reader, nint originalPcm, nint targetPcm, AlacChannelLayout layout)
    {
        var readerId = new Guid("7b981cf0-560e-4116-9875-b099895f23d7");
        nint extended = 0, reorder = 0, properties = 0, convert = 0;
        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(reader, 0))(reader, &readerId, &extended));
            SetCurrentMediaType(reader, FirstAudioStream, originalPcm);
            reorder = CreateResampler();
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(extended, 14))(extended, FirstAudioStream, reorder));
            SetCurrentMediaType(reader, FirstAudioStream, originalPcm);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, uint, int>)Slot(reorder, 15))(reorder, 0, originalPcm, 0));
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, uint, int>)Slot(reorder, 16))(reorder, 0, originalPcm, 0));
            convert = CreateResampler();
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(extended, 14))(extended, FirstAudioStream, convert));
            SetCurrentMediaType(reader, FirstAudioStream, targetPcm);
            var propertiesId = new Guid("e7e9984f-f09f-4da4-903f-6e2e0efe56b5");
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(reorder, 0))(reorder, &propertiesId, &properties));
            var channels = layout.Order.Length;
            var matrix = stackalloc float[channels * channels];
            new Span<float>(matrix, channels * channels).Clear();
            // SetUserChannelMtx: linha de saída, coluna de entrada (transposta da propriedade inteira).
            for (var apple = 0; apple < channels; apple++) matrix[layout.Order[apple] * channels + apple] = 1;
            Check(((delegate* unmanaged[Stdcall]<nint, float*, int>)Slot(properties, 4))(properties, matrix));
        }
        finally
        {
            Release(convert); Release(properties); Release(reorder); Release(extended);
        }
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

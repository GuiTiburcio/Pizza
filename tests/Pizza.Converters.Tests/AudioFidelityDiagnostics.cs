using System.Buffers.Binary;
using System.Text;
using Pizza.Converters.Audio;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    // Somente fixtures sintéticos do teste: nunca registrar conteúdo nem caminhos da pessoa no produto.
    private static string InputPcmTimeline(byte[] wave)
    {
        MediaFoundation.Startup();
        try
        {
            using var source = new MemoryStream(wave);
            using var reader = AudioReader.Open(source);
            Assert.NotNull(reader);
            long bytes = 0, lastTime = 0, lastDuration = 0;
            while (true)
            {
                MediaFoundation.Check(MediaFoundation.ReadSample(reader.Handle, out var flags, out var time, out var sample));
                try
                {
                    if (sample != 0)
                    {
                        bytes += MediaFoundation.TotalLength(sample);
                        lastTime = time;
                        lastDuration = SampleDuration(sample);
                    }
                }
                finally
                {
                    MediaFoundation.Release(sample);
                }

                if ((flags & MediaFoundation.EndOfStream) != 0)
                {
                    break;
                }
            }

            return $"bytes={bytes}, último início={lastTime}, duração={lastDuration} (100 ns)";
        }
        finally
        {
            MediaFoundation.Shutdown();
        }
    }

    private static unsafe long SampleDuration(nint sample)
    {
        // IMFSample::GetSampleDuration, depois de IMFAttributes; unidade documentada de 100 ns.
        long duration;
        MediaFoundation.Check(((delegate* unmanaged[Stdcall]<nint, long*, int>)(*(nint**)sample)[37])(sample, &duration));
        return duration;
    }

    private static string M4aTimeline(byte[] data)
    {
        if (data.Length < 8 || !data.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            return "não é M4A";
        }

        var result = new List<string>();
        Visit(0, data.Length, 0);
        return string.Join("; ", result);

        void Visit(int start, int end, int depth)
        {
            Assert.InRange(depth, 0, 8);
            for (var position = start; position < end;)
            {
                Assert.True(end - position >= 8, "Cabeçalho de atom incompleto no fixture.");
                long size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
                var header = 8;
                if (size == 1)
                {
                    Assert.True(end - position >= 16);
                    size = checked((long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(position + 8, 8)));
                    header = 16;
                }
                else if (size == 0)
                {
                    size = end - position;
                }

                Assert.InRange(size, header, end - position);
                var stop = position + (int)size;
                var payload = position + header;
                var name = Encoding.ASCII.GetString(data, position + 4, 4);
                if (name is "moov" or "trak" or "mdia" or "minf" or "stbl")
                {
                    Visit(payload, stop, depth + 1);
                }
                else if (name is "mdhd" or "mvhd")
                {
                    Assert.True(stop - payload >= 20);
                    var version = data[payload];
                    Assert.InRange(version, (byte)0, (byte)1);
                    var scaleOffset = payload + (version == 0 ? 12 : 20);
                    Assert.True(stop - scaleOffset >= (version == 0 ? 8 : 12));
                    var scale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(scaleOffset, 4));
                    var duration = version == 0
                        ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(scaleOffset + 4, 4))
                        : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(scaleOffset + 4, 8));
                    result.Add($"{name}: escala={scale}, duração={duration}");
                }
                else if (name == "stts")
                {
                    Assert.True(stop - payload >= 8);
                    var count = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(payload + 4, 4));
                    Assert.True(count <= (stop - payload - 8) / 8);
                    ulong packets = 0, ticks = 0;
                    for (var index = 0; index < count; index++)
                    {
                        var entry = payload + 8 + index * 8;
                        var samples = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(entry, 4));
                        var delta = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(entry + 4, 4));
                        packets += samples;
                        ticks += (ulong)samples * delta;
                    }

                    result.Add($"stts: pacotes={packets}, duração={ticks}");
                }

                position = stop;
            }
        }
    }
}

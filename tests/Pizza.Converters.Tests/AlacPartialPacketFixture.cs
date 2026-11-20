using System.Buffers.Binary;
using System.Text;

namespace Pizza.Converters.Tests;

public partial class AudioConverterTests
{
    private static byte[] ReplaceSingleAlacPacket(byte[] container, byte[] pcm)
    {
        // Fixture 16-bit estéreo. Estrutura do formato: quadro CPE, comprimento explícito, PCM sem compressão.
        // Referência de formato: https://github.com/macosforge/alac/blob/master/codec/ALACEncoder.cpp
        var packet = new byte[pcm.Length + 8];
        var bit = 0;
        Write(1, 3); // CPE
        Write(0, 4); // instância
        Write(0, 12);
        Write(9, 4); // quadro parcial, sem compressão
        Write((uint)(pcm.Length / 4), 32);
        for (var position = 0; position < pcm.Length; position += 2)
        {
            Write(BinaryPrimitives.ReadUInt16LittleEndian(pcm.AsSpan(position, 2)), 16);
        }

        Write(7, 3); // END, seguido de alinhamento em zero
        Assert.Equal(packet.Length, (bit + 7) / 8);
        var atoms = new List<(string Name, int Position, int Payload, int End)>();
        ReadAtoms(0, container.Length);
        var sizes = atoms.Single(atom => atom.Name == "stsz");
        Assert.Equal(1U, Read32(sizes.Payload + 8));
        var fixedSize = Read32(sizes.Payload + 4);
        var oldSize = checked((int)(fixedSize == 0 ? Read32(sizes.Payload + 12) : fixedSize));
        var offsets = atoms.Single(atom => atom.Name is "stco" or "co64");
        Assert.Equal(1U, Read32(offsets.Payload + 4));
        var offset = checked((int)(offsets.Name == "stco" ? Read32(offsets.Payload + 8)
            : BinaryPrimitives.ReadUInt64BigEndian(container.AsSpan(offsets.Payload + 8, 8))));
        var media = atoms.Single(atom => atom.Name == "mdat");
        Assert.InRange(offset, media.Payload, media.End - oldSize);
        var delta = packet.Length - oldSize;
        byte[] result = [.. container.AsSpan(0, offset), .. packet, .. container.AsSpan(offset + oldSize)];
        if (Read32(media.Position) == 1)
        {
            BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(media.Position + 8, 8), checked((ulong)(media.End - media.Position + delta)));
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(media.Position, 4), checked((uint)(media.End - media.Position + delta)));
        }

        var sizePosition = sizes.Payload + (fixedSize == 0 ? 12 : 4);
        if (sizePosition >= offset + oldSize) sizePosition += delta;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(sizePosition, 4), (uint)packet.Length);
        return result;

        void Write(uint value, int count)
        {
            for (var shift = count - 1; shift >= 0; shift--, bit++)
            {
                packet[bit / 8] |= (byte)(((value >> shift) & 1) << (7 - bit % 8));
            }
        }

        uint Read32(int position) => BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(position, 4));

        void ReadAtoms(int begin, int end)
        {
            for (var position = begin; position < end;)
            {
                Assert.True(end - position >= 8);
                var size = Read32(position);
                var header = size == 1 ? 16 : 8;
                var length = size == 1 ? checked((int)BinaryPrimitives.ReadUInt64BigEndian(container.AsSpan(position + 8, 8)))
                    : size == 0 ? end - position : checked((int)size);
                Assert.InRange(length, header, end - position);
                var name = Encoding.ASCII.GetString(container, position + 4, 4);
                atoms.Add((name, position, position + header, position + length));
                if (name is "moov" or "trak" or "mdia" or "minf" or "stbl") ReadAtoms(position + header, position + length);
                position += length;
            }
        }
    }
}

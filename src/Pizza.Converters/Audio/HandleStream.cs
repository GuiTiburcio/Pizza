using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>
/// Um IStream do COM sobre o <see cref="Stream"/> do handle que o residente entregou (SEC-22). O Media Foundation lê e
/// grava só por ele, então nenhum caminho de arquivo chega ao decodificador (SEC-15). Feito à mão, sem o interop
/// clássico do COM, para seguir compatível com o trimming.
/// </summary>
internal static unsafe class HandleStream
{
    private const int Ok = 0;
    private const int False = 1;
    private const int NoInterface = unchecked((int)0x80004002);
    private const int NotImplemented = unchecked((int)0x80004001);
    private const int ReadFault = unchecked((int)0x8003001E); // STG_E_READFAULT
    private const int WriteFault = unchecked((int)0x8003001D); // STG_E_WRITEFAULT

    private static readonly Guid Unknown = new("00000000-0000-0000-c000-000000000046");
    private static readonly Guid SequentialStream = new("0c733a30-2a1c-11ce-ade5-00aa0044773d");
    private static readonly Guid StreamId = new("0000000c-0000-0000-c000-000000000046");

    private static readonly nint* Table = CreateTable();

    /// <summary>Cria o objeto com uma referência; quem recebe chama Release (<see cref="MediaFoundation.Release"/>).</summary>
    public static nint Create(Stream inner)
    {
        var instance = (Instance*)NativeMemory.AllocZeroed((nuint)sizeof(Instance));
        instance->Table = Table;
        instance->References = 1;
        instance->Target = GCHandle.ToIntPtr(GCHandle.Alloc(inner));
        return (nint)instance;
    }

    private struct Instance
    {
        public nint* Table;
        public int References;
        public nint Target;
    }

    private static Stream Of(nint self) => (Stream)GCHandle.FromIntPtr(((Instance*)self)->Target).Target!;

    private static nint* CreateTable()
    {
        var table = (nint*)NativeMemory.Alloc(14, (nuint)sizeof(nint));
        table[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&QueryInterface;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release;
        table[3] = (nint)(delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)&Read;
        table[4] = (nint)(delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)&Write;
        table[5] = (nint)(delegate* unmanaged[Stdcall]<nint, long, uint, ulong*, int>)&Seek;
        table[6] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, int>)&SetSize;
        table[7] = (nint)(delegate* unmanaged[Stdcall]<nint, nint, ulong, ulong*, ulong*, int>)&CopyTo;
        table[8] = (nint)(delegate* unmanaged[Stdcall]<nint, uint, int>)&Commit;
        table[9] = (nint)(delegate* unmanaged[Stdcall]<nint, int>)&Revert;
        table[10] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, ulong, uint, int>)&LockRegion;
        table[11] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, ulong, uint, int>)&LockRegion;
        table[12] = (nint)(delegate* unmanaged[Stdcall]<nint, Statistics*, uint, int>)&Stat;
        table[13] = (nint)(delegate* unmanaged[Stdcall]<nint, nint*, int>)&Clone;
        return table;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(nint self, Guid* iid, nint* result)
    {
        if (*iid == Unknown || *iid == SequentialStream || *iid == StreamId)
        {
            Interlocked.Increment(ref ((Instance*)self)->References);
            *result = self;
            return Ok;
        }

        *result = 0;
        return NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(nint self) => (uint)Interlocked.Increment(ref ((Instance*)self)->References);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(nint self)
    {
        var left = Interlocked.Decrement(ref ((Instance*)self)->References);
        if (left == 0)
        {
            // O Stream continua de quem o abriu; aqui só some a ponte.
            GCHandle.FromIntPtr(((Instance*)self)->Target).Free();
            NativeMemory.Free((void*)self);
        }

        return (uint)left;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Read(nint self, byte* buffer, uint count, uint* done)
    {
        try
        {
            var total = 0;
            var target = new Span<byte>(buffer, (int)count);
            while (total < target.Length)
            {
                var read = Of(self).Read(target[total..]);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (done is not null)
            {
                *done = (uint)total;
            }

            return total == count ? Ok : False;
        }
        catch (Exception)
        {
            return ReadFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Write(nint self, byte* buffer, uint count, uint* done)
    {
        try
        {
            Of(self).Write(new ReadOnlySpan<byte>(buffer, (int)count));
            if (done is not null)
            {
                *done = count;
            }

            return Ok;
        }
        catch (Exception)
        {
            return WriteFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Seek(nint self, long move, uint origin, ulong* position)
    {
        try
        {
            var at = Of(self).Seek(move, (SeekOrigin)origin);
            if (position is not null)
            {
                *position = (ulong)at;
            }

            return Ok;
        }
        catch (Exception)
        {
            return ReadFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetSize(nint self, ulong size)
    {
        try
        {
            Of(self).SetLength((long)size);
            return Ok;
        }
        catch (Exception)
        {
            return WriteFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CopyTo(nint self, nint target, ulong count, ulong* read, ulong* written) => NotImplemented;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Commit(nint self, uint flags)
    {
        try
        {
            Of(self).Flush();
            return Ok;
        }
        catch (Exception)
        {
            return WriteFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Revert(nint self) => NotImplemented;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LockRegion(nint self, ulong offset, ulong count, uint type) => NotImplemented;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Stat(nint self, Statistics* statistics, uint flags)
    {
        try
        {
            var stream = Of(self);
            *statistics = default;
            statistics->Type = 2; // STGTY_STREAM
            statistics->Size = (ulong)stream.Length;
            statistics->Mode = stream.CanWrite ? (stream.CanRead ? 2u : 1u) : 0u; // STGM_READWRITE, STGM_WRITE, STGM_READ
            return Ok;
        }
        catch (Exception)
        {
            return ReadFault;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Clone(nint self, nint* result)
    {
        *result = 0;
        return NotImplemented;
    }

    /// <summary>STATSTG. O nome fica nulo: o Pizza não passa nome nenhum adiante.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Statistics
    {
        public nint Name;
        public uint Type;
        public ulong Size;
        public long Modified;
        public long Created;
        public long Accessed;
        public uint Mode;
        public uint LocksSupported;
        public Guid Class;
        public uint StateBits;
        public uint Reserved;
    }
}

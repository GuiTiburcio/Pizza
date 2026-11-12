using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>IStream sobre o handle/stream recebido. Não fecha o stream e nunca aceita caminhos.</summary>
internal sealed unsafe class ComStream : IDisposable
{
    private const long MaximumLength = 2L * 1024 * 1024 * 1024;
    private const int InvalidFunction = unchecked((int)0x80030001);
    private const int AccessDenied = unchecked((int)0x80030005);
    private const int InvalidPointer = unchecked((int)0x80004003);
    private const int InvalidArgument = unchecked((int)0x80070057);
    private const int WriteFault = unchecked((int)0x8003001D);
    private const int ReadFault = unchecked((int)0x8003001E);
    private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid StreamId = new("0000000c-0000-0000-C000-000000000046");
    private static readonly Guid SequentialId = new("0c733a30-2a1c-11ce-ade5-00aa0044773d");
    private static readonly nint Vtable = CreateVtable();
    private nint _pointer;

    private sealed class State(Stream stream)
    {
        internal readonly Stream Stream = stream;
        internal readonly Lock Gate = new();
        internal int References = 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Instance { internal nint Table; internal nint State; }

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamStat
    {
        internal nint Name;
        internal uint Type;
        internal ulong Size;
        internal long Modified, Created, Accessed;
        internal uint Mode, Locks;
        internal Guid Class;
        internal uint State, Reserved;
    }

    internal ComStream(Stream stream)
    {
        if (!stream.CanSeek) throw new ArgumentException("O áudio precisa de um stream com busca.", nameof(stream));
        var instance = (Instance*)NativeMemory.AllocZeroed((nuint)sizeof(Instance));
        instance->Table = Vtable;
        instance->State = GCHandle.ToIntPtr(GCHandle.Alloc(new State(stream)));
        _pointer = (nint)instance;
    }

    internal nint Pointer => _pointer;
    public void Dispose()
    {
        var pointer = Interlocked.Exchange(ref _pointer, 0);
        if (pointer != 0) ReleaseCore(pointer);
    }

    private static State Get(nint self) => (State)GCHandle.FromIntPtr(((Instance*)self)->State).Target!;

    private static nint CreateVtable()
    {
        var table = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComStream), 14 * sizeof(nint));
        table[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&Query;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release;
        table[3] = (nint)(delegate* unmanaged[Stdcall]<nint, void*, uint, uint*, int>)&Read;
        table[4] = (nint)(delegate* unmanaged[Stdcall]<nint, void*, uint, uint*, int>)&Write;
        table[5] = (nint)(delegate* unmanaged[Stdcall]<nint, long, uint, ulong*, int>)&Seek;
        table[6] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, int>)&SetSize;
        table[7] = (nint)(delegate* unmanaged[Stdcall]<nint, nint, ulong, ulong*, ulong*, int>)&CopyTo;
        table[8] = (nint)(delegate* unmanaged[Stdcall]<nint, uint, int>)&Commit;
        table[9] = (nint)(delegate* unmanaged[Stdcall]<nint, int>)&Revert;
        table[10] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, ulong, uint, int>)&LockRegion;
        table[11] = (nint)(delegate* unmanaged[Stdcall]<nint, ulong, ulong, uint, int>)&UnlockRegion;
        table[12] = (nint)(delegate* unmanaged[Stdcall]<nint, StreamStat*, uint, int>)&Stat;
        table[13] = (nint)(delegate* unmanaged[Stdcall]<nint, nint*, int>)&Clone;
        return (nint)table;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(nint self, Guid* id, nint* result)
    {
        if (result == null || id == null) return InvalidPointer;
        *result = 0;
        if (*id != UnknownId && *id != StreamId && *id != SequentialId) return unchecked((int)0x80004002);
        Interlocked.Increment(ref Get(self).References);
        *result = self;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(nint self) => (uint)Interlocked.Increment(ref Get(self).References);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(nint self) => ReleaseCore(self);

    private static uint ReleaseCore(nint self)
    {
        var count = Interlocked.Decrement(ref Get(self).References);
        if (count == 0)
        {
            GCHandle.FromIntPtr(((Instance*)self)->State).Free();
            NativeMemory.Free((void*)self);
        }
        return (uint)count;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Read(nint self, void* buffer, uint count, uint* read)
    {
        if (read != null) *read = 0;
        if (count == 0) return 0;
        if (buffer == null || count > int.MaxValue) return InvalidArgument;
        try
        {
            var state = Get(self);
            lock (state.Gate)
            {
                if (!state.Stream.CanRead) return AccessDenied;
                var actual = state.Stream.Read(new Span<byte>(buffer, (int)count));
                if (read != null) *read = (uint)actual;
                return actual == count ? 0 : 1;
            }
        }
        catch { return ReadFault; } // Nenhuma exceção pode atravessar uma fronteira COM.
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Write(nint self, void* buffer, uint count, uint* written)
    {
        if (written != null) *written = 0;
        if (count == 0) return 0;
        if (buffer == null || count > int.MaxValue) return InvalidArgument;
        try
        {
            var state = Get(self);
            lock (state.Gate)
            {
                if (!state.Stream.CanWrite) return AccessDenied;
                if (count > MaximumLength - state.Stream.Position) return WriteFault;
                state.Stream.Write(new ReadOnlySpan<byte>(buffer, (int)count));
                if (written != null) *written = count;
                return 0;
            }
        }
        catch { return WriteFault; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Seek(nint self, long move, uint origin, ulong* position)
    {
        if (origin > 2) return InvalidArgument;
        try
        {
            var state = Get(self);
            lock (state.Gate)
            {
                var start = origin switch { 0 => 0, 1 => state.Stream.Position, _ => state.Stream.Length };
                var next = checked(start + move);
                if (next < 0 || next > MaximumLength) return InvalidArgument;
                state.Stream.Position = next;
                if (position != null) *position = (ulong)next;
                return 0;
            }
        }
        catch { return InvalidFunction; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetSize(nint self, ulong size)
    {
        if (size > MaximumLength) return InvalidArgument;
        try
        {
            var state = Get(self);
            lock (state.Gate)
            {
                if (!state.Stream.CanWrite) return AccessDenied;
                state.Stream.SetLength((long)size);
                return 0;
            }
        }
        catch { return WriteFault; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Commit(nint self, uint flags)
    {
        try { var state = Get(self); lock (state.Gate) state.Stream.Flush(); return 0; }
        catch { return WriteFault; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Stat(nint self, StreamStat* stat, uint flags)
    {
        if (stat == null) return InvalidPointer;
        *stat = default;
        try
        {
            var state = Get(self);
            lock (state.Gate)
            {
                stat->Type = 2; // STGTY_STREAM. Nenhum nome/caminho é exposto.
                stat->Size = (ulong)state.Stream.Length;
                stat->Mode = state.Stream.CanRead ? (state.Stream.CanWrite ? 2u : 0u) : 1u;
                return 0;
            }
        }
        catch { return InvalidFunction; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CopyTo(nint self, nint destination, ulong count, ulong* read, ulong* written) => unchecked((int)0x80004001);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Revert(nint self) => InvalidFunction;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LockRegion(nint self, ulong start, ulong count, uint type) => InvalidFunction;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int UnlockRegion(nint self, ulong start, ulong count, uint type) => InvalidFunction;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Clone(nint self, nint* result) { if (result != null) *result = 0; return unchecked((int)0x80004001); }
}

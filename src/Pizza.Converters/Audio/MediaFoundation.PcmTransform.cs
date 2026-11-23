using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

internal static unsafe partial class MediaFoundation
{
    /// <summary>Resampler Windows explícito, usado somente depois de corrigir o PCM ALAC multicanal.</summary>
    internal sealed class PcmTransform : IDisposable
    {
        private const int NeedMoreInput = unchecked((int)0xC00D6D72);
        private const int NotAccepting = unchecked((int)0xC00D36B5);
        private nint _transform;
        private readonly uint _size, _alignment;

        internal PcmTransform(nint input, nint output)
        {
            _transform = CreateResampler();
            try
            {
                Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, uint, int>)Slot(_transform, 15))(_transform, 0, input, 0));
                Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, uint, int>)Slot(_transform, 16))(_transform, 0, output, 0));
                OutputInfo info;
                Check(((delegate* unmanaged[Stdcall]<nint, uint, OutputInfo*, int>)Slot(_transform, 7))(_transform, 0, &info));
                // O resampler usa buffers fornecidos pelo chamador. Limitar alocação mesmo se o contrato mudar.
                if ((info.Flags & 0x100) != 0 || info.Size > 1024 * 1024 || info.Alignment > 4096)
                    throw new InvalidDataException("Contrato do resampler não suportado.");
                _size = Math.Max(64 * 1024u, info.Size);
                _alignment = info.Alignment == 0 ? 15u : info.Alignment - 1;
                if ((_alignment & (_alignment + 1)) != 0) throw new InvalidDataException("Alinhamento do resampler inválido.");
                Reset();
            }
            catch { Dispose(); throw; }
        }

        internal void Push(nint sample, Action<nint> consume)
        {
            var hr = Input(sample);
            if (hr == NotAccepting)
            {
                Read(consume);
                hr = Input(sample);
            }
            Check(hr);
            Read(consume);
        }

        internal void Drain(Action<nint> consume)
        {
            Message(0x10000002); // NOTIFY_END_OF_STREAM
            Message(1); // COMMAND_DRAIN
            Read(consume);
        }

        internal void Reset()
        {
            Message(0); // COMMAND_FLUSH: não manter histórico da passada do Normalizar.
            Message(0x10000000); // NOTIFY_BEGIN_STREAMING
            Message(0x10000003); // NOTIFY_START_OF_STREAM
        }

        private int Input(nint sample) =>
            ((delegate* unmanaged[Stdcall]<nint, uint, nint, uint, int>)Slot(_transform, 24))(_transform, 0, sample, 0);

        private void Message(uint message) =>
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nuint, int>)Slot(_transform, 23))(_transform, message, 0));

        private void Read(Action<nint> consume)
        {
            while (true)
            {
                nint sample = 0, buffer = 0;
                OutputData output = default;
                try
                {
                    Check(MFCreateSample(out sample));
                    Check(MFCreateAlignedMemoryBuffer(_size, _alignment, out buffer));
                    Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(sample, 42))(sample, buffer)); // AddBuffer
                    output.Sample = sample;
                    uint status;
                    var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, OutputData*, uint*, int>)Slot(_transform, 25))(_transform, 0, 1, &output, &status);
                    if (hr == NeedMoreInput) return;
                    Check(hr);
                    if (output.Sample != sample) throw new InvalidDataException("Amostra do resampler inesperada.");
                    if (TotalLength(sample) != 0) consume(sample);
                }
                finally
                {
                    Release(output.Events);
                    if (output.Sample != sample) Release(output.Sample);
                    Release(buffer); Release(sample);
                }
            }
        }

        public void Dispose()
        {
            Release(_transform);
            _transform = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OutputInfo { internal uint Flags, Size, Alignment; }

        [StructLayout(LayoutKind.Sequential)]
        private struct OutputData { internal uint Stream; internal nint Sample; internal uint Status; internal nint Events; }
    }

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateSample(out nint sample);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateAlignedMemoryBuffer(uint length, uint alignment, out nint buffer);
}

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Pizza.Converters.Jxl;

/// <summary>Pixels 8 bits, RGB ou RGBA, em sRGB, linha após linha.</summary>
internal sealed record JxlPixels(int Width, int Height, int Channels, byte[] Data, int Orientation = 1);

/// <summary>
/// Codifica e decodifica JPEG XL com a libjxl (ADR-040). O arquivo é não confiável: roda só no Worker (SEC-20), lê da
/// memória (SEC-15), confere os limites antes de alocar (SEC-40, SEC-41) e devolve só códigos de erro, sem nome de
/// arquivo nem conteúdo (PRIV-16). Os buffers entregues à libjxl ficam fixados enquanto ela os usa.
/// </summary>
internal static unsafe class JxlCodec
{
    private const int OutputChunk = 1 << 16;

    public static bool IsJpeg(ReadOnlySpan<byte> data) => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    public static bool IsJxl(ReadOnlySpan<byte> data)
    {
        fixed (byte* pointer = data)
        {
            return Native.SignatureCheck(pointer, (nuint)data.Length) is Native.SignatureCodestream or Native.SignatureContainer;
        }
    }

    /// <summary>Decodifica o primeiro quadro para sRGB de 8 bits. Erros: CORRUPTED, LIMIT_EXCEEDED.</summary>
    public static string? Decode(byte[] data, long maxPixels, out JxlPixels? pixels)
    {
        pixels = null;
        var decoder = Native.DecoderCreate(0);
        if (decoder == 0)
        {
            return "CORRUPTED";
        }

        GCHandle output = default;
        try
        {
            const int events = Native.StatusBasicInfo | Native.StatusFullImage | 0x100; // 0x100: perfil de cor.
            if (Native.DecoderSubscribeEvents(decoder, events) != Native.StatusSuccess)
            {
                return "CORRUPTED";
            }

            int width = 0, height = 0, channels = 3, orientation = 1;
            byte[]? buffer = null;
            var info = stackalloc byte[Native.BasicInfoBytes];
            var encoding = stackalloc byte[Native.ColorEncodingBytes];
            fixed (byte* input = data)
            {
                if (Native.DecoderSetInput(decoder, input, (nuint)data.Length) != Native.StatusSuccess)
                {
                    return "CORRUPTED";
                }

                Native.DecoderCloseInput(decoder);
                while (true)
                {
                    switch (Native.DecoderProcessInput(decoder))
                    {
                        case Native.StatusBasicInfo:
                            if (Native.DecoderGetBasicInfo(decoder, info) != Native.StatusSuccess)
                            {
                                return "CORRUPTED";
                            }

                            width = (int)Math.Min(ReadUInt(info, Native.InfoXSize), int.MaxValue);
                            height = (int)Math.Min(ReadUInt(info, Native.InfoYSize), int.MaxValue);
                            if (width == 0 || height == 0)
                            {
                                return "CORRUPTED";
                            }

                            // SEC-41: o limite vale antes de qualquer alocação do tamanho da imagem.
                            if ((long)width * height > maxPixels)
                            {
                                return "LIMIT_EXCEEDED";
                            }

                            channels = ReadUInt(info, Native.InfoAlphaBits) > 0 ? 4 : 3;
                            orientation = (int)ReadUInt(info, Native.InfoOrientation);
                            break;

                        case 0x100: // Perfil de cor: pede a saída em sRGB, para as cores não mudarem (PRIV-31).
                            new Span<byte>(encoding, Native.ColorEncodingBytes).Clear();
                            Native.ColorEncodingSetToSrgb(encoding, 0);
                            Native.DecoderSetOutputColorProfile(decoder, encoding, null, 0);
                            break;

                        case Native.StatusNeedImageOutBuffer:
                            var format = new Native.PixelFormat { Channels = (uint)channels, DataType = Native.TypeUint8, Endianness = Native.NativeEndian };
                            nuint size;
                            if (Native.DecoderImageOutBufferSize(decoder, &format, &size) != Native.StatusSuccess || size != (nuint)((long)width * height * channels))
                            {
                                return "CORRUPTED";
                            }

                            buffer = GC.AllocateUninitializedArray<byte>((int)size);
                            output = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                            if (Native.DecoderSetImageOutBuffer(decoder, &format, (byte*)output.AddrOfPinnedObject(), size) != Native.StatusSuccess)
                            {
                                return "CORRUPTED";
                            }

                            break;

                        case Native.StatusFullImage:
                        case Native.StatusSuccess:
                            if (buffer is null)
                            {
                                return "CORRUPTED";
                            }

                            pixels = new JxlPixels(width, height, channels, buffer, orientation);
                            return null;

                        default: // Erro ou entrada truncada.
                            return "CORRUPTED";
                    }
                }
            }
        }
        finally
        {
            if (output.IsAllocated)
            {
                output.Free();
            }

            Native.DecoderDestroy(decoder);
        }
    }

    /// <summary>
    /// Refaz o JPEG original, byte a byte, de um JXL que guarda a recompressão sem perdas. Erros: UNSUPPORTED (o
    /// arquivo não veio de um JPEG), CORRUPTED, LIMIT_EXCEEDED.
    /// </summary>
    public static string? ReconstructJpeg(byte[] data, Stream destination, long maxBytes)
    {
        var decoder = Native.DecoderCreate(0);
        if (decoder == 0)
        {
            return "CORRUPTED";
        }

        GCHandle output = default;
        try
        {
            if (Native.DecoderSubscribeEvents(decoder, Native.StatusJpegReconstruction | Native.StatusFullImage) != Native.StatusSuccess)
            {
                return "CORRUPTED";
            }

            var buffer = new byte[Math.Max(OutputChunk, data.Length)];
            var reconstructable = false;
            fixed (byte* input = data)
            {
                if (Native.DecoderSetInput(decoder, input, (nuint)data.Length) != Native.StatusSuccess)
                {
                    return "CORRUPTED";
                }

                Native.DecoderCloseInput(decoder);
                while (true)
                {
                    switch (Native.DecoderProcessInput(decoder))
                    {
                        case Native.StatusJpegReconstruction:
                            reconstructable = true;
                            output = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                            if (Native.DecoderSetJpegBuffer(decoder, (byte*)output.AddrOfPinnedObject(), (nuint)buffer.Length) != Native.StatusSuccess)
                            {
                                return "CORRUPTED";
                            }

                            break;

                        case Native.StatusJpegNeedMoreOutput:
                            var used = buffer.Length - (long)Native.DecoderReleaseJpegBuffer(decoder);
                            if (buffer.Length > maxBytes)
                            {
                                return "LIMIT_EXCEEDED";
                            }

                            output.Free();
                            Array.Resize(ref buffer, buffer.Length * 2);
                            output = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                            if (Native.DecoderSetJpegBuffer(decoder, (byte*)output.AddrOfPinnedObject() + used, (nuint)(buffer.Length - used)) != Native.StatusSuccess)
                            {
                                return "CORRUPTED";
                            }

                            break;

                        case Native.StatusFullImage:
                        case Native.StatusSuccess:
                            if (!reconstructable)
                            {
                                return "UNSUPPORTED";
                            }

                            destination.Write(buffer, 0, buffer.Length - (int)Native.DecoderReleaseJpegBuffer(decoder));
                            return null;

                        case Native.StatusNeedImageOutBuffer:
                            return "UNSUPPORTED";

                        default:
                            return "CORRUPTED";
                    }
                }
            }
        }
        finally
        {
            if (output.IsAllocated)
            {
                output.Free();
            }

            Native.DecoderDestroy(decoder);
        }
    }

    /// <summary>Recompressão sem perdas: o JPEG original cabe inteiro no JXL, com os metadados que o refazem.</summary>
    public static string? EncodeJpeg(byte[] jpeg, Stream destination) =>
        Encode(destination, (encoder, settings) =>
        {
            if (Native.EncoderStoreJpegMetadata(encoder, 1) != Native.EncSuccess)
            {
                return false;
            }

            fixed (byte* data = jpeg)
            {
                return Native.EncoderAddJpegFrame(settings, data, (nuint)jpeg.Length) == Native.EncSuccess;
            }
        });

    /// <summary>Distância 0 é sem perdas; acima disso, é a distância visual da libjxl (1 é visualmente sem perdas).</summary>
    public static string? EncodePixels(JxlPixels image, float distance, Stream destination)
    {
        var lossless = distance <= 0;
        return Encode(destination, (encoder, settings) =>
        {
            var info = stackalloc byte[Native.BasicInfoBytes];
            new Span<byte>(info, Native.BasicInfoBytes).Clear();
            Native.EncoderInitBasicInfo(info);
            WriteUInt(info, Native.InfoXSize, (uint)image.Width);
            WriteUInt(info, Native.InfoYSize, (uint)image.Height);
            WriteUInt(info, Native.InfoBitsPerSample, 8);
            WriteUInt(info, Native.InfoExponentBits, 0);
            WriteUInt(info, Native.InfoUsesOriginalProfile, lossless ? 1u : 0u);
            WriteUInt(info, Native.InfoNumColorChannels, 3);
            if (image.Channels == 4)
            {
                WriteUInt(info, Native.InfoNumExtraChannels, 1);
                WriteUInt(info, Native.InfoAlphaBits, 8);
                WriteUInt(info, Native.InfoAlphaExponentBits, 0);
            }

            if (Native.EncoderSetBasicInfo(encoder, info) != Native.EncSuccess)
            {
                return false;
            }

            var color = stackalloc byte[Native.ColorEncodingBytes];
            new Span<byte>(color, Native.ColorEncodingBytes).Clear();
            Native.ColorEncodingSetToSrgb(color, 0);
            if (Native.EncoderSetColorEncoding(encoder, color) != Native.EncSuccess)
            {
                return false;
            }

            var ok = lossless
                ? Native.EncoderSetFrameLossless(settings, 1) == Native.EncSuccess
                : Native.EncoderSetFrameDistance(settings, distance) == Native.EncSuccess;
            if (!ok)
            {
                return false;
            }

            var format = new Native.PixelFormat { Channels = (uint)image.Channels, DataType = Native.TypeUint8, Endianness = Native.NativeEndian };
            fixed (byte* data = image.Data)
            {
                return Native.EncoderAddImageFrame(settings, &format, data, (nuint)image.Data.Length) == Native.EncSuccess;
            }
        });
    }

    private static string? Encode(Stream destination, Func<nint, nint, bool> addFrame)
    {
        var encoder = Native.EncoderCreate(0);
        if (encoder == 0)
        {
            return "CORRUPTED";
        }

        nint runner = 0;
        try
        {
            runner = Native.ThreadRunnerCreate(0, Native.ThreadRunnerDefaultWorkers());
            if (runner == 0 || Native.EncoderSetParallelRunner(encoder, Native.ThreadRunnerFunction, runner) != Native.EncSuccess)
            {
                return "CORRUPTED";
            }

            var settings = Native.EncoderFrameSettingsCreate(encoder, 0);
            if (settings == 0 || Native.EncoderFrameSettingsSetOption(settings, Native.FrameSettingEffort, 5) != Native.EncSuccess)
            {
                return "CORRUPTED";
            }

            if (!addFrame(encoder, settings))
            {
                return "CORRUPTED";
            }

            Native.EncoderCloseInput(encoder);
            var chunk = new byte[OutputChunk];
            fixed (byte* start = chunk)
            {
                while (true)
                {
                    var next = start;
                    var available = (nuint)chunk.Length;
                    var status = Native.EncoderProcessOutput(encoder, &next, &available);
                    destination.Write(chunk, 0, chunk.Length - (int)available);
                    if (status == Native.EncSuccess)
                    {
                        return null;
                    }

                    if (status != Native.EncNeedMoreOutput)
                    {
                        return "CORRUPTED";
                    }
                }
            }
        }
        finally
        {
            if (runner != 0)
            {
                Native.ThreadRunnerDestroy(runner);
            }

            Native.EncoderDestroy(encoder);
        }
    }

    private static uint ReadUInt(byte* info, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(info + offset, 4));

    private static void WriteUInt(byte* info, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(info + offset, 4), value);
}

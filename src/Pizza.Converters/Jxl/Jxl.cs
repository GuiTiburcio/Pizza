using System.Runtime.InteropServices;

namespace Pizza.Converters.Jxl;

/// <summary>
/// A API em C da libjxl 0.11 (ADR-040), só o que o Pizza usa. Entra e sai sempre da memória: nenhum caminho de
/// arquivo chega à biblioteca (SEC-15). As estruturas da libjxl são opacas aqui: viram blocos de bytes que a própria
/// biblioteca inicializa, e só os campos usados são lidos ou escritos, nos deslocamentos de <c>jxl/codestream_header.h</c>.
/// </summary>
internal static unsafe partial class Native
{
    private const string Library = "jxl";
    private const string Threads = "jxl_threads";

    // Tamanhos de JxlBasicInfo (204) e JxlColorEncoding (104), com folga.
    public const int BasicInfoBytes = 256;
    public const int ColorEncodingBytes = 128;

    // Deslocamentos em JxlBasicInfo.
    public const int InfoXSize = 4;
    public const int InfoYSize = 8;
    public const int InfoBitsPerSample = 12;
    public const int InfoExponentBits = 16;
    public const int InfoUsesOriginalProfile = 36;
    public const int InfoOrientation = 48;
    public const int InfoNumColorChannels = 52;
    public const int InfoNumExtraChannels = 56;
    public const int InfoAlphaBits = 60;
    public const int InfoAlphaExponentBits = 64;

    // JxlDecoderStatus
    public const int StatusSuccess = 0;
    public const int StatusError = 1;
    public const int StatusNeedMoreInput = 2;
    public const int StatusNeedImageOutBuffer = 5;
    public const int StatusJpegNeedMoreOutput = 6;
    public const int StatusBasicInfo = 0x40;
    public const int StatusFullImage = 0x1000;
    public const int StatusJpegReconstruction = 0x2000;

    // JxlEncoderStatus
    public const int EncSuccess = 0;
    public const int EncNeedMoreOutput = 2;

    // JxlSignature
    public const int SignatureCodestream = 2;
    public const int SignatureContainer = 3;

    public const int TypeUint8 = 2;
    public const int TypeUint16 = 3;
    public const int NativeEndian = 0;
    public const int FrameSettingEffort = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct PixelFormat
    {
        public uint Channels;
        public int DataType;
        public int Endianness;
        public nuint Align;
    }

    [LibraryImport(Library, EntryPoint = "JxlSignatureCheck")]
    public static partial int SignatureCheck(byte* buffer, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlDecoderCreate")]
    public static partial nint DecoderCreate(nint memoryManager);

    [LibraryImport(Library, EntryPoint = "JxlDecoderDestroy")]
    public static partial void DecoderDestroy(nint decoder);

    [LibraryImport(Library, EntryPoint = "JxlDecoderSubscribeEvents")]
    public static partial int DecoderSubscribeEvents(nint decoder, int events);

    [LibraryImport(Library, EntryPoint = "JxlDecoderSetInput")]
    public static partial int DecoderSetInput(nint decoder, byte* data, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlDecoderCloseInput")]
    public static partial void DecoderCloseInput(nint decoder);

    [LibraryImport(Library, EntryPoint = "JxlDecoderProcessInput")]
    public static partial int DecoderProcessInput(nint decoder);

    [LibraryImport(Library, EntryPoint = "JxlDecoderGetBasicInfo")]
    public static partial int DecoderGetBasicInfo(nint decoder, byte* info);

    [LibraryImport(Library, EntryPoint = "JxlDecoderImageOutBufferSize")]
    public static partial int DecoderImageOutBufferSize(nint decoder, PixelFormat* format, nuint* size);

    [LibraryImport(Library, EntryPoint = "JxlDecoderSetImageOutBuffer")]
    public static partial int DecoderSetImageOutBuffer(nint decoder, PixelFormat* format, byte* buffer, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlDecoderSetJPEGBuffer")]
    public static partial int DecoderSetJpegBuffer(nint decoder, byte* data, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlDecoderReleaseJPEGBuffer")]
    public static partial nuint DecoderReleaseJpegBuffer(nint decoder);

    [LibraryImport(Library, EntryPoint = "JxlDecoderSetOutputColorProfile")]
    public static partial int DecoderSetOutputColorProfile(nint decoder, byte* colorEncoding, byte* icc, nuint iccSize);

    [LibraryImport(Library, EntryPoint = "JxlColorEncodingSetToSRGB")]
    public static partial void ColorEncodingSetToSrgb(byte* colorEncoding, int isGray);

    [LibraryImport(Library, EntryPoint = "JxlEncoderCreate")]
    public static partial nint EncoderCreate(nint memoryManager);

    [LibraryImport(Library, EntryPoint = "JxlEncoderDestroy")]
    public static partial void EncoderDestroy(nint encoder);

    [LibraryImport(Library, EntryPoint = "JxlEncoderInitBasicInfo")]
    public static partial void EncoderInitBasicInfo(byte* info);

    [LibraryImport(Library, EntryPoint = "JxlEncoderSetBasicInfo")]
    public static partial int EncoderSetBasicInfo(nint encoder, byte* info);

    [LibraryImport(Library, EntryPoint = "JxlEncoderSetColorEncoding")]
    public static partial int EncoderSetColorEncoding(nint encoder, byte* colorEncoding);

    [LibraryImport(Library, EntryPoint = "JxlEncoderFrameSettingsCreate")]
    public static partial nint EncoderFrameSettingsCreate(nint encoder, nint source);

    [LibraryImport(Library, EntryPoint = "JxlEncoderFrameSettingsSetOption")]
    public static partial int EncoderFrameSettingsSetOption(nint settings, int option, long value);

    [LibraryImport(Library, EntryPoint = "JxlEncoderSetFrameDistance")]
    public static partial int EncoderSetFrameDistance(nint settings, float distance);

    [LibraryImport(Library, EntryPoint = "JxlEncoderSetFrameLossless")]
    public static partial int EncoderSetFrameLossless(nint settings, int lossless);

    [LibraryImport(Library, EntryPoint = "JxlEncoderStoreJPEGMetadata")]
    public static partial int EncoderStoreJpegMetadata(nint encoder, int store);

    [LibraryImport(Library, EntryPoint = "JxlEncoderAddJPEGFrame")]
    public static partial int EncoderAddJpegFrame(nint settings, byte* data, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlEncoderAddImageFrame")]
    public static partial int EncoderAddImageFrame(nint settings, PixelFormat* format, byte* data, nuint size);

    [LibraryImport(Library, EntryPoint = "JxlEncoderCloseInput")]
    public static partial void EncoderCloseInput(nint encoder);

    [LibraryImport(Library, EntryPoint = "JxlEncoderProcessOutput")]
    public static partial int EncoderProcessOutput(nint encoder, byte** next, nuint* available);

    [LibraryImport(Library, EntryPoint = "JxlEncoderSetParallelRunner")]
    public static partial int EncoderSetParallelRunner(nint encoder, nint runner, nint runnerOpaque);

    [LibraryImport(Threads, EntryPoint = "JxlThreadParallelRunnerCreate")]
    public static partial nint ThreadRunnerCreate(nint memoryManager, nuint workers);

    [LibraryImport(Threads, EntryPoint = "JxlThreadParallelRunnerDestroy")]
    public static partial void ThreadRunnerDestroy(nint runner);

    [LibraryImport(Threads, EntryPoint = "JxlThreadParallelRunnerDefaultNumWorkerThreads")]
    public static partial nuint ThreadRunnerDefaultWorkers();

    /// <summary>O ponteiro de função <c>JxlThreadParallelRunner</c>, que a libjxl chama com o runner como opaco.</summary>
    public static nint ThreadRunnerFunction { get; } = NativeLibrary.GetExport(NativeLibrary.Load(Threads, typeof(Native).Assembly, null), "JxlThreadParallelRunner");
}

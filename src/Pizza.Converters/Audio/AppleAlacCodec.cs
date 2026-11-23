using System.Runtime.InteropServices;

namespace Pizza.Converters.Audio;

/// <summary>ADR-041: carrega somente a DLL aprovada, por caminho absoluto, antes de MicrosoftSignedOnly.</summary>
public static class AppleAlacCodec
{
    private static readonly Lazy<Api?> Loaded = new(() =>
    {
        try { return new Api(); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { return null; }
    });

    public static bool Preload() => Loaded.Value is not null;
    internal static Api? Native => Loaded.Value;

    internal sealed unsafe class Api
    {
        internal readonly delegate* unmanaged[Cdecl]<uint, uint, uint, uint*, nint> Create;
        internal readonly delegate* unmanaged[Cdecl]<nint, byte*, int, byte*, uint, int> Encode;
        internal readonly delegate* unmanaged[Cdecl]<nint, byte*, uint, int> Config;
        internal readonly delegate* unmanaged[Cdecl]<nint, void> Destroy;

        internal Api()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Pizza.Alac.dll");
            var library = NativeLibrary.Load(path, typeof(AppleAlacCodec).Assembly,
                DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32);
            try
            {
                Create = (delegate* unmanaged[Cdecl]<uint, uint, uint, uint*, nint>)NativeLibrary.GetExport(library, "pizza_alac_create");
                Encode = (delegate* unmanaged[Cdecl]<nint, byte*, int, byte*, uint, int>)NativeLibrary.GetExport(library, "pizza_alac_encode");
                Config = (delegate* unmanaged[Cdecl]<nint, byte*, uint, int>)NativeLibrary.GetExport(library, "pizza_alac_config");
                Destroy = (delegate* unmanaged[Cdecl]<nint, void>)NativeLibrary.GetExport(library, "pizza_alac_destroy");
                // O módulo fica carregado até o Worker sair; nenhum ponteiro sobrevive a FreeLibrary.
            }
            catch { NativeLibrary.Free(library); throw; }
        }
    }
}

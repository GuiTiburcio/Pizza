using System.Runtime.InteropServices;
using System.Text;

namespace Pizza.Docs;

/// <summary>
/// O LibreOffice dentro deste processo, pelo LibreOfficeKit (ADR-034): sem soffice.bin e sem o canal único, que o
/// AppContainer não permite. Só as três chamadas de que o Pizza precisa: iniciar, abrir e salvar.
/// </summary>
internal static unsafe class LibreOfficeKit
{
    // Posições nas tabelas de funções de LibreOfficeKit.h (estáveis desde a versão 4.4).
    private const int OfficeDocumentLoad = 2;
    private const int DocumentDestroy = 1;
    private const int DocumentSaveAs = 2;

    private static nint* _office;

    /// <summary>Carrega o motor da pasta "program" e cria a instância com o perfil indicado.</summary>
    public static bool Start(string program, string profileUrl)
    {
        Environment.SetEnvironmentVariable("PATH", program + ";" + Environment.GetEnvironmentVariable("PATH"));
        SetDllDirectoryW(program);
        var module = LoadLibraryExW(Path.Combine(program, "mergedlo.dll"), 0, LoadWithAlteredSearchPath);
        var hook = module == 0 ? 0 : NativeLibrary.GetExport(module, "libreofficekit_hook_2");
        if (hook == 0)
        {
            return false;
        }

        fixed (byte* install = Utf8(program), profile = Utf8(profileUrl))
        {
            _office = ((delegate* unmanaged<byte*, byte*, nint*>)hook)(install, profile);
        }

        return _office != null;
    }

    /// <summary>Abre o documento e o salva em outro formato. Devolve nulo se der certo, ou o código de erro.</summary>
    public static string? Convert(string source, string target, string format)
    {
        var officeClass = (nint*)_office[0];
        nint* document;
        fixed (byte* url = Utf8(new Uri(source).AbsoluteUri))
        {
            document = ((delegate* unmanaged<nint*, byte*, nint*>)officeClass[OfficeDocumentLoad])(_office, url);
        }

        if (document == null)
        {
            return "CORRUPTED";
        }

        var documentClass = (nint*)document[0];
        int saved;
        fixed (byte* url = Utf8(new Uri(target).AbsoluteUri), filter = Utf8(format))
        {
            saved = ((delegate* unmanaged<nint*, byte*, byte*, byte*, int>)documentClass[DocumentSaveAs])(document, url, filter, null);
        }

        ((delegate* unmanaged<nint*, void>)documentClass[DocumentDestroy])(document);
        return saved != 0 && File.Exists(target) ? null : "CORRUPTED";
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");

    private const uint LoadWithAlteredSearchPath = 0x00000008;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryExW(string name, nint file, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectoryW(string path);
}

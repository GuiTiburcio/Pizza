using System.Runtime.InteropServices;
using Pizza.Core.Output;

namespace Pizza.App;

/// <summary>
/// Lixeira do Windows via SHFileOperation com FO_DELETE e FOF_ALLOWUNDO (nunca File.Delete). Roda no residente,
/// sem janela e sem diálogo. Só recebe caminhos de arquivos que o Pizza acabou de criar (<see cref="OutputUndo"/>).
/// </summary>
internal sealed unsafe partial class RecycleBin : IRecycleBin
{
    private const uint FoDelete = 0x3;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShFileOpStruct
    {
        public nint Hwnd;
        public uint Func;
        public char* From;
        public char* To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public nint NameMappings;
        public char* ProgressTitle;
    }

    public bool Recycle(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return false;
        }

        // pFrom: caminhos separados por NUL e terminados por duplo NUL.
        var list = string.Join('\0', paths) + "\0\0";
        fixed (char* from = list)
        {
            var operation = new ShFileOpStruct
            {
                Func = FoDelete,
                From = from,
                Flags = RecycleFlags.Delete,
            };
            return SHFileOperationW(&operation) == 0 && operation.AnyOperationsAborted == 0;
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHFileOperationW(ShFileOpStruct* operation);
}

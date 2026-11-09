using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static Pizza.App.Interop.Native;

namespace Pizza.App.Interop;

/// <summary>IDropTarget do OLE. O Explorer chama estes métodos enquanto o usuário arrasta por cima das nossas janelas.</summary>
[GeneratedComInterface]
[Guid("00000122-0000-0000-C000-000000000046")]
internal partial interface IDropTarget
{
    [PreserveSig]
    int DragEnter(nint dataObject, int keyState, POINT pt, ref int effect);

    [PreserveSig]
    int DragOver(int keyState, POINT pt, ref int effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(nint dataObject, int keyState, POINT pt, ref int effect);
}

/// <summary>Arquivos lidos de um arraste: o primeiro caminho e quantos são no total.</summary>
internal readonly record struct DraggedFiles(string First, int Count);

/// <summary>Recebe os eventos de arraste de uma janela do overlay e repassa para o <see cref="Overlay"/>.</summary>
/// <param name="acceptsDrop">
/// Verdadeiro para o disco (aceita o drop nas fatias, como cópia); falso para a camada que cobre a tela,
/// que recusa o drop para que soltar fora do menu nunca chegue ao Explorer (Parte 14.1).
/// </param>
[GeneratedComClass]
internal sealed partial class DropTarget(bool acceptsDrop) : IDropTarget
{
    private static readonly Guid IidDropTarget = new("00000122-0000-0000-C000-000000000046");
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public int DragEnter(nint dataObject, int keyState, POINT pt, ref int effect)
    {
        var files = ReadFiles(dataObject);
        Overlay.OnDragEnter(files, pt.X, pt.Y);
        return DragOver(keyState, pt, ref effect);
    }

    public int DragOver(int keyState, POINT pt, ref int effect)
    {
        // SEC-05: só cópia, e só com o cursor sobre uma fatia.
        effect = acceptsDrop && Overlay.OnDragOver(pt.X, pt.Y) ? effect & DROPEFFECT_COPY : DROPEFFECT_NONE;
        return S_OK;
    }

    public int DragLeave()
    {
        Overlay.OnDragLeave();
        return S_OK;
    }

    public int Drop(nint dataObject, int keyState, POINT pt, ref int effect)
    {
        if (acceptsDrop && Overlay.OnDrop(ReadFiles(dataObject), pt.X, pt.Y))
        {
            effect &= DROPEFFECT_COPY;
        }
        else
        {
            Overlay.OnDropOutside();
            effect = DROPEFFECT_NONE;
        }

        return S_OK;
    }

    /// <summary>Registra a janela como alvo de drop. O objeto precisa ficar vivo enquanto a janela existir.</summary>
    public void Register(nint hwnd)
    {
        var unknown = ComWrappers.GetOrCreateComInterfaceForObject(this, CreateComInterfaceFlags.None);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in IidDropTarget, out var dropTarget));
            try
            {
                Marshal.ThrowExceptionForHR(RegisterDragDrop(hwnd, dropTarget));
            }
            finally
            {
                Marshal.Release(dropTarget);
            }
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>Lê a lista de arquivos (CF_HDROP) do IDataObject. Devolve null se o arraste não for de arquivos.</summary>
    private static unsafe DraggedFiles? ReadFiles(nint dataObject)
    {
        if (dataObject == 0)
        {
            return null;
        }

        var format = new FORMATETC
        {
            CfFormat = CF_HDROP,
            DwAspect = DVASPECT_CONTENT,
            Lindex = -1,
            Tymed = TYMED_HGLOBAL,
        };
        STGMEDIUM medium;

        // IDataObject::GetData é o 4º método da vtable (depois de QueryInterface, AddRef e Release).
        var vtable = *(nint**)dataObject;
        var getData = (delegate* unmanaged[Stdcall]<nint, FORMATETC*, STGMEDIUM*, int>)vtable[3];
        if (getData(dataObject, &format, &medium) < 0)
        {
            return null;
        }

        try
        {
            // A origem do arraste é outro programa: não confiar que devolveu o tipo pedido.
            if (medium.Tymed != TYMED_HGLOBAL)
            {
                return null;
            }

            var count = DragQueryFileW(medium.HGlobal, uint.MaxValue, null, 0);
            if (count == 0)
            {
                return null;
            }

            var length = DragQueryFileW(medium.HGlobal, 0, null, 0);
            var buffer = new char[length + 1];
            fixed (char* p = buffer)
            {
                DragQueryFileW(medium.HGlobal, 0, p, length + 1);
            }

            return new DraggedFiles(new string(buffer, 0, (int)length), (int)count);
        }
        finally
        {
            ReleaseStgMedium(&medium);
        }
    }
}

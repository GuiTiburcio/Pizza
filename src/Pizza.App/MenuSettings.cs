using Pizza.Core;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>
/// ADR-037: fator de tamanho de todos os menus e posição dos painéis, em <c>%LOCALAPPDATA%\Pizza\menus.ini</c>
/// (PRIV-15). Só números e chaves fixas; arquivo ausente ou estranho volta ao padrão.
/// </summary>
internal static class MenuSettings
{
    private static readonly string FilePath = Path.Combine(ProductInfo.DataFolder, "menus.ini");
    private static MenuPrefs? _prefs;

    public static MenuPrefs Prefs => _prefs ??= Load();

    /// <summary>Fator do usuário, de 50% a 100% do teto de 1/15 da tela.</summary>
    public static double Scale
    {
        get => Prefs.Scale;
        set => Prefs.Scale = MenuScale.Clamp(value);
    }

    /// <summary>Dpi efetivo das janelas do Pizza nesta área útil: o menu inteiro com 1/15 dela, vezes o fator.</summary>
    public static int Dpi(RECT work) => MenuScale.EffectiveDpi(work.Right - work.Left, work.Bottom - work.Top, Scale);

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(ProductInfo.DataFolder);
            File.WriteAllText(FilePath, Prefs.Serialize());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferência que não grava nunca pode derrubar o residente; vale até fechar.
        }
    }

    private static MenuPrefs Load()
    {
        try
        {
            return File.Exists(FilePath) ? MenuPrefs.Parse(File.ReadAllText(FilePath)) : new MenuPrefs();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MenuPrefs();
        }
    }
}

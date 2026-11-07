namespace Pizza.Core;

/// <summary>Dados fixos do produto, usados na bandeja, nos logs e no "Sobre".</summary>
public static class ProductInfo
{
    public const string Name = "Pizza";

    /// <summary>Pasta de dados do usuário: <c>%LOCALAPPDATA%\Pizza</c> (Parte 5.7).</summary>
    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);
}

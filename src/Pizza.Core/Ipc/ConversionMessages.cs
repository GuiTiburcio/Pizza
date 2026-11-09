namespace Pizza.Core.Ipc;

/// <summary>Texto que o usuário vê para cada código de erro (SPECS §10.4).</summary>
public static class ConversionMessages
{
    public static string For(string code) => code switch
    {
        "CORRUPTED" => "O arquivo está corrompido ou não é do tipo esperado",
        "LOCKED" => "O arquivo está aberto em outro programa",
        "NO_SPACE" => "Não há espaço em disco suficiente",
        "NO_PERMISSION" => "Sem permissão para gravar nesta pasta",
        "LIMIT_EXCEEDED" => "O arquivo passa do limite permitido (tamanho, páginas ou pixels)",
        "TIMEOUT" => "A conversão demorou mais que o limite",
        "UNSUPPORTED" => "Este tipo de conversão não é suportado para este arquivo",
        _ => "O conversor parou inesperadamente; o original não foi alterado",
    };
}

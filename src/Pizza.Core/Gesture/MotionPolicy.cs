namespace Pizza.Core.Gesture;

/// <summary>Opção "Animações" do Pizza.</summary>
public enum AnimationSetting
{
    Always,
    FollowWindows,
    Never,
}

/// <summary>Como uma transição é mostrada de fato.</summary>
public enum MotionStyle
{
    Slide,

    /// <summary>Sem deslocamento, só troca suave de opacidade: é o "menos movimento" (Parte 2.4).</summary>
    Fade,

    Instant,
}

/// <summary>Decide o estilo da transição a partir da opção do usuário e do Windows.</summary>
public static class MotionPolicy
{
    public static MotionStyle Resolve(AnimationSetting setting, bool windowsAnimationsOn) => setting switch
    {
        AnimationSetting.Never => MotionStyle.Instant,
        AnimationSetting.FollowWindows when !windowsAnimationsOn => MotionStyle.Fade,
        _ => MotionStyle.Slide,
    };

    /// <summary>Lê o valor textual da opção. Qualquer valor desconhecido vale o padrão, "Sempre".</summary>
    public static AnimationSetting Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "windows" => AnimationSetting.FollowWindows,
        "never" => AnimationSetting.Never,
        _ => AnimationSetting.Always,
    };
}

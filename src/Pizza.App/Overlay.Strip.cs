using Pizza.Core.Conversions;
using Pizza.Core.Gesture;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Faixa de níveis acima do disco: nível sob o cursor, roda sobre a faixa e espera antes de trocar (ADR-027).</summary>
internal static unsafe partial class Overlay
{
    private const nuint TimerStrip = 8;

    // A caminho da faixa, o cursor cruza a fatia de cima. Só uma parada nela troca os níveis mostrados.
    private const uint StripDwellMs = 150;

    private static readonly QuickOptionsState Strip = new();
    private static int? _levelHover;

    private static IReadOnlyList<ConverterInfo> CurrentSlices()
    {
        var page = _pages[Carousel.Target];
        return _files is { } file ? RadialMenu.Slices(page.Tab, file.First, page.Turn) : [];
    }

    /// <summary>A faixa segue a fatia em hover: na hora, se ainda não mostra nada; senão, depois da espera.</summary>
    private static void PointStripAt(int slot)
    {
        var converter = CurrentSlices().ElementAtOrDefault(slot);
        if (converter is null || converter.Id == Strip.Converter?.Id)
        {
            KillTimer(Disc, TimerStrip);
            return;
        }

        if (Strip.Converter is null)
        {
            Strip.PointAt(converter);
            return;
        }

        SetTimer(Disc, TimerStrip, StripDwellMs, 0);
    }

    private static void OnStripTimer()
    {
        KillTimer(Disc, TimerStrip);
        if (_hoverSlot is { } slot && CurrentSlices().ElementAtOrDefault(slot) is { } converter)
        {
            Strip.PointAt(converter);
            Render();
        }
    }

    /// <summary>Nível sob o cursor, se a faixa estiver na tela.</summary>
    private static int? TargetLevel(int x, int y) =>
        _files is { Count: 1 } && IsVisible && _pulseSlot is null && Strip.Primary is { } primary
            ? DiscLayout.LevelAt((x - _left) / _scale, (y - _top) / _scale, primary.Choices.Count)
            : null;

    /// <summary>Soltar ou clicar num nível converte com ele, pela fatia dona da faixa.</summary>
    private static bool SelectLevel(string path, int level)
    {
        var slot = Strip.Converter is { } converter ? CurrentSlices().ToList().FindIndex(candidate => candidate.Id == converter.Id) : -1;
        if (slot < 0)
        {
            return false;
        }

        Strip.Step(level - Strip.Selected);
        return Select(path, slot);
    }

    /// <summary>Com o cursor sobre a faixa, a roda troca o nível; no resto do menu, continua trocando a aba.</summary>
    private static void OnWheel(int delta, int x, int y)
    {
        if (IsVisible && _pulseSlot is null)
        {
            if (Strip.Primary is not null && DiscLayout.InStrip((x - _left) / _scale, (y - _top) / _scale))
            {
                if (Strip.Wheel(delta))
                {
                    Render();
                }
            }
            else if (Carousel.OnWheel(delta))
            {
                SetHover(null);
                KillTimer(Disc, TimerStrip);
                Strip.Clear();
                if (_motion != MotionStyle.Slide)
                {
                    Carousel.Snap();
                }

                StartAnimation();
            }
        }

        KeepChoosing();
    }
}

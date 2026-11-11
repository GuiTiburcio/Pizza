using System.Diagnostics;
using Pizza.Core.Gesture;
using Pizza.Core.Notifications;
using Pizza.Core.Visual;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Alvo e movimento: fatia sob o cursor, giro na troca de aba, hover e animação (WF-2, FR-04).</summary>
internal static unsafe partial class Overlay
{
    /// <summary>Bolha de vidro das fatias, em posição de fatia (1,5 fica entre as fatias 1 e 2).</summary>
    private static readonly GlideSpring SliceGlide = new();

    /// <summary>Fatia sob o cursor, se houver uma disponível para o arquivo arrastado.</summary>
    private static int? TargetSlot(int x, int y)
    {
        // FR-30: por enquanto, um arquivo por vez.
        if (_files is not { Count: 1 } file || !IsVisible || _pulseSlot is not null)
        {
            return null;
        }

        // O alvo é a fatia como está desenhada: girada na troca de aba e, a em hover, maior.
        var (centerX, centerY) = DiscCenter();
        var growth = Hover.Select(transition => _motion == MotionStyle.Slide ? transition.Value : 0).ToArray();
        var slot = RadialMenu.SlotAt((x - centerX) / _scale, (y - centerY) / _scale, Rotation(), growth);
        var page = _pages[Carousel.Target];
        return slot < RadialMenu.Slices(page.Tab, file.First, page.Turn).Count ? slot : null;
    }

    private static double Rotation() =>
        _motion == MotionStyle.Slide ? -TabRotation * Carousel.DistanceTo(Carousel.Target) : 0;

    private static (int X, int Y) DiscCenter() =>
        (_left + (int)Math.Round(DiscLayout.CenterX * _scale), _top + (int)Math.Round(DiscLayout.CenterY * _scale));

    private static void SetHover(int? slot)
    {
        if (_hoverSlot is { } previous)
        {
            Move(Hover[previous], 0);
        }

        if (slot is { } next)
        {
            // ADR-027: a bolha nasce na fatia quando não está na tela; senão, desliza até ela, mesmo
            // passando pelo vão entre duas fatias.
            if (_motion != MotionStyle.Slide || (_hoverSlot is null && Hover.All(transition => transition.Value < 0.05)))
            {
                SliceGlide.Snap(next);
            }
            else
            {
                SliceGlide.RetargetOnRing(next, RadialMenu.SlotCount);
            }

            Move(Hover[next], 1);
            PointStripAt(next);
        }

        _hoverSlot = slot;
        StartAnimation();
    }

    private static void Move(CardTransition transition, double target)
    {
        if (_motion == MotionStyle.Instant)
        {
            transition.Snap(target);
        }
        else
        {
            transition.Retarget(target);
        }
    }

    private static void StartAnimation()
    {
        Render();

        // Já animando: só o destino muda, e o movimento em curso continua sem tranco.
        if (_lastFrame == 0 && IsAnimating())
        {
            _lastFrame = Stopwatch.GetTimestamp();
            SetTimer(Disc, TimerAnimation, AnimationMs, 0);
        }
    }

    private static bool IsAnimating() =>
        Carousel.IsAnimating || SliceGlide.IsAnimating || LevelGlide.IsAnimating || Hover.Any(transition => !transition.IsDone);

    private static void AnimationFrame()
    {
        // O tempo real decorrido, e não o intervalo do timer, mantém a velocidade constante.
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;

        Carousel.Advance(elapsedMs);
        SliceGlide.Advance(elapsedMs);
        LevelGlide.Advance(elapsedMs);
        foreach (var transition in Hover)
        {
            transition.Advance(elapsedMs);
        }

        Render();
        if (!IsAnimating())
        {
            KillTimer(Disc, TimerAnimation);
            _lastFrame = 0;
        }
    }
}

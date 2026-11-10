using Pizza.Core.Gesture;
using static Pizza.App.Interop.Native;

namespace Pizza.App;

/// <summary>Escolha por clique depois de soltar fora das fatias: foco, cliques e prazo sem interação (ADR-025).</summary>
internal static unsafe partial class Overlay
{
    private static void PrepareChoice()
    {
        if (Session.State != GestureState.Choosing)
        {
            return;
        }

        ShowWindow(_catcher, SW_HIDE);
        SetHover(null);
        OptionsPanel.TakeFocus(Disc);
        if (GetForegroundWindow() != Disc)
        {
            // Se o Windows recusar foco, não deixar um menu preso sem receber Esc.
            Hide();
            return;
        }

        KeepChoosing();
    }

    /// <summary>Clique durante a escolha, com qualquer botão: a fatia sob o cursor escolhe o formato.</summary>
    private static bool ChooseAt(int x, int y) =>
        Session.State == GestureState.Choosing && _files is { } file && TargetSlot(x, y) is { } slot && Select(file.First, slot);

    /// <summary>Qualquer interação na escolha (mouse, roda, teclado) reinicia o prazo até fechar.</summary>
    private static void KeepChoosing()
    {
        if (Session.State == GestureState.Choosing)
        {
            _choiceDeadline = Environment.TickCount64 + ChoiceIdleMs;
            if (Disc != 0)
            {
                SetTimer(Disc, TimerChoice, ChoiceIdleMs, 0);
            }
        }
    }

    private static void OnChoiceButton(uint message, int x, int y)
    {
        KeepChoosing();
        if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN)
        {
            _clickArmed = true;
        }
        else
        {
            if (_clickArmed)
            {
                ChooseAt(x, y);
            }

            _clickArmed = false;
        }
    }
}

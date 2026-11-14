using System.Diagnostics;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Core.Tests.Ipc;

public sealed class FinalizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pausa_para_o_relogio_e_permite_continuar_ou_cancelar(bool cancel)
    {
        var runner = new ConversionRunner("unused.exe", "unused", new TempJournal("unused.dat"), TimeSpan.FromMinutes(2));
        var control = new ConversionControl { Paused = true };
        using var cancellation = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var finishing = Task.Run(() => Record.Exception(() => runner.CheckFinalization(cancellation.Token, clock, control)));
        try
        {
            Assert.True(SpinWait.SpinUntil(() => !clock.IsRunning, TimeSpan.FromSeconds(5)));
            Assert.False(finishing.IsCompleted);
            if (cancel)
            {
                cancellation.Cancel();
            }
            else
            {
                control.Paused = false;
            }

            var error = await finishing.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel)
            {
                Assert.IsAssignableFrom<OperationCanceledException>(error);
            }
            else
            {
                Assert.Null(error);
                Assert.True(clock.IsRunning);
            }
        }
        finally
        {
            control.Paused = false;
            cancellation.Cancel();
            await finishing.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void Prazo_esgotado_nao_publica_mesmo_que_a_pausa_chegue_depois()
    {
        var runner = new ConversionRunner("unused.exe", "unused", new TempJournal("unused.dat"), TimeSpan.Zero);
        Assert.Throws<TimeoutException>(() => runner.CheckFinalization(default, Stopwatch.StartNew(), new ConversionControl { Paused = true }));
    }
}

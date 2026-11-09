using Pizza.Core.Gesture;

namespace Pizza.Core.Tests.Gesture;

public class MotionPolicyTests
{
    [Theory]
    [InlineData(AnimationSetting.Always, true, MotionStyle.Slide)]
    [InlineData(AnimationSetting.Always, false, MotionStyle.Slide)]
    [InlineData(AnimationSetting.FollowWindows, true, MotionStyle.Slide)]
    [InlineData(AnimationSetting.FollowWindows, false, MotionStyle.Fade)]
    [InlineData(AnimationSetting.Never, true, MotionStyle.Instant)]
    [InlineData(AnimationSetting.Never, false, MotionStyle.Instant)]
    public void Resolve_combina_a_opcao_com_o_Windows(AnimationSetting setting, bool windows, MotionStyle expected)
    {
        Assert.Equal(expected, MotionPolicy.Resolve(setting, windows));
    }

    [Theory]
    [InlineData(null, AnimationSetting.Always)]
    [InlineData("", AnimationSetting.Always)]
    [InlineData("qualquer", AnimationSetting.Always)]
    [InlineData("windows", AnimationSetting.FollowWindows)]
    [InlineData("NEVER", AnimationSetting.Never)]
    public void Parse_usa_Sempre_como_padrao(string? value, AnimationSetting expected)
    {
        Assert.Equal(expected, MotionPolicy.Parse(value));
    }
}

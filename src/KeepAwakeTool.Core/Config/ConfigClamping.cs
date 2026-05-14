namespace KeepAwakeTool.Core.Config;

public static class ConfigClamping
{
    public static AppConfig Sanitize(AppConfig input) => input with
    {
        Activity = input.Activity with
        {
            IntervalSeconds      = Clamp(input.Activity.IntervalSeconds, 10, 240),
            IdleThresholdSeconds = Clamp(input.Activity.IdleThresholdSeconds, 5, 120),
            Mouse = input.Activity.Mouse with
            {
                JigglePixels = Clamp(input.Activity.Mouse.JigglePixels, 1, 10)
            },
            Keystroke = input.Activity.Keystroke with
            {
                EveryNthCycle = Clamp(input.Activity.Keystroke.EveryNthCycle, 1, 10)
            }
        }
    };

    private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
}

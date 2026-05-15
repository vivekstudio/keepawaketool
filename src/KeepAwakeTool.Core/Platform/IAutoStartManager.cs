namespace KeepAwakeTool.Core.Platform;

public interface IAutoStartManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

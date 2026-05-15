namespace KeepAwakeTool.Core.Platform;

public interface IPowerManager
{
    void KeepSystemAwake(bool on);
    void ForceDisplayOff();
}

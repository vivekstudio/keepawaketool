using KeepAwakeTool.Core.Activity;

namespace KeepAwakeTool.Core.Platform;

public interface IInputSimulator
{
    void MoveMouse(MouseMode mode, int jigglePixels);
    void SendKey(VirtualKey key);
}

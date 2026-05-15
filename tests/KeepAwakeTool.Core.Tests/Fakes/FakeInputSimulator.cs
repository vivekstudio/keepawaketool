using System.Collections.Generic;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeInputSimulator : IInputSimulator
{
    public List<(MouseMode mode, int px)> MouseMoves { get; } = new();
    public List<VirtualKey> Keys { get; } = new();
    public void MoveMouse(MouseMode mode, int jigglePixels) => MouseMoves.Add((mode, jigglePixels));
    public void SendKey(VirtualKey key) => Keys.Add(key);
}

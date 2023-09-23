using Godot;

namespace BasicRaytracer.scripts;

public partial class FpsLabel : Label
{
    public override void _Process(double delta)
    {
        base._Process(delta);
        Text = "FPS: " + Engine.GetFramesPerSecond();
    }
}
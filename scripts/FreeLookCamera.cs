using Godot;

namespace BasicRaytracer.scripts;

public partial class FreeLookCamera : Camera3D
{

    [Export] public float MouseSensitivity = 1.0f;

    [Export] public float MoveSpeed = 0.1f;

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventMouseMotion)
        {
            var motionEvent = (InputEventMouseMotion) @event;
            if (Input.IsActionPressed("RMB"))
            {
                RotateY(Mathf.DegToRad(-motionEvent.Relative.X * MouseSensitivity));
                RotateObjectLocal(new Vector3(1.0f,0.0f,0.0f), Mathf.DegToRad(motionEvent.Relative.Y * MouseSensitivity));
            }
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        Input.MouseMode = Input.IsActionPressed("RMB") ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        _Move();
    }

    private void _Move()
    {
        var inputVector = Vector3.Zero;
        inputVector.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
        inputVector.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
        inputVector.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");

        if (inputVector.Length() > 1.0f)
            inputVector = inputVector.Normalized();

        // ReSharper disable once JoinDeclarationAndInitializer
        Vector3 displacement;
        displacement = GlobalTransform.Basis.Z * MoveSpeed * inputVector.Z;
        GlobalTranslate(displacement);
        
        displacement = GlobalTransform.Basis.X * MoveSpeed * inputVector.X;
        GlobalTranslate(displacement);
        
        displacement = GlobalTransform.Basis.Y * MoveSpeed * inputVector.Y;
        GlobalTranslate(-displacement);

    }
}
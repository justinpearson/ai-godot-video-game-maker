using Godot;
using System;

namespace TeaLeaves;

public partial class WreckingBall : RigidBody2D
{
    private float _ropeLength;
    private Func<Vector2>? _getCranePos;

    public void Init(float ropeLength, Func<Vector2> getCranePos)
    {
        _ropeLength = ropeLength;
        _getCranePos = getCranePos;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        if (_getCranePos == null) return;

        Vector2 cranePos = _getCranePos();
        Vector2 ballPos = state.Transform.Origin;
        Vector2 diff = ballPos - cranePos;
        float dist = diff.Length();

        if (dist > _ropeLength && dist > 0.001f)
        {
            Vector2 ropeDir = diff / dist;
            Vector2 tangent = new Vector2(-ropeDir.Y, ropeDir.X);

            // Clamp position to rope length
            Vector2 targetPos = cranePos + ropeDir * _ropeLength;
            Vector2 correction = targetPos - ballPos;

            state.Transform = new Transform2D(state.Transform.Rotation, targetPos);

            // Transfer tangential component of correction as velocity
            // This is how crane movement pumps energy into the pendulum
            Vector2 correctionVel = correction / (float)state.Step;
            float tangentialAdd = correctionVel.Dot(tangent);
            state.LinearVelocity += tangent * tangentialAdd;

            // Remove outward radial velocity (rope can't stretch)
            float radialVel = state.LinearVelocity.Dot(ropeDir);
            if (radialVel > 0)
                state.LinearVelocity -= ropeDir * radialVel;
        }
    }
}

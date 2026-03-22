using Godot;
using System.Collections.Generic;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class Robot : CharacterBody2D
    {
        [Export] public float MoveSpeed { get; set; } = 250f;
        [Export] public float AutoDriveSpeed { get; set; } = 300f;

        private float _bobTimer = 0f;
        private float _bobAmount = 0f;

        // Robot state
        private bool _isAutoDriving = false;
        private Vector2 _autoDriveTarget;
        private Node2D? _carriedBox = null;

        // Visual properties
        private float _bodyWidth = 40f;
        private float _bodyHeight = 50f;
        private float _trackWidth = 8f;
        private float _eyeRadius = 6f;
        private float _armLength = 16f;
        private float _armAngle = 0f;

        // Compactor animation
        private float _compactTimer = 0f;
        private bool _isCompacting = false;

        // Exhaust particles
        private struct ExhaustPuff
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
        }
        private List<ExhaustPuff> _exhaustPuffs = new();

        public bool IsAutoDriving => _isAutoDriving;

        public override void _Ready()
        {
        }

        public void StartAutoDrive(Vector2 target)
        {
            _isAutoDriving = true;
            _autoDriveTarget = target;
        }

        public void AttachBox(Node2D box)
        {
            _carriedBox = box;
            _isCompacting = true;
            _compactTimer = 0f;
        }

        public void DetachBox()
        {
            _carriedBox = null;
        }

        public void StopAutoDrive()
        {
            _isAutoDriving = false;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_isAutoDriving)
            {
                var direction = (_autoDriveTarget - GlobalPosition).Normalized();
                float distance = GlobalPosition.DistanceTo(_autoDriveTarget);

                if (distance < 20f)
                {
                    Velocity = Vector2.Zero;
                    _isAutoDriving = false;
                    return;
                }

                Velocity = direction * AutoDriveSpeed;
            }
            else
            {
                var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
                Velocity = input * MoveSpeed;
            }

            if (Velocity.Length() > 10f)
            {
                _bobTimer += dt * 12f;
                _bobAmount = Mathf.Sin(_bobTimer) * 2f;

                // Spawn exhaust puffs
                if (GD.Randf() < 0.3f)
                    SpawnExhaustPuff();
            }
            else
            {
                _bobAmount = Mathf.Lerp(_bobAmount, 0f, dt * 8f);
            }

            MoveAndSlide();

            // Update carried box position
            if (_carriedBox != null && IsInstanceValid(_carriedBox))
            {
                _carriedBox.GlobalPosition = GlobalPosition + new Vector2(0, -_bodyHeight * 0.3f);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Compact animation
            if (_isCompacting)
            {
                _compactTimer += dt;
                _armAngle = Mathf.Sin(_compactTimer * 8f) * 0.3f;
                if (_compactTimer > 0.5f)
                    _isCompacting = false;
            }
            else
            {
                _armAngle = Mathf.Lerp(_armAngle, 0f, dt * 4f);
            }

            // Update exhaust puffs
            for (int i = _exhaustPuffs.Count - 1; i >= 0; i--)
            {
                var p = _exhaustPuffs[i];
                p.Life -= dt;
                if (p.Life <= 0)
                {
                    _exhaustPuffs.RemoveAt(i);
                    continue;
                }
                p.Position += p.Velocity * dt;
                p.Size += dt * 8f;
                _exhaustPuffs[i] = p;
            }

            QueueRedraw();
        }

        private void SpawnExhaustPuff()
        {
            var rng = new RandomNumberGenerator();
            _exhaustPuffs.Add(new ExhaustPuff
            {
                Position = new Vector2(rng.RandfRange(-5f, 5f), _bodyHeight * 0.4f),
                Velocity = new Vector2(rng.RandfRange(-20f, 20f), rng.RandfRange(10f, 30f)),
                Life = 0.6f,
                MaxLife = 0.6f,
                Size = rng.RandfRange(3f, 6f),
            });
        }

        public override void _Draw()
        {
            float bob = _bobAmount;

            // --- Exhaust puffs (behind robot) ---
            foreach (var p in _exhaustPuffs)
            {
                float alpha = (p.Life / p.MaxLife) * 0.3f;
                DrawCircle(p.Position, p.Size, new Color(0.6f, 0.6f, 0.6f, alpha));
            }

            // --- Shadow ---
            DrawCircle(new Vector2(0, _bodyHeight * 0.35f), _bodyWidth * 0.6f, new Color(0, 0, 0, 0.15f));

            // --- Tracks (left and right) ---
            Color trackColor = new Color(0.25f, 0.25f, 0.25f);
            Color trackDetailColor = new Color(0.35f, 0.35f, 0.35f);

            // Left track
            var leftTrackRect = new Rect2(-_bodyWidth / 2f - _trackWidth, -_bodyHeight / 2f + bob, _trackWidth, _bodyHeight);
            DrawRect(leftTrackRect, trackColor);
            // Track treads
            for (int i = 0; i < 6; i++)
            {
                float y = -_bodyHeight / 2f + bob + i * (_bodyHeight / 6f) + 4f;
                DrawLine(
                    new Vector2(-_bodyWidth / 2f - _trackWidth, y),
                    new Vector2(-_bodyWidth / 2f, y),
                    trackDetailColor, 1.5f);
            }

            // Right track
            var rightTrackRect = new Rect2(_bodyWidth / 2f, -_bodyHeight / 2f + bob, _trackWidth, _bodyHeight);
            DrawRect(rightTrackRect, trackColor);
            for (int i = 0; i < 6; i++)
            {
                float y = -_bodyHeight / 2f + bob + i * (_bodyHeight / 6f) + 4f;
                DrawLine(
                    new Vector2(_bodyWidth / 2f, y),
                    new Vector2(_bodyWidth / 2f + _trackWidth, y),
                    trackDetailColor, 1.5f);
            }

            // --- Main body (boxy robot) ---
            Color bodyColor = new Color(0.85f, 0.72f, 0.35f); // Wall-E yellowish
            Color bodyDarkColor = new Color(0.7f, 0.58f, 0.25f);
            Color bodyLightColor = new Color(0.95f, 0.85f, 0.5f);

            var bodyRect = new Rect2(-_bodyWidth / 2f, -_bodyHeight / 2f + bob, _bodyWidth, _bodyHeight);
            DrawRect(bodyRect, bodyColor);

            // Body panel lines
            DrawLine(
                new Vector2(-_bodyWidth / 2f + 4f, -_bodyHeight / 2f + bob + 10f),
                new Vector2(_bodyWidth / 2f - 4f, -_bodyHeight / 2f + bob + 10f),
                bodyDarkColor, 1.5f);
            DrawLine(
                new Vector2(-_bodyWidth / 2f + 4f, _bodyHeight / 2f + bob - 10f),
                new Vector2(_bodyWidth / 2f - 4f, _bodyHeight / 2f + bob - 10f),
                bodyDarkColor, 1.5f);

            // Body highlight (top edge)
            DrawLine(
                new Vector2(-_bodyWidth / 2f, -_bodyHeight / 2f + bob),
                new Vector2(_bodyWidth / 2f, -_bodyHeight / 2f + bob),
                bodyLightColor, 2f);

            // --- Eyes (binocular-style on top) ---
            float eyeY = -_bodyHeight / 2f + bob - 8f;
            Color eyeHousingColor = new Color(0.5f, 0.5f, 0.5f);
            Color eyeWhiteColor = new Color(0.95f, 0.95f, 0.95f);
            Color pupilColor = new Color(0.15f, 0.35f, 0.6f);

            // Eye housings (cylinders from top)
            DrawCircle(new Vector2(-10f, eyeY), _eyeRadius + 3f, eyeHousingColor);
            DrawCircle(new Vector2(10f, eyeY), _eyeRadius + 3f, eyeHousingColor);

            // Connecting bar between eyes
            DrawLine(new Vector2(-10f, eyeY), new Vector2(10f, eyeY), eyeHousingColor, 4f);

            // Eye whites
            DrawCircle(new Vector2(-10f, eyeY), _eyeRadius, eyeWhiteColor);
            DrawCircle(new Vector2(10f, eyeY), _eyeRadius, eyeWhiteColor);

            // Pupils
            DrawCircle(new Vector2(-10f, eyeY + 1f), _eyeRadius * 0.5f, pupilColor);
            DrawCircle(new Vector2(10f, eyeY + 1f), _eyeRadius * 0.5f, pupilColor);

            // Eye highlights
            DrawCircle(new Vector2(-12f, eyeY - 2f), 2f, new Color(1, 1, 1, 0.7f));
            DrawCircle(new Vector2(8f, eyeY - 2f), 2f, new Color(1, 1, 1, 0.7f));

            // --- Arms (compactor arms) ---
            Color armColor = new Color(0.6f, 0.6f, 0.55f);
            float armY = bob;

            // Left arm
            var leftArmStart = new Vector2(-_bodyWidth / 2f - _trackWidth, armY);
            var leftArmEnd = leftArmStart + new Vector2(-_armLength, Mathf.Sin(_armAngle) * 10f);
            DrawLine(leftArmStart, leftArmEnd, armColor, 4f);
            DrawCircle(leftArmEnd, 4f, armColor);

            // Right arm
            var rightArmStart = new Vector2(_bodyWidth / 2f + _trackWidth, armY);
            var rightArmEnd = rightArmStart + new Vector2(_armLength, Mathf.Sin(-_armAngle) * 10f);
            DrawLine(rightArmStart, rightArmEnd, armColor, 4f);
            DrawCircle(rightArmEnd, 4f, armColor);

            // --- Carrying indicator ---
            if (_carriedBox != null && IsInstanceValid(_carriedBox))
            {
                // Draw a small icon above the robot showing it's carrying
                float indicatorY = -_bodyHeight / 2f + bob - 24f;
                DrawCircle(new Vector2(0, indicatorY), 4f, new Color(0.2f, 0.9f, 0.3f));
            }
        }
    }
}

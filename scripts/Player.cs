using Godot;

namespace TeaLeaves
{
    public partial class Player : CharacterBody2D
    {
        [Export] public float MoveSpeed { get; set; } = 300f;

        // Visual properties
        private float _bodyRadius = 20f;
        private float _bobAmount = 0f;
        private float _bobTimer = 0f;
        private Vector2 _facingDirection = Vector2.Down;

        public override void _PhysicsProcess(double delta)
        {
            var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            Velocity = input * MoveSpeed;

            if (input != Vector2.Zero)
            {
                _facingDirection = input.Normalized();
                _bobTimer += (float)delta * 10f;
                _bobAmount = Mathf.Sin(_bobTimer) * 3f;
            }
            else
            {
                _bobAmount = Mathf.Lerp(_bobAmount, 0f, (float)delta * 8f);
            }

            MoveAndSlide();
            QueueRedraw();
        }

        public override void _Draw()
        {
            var bobOffset = new Vector2(0, _bobAmount);

            // Shadow ellipse below the character
            var shadowCenter = new Vector2(0, _bodyRadius * 0.5f);
            DrawSetTransform(shadowCenter);
            DrawCircle(Vector2.Zero, _bodyRadius * 0.8f, new Color(0, 0, 0, 0.2f));
            DrawSetTransform(Vector2.Zero);

            // Body — bright blue circle
            var bodyCenter = new Vector2(0, 0) + bobOffset;
            var bodyColor = new Color(0.3f, 0.6f, 0.95f);
            DrawCircle(bodyCenter, _bodyRadius, bodyColor);

            // Body highlight — lighter blue arc on top-left for 3D feel
            var highlightCenter = bodyCenter + new Vector2(-4f, -5f);
            var highlightColor = new Color(0.55f, 0.8f, 1.0f, 0.6f);
            DrawCircle(highlightCenter, _bodyRadius * 0.45f, highlightColor);

            // Eyes — two white circles with black pupils
            float eyeSpacing = 7f;
            float eyeY = -4f;
            float eyeRadius = 5.5f;
            float pupilRadius = 2.5f;
            float pupilShift = 1.8f;

            var leftEyePos = bodyCenter + new Vector2(-eyeSpacing, eyeY);
            var rightEyePos = bodyCenter + new Vector2(eyeSpacing, eyeY);

            // Eye whites
            DrawCircle(leftEyePos, eyeRadius, Colors.White);
            DrawCircle(rightEyePos, eyeRadius, Colors.White);

            // Pupils follow facing direction
            var pupilOffset = _facingDirection * pupilShift;
            var pupilColor = new Color(0.15f, 0.15f, 0.25f);
            DrawCircle(leftEyePos + pupilOffset, pupilRadius, pupilColor);
            DrawCircle(rightEyePos + pupilOffset, pupilRadius, pupilColor);

            // Tiny white shine on each eye
            var shineOffset = new Vector2(-1.2f, -1.5f);
            DrawCircle(leftEyePos + shineOffset, 1.2f, new Color(1, 1, 1, 0.9f));
            DrawCircle(rightEyePos + shineOffset, 1.2f, new Color(1, 1, 1, 0.9f));

            // Pink blush circles on cheeks
            float blushY = 2f;
            float blushX = 12f;
            var blushColor = new Color(0.95f, 0.5f, 0.6f, 0.5f);
            DrawCircle(bodyCenter + new Vector2(-blushX, blushY), 4f, blushColor);
            DrawCircle(bodyCenter + new Vector2(blushX, blushY), 4f, blushColor);

            // Smile — small curved line below the eyes
            float smileY = 5f;
            var smileColor = new Color(0.15f, 0.15f, 0.25f);
            var smileCenter = bodyCenter + new Vector2(0, smileY);

            // Draw smile as a small arc using line segments
            int smileSegments = 8;
            float smileWidth = 6f;
            float smileHeight = 3f;
            for (int i = 0; i < smileSegments; i++)
            {
                float t0 = (float)i / smileSegments;
                float t1 = (float)(i + 1) / smileSegments;
                float angle0 = Mathf.Lerp(0f, Mathf.Pi, t0);
                float angle1 = Mathf.Lerp(0f, Mathf.Pi, t1);
                var p0 = smileCenter + new Vector2(Mathf.Cos(angle0) * smileWidth, Mathf.Sin(angle0) * smileHeight);
                var p1 = smileCenter + new Vector2(Mathf.Cos(angle1) * smileWidth, Mathf.Sin(angle1) * smileHeight);
                DrawLine(p0, p1, smileColor, 1.5f);
            }

            // Body outline for definition
            DrawArc(bodyCenter, _bodyRadius, 0, Mathf.Tau, 32, new Color(0.2f, 0.45f, 0.8f), 1.5f);
        }
    }
}

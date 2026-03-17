using Godot;
using System.Collections.Generic;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class Player : CharacterBody2D
    {
        [Export] public float MoveSpeed { get; set; } = 300f;

        private Sprite2D _sprite = null!;
        private float _bobAmount = 0f;
        private float _bobTimer = 0f;
        private Vector2 _facingDirection = Vector2.Down;

        // Horn sparkle particles
        private struct Sparkle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Life;
            public float MaxLife;
            public float Size;
        }

        private List<Sparkle> _sparkles = new();
        private bool _emitting = false;
        private float _emitTimer = 0f;
        private const float EmitDuration = 1.2f;

        // Rainbow colors for sparkles
        private static readonly Color[] RainbowColors = new[]
        {
            new Color(1f, 0.2f, 0.2f),     // red
            new Color(1f, 0.5f, 0.1f),      // orange
            new Color(1f, 0.9f, 0.1f),      // yellow
            new Color(0.2f, 0.9f, 0.3f),    // green
            new Color(0.2f, 0.6f, 1f),      // blue
            new Color(0.5f, 0.2f, 0.9f),    // indigo
            new Color(0.8f, 0.3f, 0.9f),    // violet
        };

        public override void _Ready()
        {
            _sprite = new Sprite2D();
            var texture = GD.Load<Texture2D>("res://assets/images/unicorn.png");
            _sprite.Texture = texture;

            // Scale the unicorn to a reasonable player size (~60px tall)
            float desiredHeight = 60f;
            float scaleRatio = desiredHeight / texture.GetHeight();
            _sprite.Scale = new Vector2(scaleRatio, scaleRatio);

            AddChild(_sprite);

            // Listen for correct answers
            EventBus.Instance!.CorrectAnswer += OnCorrectAnswer;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.CorrectAnswer -= OnCorrectAnswer;
        }

        private void OnCorrectAnswer(int _score)
        {
            _emitting = true;
            _emitTimer = 0f;
        }

        public override void _PhysicsProcess(double delta)
        {
            var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            Velocity = input * MoveSpeed;

            if (input != Vector2.Zero)
            {
                _facingDirection = input.Normalized();
                _bobTimer += (float)delta * 10f;
                _bobAmount = Mathf.Sin(_bobTimer) * 3f;

                // Flip sprite based on horizontal movement
                _sprite.FlipH = input.X < 0;
            }
            else
            {
                _bobAmount = Mathf.Lerp(_bobAmount, 0f, (float)delta * 8f);
            }

            _sprite.Position = new Vector2(0, _bobAmount);

            MoveAndSlide();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Emit sparkles from horn
            if (_emitting)
            {
                _emitTimer += dt;
                if (_emitTimer >= EmitDuration)
                    _emitting = false;

                // Spawn several sparkles per frame
                for (int i = 0; i < 4; i++)
                {
                    SpawnSparkle();
                }
            }

            // Update existing sparkles
            for (int i = _sparkles.Count - 1; i >= 0; i--)
            {
                var s = _sparkles[i];
                s.Life -= dt;
                if (s.Life <= 0)
                {
                    _sparkles.RemoveAt(i);
                    continue;
                }
                s.Position += s.Velocity * dt;
                s.Velocity += new Vector2(0, 40f) * dt; // gentle gravity
                _sparkles[i] = s;
            }

            if (_sparkles.Count > 0 || _emitting)
                QueueRedraw();
        }

        private void SpawnSparkle()
        {
            var rng = new RandomNumberGenerator();

            // Horn tip is roughly at top-right of the sprite when facing right
            // The unicorn faces right by default, horn is at upper area
            float hornX = _sprite.FlipH ? -12f : 12f;
            float hornY = -28f + _bobAmount;

            var sparkle = new Sparkle
            {
                Position = new Vector2(hornX + rng.RandfRange(-3f, 3f), hornY + rng.RandfRange(-3f, 3f)),
                Velocity = new Vector2(
                    rng.RandfRange(-80f, 80f),
                    rng.RandfRange(-160f, -40f)
                ),
                Color = RainbowColors[rng.RandiRange(0, RainbowColors.Length - 1)],
                Life = rng.RandfRange(0.5f, 1.0f),
                MaxLife = 1.0f,
                Size = rng.RandfRange(2f, 5f),
            };
            sparkle.MaxLife = sparkle.Life;
            _sparkles.Add(sparkle);
        }

        public override void _Draw()
        {
            // Draw sparkles
            foreach (var s in _sparkles)
            {
                float alpha = s.Life / s.MaxLife;
                var color = new Color(s.Color.R, s.Color.G, s.Color.B, alpha);
                DrawCircle(s.Position, s.Size * alpha, color);

                // Draw a small star/cross shape for extra sparkle
                float half = s.Size * alpha * 0.7f;
                var brightColor = new Color(1f, 1f, 1f, alpha * 0.6f);
                DrawLine(s.Position - new Vector2(half, 0), s.Position + new Vector2(half, 0), brightColor, 1.5f);
                DrawLine(s.Position - new Vector2(0, half), s.Position + new Vector2(0, half), brightColor, 1.5f);
            }
        }
    }
}

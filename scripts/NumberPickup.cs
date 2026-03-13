using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class NumberPickup : Area2D
    {
        public int Value { get; private set; }

        private float _bobTimer = 0f;
        private float _radius = 28f;
        private float _baseRadius = 28f;
        private Color _color = Colors.Orange;
        private Label _label = null!;
        private bool _isCorrectAnswer = false;

        // Pop animation state
        private bool _isPopping = false;
        private float _popTimer = 0f;
        private float _popDuration = 0.4f;

        // Confetti explosion state
        private bool _isExploding = false;
        private float _explodeTimer = 0f;
        private float _explodeDuration = 1.0f;
        private Vector2[] _confettiPositions = null!;
        private Vector2[] _confettiVelocities = null!;
        private Color[] _confettiColors = null!;
        private float[] _confettiSizes = null!;
        private const int ConfettiCount = 30;

        // Color palette for numbers (rainbow-ish, child-friendly)
        private static readonly Color[] NumberColors = {
            new Color(0.95f, 0.3f, 0.3f),   // Red
            new Color(0.95f, 0.6f, 0.2f),    // Orange
            new Color(0.95f, 0.85f, 0.2f),   // Yellow
            new Color(0.3f, 0.85f, 0.3f),    // Green
            new Color(0.3f, 0.7f, 0.95f),    // Blue
            new Color(0.6f, 0.4f, 0.95f),    // Purple
            new Color(0.95f, 0.4f, 0.7f),    // Pink
        };

        public void SetValue(int value, bool isCorrect = false)
        {
            Value = value;
            _isCorrectAnswer = isCorrect;
            _color = NumberColors[Mathf.Abs(value) % NumberColors.Length];

            if (_label != null)
            {
                _label.Text = value.ToString();
            }
        }

        public override void _Ready()
        {
            // Create the label for displaying the number
            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            _label.VerticalAlignment = VerticalAlignment.Center;
            _label.Text = Value.ToString();

            // Configure large, bold font via LabelSettings
            var settings = new LabelSettings();
            settings.FontSize = 34;
            settings.FontColor = Colors.White;
            settings.OutlineSize = 3;
            settings.OutlineColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            _label.LabelSettings = settings;

            // Size and position the label to be centered
            _label.Size = new Vector2(60, 44);
            _label.Position = new Vector2(-30, -22);

            AddChild(_label);

            // Connect body_entered signal
            BodyEntered += OnBodyEntered;

            // Randomize bob phase so numbers don't all bob in sync
            _bobTimer = (float)GD.RandRange(0, Mathf.Tau);
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is Player)
            {
                EventBus.Instance?.EmitNumberTouched(Value, this);
            }
        }

        public void PlayPop()
        {
            _isPopping = true;
            _popTimer = 0f;
            // Disable collision so it can't be touched again
            SetDeferred("monitoring", false);
        }

        public void PlayConfettiExplosion()
        {
            _isExploding = true;
            _explodeTimer = 0f;
            SetDeferred("monitoring", false);

            // Initialize confetti particles
            _confettiPositions = new Vector2[ConfettiCount];
            _confettiVelocities = new Vector2[ConfettiCount];
            _confettiColors = new Color[ConfettiCount];
            _confettiSizes = new float[ConfettiCount];

            Color[] palette = {
                new Color(1f, 0.2f, 0.3f),
                new Color(1f, 0.85f, 0.1f),
                new Color(0.2f, 0.9f, 0.3f),
                new Color(0.3f, 0.6f, 1f),
                new Color(1f, 0.5f, 0.1f),
                new Color(0.9f, 0.3f, 0.9f),
                new Color(0.3f, 1f, 0.9f),
            };

            for (int i = 0; i < ConfettiCount; i++)
            {
                _confettiPositions[i] = Vector2.Zero;
                float angle = (float)GD.RandRange(0, Mathf.Tau);
                float speed = (float)GD.RandRange(150, 400);
                _confettiVelocities[i] = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
                _confettiColors[i] = palette[i % palette.Length];
                _confettiSizes[i] = (float)GD.RandRange(3, 7);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_isPopping)
            {
                _popTimer += dt;
                float t = _popTimer / _popDuration;
                if (t >= 1f)
                {
                    QueueFree();
                    return;
                }
                // Swell up then shrink quickly
                if (t < 0.3f)
                    _radius = _baseRadius * (1f + t * 2f); // swell
                else
                    _radius = _baseRadius * 1.6f * (1f - (t - 0.3f) / 0.7f); // shrink to 0
                QueueRedraw();
                return;
            }

            if (_isExploding)
            {
                _explodeTimer += dt;
                if (_explodeTimer >= _explodeDuration)
                {
                    QueueFree();
                    return;
                }
                // Shrink the main circle quickly
                float shrinkT = Mathf.Clamp(_explodeTimer / 0.2f, 0f, 1f);
                _radius = _baseRadius * (1f - shrinkT);
                // Move confetti outward with gravity
                for (int i = 0; i < ConfettiCount; i++)
                {
                    _confettiPositions[i] += _confettiVelocities[i] * dt;
                    _confettiVelocities[i] += new Vector2(0, 300f) * dt; // gravity
                }
                QueueRedraw();
                return;
            }

            _bobTimer += dt * 2.5f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // Draw confetti particles if exploding
            if (_isExploding && _confettiPositions != null)
            {
                float alpha = 1f - Mathf.Clamp(_explodeTimer / _explodeDuration, 0f, 1f);
                for (int i = 0; i < ConfettiCount; i++)
                {
                    var c = _confettiColors[i];
                    DrawCircle(_confettiPositions[i], _confettiSizes[i], new Color(c, alpha));
                }
            }

            // Don't draw the bubble if fully shrunk
            if (_radius < 1f)
            {
                if (_label != null) _label.Visible = false;
                return;
            }

            float bobOffset = (_isPopping || _isExploding) ? 0f : Mathf.Sin(_bobTimer) * 3f;
            var center = new Vector2(0, bobOffset);

            // Pop animation: change color to white flash
            Color drawColor = _color;
            if (_isPopping)
            {
                float t = _popTimer / _popDuration;
                drawColor = _color.Lerp(Colors.White, t * 0.7f);
            }

            // Drop shadow
            var shadowCenter = new Vector2(0, _baseRadius * 0.4f + 2f);
            DrawCircle(shadowCenter, _radius * 0.7f, new Color(0, 0, 0, 0.15f));

            // Main circle
            DrawCircle(center, _radius, drawColor);

            // White border ring
            DrawArc(center, _radius, 0, Mathf.Tau, 32, Colors.White, 2.5f);

            // Inner lighter ring for depth
            DrawArc(center, _radius - 3f, 0, Mathf.Tau, 32, new Color(1, 1, 1, 0.25f), 1.5f);

            // Highlight/shine
            var shineCenter = center + new Vector2(-8f, -10f);
            DrawCircle(shineCenter, _radius * 0.35f, new Color(1, 1, 1, 0.35f));
            var brightSpot = center + new Vector2(-6f, -12f);
            DrawCircle(brightSpot, 3f, new Color(1, 1, 1, 0.5f));

            // Update label
            if (_label != null)
            {
                _label.Visible = !_isExploding || _radius > 5f;
                _label.Position = new Vector2(-30, -22 + bobOffset);
            }
        }
    }
}

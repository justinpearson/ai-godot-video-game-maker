using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class TrashBox : Area2D
    {
        public int Value { get; private set; }

        private float _bobTimer = 0f;
        private float _boxSize = 44f;
        private Color _color = Colors.Brown;
        private Label _label = null!;
        private bool _collected = false;

        // Shake animation for wrong answer
        private bool _isShaking = false;
        private float _shakeTimer = 0f;
        private float _shakeDuration = 0.4f;

        // Box colors (earthy/industrial)
        private static readonly Color[] BoxColors = {
            new Color(0.72f, 0.52f, 0.3f),   // Cardboard brown
            new Color(0.6f, 0.6f, 0.55f),    // Grey
            new Color(0.55f, 0.45f, 0.35f),  // Dark brown
            new Color(0.65f, 0.55f, 0.4f),   // Tan
            new Color(0.5f, 0.55f, 0.5f),    // Green-grey
            new Color(0.7f, 0.6f, 0.45f),    // Sandy
            new Color(0.58f, 0.5f, 0.42f),   // Warm grey
        };

        public void SetValue(int value)
        {
            Value = value;
            _color = BoxColors[Mathf.Abs(value) % BoxColors.Length];
            if (_label != null)
                _label.Text = value.ToString();
        }

        public override void _Ready()
        {
            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            _label.VerticalAlignment = VerticalAlignment.Center;
            _label.Text = Value.ToString();

            var settings = new LabelSettings();
            settings.FontSize = 32;
            settings.FontColor = Colors.White;
            settings.OutlineSize = 3;
            settings.OutlineColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            _label.LabelSettings = settings;

            _label.Size = new Vector2(60, 40);
            _label.Position = new Vector2(-30, -20);
            AddChild(_label);

            BodyEntered += OnBodyEntered;

            _bobTimer = (float)GD.RandRange(0, Mathf.Tau);
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is Robot && !_collected)
            {
                EventBus.Instance?.EmitBoxTouched(Value, this);
            }
        }

        public void Collect()
        {
            _collected = true;
            SetDeferred("monitoring", false);
            _label.Visible = false;
        }

        public void PlayShake()
        {
            _isShaking = true;
            _shakeTimer = 0f;
        }

        public bool IsCollected => _collected;

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_isShaking)
            {
                _shakeTimer += dt;
                if (_shakeTimer >= _shakeDuration)
                {
                    _isShaking = false;
                    Position = new Vector2(Mathf.Round(Position.X), Mathf.Round(Position.Y));
                }
            }

            _bobTimer += dt * 1.5f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_collected) return;

            float bobOffset = Mathf.Sin(_bobTimer) * 2f;
            float shakeOffset = 0f;
            if (_isShaking)
            {
                shakeOffset = Mathf.Sin(_shakeTimer * 40f) * 4f * (1f - _shakeTimer / _shakeDuration);
            }

            var center = new Vector2(shakeOffset, bobOffset);
            float half = _boxSize / 2f;

            // Shadow
            DrawCircle(new Vector2(0, half + 4f), _boxSize * 0.4f, new Color(0, 0, 0, 0.12f));

            // Main box body
            var boxRect = new Rect2(center.X - half, center.Y - half, _boxSize, _boxSize);
            DrawRect(boxRect, _color);

            // Box outline
            DrawRect(boxRect, new Color(0.3f, 0.25f, 0.2f), false, 2f);

            // Tape/cross lines on box (like a sealed package)
            Color tapeColor = new Color(0.85f, 0.8f, 0.6f, 0.6f);
            DrawLine(
                new Vector2(center.X, center.Y - half),
                new Vector2(center.X, center.Y + half),
                tapeColor, 3f);
            DrawLine(
                new Vector2(center.X - half, center.Y),
                new Vector2(center.X + half, center.Y),
                tapeColor, 3f);

            // Highlight on top-left
            DrawLine(
                new Vector2(center.X - half + 2f, center.Y - half + 2f),
                new Vector2(center.X - half + 2f, center.Y - half + 10f),
                new Color(1, 1, 1, 0.3f), 2f);

            // Update label position
            if (_label != null)
            {
                _label.Position = new Vector2(-30 + shakeOffset, -20 + bobOffset);
            }
        }
    }
}

using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class NumberPickup : Area2D
    {
        public int Value { get; private set; }

        private float _bobTimer = 0f;
        private float _radius = 28f;
        private Color _color = Colors.Orange;
        private Label _label = null!;
        private bool _isCorrectAnswer = false;

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
                EventBus.Instance?.EmitNumberTouched(Value);
            }
        }

        public override void _Process(double delta)
        {
            _bobTimer += (float)delta * 2.5f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float bobOffset = Mathf.Sin(_bobTimer) * 3f;
            var center = new Vector2(0, bobOffset);

            // Drop shadow — dark ellipse below
            var shadowCenter = new Vector2(0, _radius * 0.4f + 2f);
            DrawCircle(shadowCenter, _radius * 0.7f, new Color(0, 0, 0, 0.15f));

            // Filled circle with the number's color
            DrawCircle(center, _radius, _color);

            // White border ring
            DrawArc(center, _radius, 0, Mathf.Tau, 32, Colors.White, 2.5f);

            // Inner lighter ring for depth
            DrawArc(center, _radius - 3f, 0, Mathf.Tau, 32, new Color(1, 1, 1, 0.25f), 1.5f);

            // Highlight/shine on top-left for 3D-ish feel
            var shineCenter = center + new Vector2(-8f, -10f);
            var shineColor = new Color(1, 1, 1, 0.35f);
            DrawCircle(shineCenter, _radius * 0.35f, shineColor);

            // Smaller bright spot for extra shine
            var brightSpot = center + new Vector2(-6f, -12f);
            DrawCircle(brightSpot, 3f, new Color(1, 1, 1, 0.5f));

            // Update label position to follow bob
            if (_label != null)
            {
                _label.Position = new Vector2(-30, -22 + bobOffset);
            }
        }
    }
}

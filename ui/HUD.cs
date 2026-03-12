using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class HUD : CanvasLayer
    {
        private Label _problemLabel = null!;
        private Label _scoreLabel = null!;
        private Label _feedbackLabel = null!;
        private PanelContainer _topBar = null!;

        private float _feedbackTimer = 0f;
        private float _feedbackScale = 1f;
        private bool _showingFeedback = false;
        private Color _feedbackColor = Colors.White;

        public override void _Ready()
        {
            BuildTopBar();
            BuildFeedbackLabel();

            // Subscribe to EventBus
            EventBus.Instance!.NewProblem += OnNewProblem;
            EventBus.Instance!.CorrectAnswer += OnCorrectAnswer;
            EventBus.Instance!.WrongAnswer += OnWrongAnswer;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.NewProblem -= OnNewProblem;
                EventBus.Instance.CorrectAnswer -= OnCorrectAnswer;
                EventBus.Instance.WrongAnswer -= OnWrongAnswer;
            }
        }

        private void BuildTopBar()
        {
            // Top bar panel container
            _topBar = new PanelContainer();
            _topBar.Name = "TopBar";

            // Set anchors for full width at top
            _topBar.AnchorsPreset = (int)Control.LayoutPreset.TopWide;
            _topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _topBar.CustomMinimumSize = new Vector2(0, 80);

            // Dark semi-transparent background
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.2f, 0.85f);
            styleBox.ContentMarginLeft = 24;
            styleBox.ContentMarginRight = 24;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.CornerRadiusBottomLeft = 8;
            styleBox.CornerRadiusBottomRight = 8;
            _topBar.AddThemeStyleboxOverride("panel", styleBox);

            // HBoxContainer for layout
            var hbox = new HBoxContainer();
            hbox.Name = "HBox";
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Problem label (centered, large)
            _problemLabel = new Label();
            _problemLabel.Name = "ProblemLabel";
            _problemLabel.Text = "? + ? = ?";
            _problemLabel.AddThemeFontSizeOverride("font_size", 48);
            _problemLabel.AddThemeColorOverride("font_color", Colors.White);
            _problemLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _problemLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Score label (right-aligned)
            _scoreLabel = new Label();
            _scoreLabel.Name = "ScoreLabel";
            _scoreLabel.Text = "Score: 0";
            _scoreLabel.AddThemeFontSizeOverride("font_size", 36);
            _scoreLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f)); // gold
            _scoreLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _scoreLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            _scoreLabel.CustomMinimumSize = new Vector2(200, 0);

            // A small star icon before score (using unicode)
            var starLabel = new Label();
            starLabel.Name = "Star";
            starLabel.Text = "\u2b50";
            starLabel.AddThemeFontSizeOverride("font_size", 32);
            starLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;

            hbox.AddChild(_problemLabel);
            hbox.AddChild(starLabel);
            hbox.AddChild(_scoreLabel);

            _topBar.AddChild(hbox);
            AddChild(_topBar);
        }

        private void BuildFeedbackLabel()
        {
            // Container to center the feedback label on screen
            var centerContainer = new CenterContainer();
            centerContainer.Name = "FeedbackCenter";
            centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;

            _feedbackLabel = new Label();
            _feedbackLabel.Name = "FeedbackLabel";
            _feedbackLabel.Text = "";
            _feedbackLabel.AddThemeFontSizeOverride("font_size", 64);
            _feedbackLabel.AddThemeColorOverride("font_color", Colors.White);
            _feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _feedbackLabel.VerticalAlignment = VerticalAlignment.Center;
            _feedbackLabel.Visible = false;
            _feedbackLabel.PivotOffset = new Vector2(200, 40); // approximate center for scale
            _feedbackLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            // Add a shadow effect using a duplicate label behind
            var shadowLabel = new Label();
            shadowLabel.Name = "FeedbackShadow";
            shadowLabel.AddThemeFontSizeOverride("font_size", 64);
            shadowLabel.AddThemeColorOverride("font_color", new Color(0, 0, 0, 0.4f));
            shadowLabel.HorizontalAlignment = HorizontalAlignment.Center;
            shadowLabel.VerticalAlignment = VerticalAlignment.Center;
            shadowLabel.Position = new Vector2(3, 3);
            shadowLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            centerContainer.AddChild(_feedbackLabel);
            AddChild(centerContainer);
        }

        private void OnNewProblem(int a, int b, int answer)
        {
            _problemLabel.Text = $"{a} + {b} = ?";
        }

        private void OnCorrectAnswer(int newScore)
        {
            _scoreLabel.Text = $"Score: {newScore}";
            ShowFeedback("CORRECT! +10", new Color(0.2f, 0.9f, 0.3f));
        }

        private void OnWrongAnswer()
        {
            ShowFeedback("Try again!", new Color(0.95f, 0.3f, 0.3f));
        }

        private void ShowFeedback(string text, Color color)
        {
            _feedbackLabel.Text = text;
            _feedbackLabel.Visible = true;
            _feedbackColor = color;
            _feedbackTimer = 1.5f;
            _feedbackScale = 1.5f;
            _showingFeedback = true;

            // Reset modulate
            _feedbackLabel.Modulate = new Color(color, 1f);
            _feedbackLabel.Scale = Vector2.One * 1.5f;

            // Update pivot to approximate center of text
            _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;
        }

        public override void _Process(double delta)
        {
            if (!_showingFeedback)
                return;

            _feedbackTimer -= (float)delta;

            if (_feedbackTimer <= 0f)
            {
                _showingFeedback = false;
                _feedbackLabel.Visible = false;
                return;
            }

            // Phase 1 (1.5 -> 1.0): scale from 1.5 down to 1.0, full opacity
            // Phase 2 (1.0 -> 0.0): hold at scale 1.0, fade alpha out
            if (_feedbackTimer > 1.0f)
            {
                // Scaling phase
                float t = (_feedbackTimer - 1.0f) / 0.5f; // 1.0 -> 0.0 as timer goes 1.5 -> 1.0
                _feedbackScale = Mathf.Lerp(1.0f, 1.5f, t);
                _feedbackLabel.Scale = Vector2.One * _feedbackScale;
                _feedbackLabel.Modulate = new Color(_feedbackColor, 1f);
            }
            else
            {
                // Fade phase
                float alpha = Mathf.Clamp(_feedbackTimer / 1.0f, 0f, 1f);
                _feedbackLabel.Scale = Vector2.One;
                _feedbackLabel.Modulate = new Color(_feedbackColor, alpha);
            }

            // Keep pivot centered
            _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;
        }
    }
}

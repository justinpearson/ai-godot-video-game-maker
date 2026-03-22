using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class HUD : CanvasLayer
    {
        private Label _problemLabel = null!;
        private Label _scoreLabel = null!;
        private Label _feedbackLabel = null!;
        private Label _progressLabel = null!;
        private PanelContainer _topBar = null!;

        // Celebration UI
        private Label _celebrateLabel = null!;
        private Label _medalLabel = null!;
        private bool _showingCelebration = false;
        private float _celebrateTimer = 0f;

        private float _feedbackTimer = 0f;
        private bool _showingFeedback = false;
        private Color _feedbackColor = Colors.White;

        private int _totalBoxes = 8;
        private int _dumped = 0;

        public override void _Ready()
        {
            BuildTopBar();
            BuildFeedbackLabel();
            BuildCelebrationUI();

            EventBus.Instance!.NewProblem += OnNewProblem;
            EventBus.Instance!.CorrectAnswer += OnCorrectAnswer;
            EventBus.Instance!.WrongAnswer += OnWrongAnswer;
            EventBus.Instance!.BoxDumped += OnBoxDumped;
            EventBus.Instance!.AllBoxesCollected += OnAllBoxesCollected;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.NewProblem -= OnNewProblem;
                EventBus.Instance.CorrectAnswer -= OnCorrectAnswer;
                EventBus.Instance.WrongAnswer -= OnWrongAnswer;
                EventBus.Instance.BoxDumped -= OnBoxDumped;
                EventBus.Instance.AllBoxesCollected -= OnAllBoxesCollected;
            }
        }

        private void BuildTopBar()
        {
            _topBar = new PanelContainer();
            _topBar.Name = "TopBar";
            _topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _topBar.CustomMinimumSize = new Vector2(0, 90);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            styleBox.ContentMarginLeft = 24;
            styleBox.ContentMarginRight = 24;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.CornerRadiusBottomLeft = 12;
            styleBox.CornerRadiusBottomRight = 12;
            _topBar.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.Name = "VBox";
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var hbox = new HBoxContainer();
            hbox.Name = "HBox";
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Problem label
            _problemLabel = new Label();
            _problemLabel.Name = "ProblemLabel";
            _problemLabel.Text = "? + ? = ?";
            _problemLabel.AddThemeFontSizeOverride("font_size", 44);
            _problemLabel.AddThemeColorOverride("font_color", Colors.White);
            _problemLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _problemLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Score label
            _scoreLabel = new Label();
            _scoreLabel.Name = "ScoreLabel";
            _scoreLabel.Text = "Score: 0";
            _scoreLabel.AddThemeFontSizeOverride("font_size", 32);
            _scoreLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.35f));
            _scoreLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _scoreLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            _scoreLabel.CustomMinimumSize = new Vector2(180, 0);

            hbox.AddChild(_problemLabel);
            hbox.AddChild(_scoreLabel);

            // Progress bar label
            _progressLabel = new Label();
            _progressLabel.Name = "ProgressLabel";
            _progressLabel.Text = "Boxes: 0 / 8";
            _progressLabel.AddThemeFontSizeOverride("font_size", 22);
            _progressLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.65f));
            _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;

            vbox.AddChild(hbox);
            vbox.AddChild(_progressLabel);

            _topBar.AddChild(vbox);
            AddChild(_topBar);
        }

        private void BuildFeedbackLabel()
        {
            var centerContainer = new CenterContainer();
            centerContainer.Name = "FeedbackCenter";
            centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;

            _feedbackLabel = new Label();
            _feedbackLabel.Name = "FeedbackLabel";
            _feedbackLabel.Text = "";
            _feedbackLabel.AddThemeFontSizeOverride("font_size", 56);
            _feedbackLabel.AddThemeColorOverride("font_color", Colors.White);
            _feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _feedbackLabel.VerticalAlignment = VerticalAlignment.Center;
            _feedbackLabel.Visible = false;
            _feedbackLabel.PivotOffset = new Vector2(200, 40);
            _feedbackLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            centerContainer.AddChild(_feedbackLabel);
            AddChild(centerContainer);
        }

        private void BuildCelebrationUI()
        {
            var centerContainer = new CenterContainer();
            centerContainer.Name = "CelebrateCenter";
            centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

            _medalLabel = new Label();
            _medalLabel.Name = "MedalLabel";
            _medalLabel.Text = "GOLD STAR!";
            _medalLabel.AddThemeFontSizeOverride("font_size", 96);
            _medalLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
            _medalLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _medalLabel.Visible = false;
            _medalLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            _celebrateLabel = new Label();
            _celebrateLabel.Name = "CelebrateLabel";
            _celebrateLabel.Text = "ALL TRASH COLLECTED!";
            _celebrateLabel.AddThemeFontSizeOverride("font_size", 48);
            _celebrateLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.95f, 0.4f));
            _celebrateLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _celebrateLabel.Visible = false;
            _celebrateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            vbox.AddChild(_medalLabel);
            vbox.AddChild(_celebrateLabel);
            centerContainer.AddChild(vbox);
            AddChild(centerContainer);
        }

        private void OnNewProblem(int a, int b, int answer)
        {
            _problemLabel.Text = $"{a} + {b} = ?";
        }

        private void OnCorrectAnswer(int newScore)
        {
            _scoreLabel.Text = $"Score: {newScore}";
            ShowFeedback("BEEP BOOP! +10", new Color(0.2f, 0.9f, 0.3f));
        }

        private void OnWrongAnswer()
        {
            ShowFeedback("Wrong box!", new Color(0.95f, 0.3f, 0.3f));
        }

        private void OnBoxDumped(int value, int totalDumped, int totalBoxes)
        {
            _dumped = totalDumped;
            _totalBoxes = totalBoxes;
            _progressLabel.Text = $"Boxes: {totalDumped} / {totalBoxes}";
        }

        private void OnAllBoxesCollected()
        {
            _showingCelebration = true;
            _celebrateTimer = 0f;
            _problemLabel.Text = "COMPLETE!";
        }

        private void ShowFeedback(string text, Color color)
        {
            _feedbackLabel.Text = text;
            _feedbackLabel.Visible = true;
            _feedbackColor = color;
            _feedbackTimer = 1.5f;
            _showingFeedback = true;

            _feedbackLabel.Modulate = new Color(color, 1f);
            _feedbackLabel.Scale = Vector2.One * 1.5f;
            _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_showingFeedback)
            {
                _feedbackTimer -= dt;
                if (_feedbackTimer <= 0f)
                {
                    _showingFeedback = false;
                    _feedbackLabel.Visible = false;
                }
                else if (_feedbackTimer > 1.0f)
                {
                    float t = (_feedbackTimer - 1.0f) / 0.5f;
                    float scale = Mathf.Lerp(1.0f, 1.5f, t);
                    _feedbackLabel.Scale = Vector2.One * scale;
                    _feedbackLabel.Modulate = new Color(_feedbackColor, 1f);
                }
                else
                {
                    float alpha = Mathf.Clamp(_feedbackTimer / 1.0f, 0f, 1f);
                    _feedbackLabel.Scale = Vector2.One;
                    _feedbackLabel.Modulate = new Color(_feedbackColor, alpha);
                }
                _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;
            }

            if (_showingCelebration)
            {
                _celebrateTimer += dt;

                if (_celebrateTimer > 0.5f)
                {
                    _medalLabel.Visible = true;
                    _celebrateLabel.Visible = true;

                    // Pulsing scale on medal
                    float pulse = 1f + Mathf.Sin(_celebrateTimer * 3f) * 0.1f;
                    _medalLabel.Scale = Vector2.One * pulse;
                    _medalLabel.PivotOffset = _medalLabel.Size / 2f;
                }
            }
        }
    }
}

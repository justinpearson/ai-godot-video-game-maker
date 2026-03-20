using Godot;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class HUD : CanvasLayer
    {
        private Label _scoreLabel = null!;
        private Label _blocksLabel = null!;
        private Label _instructionsLabel = null!;
        private Label _feedbackLabel = null!;
        private PanelContainer _topBar = null!;

        private float _feedbackTimer;
        private bool _showingFeedback;
        private bool _gameWon;

        public override void _Ready()
        {
            BuildTopBar();
            BuildInstructions();
            BuildFeedbackLabel();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.BlocksUpdate += OnBlocksUpdate;
                EventBus.Instance.GameWon += OnGameWon;
            }
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.BlocksUpdate -= OnBlocksUpdate;
                EventBus.Instance.GameWon -= OnGameWon;
            }
        }

        private void BuildTopBar()
        {
            _topBar = new PanelContainer();
            _topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _topBar.CustomMinimumSize = new Vector2(0, 70);

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.15f, 0.80f),
                ContentMarginLeft = 32,
                ContentMarginRight = 32,
                ContentMarginTop = 10,
                ContentMarginBottom = 10,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
            };
            _topBar.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            // Blocks remaining (left)
            _blocksLabel = new Label
            {
                Text = "Blocks: ? / ?",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _blocksLabel.AddThemeFontSizeOverride("font_size", 32);
            _blocksLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));

            // Title (center)
            var titleLabel = new Label
            {
                Text = "WRECKING BALL",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 38);
            titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));

            // Score (right)
            _scoreLabel = new Label
            {
                Text = "Score: 0",
                HorizontalAlignment = HorizontalAlignment.Right,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _scoreLabel.AddThemeFontSizeOverride("font_size", 32);
            _scoreLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.4f));

            hbox.AddChild(_blocksLabel);
            hbox.AddChild(titleLabel);
            hbox.AddChild(_scoreLabel);
            _topBar.AddChild(hbox);
            AddChild(_topBar);
        }

        private void BuildInstructions()
        {
            _instructionsLabel = new Label
            {
                Text = "Arrows: move crane    |    Click+Drag: move bricks    |    R: restart",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _instructionsLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _instructionsLabel.OffsetTop = -50;
            _instructionsLabel.AddThemeFontSizeOverride("font_size", 24);
            _instructionsLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
            AddChild(_instructionsLabel);
        }

        private void BuildFeedbackLabel()
        {
            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            center.MouseFilter = Control.MouseFilterEnum.Ignore;

            _feedbackLabel = new Label
            {
                Text = "",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _feedbackLabel.AddThemeFontSizeOverride("font_size", 80);

            center.AddChild(_feedbackLabel);
            AddChild(center);
        }

        private void OnBlocksUpdate(int remaining, int total, int score)
        {
            _blocksLabel.Text = $"Blocks: {remaining} / {total}";
            _scoreLabel.Text = $"Score: {score}";
        }

        private void OnGameWon(int finalScore)
        {
            if (_gameWon) return;
            _gameWon = true;

            _feedbackLabel.Text = $"DEMOLISHED!\nScore: {finalScore}";
            _feedbackLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
            _feedbackLabel.Visible = true;
            _feedbackLabel.Scale = Vector2.One * 1.5f;
            _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;
            _showingFeedback = true;
            _feedbackTimer = 4f;
        }

        public override void _Process(double delta)
        {
            if (!_showingFeedback) return;

            _feedbackTimer -= (float)delta;
            _feedbackLabel.PivotOffset = _feedbackLabel.Size / 2f;

            if (_feedbackTimer > 3f)
            {
                float t = (_feedbackTimer - 3f);
                _feedbackLabel.Scale = Vector2.One * Mathf.Lerp(1f, 1.5f, t);
            }
            else
            {
                _feedbackLabel.Scale = Vector2.One;
            }

            if (_feedbackTimer <= 0f)
            {
                _showingFeedback = false;
                // Keep the message visible after win
            }
        }
    }
}

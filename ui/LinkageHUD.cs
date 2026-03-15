using Godot;
using System;

namespace TeaLeaves
{
    /// <summary>
    /// HUD for the Mechanical Linkage Drawing Simulator.
    /// Toolbar, playback controls, element info.
    /// </summary>
    public partial class LinkageHUD : CanvasLayer
    {
        [Export] public NodePath CanvasPath { get; set; } = null!;

        private LinkageCanvas _canvas = null!;

        // Toolbar buttons
        private Button _selectBtn = null!;
        private Button _wheelBtn = null!;
        private Button _rodBtn = null!;
        private Button _pivotBtn = null!;
        private Button _penBtn = null!;
        private Button _connectBtn = null!;

        // Action buttons
        private Button _playBtn = null!;
        private Button _stopBtn = null!;
        private Button _clearTraceBtn = null!;
        private Button _deleteBtn = null!;
        private Button _deleteAllBtn = null!;
        private Button _demoBtn = null!;

        // Speed control
        private HSlider _speedSlider = null!;
        private Label _speedLabel = null!;

        // Info
        private Label _statusLabel = null!;
        private Label _infoLabel = null!;

        // Styling
        private static readonly Color PanelBg = new(0.15f, 0.15f, 0.2f, 0.9f);
        private static readonly Color BtnNormal = new(0.25f, 0.25f, 0.3f);
        private static readonly Color BtnHover = new(0.35f, 0.35f, 0.4f);
        private static readonly Color BtnActive = new(0.3f, 0.5f, 0.8f);
        private static readonly Color TextColor = new(0.9f, 0.9f, 0.95f);

        public override void _Ready()
        {
            _canvas = GetNode<LinkageCanvas>(CanvasPath);

            BuildToolbar();
            BuildPlaybackControls();
            BuildInfoPanel();
            UpdateToolHighlight();

            // Subscribe to events
            if (EventBus.Instance != null)
            {
                EventBus.Instance.SelectionChanged += OnSelectionChanged;
                EventBus.Instance.ToolChanged += OnToolChanged;
                EventBus.Instance.PlaybackToggled += OnPlaybackToggled;
            }
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.SelectionChanged -= OnSelectionChanged;
                EventBus.Instance.ToolChanged -= OnToolChanged;
                EventBus.Instance.PlaybackToggled -= OnPlaybackToggled;
            }
        }

        public override void _Process(double delta)
        {
            UpdateStatusLabel();
        }

        private void BuildToolbar()
        {
            var panel = new PanelContainer();
            panel.Position = new Vector2(10, 10);
            panel.Size = new Vector2(620, 56);

            var style = new StyleBoxFlat();
            style.BgColor = PanelBg;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            var titleLabel = new Label();
            titleLabel.Text = "TOOLS";
            titleLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
            titleLabel.AddThemeFontSizeOverride("font_size", 13);
            hbox.AddChild(titleLabel);

            var sep = new VSeparator();
            sep.CustomMinimumSize = new Vector2(2, 0);
            hbox.AddChild(sep);

            _selectBtn = CreateToolButton("Select", () => _canvas.SetTool(LinkageTool.Select));
            _wheelBtn = CreateToolButton("Wheel", () => _canvas.SetTool(LinkageTool.Wheel));
            _rodBtn = CreateToolButton("Rod", () => _canvas.SetTool(LinkageTool.Rod));
            _pivotBtn = CreateToolButton("Pivot", () => _canvas.SetTool(LinkageTool.Pivot));
            _penBtn = CreateToolButton("Pen", () => _canvas.SetTool(LinkageTool.Pen));
            _connectBtn = CreateToolButton("Connect", () => _canvas.SetTool(LinkageTool.Connect));

            hbox.AddChild(_selectBtn);
            hbox.AddChild(_wheelBtn);
            hbox.AddChild(_rodBtn);
            hbox.AddChild(_pivotBtn);
            hbox.AddChild(_penBtn);
            hbox.AddChild(_connectBtn);

            panel.AddChild(hbox);
            AddChild(panel);
        }

        private void BuildPlaybackControls()
        {
            var panel = new PanelContainer();
            panel.Position = new Vector2(10, 76);
            panel.Size = new Vector2(620, 56);

            var style = new StyleBoxFlat();
            style.BgColor = PanelBg;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            _playBtn = CreateActionButton("Play", OnPlayPressed);
            _stopBtn = CreateActionButton("Stop", OnStopPressed);
            _clearTraceBtn = CreateActionButton("Clear Drawing", () => _canvas.ClearTrace());

            var sep1 = new VSeparator();
            sep1.CustomMinimumSize = new Vector2(2, 0);

            _speedLabel = new Label();
            _speedLabel.Text = "Speed: 1.0x";
            _speedLabel.AddThemeColorOverride("font_color", TextColor);
            _speedLabel.AddThemeFontSizeOverride("font_size", 14);
            _speedLabel.CustomMinimumSize = new Vector2(100, 0);

            _speedSlider = new HSlider();
            _speedSlider.MinValue = 0.1;
            _speedSlider.MaxValue = 3.0;
            _speedSlider.Step = 0.1;
            _speedSlider.Value = 1.0;
            _speedSlider.CustomMinimumSize = new Vector2(120, 0);
            _speedSlider.ValueChanged += OnSpeedChanged;

            hbox.AddChild(_playBtn);
            hbox.AddChild(_stopBtn);
            hbox.AddChild(_clearTraceBtn);
            hbox.AddChild(sep1);
            hbox.AddChild(_speedLabel);
            hbox.AddChild(_speedSlider);

            panel.AddChild(hbox);
            AddChild(panel);
        }

        private void BuildInfoPanel()
        {
            // Bottom-left action buttons
            var bottomPanel = new PanelContainer();
            bottomPanel.Position = new Vector2(10, 1020);
            bottomPanel.Size = new Vector2(500, 50);

            var style = new StyleBoxFlat();
            style.BgColor = PanelBg;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            bottomPanel.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            _deleteBtn = CreateActionButton("Delete Selected", OnDeletePressed);
            _deleteAllBtn = CreateActionButton("Delete All", OnDeleteAllPressed);
            _demoBtn = CreateActionButton("Spirograph Demo", OnDemoPressed);

            var sep = new VSeparator();
            sep.CustomMinimumSize = new Vector2(2, 0);

            _statusLabel = new Label();
            _statusLabel.AddThemeColorOverride("font_color", TextColor);
            _statusLabel.AddThemeFontSizeOverride("font_size", 13);

            hbox.AddChild(_deleteBtn);
            hbox.AddChild(_deleteAllBtn);
            hbox.AddChild(sep);
            hbox.AddChild(_demoBtn);

            bottomPanel.AddChild(hbox);
            AddChild(bottomPanel);

            // Top-right info
            _infoLabel = new Label();
            _infoLabel.Position = new Vector2(660, 16);
            _infoLabel.Size = new Vector2(500, 120);
            _infoLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.4f));
            _infoLabel.AddThemeFontSizeOverride("font_size", 13);
            _infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(_infoLabel);

            UpdateInfoText();
        }

        private Button CreateToolButton(string text, Action action)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(72, 36);
            StyleButton(btn, false);
            btn.Pressed += action;
            return btn;
        }

        private Button CreateActionButton(string text, Action action)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(80, 32);
            StyleButton(btn, false);
            btn.Pressed += action;
            return btn;
        }

        private void StyleButton(Button btn, bool active)
        {
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = active ? BtnActive : BtnNormal;
            normalStyle.CornerRadiusTopLeft = 4;
            normalStyle.CornerRadiusTopRight = 4;
            normalStyle.CornerRadiusBottomLeft = 4;
            normalStyle.CornerRadiusBottomRight = 4;
            normalStyle.ContentMarginLeft = 8;
            normalStyle.ContentMarginRight = 8;
            normalStyle.ContentMarginTop = 4;
            normalStyle.ContentMarginBottom = 4;
            btn.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = active ? BtnActive : BtnHover;
            hoverStyle.CornerRadiusTopLeft = 4;
            hoverStyle.CornerRadiusTopRight = 4;
            hoverStyle.CornerRadiusBottomLeft = 4;
            hoverStyle.CornerRadiusBottomRight = 4;
            hoverStyle.ContentMarginLeft = 8;
            hoverStyle.ContentMarginRight = 8;
            hoverStyle.ContentMarginTop = 4;
            hoverStyle.ContentMarginBottom = 4;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.AddThemeColorOverride("font_color", TextColor);
            btn.AddThemeFontSizeOverride("font_size", 14);
        }

        private void UpdateToolHighlight()
        {
            var tool = _canvas.CurrentTool;
            StyleButton(_selectBtn, tool == LinkageTool.Select);
            StyleButton(_wheelBtn, tool == LinkageTool.Wheel);
            StyleButton(_rodBtn, tool == LinkageTool.Rod);
            StyleButton(_pivotBtn, tool == LinkageTool.Pivot);
            StyleButton(_penBtn, tool == LinkageTool.Pen);
            StyleButton(_connectBtn, tool == LinkageTool.Connect);

            _selectBtn.Text = tool == LinkageTool.Select && _canvas.SelectedCount > 0
                ? $"Select ({_canvas.SelectedCount})"
                : "Select";
        }

        private void UpdateInfoText()
        {
            var tool = _canvas.CurrentTool;
            _infoLabel.Text = tool switch
            {
                LinkageTool.Select => "Click to select. Shift+click for multi-select. Drag to move. Drag wheel edge to resize. Right-click wheel to make it a driver.",
                LinkageTool.Wheel => "Click to place a wheel. Right-click a wheel to toggle driver mode (auto-rotation).",
                LinkageTool.Rod => "Click and drag to create a rigid rod between two points.",
                LinkageTool.Pivot => "Click to place a fixed pivot point.",
                LinkageTool.Pen => "Click on a wheel to attach a drawing pen. The pen traces a path during animation.",
                LinkageTool.Connect => "Click two connection points to link them. Wheels have center and rim points. Rods have start and end.",
                LinkageTool.Animate => "Press Play to animate. Driver wheels rotate and connected elements follow.",
                _ => ""
            };
        }

        private void UpdateStatusLabel()
        {
            var solver = _canvas.Solver;
            var parts = new System.Collections.Generic.List<string>();
            if (solver.Wheels.Count > 0) parts.Add($"{solver.Wheels.Count} wheel(s)");
            if (solver.Rods.Count > 0) parts.Add($"{solver.Rods.Count} rod(s)");
            if (solver.Pivots.Count > 0) parts.Add($"{solver.Pivots.Count} pivot(s)");
            if (solver.Pens.Count > 0) parts.Add($"{solver.Pens.Count} pen(s)");
            if (solver.Connections.Count > 0) parts.Add($"{solver.Connections.Count} connection(s)");

            var status = parts.Count > 0 ? string.Join(" | ", parts) : "Empty canvas";
            if (_canvas.IsPlaying) status += " | PLAYING";

            if (_statusLabel != null)
                _statusLabel.Text = status;
        }

        // ==================== EVENT HANDLERS ====================

        private void OnSelectionChanged(int count)
        {
            UpdateToolHighlight();
            _deleteBtn.Text = count > 0 ? $"Delete ({count})" : "Delete Selected";
        }

        private void OnToolChanged(string toolName)
        {
            UpdateToolHighlight();
            UpdateInfoText();
        }

        private void OnPlaybackToggled(bool isPlaying)
        {
            _playBtn.Text = isPlaying ? "Pause" : "Play";
        }

        private void OnPlayPressed()
        {
            _canvas.TogglePlayback();
        }

        private void OnStopPressed()
        {
            _canvas.StopPlayback();
        }

        private void OnDeletePressed()
        {
            _canvas.DeleteSelected();
        }

        private void OnDeleteAllPressed()
        {
            _canvas.DeleteAll();
        }

        private void OnDemoPressed()
        {
            _canvas.CreateSpiralDemo();
        }

        private void OnSpeedChanged(double value)
        {
            _canvas.SetAnimSpeed((float)value);
            _speedLabel.Text = $"Speed: {value:F1}x";
        }
    }
}

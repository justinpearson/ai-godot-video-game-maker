#if TOOLS
using Godot;
using System;
using System.IO;
using System.Linq;
using Mansion.Shared.MansionGeneration.Meshing;

namespace Mansion.Client.Meshing;

[Tool]
public partial class HalfEdgeVisualizerPanel : Control
{
    private LineEdit _seedInput = null!;
    private SpinBox _widthInput = null!;
    private SpinBox _heightInput = null!;
    private SpinBox _thicknessInput = null!;
    private CheckBox _rectToggle = null!;
    private Button _generateButton = null!;
    private Button _exportButton = null!;
    private Button _prevButton = null!;
    private Button _nextButton = null!;
    private Button _playButton = null!;
    private Slider _stepSlider = null!;
    private Label _stepLabel = null!;
    private HalfEdgeCanvas _canvas = null!;
    private Timer _playTimer = null!;
    private bool _isPlaying;

    private ProceduralPlanResult? _result;
    private ChunkDesc? _chunk;
    private HalfEdgeVisualization? _viz;
    private readonly ProceduralMansionBuilder _builder = new();
    private int _currentStep;

    public override void _Ready()
    {
        Name = "HalfEdgeVisualizer";
        BuildUi();
        _playTimer = new Timer
        {
            OneShot = false,
            WaitTime = 0.35f,
            Autostart = false,
            ProcessCallback = Timer.TimerProcessCallback.Idle
        };
        AddChild(_playTimer);
        _playTimer.Timeout += OnPlayTimerTimeout;
    }

    private void BuildUi()
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(root);

        var configRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(configRow);

        _seedInput = new LineEdit { PlaceholderText = "Seed (blank = random)", CustomMinimumSize = new Vector2(140, 0) };
        configRow.AddChild(_seedInput);

        _widthInput = CreateSpinBox(10, 200, 60, "Width (m)");
        configRow.AddChild(_widthInput);
        _heightInput = CreateSpinBox(10, 200, 40, "Height (m)");
        configRow.AddChild(_heightInput);
        _thicknessInput = CreateSpinBox(0.05, 0.5, 0.18, "Wall (m)", 0.01);
        configRow.AddChild(_thicknessInput);

        _rectToggle = new CheckBox { Text = "Rect Floors" };
        configRow.AddChild(_rectToggle);

        _generateButton = new Button { Text = "Generate", CustomMinimumSize = new Vector2(100, 0) };
        _generateButton.Pressed += OnGeneratePressed;
        configRow.AddChild(_generateButton);

        _exportButton = new Button { Text = "Export JSON", CustomMinimumSize = new Vector2(110, 0) };
        _exportButton.Pressed += OnExportPressed;
        configRow.AddChild(_exportButton);

        var sliderRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(sliderRow);

        _prevButton = new Button { Text = "<" };
        _prevButton.Pressed += () => StepRelative(-1);
        sliderRow.AddChild(_prevButton);

        _stepSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 0,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _stepSlider.ValueChanged += OnSliderValueChanged;
        sliderRow.AddChild(_stepSlider);

        _nextButton = new Button { Text = ">" };
        _nextButton.Pressed += () => StepRelative(1);
        sliderRow.AddChild(_nextButton);

        _playButton = new Button { Text = "Play" };
        _playButton.Pressed += TogglePlayback;
        sliderRow.AddChild(_playButton);

        _stepLabel = new Label { Text = "No data loaded." };
        root.AddChild(_stepLabel);

        _canvas = new HalfEdgeCanvas
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 300)
        };
        root.AddChild(_canvas);
    }

    private static SpinBox CreateSpinBox(double min, double max, double value, string tooltip, double step = 1)
    {
        return new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(90, 0),
            TooltipText = tooltip
        };
    }

    private void OnGeneratePressed()
    {
        StopPlayback();
        try
        {
            GeneratePlan();
        }
        catch (Exception ex)
        {
            GD.PushError($"[HalfEdgeVisualizer] Generation failed: {ex.Message}");
            ClearVisualization();
        }
    }

    private void GeneratePlan()
    {
        int width = (int)Math.Round(_widthInput.Value);
        int height = (int)Math.Round(_heightInput.Value);
        float thickness = (float)_thicknessInput.Value;
        int seed = ResolveSeed();

        var request = new ProceduralPlanRequest
        {
            Seed = seed,
            MansionSizeM = new Vector2I(width, height),
            WallThicknessM = thickness,
            DoorScenePath = null,
            UseRoomRectangles = _rectToggle.ButtonPressed,
            CaptureHalfEdgeVisualization = true,
            ChunkName = "Chunk_Visualizer",
            DefaultWallMaterial = "Walls/Default",
            DefaultRoomHeightM = 3.0f
        };

        var result = _builder.Generate(request);
        var chunk = result.Plan.Chunks.FirstOrDefault();
        if (chunk == null)
        {
            GD.PushWarning("[HalfEdgeVisualizer] Build plan did not emit any chunks.");
            ClearVisualization();
            return;
        }

        if (chunk.HalfEdgeVisualization == null)
        {
            GD.PushWarning("[HalfEdgeVisualizer] Visualization payload missing. Ensure CaptureHalfEdgeVisualization is enabled.");
            ClearVisualization();
            return;
        }

        _result = result;
        _chunk = chunk;
        _viz = chunk.HalfEdgeVisualization;
        _canvas.LoadData(chunk, _viz);
        _stepSlider.MaxValue = Math.Max(0, _viz.Steps.Count - 1);
        _stepSlider.Editable = _viz.Steps.Count > 0;
        SetCurrentStep(0);
        GD.Print($"[HalfEdgeVisualizer] Generated plan seed={seed} with {_viz.Steps.Count} half-edge steps.");
    }

    private int ResolveSeed()
    {
        if (!string.IsNullOrWhiteSpace(_seedInput.Text) && int.TryParse(_seedInput.Text, out var parsed))
        {
            return parsed;
        }

        int randomSeed = (int)(GD.Randi() & 0x7fffffff);
        _seedInput.Text = randomSeed.ToString();
        return randomSeed;
    }

    private void ClearVisualization()
    {
        _result = null;
        _chunk = null;
        _viz = null;
        _canvas.ClearData();
        _stepSlider.MaxValue = 0;
        _stepSlider.Value = 0;
        _stepSlider.Editable = false;
        _currentStep = 0;
        _stepLabel.Text = "No data loaded.";
    }

    private void OnSliderValueChanged(double value)
    {
        if (_viz == null)
            return;
        int step = (int)Math.Round(value);
        if (step == _currentStep)
            return;
        SetCurrentStep(step);
    }

    private void StepRelative(int delta)
    {
        if (_viz == null || _viz.Steps.Count == 0)
            return;
        int target = Math.Clamp(_currentStep + delta, 0, _viz.Steps.Count - 1);
        _stepSlider.Value = target;
        SetCurrentStep(target);
    }

    private void SetCurrentStep(int step)
    {
        if (_viz == null || _viz.Steps.Count == 0)
        {
            _currentStep = 0;
            _stepLabel.Text = "No segments available.";
            _canvas.SetStep(0);
            return;
        }

        _currentStep = Math.Clamp(step, 0, _viz.Steps.Count - 1);
        if (Math.Abs(_stepSlider.Value - _currentStep) > 0.01)
            _stepSlider.Value = _currentStep;
        _canvas.SetStep(_currentStep);
        var s = _viz.Steps[_currentStep];
        var doors = s.DoorIntervals.Count > 0
            ? string.Join(", ", s.DoorIntervals.Select(d => $"{d.StartM:F2}-{d.EndM:F2}m"))
            : "none";
        var roomText = s.RoomId > 0 ? $"Room {s.RoomId}" : "Exterior";
        _stepLabel.Text =
            $"Step {_currentStep + 1}/{_viz.Steps.Count} | {roomText} | Outline {s.OutlineIndex} Seg {s.SegmentIndex} | {s.VertexU}->{s.VertexV} | Doors: {doors}";
    }

    private void TogglePlayback()
    {
        if (_viz == null || _viz.Steps.Count == 0)
            return;

        _isPlaying = !_isPlaying;
        if (_isPlaying)
        {
            _playButton.Text = "Pause";
            _playTimer.Start();
        }
        else
        {
            StopPlayback();
        }
    }

    private void StopPlayback()
    {
        _isPlaying = false;
        _playTimer?.Stop();
        if (_playButton != null)
            _playButton.Text = "Play";
    }

    private void OnPlayTimerTimeout()
    {
        if (_viz == null || _viz.Steps.Count == 0)
            return;
        if (_currentStep >= _viz.Steps.Count - 1)
        {
            StopPlayback();
            return;
        }
        StepRelative(1);
    }

    private void OnExportPressed()
    {
        if (_result == null || _chunk?.HalfEdgeVisualization == null)
        {
            GD.PushWarning("[HalfEdgeVisualizer] Nothing to export.");
            return;
        }

        try
        {
            var json = HalfEdgeVisualizationExporter.ToJson(_result);
            var path = ProjectSettings.GlobalizePath("user://half_edge_visualization.json");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            GD.Print($"[HalfEdgeVisualizer] Saved payload to {path}");
        }
        catch (Exception ex)
        {
            GD.PushError($"[HalfEdgeVisualizer] Failed to export JSON: {ex.Message}");
        }
    }
}
#endif

#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Mansion.Shared.MansionGeneration.Meshing;

namespace Mansion.Client.Meshing;

[Tool]
public partial class HalfEdgeCanvas : Control
{
    private readonly List<Vector2[]> _rooms = new();
    private readonly List<Vector2[]> _outlines = new();
    private readonly List<Vector2> _doors = new();
    private readonly List<SegmentVisual> _segments = new();
    private readonly Dictionary<int, Vector2> _graphVertices = new();
    private Rect2 _bounds;
    private float _scale = 1f;
    private Vector2 _offset = Vector2.Zero;
    private const float Padding = 20f;
    private int _currentStep;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
    }

    public void ClearData()
    {
        _rooms.Clear();
        _outlines.Clear();
        _doors.Clear();
        _segments.Clear();
        _graphVertices.Clear();
        _bounds = new Rect2();
        _currentStep = 0;
        QueueRedraw();
    }

    public void LoadData(ChunkDesc chunk, HalfEdgeVisualization visualization)
    {
        _rooms.Clear();
        _outlines.Clear();
        _doors.Clear();
        _segments.Clear();
        _graphVertices.Clear();
        _currentStep = 0;

        foreach (var room in chunk.Rooms)
            _rooms.Add(room.Floor.Select(To2D).ToArray());

        foreach (var outline in chunk.WallOutlines)
            _outlines.Add(outline.Points.Select(To2D).ToArray());

        foreach (var door in chunk.Doors)
            _doors.Add(new Vector2(door.Center.X, door.Center.Z));

        foreach (var vertex in visualization.GraphVertices)
            _graphVertices[vertex.VertexId] = new Vector2(vertex.Position.X, vertex.Position.Y);

        for (int i = 0; i < visualization.Steps.Count; i++)
        {
            var step = visualization.Steps[i];
            if (!_graphVertices.TryGetValue(step.VertexU, out var centerStart) ||
                !_graphVertices.TryGetValue(step.VertexV, out var centerEnd))
            {
                continue;
            }

            _segments.Add(new SegmentVisual(
                new Vector2(step.Start.X, step.Start.Y),
                new Vector2(step.End.X, step.End.Y),
                centerStart,
                centerEnd,
                step.Kind == WallOutlineSegmentKind.Cap,
                step.DoorIntervals.Select(di => (di.StartM, di.EndM)).ToList(),
                i));
        }

        _bounds = ComputeBounds();
        QueueRedraw();
    }

    public void SetStep(int stepIndex)
    {
        if (_segments.Count == 0)
        {
            _currentStep = 0;
        }
        else
        {
            _currentStep = Math.Clamp(stepIndex, 0, _segments.Count - 1);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_rooms.Count == 0 && _segments.Count == 0)
        {
            DrawCenteredText("Generate a plan to inspect half-edges.");
            return;
        }

        UpdateTransform();
        DrawRooms();
        DrawOutlines();
        DrawDoorCenters();
        DrawSegments();
    }

    private void DrawRooms()
    {
        var fill = new Color("#1e1f29");
        var border = new Color("#3a3c4e");
        foreach (var room in _rooms)
        {
            var points = room.Select(Project).ToArray();
            if (points.Length < 3)
                continue;
            DrawPolygon(points, Enumerable.Repeat(fill, points.Length).ToArray());
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                DrawLine(a, b, border, 1f);
            }
        }
    }

    private void DrawOutlines()
    {
        var color = new Color("#2c2d3c");
        foreach (var outline in _outlines)
        {
            var points = outline.Select(Project).ToArray();
            for (int i = 0; i < points.Length - 1; i++)
                DrawLine(points[i], points[i + 1], color, 1f);
        }
    }

    private void DrawDoorCenters()
    {
        var color = new Color("#ffb347");
        foreach (var door in _doors)
        {
            var p = Project(door);
            DrawCircle(p, 3f, color);
        }
    }

    private void DrawSegments()
    {
        if (_segments.Count == 0)
            return;

        for (int i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            var outlineA = Project(segment.OutlineStart);
            var outlineB = Project(segment.OutlineEnd);
            DrawLine(outlineA, outlineB, new Color("#26283d"), 1f);

            var a = Project(segment.CenterStart);
            var b = Project(segment.CenterEnd);
            var color = new Color("#2f3244");
            float width = 1.2f;

            if (segment.StepIndex < _currentStep)
            {
                color = new Color("#7aa2ff");
                width = 2f;
            }
            else if (segment.StepIndex == _currentStep)
            {
                color = new Color("#00ffd2");
                width = 3.5f;
            }

            DrawLine(a, b, color, width);

            if (segment.StepIndex == _currentStep && segment.DoorIntervals.Count > 0)
            {
                DrawDoorIntervals(segment);
            }
        }
    }

    private void DrawDoorIntervals(SegmentVisual segment)
    {
        var dir = (segment.CenterEnd - segment.CenterStart);
        float length = dir.Length();
        if (length < 1e-4f)
            return;
        dir /= length;
        var color = new Color("#ff4d4d");
        foreach (var (start, end) in segment.DoorIntervals)
        {
            var a = Project(segment.CenterStart + dir * start);
            var b = Project(segment.CenterStart + dir * end);
            DrawLine(a, b, color, 4f);
        }
    }

    private void DrawCenteredText(string text)
    {
        var font = GetThemeDefaultFont();
        if (font == null)
            return;
        var size = GetSize();
        var pos = size * 0.5f;
        var measurement = font.GetStringSize(text, HorizontalAlignment.Left, -1, 14);
        pos -= measurement * 0.5f;
        DrawString(font, pos, text, HorizontalAlignment.Left, -1, 14, new Color("#8f93a2"));
    }

    private void UpdateTransform()
    {
        if (_bounds.Size.X <= 0f || _bounds.Size.Y <= 0f)
        {
            _scale = 1f;
            _offset = Vector2.Zero;
            return;
        }

        var size = GetSize();
        var inner = size - Vector2.One * (Padding * 2f);
        if (inner.X <= 0f || inner.Y <= 0f)
        {
            _scale = 1f;
            _offset = Vector2.Zero;
            return;
        }

        var scaleX = inner.X / _bounds.Size.X;
        var scaleY = inner.Y / _bounds.Size.Y;
        _scale = Math.Min(scaleX, scaleY);
        var contentSize = _bounds.Size * _scale;
        _offset = new Vector2(
            (size.X - contentSize.X) * 0.5f,
            (size.Y - contentSize.Y) * 0.5f);
    }

    private Rect2 ComputeBounds()
    {
        bool found = false;
        float minX = 0, maxX = 0, minY = 0, maxY = 0;

        void Include(Vector2 p)
        {
            if (!found)
            {
                minX = maxX = p.X;
                minY = maxY = p.Y;
                found = true;
            }
            else
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        foreach (var room in _rooms)
            foreach (var p in room) Include(p);
        foreach (var outline in _outlines)
            foreach (var p in outline) Include(p);
        foreach (var seg in _segments)
        {
            Include(seg.OutlineStart);
            Include(seg.OutlineEnd);
            Include(seg.CenterStart);
            Include(seg.CenterEnd);
        }
        foreach (var door in _doors)
            Include(door);

        if (!found)
            return new Rect2();

        var size = new Vector2(maxX - minX, maxY - minY);
        if (size.X < 1f) size.X = 1f;
        if (size.Y < 1f) size.Y = 1f;
        return new Rect2(new Vector2(minX, minY), size);
    }

    private Vector2 Project(Vector2 world)
    {
        if (_bounds.Size.X <= 0f || _bounds.Size.Y <= 0f)
            return world;
        var relative = world - _bounds.Position;
        relative.Y = _bounds.Size.Y - relative.Y;
        return _offset + relative * _scale;
    }

    private static Vector2 To2D(System.Numerics.Vector3 v) => new(v.X, v.Z);

    private readonly struct SegmentVisual
    {
        public SegmentVisual(Vector2 outlineStart, Vector2 outlineEnd, Vector2 centerStart, Vector2 centerEnd, bool cap, List<(float start, float end)> doors, int stepIndex)
        {
            OutlineStart = outlineStart;
            OutlineEnd = outlineEnd;
            CenterStart = centerStart;
            CenterEnd = centerEnd;
            IsCap = cap;
            DoorIntervals = doors;
            StepIndex = stepIndex;
        }

        public Vector2 OutlineStart { get; }
        public Vector2 OutlineEnd { get; }
        public Vector2 CenterStart { get; }
        public Vector2 CenterEnd { get; }
        public bool IsCap { get; }
        public List<(float start, float end)> DoorIntervals { get; }
        public int StepIndex { get; }
    }
}
#endif

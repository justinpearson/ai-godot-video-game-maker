using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaLeaves
{
    /// <summary>
    /// Main canvas for the Mechanical Linkage Drawing Simulator.
    /// Handles rendering of all linkage elements, mouse interaction,
    /// and animation playback.
    /// </summary>
    public partial class LinkageCanvas : Node2D
    {
        private LinkageSolver _solver = new();
        private LinkageTool _currentTool = LinkageTool.Wheel;

        // Interaction state
        private DragTarget _dragTarget = DragTarget.None;
        private int _dragElementId = -1;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartPos;
        private Vector2 _dragStartPos2; // For rod end
        private float _dragStartRadius;
        private bool _isDrawingRod;
        private Vector2 _rodDrawStart;

        // Selection
        private HashSet<(string type, int id)> _selected = new();

        // Animation
        private bool _isPlaying;
        private float _animTime;
        private float _animSpeed = 1.0f;

        // Connect tool state
        private (string type, int id, string point)? _connectFrom;

        // Canvas bounds
        private readonly Vector2 _canvasSize = new(1920, 1080);
        private const float GridSpacing = 40f;

        // Colors
        private static readonly Color GridColor = new(0.92f, 0.92f, 0.92f);
        private static readonly Color WheelColor = new(0.2f, 0.4f, 0.8f);
        private static readonly Color WheelFillColor = new(0.85f, 0.9f, 1.0f, 0.3f);
        private static readonly Color RodColor = new(0.55f, 0.35f, 0.2f);
        private static readonly Color PivotColor = new(0.8f, 0.2f, 0.2f);
        private static readonly Color PenColor = new(0.2f, 0.7f, 0.3f);
        private static readonly Color SelectionColor = new(0.0f, 0.5f, 1.0f);
        private static readonly Color ConnectionColor = new(1.0f, 0.6f, 0.0f, 0.6f);
        private static readonly Color HoverHighlight = new(1.0f, 1.0f, 0.0f, 0.3f);
        private static readonly Color DriverWheelColor = new(0.9f, 0.3f, 0.1f);
        private static readonly Color TraceDefaultColor = new(0.1f, 0.1f, 0.8f);

        // Hover state
        private (string type, int id)? _hoveredElement;

        // Sounds
        private AudioStreamPlayer? _clickSound;
        private AudioStreamPlayer? _deleteSound;
        private AudioStreamPlayer? _connectSound;

        public LinkageSolver Solver => _solver;
        public LinkageTool CurrentTool => _currentTool;
        public bool IsPlaying => _isPlaying;
        public float AnimSpeed => _animSpeed;
        public int SelectedCount => _selected.Count;

        public override void _Ready()
        {
            // Create sound players
            _clickSound = CreateSoundPlayer("res://assets/sounds/click.wav");
            _deleteSound = CreateSoundPlayer("res://assets/sounds/delete.wav");
            _connectSound = CreateSoundPlayer("res://assets/sounds/connect.wav");
        }

        private AudioStreamPlayer? CreateSoundPlayer(string path)
        {
            if (!FileAccess.FileExists(path)) return null;
            var player = new AudioStreamPlayer();
            player.Stream = GD.Load<AudioStream>(path);
            player.VolumeDb = -6;
            AddChild(player);
            return player;
        }

        public override void _Process(double delta)
        {
            if (_isPlaying)
            {
                _animTime += (float)delta * _animSpeed;
                _solver.Step((float)delta * _animSpeed);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawGrid();
            DrawTrace();
            DrawConnections();
            DrawRods();
            DrawWheels();
            DrawPivots();
            DrawPens();
            DrawRodPreview();
            DrawConnectPreview();
        }

        // ==================== DRAWING ====================

        private void DrawGrid()
        {
            // Background
            DrawRect(new Rect2(Vector2.Zero, _canvasSize), Colors.White, true);

            // Grid lines
            for (float x = 0; x <= _canvasSize.X; x += GridSpacing)
            {
                DrawLine(new Vector2(x, 0), new Vector2(x, _canvasSize.Y), GridColor, 1);
            }
            for (float y = 0; y <= _canvasSize.Y; y += GridSpacing)
            {
                DrawLine(new Vector2(0, y), new Vector2(_canvasSize.X, y), GridColor, 1);
            }

            // Border
            DrawRect(new Rect2(Vector2.Zero, _canvasSize), new Color(0.7f, 0.7f, 0.7f), false, 2);
        }

        private void DrawTrace()
        {
            var points = _solver.TracePoints;
            if (points.Count < 2) return;

            for (int i = 1; i < points.Count; i++)
            {
                DrawLine(points[i - 1].Position, points[i].Position, points[i].Color, 2.0f, true);
            }
        }

        private void DrawConnections()
        {
            foreach (var conn in _solver.Connections)
            {
                var posA = _solver.GetConnectionPoint(conn.ElementAType, conn.ElementAId, conn.ElementAPoint);
                var posB = _solver.GetConnectionPoint(conn.ElementBType, conn.ElementBId, conn.ElementBPoint);
                if (posA == null || posB == null) continue;

                DrawDashedLine(posA.Value, posB.Value, ConnectionColor, 2.0f, 8.0f);
                DrawCircle(posA.Value, 4, ConnectionColor);
                DrawCircle(posB.Value, 4, ConnectionColor);
            }
        }

        private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width, float dashLength)
        {
            var dir = (to - from);
            var length = dir.Length();
            if (length < 0.1f) return;
            dir /= length;

            float drawn = 0;
            bool draw = true;
            while (drawn < length)
            {
                var segLen = Mathf.Min(dashLength, length - drawn);
                if (draw)
                {
                    DrawLine(from + dir * drawn, from + dir * (drawn + segLen), color, width);
                }
                drawn += segLen;
                draw = !draw;
            }
        }

        private void DrawWheels()
        {
            foreach (var wheel in _solver.Wheels)
            {
                var isSelected = _selected.Contains(("wheel", wheel.Id));
                var isHovered = _hoveredElement?.type == "wheel" && _hoveredElement?.id == wheel.Id;
                var outlineColor = isSelected ? SelectionColor : (wheel.IsDriver ? DriverWheelColor : WheelColor);
                var fillColor = isSelected ? new Color(0.7f, 0.85f, 1.0f, 0.3f) : WheelFillColor;

                if (isHovered && !isSelected)
                {
                    DrawCircle(wheel.Center, wheel.Radius + 3, HoverHighlight);
                }

                // Fill
                DrawCircle(wheel.Center, wheel.Radius, fillColor);
                // Outline
                DrawArc(wheel.Center, wheel.Radius, 0, Mathf.Tau, 64, outlineColor, isSelected ? 3.0f : 2.0f);

                // Radial line showing rotation
                var rimPoint = wheel.Center + new Vector2(
                    wheel.Radius * Mathf.Cos(wheel.Rotation),
                    wheel.Radius * Mathf.Sin(wheel.Rotation)
                );
                DrawLine(wheel.Center, rimPoint, outlineColor, 2.0f);

                // Center dot
                DrawCircle(wheel.Center, 3, outlineColor);

                // Driver indicator
                if (wheel.IsDriver)
                {
                    DrawArc(wheel.Center, wheel.Radius + 5, 0, Mathf.Tau, 32, DriverWheelColor, 1.5f);
                }
            }
        }

        private void DrawRods()
        {
            foreach (var rod in _solver.Rods)
            {
                var isSelected = _selected.Contains(("rod", rod.Id));
                var isHovered = _hoveredElement?.type == "rod" && _hoveredElement?.id == rod.Id;
                var color = isSelected ? SelectionColor : RodColor;

                if (isHovered && !isSelected)
                {
                    DrawLine(rod.Start, rod.End, HoverHighlight, 8.0f);
                }

                DrawLine(rod.Start, rod.End, color, isSelected ? 4.0f : 3.0f);

                // Endpoint circles
                DrawCircle(rod.Start, 5, color);
                DrawCircle(rod.End, 5, color);

                // Midpoint marker
                var mid = (rod.Start + rod.End) / 2;
                DrawCircle(mid, 3, color);
            }
        }

        private void DrawPivots()
        {
            foreach (var pivot in _solver.Pivots)
            {
                var isSelected = _selected.Contains(("pivot", pivot.Id));
                var isHovered = _hoveredElement?.type == "pivot" && _hoveredElement?.id == pivot.Id;
                var color = isSelected ? SelectionColor : PivotColor;

                if (isHovered && !isSelected)
                {
                    DrawCircle(pivot.Position, 10, HoverHighlight);
                }

                // Triangle/pin shape
                DrawCircle(pivot.Position, 7, color);
                DrawCircle(pivot.Position, 4, Colors.White);
                DrawCircle(pivot.Position, 2, color);
            }
        }

        private void DrawPens()
        {
            foreach (var pen in _solver.Pens)
            {
                var wheel = _solver.Wheels.FirstOrDefault(w => w.Id == pen.AttachedWheelId);
                if (wheel == null) continue;

                var worldPos = _solver.GetPenWorldPosition(pen, wheel);
                var isSelected = _selected.Contains(("pen", pen.Id));
                var color = isSelected ? SelectionColor : pen.InkColor;

                // Pen nib
                DrawCircle(worldPos, 6, color);
                DrawCircle(worldPos, 3, Colors.White);

                // Line from wheel center to pen
                DrawDashedLine(wheel.Center, worldPos, new Color(color, 0.4f), 1.0f, 4.0f);
            }
        }

        private void DrawRodPreview()
        {
            if (_isDrawingRod)
            {
                var mousePos = GetLocalMousePosition();
                DrawLine(_rodDrawStart, mousePos, new Color(RodColor, 0.5f), 2.0f);
                DrawCircle(_rodDrawStart, 4, RodColor);
                DrawCircle(mousePos, 4, new Color(RodColor, 0.5f));
            }
        }

        private void DrawConnectPreview()
        {
            if (_currentTool == LinkageTool.Connect && _connectFrom != null)
            {
                var fromPos = _solver.GetConnectionPoint(_connectFrom.Value.type, _connectFrom.Value.id, _connectFrom.Value.point);
                if (fromPos != null)
                {
                    var mousePos = GetLocalMousePosition();
                    DrawDashedLine(fromPos.Value, mousePos, new Color(ConnectionColor, 0.5f), 2.0f, 6.0f);
                }
            }
        }

        // ==================== INPUT ====================

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                HandleMouseButton(mb);
            }
            else if (@event is InputEventMouseMotion mm)
            {
                HandleMouseMove(mm);
            }
        }

        private void HandleMouseButton(InputEventMouseButton mb)
        {
            var pos = ToLocal(mb.GlobalPosition);

            // Only handle clicks within canvas bounds
            if (pos.X < 0 || pos.Y < 0 || pos.X > _canvasSize.X || pos.Y > _canvasSize.Y)
                return;

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                    HandleLeftDown(pos, mb.ShiftPressed);
                else
                    HandleLeftUp(pos);
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                HandleRightClick(pos);
            }
        }

        private void HandleLeftDown(Vector2 pos, bool shiftHeld)
        {
            switch (_currentTool)
            {
                case LinkageTool.Select:
                    HandleSelectDown(pos, shiftHeld);
                    break;

                case LinkageTool.Wheel:
                    AddWheel(pos);
                    break;

                case LinkageTool.Rod:
                    _isDrawingRod = true;
                    _rodDrawStart = pos;
                    break;

                case LinkageTool.Pivot:
                    AddPivot(pos);
                    break;

                case LinkageTool.Pen:
                    HandlePenClick(pos);
                    break;

                case LinkageTool.Connect:
                    HandleConnectClick(pos);
                    break;
            }
        }

        private void HandleLeftUp(Vector2 pos)
        {
            if (_isDrawingRod)
            {
                _isDrawingRod = false;
                if (_rodDrawStart.DistanceTo(pos) > 10)
                {
                    AddRod(_rodDrawStart, pos);
                }
            }

            _dragTarget = DragTarget.None;
            _dragElementId = -1;
        }

        private void HandleRightClick(Vector2 pos)
        {
            // Right-click toggles driver status on wheels
            var hit = HitTestWheel(pos);
            if (hit != null)
            {
                hit.IsDriver = !hit.IsDriver;
                if (hit.IsDriver && hit.Speed == 0)
                {
                    hit.Speed = 0.25f; // Default to quarter revolution per second
                }
                _clickSound?.Play();
            }
        }

        private void HandleMouseMove(InputEventMouseMotion mm)
        {
            var pos = ToLocal(mm.GlobalPosition);

            // Update hover state
            UpdateHoverState(pos);

            // Handle dragging
            if (_dragTarget != DragTarget.None)
            {
                HandleDrag(pos);
            }
        }

        private void UpdateHoverState(Vector2 pos)
        {
            _hoveredElement = null;
            if (_currentTool != LinkageTool.Select && _currentTool != LinkageTool.Connect) return;

            var wheel = HitTestWheel(pos);
            if (wheel != null)
            {
                _hoveredElement = ("wheel", wheel.Id);
                return;
            }

            var rod = HitTestRod(pos);
            if (rod != null)
            {
                _hoveredElement = ("rod", rod.Id);
                return;
            }

            var pivot = HitTestPivot(pos);
            if (pivot != null)
            {
                _hoveredElement = ("pivot", pivot.Id);
                return;
            }

            var pen = HitTestPen(pos);
            if (pen != null)
            {
                _hoveredElement = ("pen", pen.Id);
            }
        }

        // ==================== SELECT TOOL ====================

        private void HandleSelectDown(Vector2 pos, bool shiftHeld)
        {
            // Hit test in order: pens, pivots, wheels (edge first), rods
            var pen = HitTestPen(pos);
            if (pen != null)
            {
                HandleElementSelect("pen", pen.Id, shiftHeld);
                StartDrag(DragTarget.PenOffset, pen.Id, pos);
                return;
            }

            var pivot = HitTestPivot(pos);
            if (pivot != null)
            {
                HandleElementSelect("pivot", pivot.Id, shiftHeld);
                StartDrag(DragTarget.PivotCenter, pivot.Id, pos);
                _dragStartPos = pivot.Position;
                return;
            }

            var wheel = HitTestWheel(pos);
            if (wheel != null)
            {
                HandleElementSelect("wheel", wheel.Id, shiftHeld);
                // Check if clicking near edge for resize
                var distFromEdge = Mathf.Abs(pos.DistanceTo(wheel.Center) - wheel.Radius);
                if (distFromEdge < 10 && _selected.Count == 1)
                {
                    StartDrag(DragTarget.WheelEdge, wheel.Id, pos);
                    _dragStartRadius = wheel.Radius;
                }
                else
                {
                    StartDrag(DragTarget.WheelCenter, wheel.Id, pos);
                    _dragStartPos = wheel.Center;
                }
                return;
            }

            var (rod, rodPart) = HitTestRodDetailed(pos);
            if (rod != null)
            {
                HandleElementSelect("rod", rod.Id, shiftHeld);
                switch (rodPart)
                {
                    case "start":
                        StartDrag(DragTarget.RodStart, rod.Id, pos);
                        _dragStartPos = rod.Start;
                        break;
                    case "end":
                        StartDrag(DragTarget.RodEnd, rod.Id, pos);
                        _dragStartPos = rod.End;
                        break;
                    default:
                        StartDrag(DragTarget.RodMiddle, rod.Id, pos);
                        _dragStartPos = rod.Start;
                        _dragStartPos2 = rod.End;
                        break;
                }
                return;
            }

            // Clicked empty space
            if (!shiftHeld)
            {
                _selected.Clear();
                EventBus.Instance?.EmitSelectionChanged(0);
            }
        }

        private void HandleElementSelect(string type, int id, bool shiftHeld)
        {
            var key = (type, id);
            if (shiftHeld)
            {
                if (_selected.Contains(key))
                    _selected.Remove(key);
                else
                    _selected.Add(key);
            }
            else if (!_selected.Contains(key))
            {
                _selected.Clear();
                _selected.Add(key);
            }
            EventBus.Instance?.EmitSelectionChanged(_selected.Count);
        }

        private void StartDrag(DragTarget target, int id, Vector2 mousePos)
        {
            _dragTarget = target;
            _dragElementId = id;
            _dragStartMouse = mousePos;
        }

        private void HandleDrag(Vector2 pos)
        {
            var delta = pos - _dragStartMouse;

            switch (_dragTarget)
            {
                case DragTarget.WheelCenter:
                    var wheel = _solver.Wheels.FirstOrDefault(w => w.Id == _dragElementId);
                    if (wheel != null)
                    {
                        if (_selected.Count > 1)
                        {
                            MoveSelectedElements(delta);
                            _dragStartMouse = pos;
                        }
                        else
                        {
                            wheel.Center = _dragStartPos + delta;
                        }
                    }
                    break;

                case DragTarget.WheelEdge:
                    var wEdge = _solver.Wheels.FirstOrDefault(w => w.Id == _dragElementId);
                    if (wEdge != null)
                    {
                        wEdge.Radius = Mathf.Max(10, pos.DistanceTo(wEdge.Center));
                    }
                    break;

                case DragTarget.RodStart:
                    var rStart = _solver.Rods.FirstOrDefault(r => r.Id == _dragElementId);
                    if (rStart != null) rStart.Start = _dragStartPos + delta;
                    break;

                case DragTarget.RodEnd:
                    var rEnd = _solver.Rods.FirstOrDefault(r => r.Id == _dragElementId);
                    if (rEnd != null) rEnd.End = _dragStartPos + delta;
                    break;

                case DragTarget.RodMiddle:
                    var rMid = _solver.Rods.FirstOrDefault(r => r.Id == _dragElementId);
                    if (rMid != null)
                    {
                        if (_selected.Count > 1)
                        {
                            MoveSelectedElements(delta);
                            _dragStartMouse = pos;
                        }
                        else
                        {
                            rMid.Start = _dragStartPos + delta;
                            rMid.End = _dragStartPos2 + delta;
                        }
                    }
                    break;

                case DragTarget.PivotCenter:
                    var pivot = _solver.Pivots.FirstOrDefault(p => p.Id == _dragElementId);
                    if (pivot != null)
                    {
                        if (_selected.Count > 1)
                        {
                            MoveSelectedElements(delta);
                            _dragStartMouse = pos;
                        }
                        else
                        {
                            pivot.Position = _dragStartPos + delta;
                        }
                    }
                    break;

                case DragTarget.PenOffset:
                    var pen = _solver.Pens.FirstOrDefault(p => p.Id == _dragElementId);
                    if (pen != null)
                    {
                        var attachedWheel = _solver.Wheels.FirstOrDefault(w => w.Id == pen.AttachedWheelId);
                        if (attachedWheel != null)
                        {
                            // Convert world position to local offset (inverse rotation)
                            var localPos = pos - attachedWheel.Center;
                            var cos = Mathf.Cos(-attachedWheel.Rotation);
                            var sin = Mathf.Sin(-attachedWheel.Rotation);
                            pen.Offset = new Vector2(
                                localPos.X * cos - localPos.Y * sin,
                                localPos.X * sin + localPos.Y * cos
                            );
                        }
                    }
                    break;
            }
        }

        private void MoveSelectedElements(Vector2 delta)
        {
            foreach (var (type, id) in _selected)
            {
                switch (type)
                {
                    case "wheel":
                        var w = _solver.Wheels.FirstOrDefault(x => x.Id == id);
                        if (w != null) w.Center += delta;
                        break;
                    case "rod":
                        var r = _solver.Rods.FirstOrDefault(x => x.Id == id);
                        if (r != null)
                        {
                            r.Start += delta;
                            r.End += delta;
                        }
                        break;
                    case "pivot":
                        var p = _solver.Pivots.FirstOrDefault(x => x.Id == id);
                        if (p != null) p.Position += delta;
                        break;
                    case "pen":
                        // Pens move with their wheel, not independently
                        break;
                }
            }
        }

        // ==================== HIT TESTING ====================

        private WheelData? HitTestWheel(Vector2 pos)
        {
            foreach (var wheel in _solver.Wheels)
            {
                if (pos.DistanceTo(wheel.Center) <= wheel.Radius + 5)
                    return wheel;
            }
            return null;
        }

        private RodData? HitTestRod(Vector2 pos)
        {
            return HitTestRodDetailed(pos).rod;
        }

        private (RodData? rod, string part) HitTestRodDetailed(Vector2 pos)
        {
            foreach (var rod in _solver.Rods)
            {
                // Check endpoints first
                if (pos.DistanceTo(rod.Start) < 10)
                    return (rod, "start");
                if (pos.DistanceTo(rod.End) < 10)
                    return (rod, "end");

                // Check line proximity
                var dist = DistanceToSegment(pos, rod.Start, rod.End);
                if (dist < 8)
                    return (rod, "middle");
            }
            return (null, "");
        }

        private PivotData? HitTestPivot(Vector2 pos)
        {
            foreach (var pivot in _solver.Pivots)
            {
                if (pos.DistanceTo(pivot.Position) < 12)
                    return pivot;
            }
            return null;
        }

        private PenData? HitTestPen(Vector2 pos)
        {
            foreach (var pen in _solver.Pens)
            {
                var wheel = _solver.Wheels.FirstOrDefault(w => w.Id == pen.AttachedWheelId);
                if (wheel == null) continue;
                var worldPos = _solver.GetPenWorldPosition(pen, wheel);
                if (pos.DistanceTo(worldPos) < 10)
                    return pen;
            }
            return null;
        }

        private float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var ap = point - a;
            var t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0, 1);
            var closest = a + ab * t;
            return point.DistanceTo(closest);
        }

        // ==================== CONNECT TOOL ====================

        private void HandleConnectClick(Vector2 pos)
        {
            var hit = FindNearestConnectionPoint(pos);
            if (hit == null) return;

            if (_connectFrom == null)
            {
                _connectFrom = hit;
                _connectSound?.Play();
            }
            else
            {
                // Create connection
                var conn = new ConnectionData
                {
                    Id = _solver.NextId(),
                    ElementAType = _connectFrom.Value.type,
                    ElementAId = _connectFrom.Value.id,
                    ElementAPoint = _connectFrom.Value.point,
                    ElementBType = hit.Value.type,
                    ElementBId = hit.Value.id,
                    ElementBPoint = hit.Value.point
                };
                _solver.Connections.Add(conn);
                _connectFrom = null;
                _connectSound?.Play();
            }
        }

        private (string type, int id, string point)? FindNearestConnectionPoint(Vector2 pos)
        {
            float bestDist = 15f; // Max snap distance
            (string type, int id, string point)? best = null;

            // Check wheels — center and rim
            foreach (var w in _solver.Wheels)
            {
                var dCenter = pos.DistanceTo(w.Center);
                if (dCenter < bestDist) { bestDist = dCenter; best = ("wheel", w.Id, "center"); }

                var rimPos = w.Center + new Vector2(w.Radius * Mathf.Cos(w.Rotation), w.Radius * Mathf.Sin(w.Rotation));
                var dRim = pos.DistanceTo(rimPos);
                if (dRim < bestDist) { bestDist = dRim; best = ("wheel", w.Id, "rim"); }
            }

            // Check rods — start and end
            foreach (var r in _solver.Rods)
            {
                var dStart = pos.DistanceTo(r.Start);
                if (dStart < bestDist) { bestDist = dStart; best = ("rod", r.Id, "start"); }

                var dEnd = pos.DistanceTo(r.End);
                if (dEnd < bestDist) { bestDist = dEnd; best = ("rod", r.Id, "end"); }
            }

            // Check pivots
            foreach (var p in _solver.Pivots)
            {
                var d = pos.DistanceTo(p.Position);
                if (d < bestDist) { bestDist = d; best = ("pivot", p.Id, "center"); }
            }

            return best;
        }

        // ==================== PEN TOOL ====================

        private void HandlePenClick(Vector2 pos)
        {
            // Click on a wheel to attach a pen
            var wheel = HitTestWheel(pos);
            if (wheel == null) return;

            var pen = new PenData
            {
                Id = _solver.NextId(),
                AttachedWheelId = wheel.Id,
                Offset = new Vector2(wheel.Radius * 0.6f, 0),
                InkColor = GetNextPenColor()
            };
            _solver.Pens.Add(pen);
            _clickSound?.Play();
            EventBus.Instance?.EmitElementAdded("pen");
        }

        private static readonly Color[] PenColors = {
            new(0.1f, 0.1f, 0.8f),
            new(0.8f, 0.1f, 0.1f),
            new(0.1f, 0.7f, 0.1f),
            new(0.7f, 0.1f, 0.7f),
            new(0.9f, 0.5f, 0.0f),
            new(0.0f, 0.7f, 0.7f),
        };

        private Color GetNextPenColor()
        {
            return PenColors[_solver.Pens.Count % PenColors.Length];
        }

        // ==================== ELEMENT CREATION ====================

        public void AddWheel(Vector2 pos, float radius = 50)
        {
            var wheel = new WheelData
            {
                Id = _solver.NextId(),
                Center = pos,
                Radius = radius,
                Rotation = 0
            };
            _solver.Wheels.Add(wheel);
            _clickSound?.Play();
            EventBus.Instance?.EmitElementAdded("wheel");
        }

        public void AddRod(Vector2 start, Vector2 end)
        {
            var rod = new RodData
            {
                Id = _solver.NextId(),
                Start = start,
                End = end
            };
            _solver.Rods.Add(rod);
            _clickSound?.Play();
            EventBus.Instance?.EmitElementAdded("rod");
        }

        public void AddPivot(Vector2 pos)
        {
            var pivot = new PivotData
            {
                Id = _solver.NextId(),
                Position = pos
            };
            _solver.Pivots.Add(pivot);
            _clickSound?.Play();
            EventBus.Instance?.EmitElementAdded("pivot");
        }

        // ==================== PUBLIC API ====================

        public void SetTool(LinkageTool tool)
        {
            _currentTool = tool;
            _connectFrom = null;
            _isDrawingRod = false;
            EventBus.Instance?.EmitToolChanged(tool.ToString());
        }

        public void DeleteSelected()
        {
            if (_selected.Count == 0) return;

            var count = _selected.Count;
            foreach (var (type, id) in _selected.ToList())
            {
                switch (type)
                {
                    case "wheel":
                        _solver.Wheels.RemoveAll(w => w.Id == id);
                        // Remove pens attached to this wheel
                        _solver.Pens.RemoveAll(p => p.AttachedWheelId == id);
                        // Remove connections to this wheel
                        _solver.Connections.RemoveAll(c =>
                            (c.ElementAType == "wheel" && c.ElementAId == id) ||
                            (c.ElementBType == "wheel" && c.ElementBId == id));
                        break;
                    case "rod":
                        _solver.Rods.RemoveAll(r => r.Id == id);
                        _solver.Connections.RemoveAll(c =>
                            (c.ElementAType == "rod" && c.ElementAId == id) ||
                            (c.ElementBType == "rod" && c.ElementBId == id));
                        break;
                    case "pivot":
                        _solver.Pivots.RemoveAll(p => p.Id == id);
                        _solver.Connections.RemoveAll(c =>
                            (c.ElementAType == "pivot" && c.ElementAId == id) ||
                            (c.ElementBType == "pivot" && c.ElementBId == id));
                        break;
                    case "pen":
                        _solver.Pens.RemoveAll(p => p.Id == id);
                        break;
                }
            }
            _selected.Clear();
            _deleteSound?.Play();
            EventBus.Instance?.EmitElementDeleted(count);
            EventBus.Instance?.EmitSelectionChanged(0);
        }

        public void DeleteAll()
        {
            _solver.ClearAll();
            _selected.Clear();
            _deleteSound?.Play();
            EventBus.Instance?.EmitElementDeleted(-1);
            EventBus.Instance?.EmitSelectionChanged(0);
        }

        public void TogglePlayback()
        {
            _isPlaying = !_isPlaying;
            EventBus.Instance?.EmitPlaybackToggled(_isPlaying);
        }

        public void StopPlayback()
        {
            _isPlaying = false;
            _animTime = 0;
            EventBus.Instance?.EmitPlaybackToggled(false);
        }

        public void SetAnimSpeed(float speed)
        {
            _animSpeed = speed;
        }

        public void ClearTrace()
        {
            _solver.ClearTrace();
        }

        public void CreateSpiralDemo()
        {
            _solver.ClearAll();
            _selected.Clear();

            var center = _canvasSize / 2;

            // Large fixed wheel
            var bigWheel = new WheelData
            {
                Id = _solver.NextId(),
                Center = center,
                Radius = 200,
                IsDriver = true,
                Speed = 0.15f
            };
            _solver.Wheels.Add(bigWheel);

            // Smaller wheel riding on the big wheel's rim
            var smallWheel = new WheelData
            {
                Id = _solver.NextId(),
                Center = center + new Vector2(200, 0),
                Radius = 75,
                IsDriver = true,
                Speed = -0.4f
            };
            _solver.Wheels.Add(smallWheel);

            // Connect small wheel center to big wheel rim
            _solver.Connections.Add(new ConnectionData
            {
                Id = _solver.NextId(),
                ElementAType = "wheel",
                ElementAId = bigWheel.Id,
                ElementAPoint = "rim",
                ElementBType = "wheel",
                ElementBId = smallWheel.Id,
                ElementBPoint = "center"
            });

            // Pen on the small wheel
            var pen = new PenData
            {
                Id = _solver.NextId(),
                AttachedWheelId = smallWheel.Id,
                Offset = new Vector2(50, 0),
                InkColor = new Color(0.2f, 0.1f, 0.8f)
            };
            _solver.Pens.Add(pen);

            EventBus.Instance?.EmitElementAdded("demo");
        }
    }
}

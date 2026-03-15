using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaLeaves
{
    /// <summary>
    /// Pure C# solver for mechanical linkage constraints.
    /// Given a set of wheels, rods, pivots, pens, and connections,
    /// advances time and resolves positions.
    /// </summary>
    public class LinkageSolver
    {
        public List<WheelData> Wheels { get; } = new();
        public List<RodData> Rods { get; } = new();
        public List<PivotData> Pivots { get; } = new();
        public List<PenData> Pens { get; } = new();
        public List<ConnectionData> Connections { get; } = new();
        public List<TracePoint> TracePoints { get; } = new();

        private int _nextId = 1;

        public int NextId() => _nextId++;

        /// <summary>
        /// Step the simulation forward by dt seconds.
        /// Rotates driver wheels and resolves constraints.
        /// </summary>
        public void Step(float dt)
        {
            // Rotate driver wheels
            foreach (var wheel in Wheels)
            {
                if (wheel.IsDriver)
                {
                    wheel.Rotation += wheel.Speed * Mathf.Tau * dt;
                }
            }

            // Resolve connections: propagate positions through the linkage
            ResolveConnections();

            // Record pen positions for tracing
            foreach (var pen in Pens)
            {
                var wheel = Wheels.FirstOrDefault(w => w.Id == pen.AttachedWheelId);
                if (wheel != null)
                {
                    var worldPos = GetPenWorldPosition(pen, wheel);
                    TracePoints.Add(new TracePoint { Position = worldPos, Color = pen.InkColor });
                }
            }
        }

        /// <summary>
        /// Get the world position of a pen attached to a wheel.
        /// </summary>
        public Vector2 GetPenWorldPosition(PenData pen, WheelData wheel)
        {
            var cos = Mathf.Cos(wheel.Rotation);
            var sin = Mathf.Sin(wheel.Rotation);
            var rotatedOffset = new Vector2(
                pen.Offset.X * cos - pen.Offset.Y * sin,
                pen.Offset.X * sin + pen.Offset.Y * cos
            );
            return wheel.Center + rotatedOffset;
        }

        /// <summary>
        /// Resolve all connection constraints iteratively.
        /// </summary>
        private void ResolveConnections()
        {
            // Multiple iterations for convergence
            for (int iter = 0; iter < 10; iter++)
            {
                foreach (var conn in Connections)
                {
                    var posA = GetConnectionPoint(conn.ElementAType, conn.ElementAId, conn.ElementAPoint);
                    var posB = GetConnectionPoint(conn.ElementBType, conn.ElementBId, conn.ElementBPoint);

                    if (posA == null || posB == null) continue;

                    var delta = posB.Value - posA.Value;
                    if (delta.Length() < 0.1f) continue;

                    // Move B to match A (A is considered the driver)
                    SetConnectionPoint(conn.ElementBType, conn.ElementBId, conn.ElementBPoint, posA.Value);
                }
            }
        }

        /// <summary>
        /// Get the world position of a specific point on an element.
        /// </summary>
        public Vector2? GetConnectionPoint(string elementType, int elementId, string point)
        {
            switch (elementType)
            {
                case "wheel":
                    var wheel = Wheels.FirstOrDefault(w => w.Id == elementId);
                    if (wheel == null) return null;
                    if (point == "center") return wheel.Center;
                    if (point == "rim")
                    {
                        return wheel.Center + new Vector2(
                            wheel.Radius * Mathf.Cos(wheel.Rotation),
                            wheel.Radius * Mathf.Sin(wheel.Rotation)
                        );
                    }
                    return wheel.Center;

                case "rod":
                    var rod = Rods.FirstOrDefault(r => r.Id == elementId);
                    if (rod == null) return null;
                    if (point == "start") return rod.Start;
                    if (point == "end") return rod.End;
                    return (rod.Start + rod.End) / 2;

                case "pivot":
                    var pivot = Pivots.FirstOrDefault(p => p.Id == elementId);
                    return pivot?.Position;

                case "pen":
                    var pen = Pens.FirstOrDefault(p => p.Id == elementId);
                    if (pen == null) return null;
                    var attachedWheel = Wheels.FirstOrDefault(w => w.Id == pen.AttachedWheelId);
                    if (attachedWheel == null) return null;
                    return GetPenWorldPosition(pen, attachedWheel);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Set the position of a specific point on an element, adjusting the element accordingly.
        /// </summary>
        private void SetConnectionPoint(string elementType, int elementId, string point, Vector2 pos)
        {
            switch (elementType)
            {
                case "wheel":
                    var wheel = Wheels.FirstOrDefault(w => w.Id == elementId);
                    if (wheel == null) return;
                    if (point == "center")
                    {
                        wheel.Center = pos;
                    }
                    break;

                case "rod":
                    var rod = Rods.FirstOrDefault(r => r.Id == elementId);
                    if (rod == null) return;
                    if (point == "start")
                    {
                        var diff = pos - rod.Start;
                        rod.Start = pos;
                        rod.End += diff;
                    }
                    else if (point == "end")
                    {
                        rod.End = pos;
                    }
                    break;

                case "pivot":
                    // Pivots are fixed, don't move them
                    break;
            }
        }

        public void ClearTrace()
        {
            TracePoints.Clear();
        }

        public void ClearAll()
        {
            Wheels.Clear();
            Rods.Clear();
            Pivots.Clear();
            Pens.Clear();
            Connections.Clear();
            TracePoints.Clear();
        }
    }
}

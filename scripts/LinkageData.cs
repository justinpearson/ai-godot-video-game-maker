using Godot;
using System;
using System.Collections.Generic;

namespace TeaLeaves
{
    /// <summary>
    /// Data model for a wheel in the linkage system.
    /// Wheels are circular elements that can rotate to drive connected rods.
    /// </summary>
    public class WheelData
    {
        public int Id { get; set; }
        public Vector2 Center { get; set; }
        public float Radius { get; set; }
        public float Rotation { get; set; }
        /// <summary>Speed in revolutions per second. Only the "driver" wheel rotates automatically.</summary>
        public float Speed { get; set; }
        public bool IsDriver { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Data model for a rod (rigid link) connecting two points in the linkage.
    /// </summary>
    public class RodData
    {
        public int Id { get; set; }
        public Vector2 Start { get; set; }
        public Vector2 End { get; set; }
        public bool IsSelected { get; set; }

        public float Length => Start.DistanceTo(End);
    }

    /// <summary>
    /// Data model for a pivot — a fixed point where elements can rotate around.
    /// </summary>
    public class PivotData
    {
        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// A pen attachment on a wheel that traces a path during animation.
    /// Offset from wheel center, rotates with the wheel.
    /// </summary>
    public class PenData
    {
        public int Id { get; set; }
        public int AttachedWheelId { get; set; }
        /// <summary>Offset from wheel center in local coordinates.</summary>
        public Vector2 Offset { get; set; }
        public Color InkColor { get; set; } = Colors.Blue;
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Describes a connection/constraint between two elements.
    /// </summary>
    public class ConnectionData
    {
        public int Id { get; set; }
        /// <summary>"wheel", "rod", "pivot", "pen"</summary>
        public string ElementAType { get; set; } = "";
        public int ElementAId { get; set; }
        /// <summary>For wheels: "center" or "rim". For rods: "start" or "end".</summary>
        public string ElementAPoint { get; set; } = "";
        public string ElementBType { get; set; } = "";
        public int ElementBId { get; set; }
        public string ElementBPoint { get; set; } = "";
    }

    /// <summary>
    /// The tool the user is currently using.
    /// </summary>
    public enum LinkageTool
    {
        Select,
        Wheel,
        Rod,
        Pivot,
        Pen,
        Connect,
        Animate
    }

    /// <summary>
    /// What part of an element is being dragged.
    /// </summary>
    public enum DragTarget
    {
        None,
        WheelCenter,
        WheelEdge,
        RodStart,
        RodEnd,
        RodMiddle,
        PivotCenter,
        PenOffset,
        MultiMove
    }

    /// <summary>
    /// Records a traced point during animation for drawing curves.
    /// </summary>
    public struct TracePoint
    {
        public Vector2 Position;
        public Color Color;
    }
}

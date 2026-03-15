using GdUnit4;
using TeaLeaves;
using Godot;
using static GdUnit4.Assertions;

namespace TeaLeaves.Tests
{
    [TestSuite]
    public class LinkageSolverTests
    {
        [TestCase]
        public void NextId_Increments()
        {
            var solver = new LinkageSolver();
            var id1 = solver.NextId();
            var id2 = solver.NextId();
            AssertThat(id2).IsEqual(id1 + 1);
        }

        [TestCase]
        public void AddWheel_AppearsInList()
        {
            var solver = new LinkageSolver();
            var wheel = new WheelData
            {
                Id = solver.NextId(),
                Center = new Vector2(100, 200),
                Radius = 50
            };
            solver.Wheels.Add(wheel);
            AssertThat(solver.Wheels.Count).IsEqual(1);
            AssertThat(solver.Wheels[0].Center.X).IsEqual(100f);
        }

        [TestCase]
        public void Step_RotatesDriverWheel()
        {
            var solver = new LinkageSolver();
            var wheel = new WheelData
            {
                Id = solver.NextId(),
                Center = new Vector2(100, 100),
                Radius = 50,
                IsDriver = true,
                Speed = 1.0f // 1 revolution per second
            };
            solver.Wheels.Add(wheel);

            solver.Step(0.25f); // quarter second
            // Should have rotated by ~Tau/4 radians
            AssertThat(wheel.Rotation).IsGreater(0f);
        }

        [TestCase]
        public void Step_DoesNotRotateNonDriver()
        {
            var solver = new LinkageSolver();
            var wheel = new WheelData
            {
                Id = solver.NextId(),
                Center = new Vector2(100, 100),
                Radius = 50,
                IsDriver = false
            };
            solver.Wheels.Add(wheel);

            solver.Step(1.0f);
            AssertThat(wheel.Rotation).IsEqual(0f);
        }

        [TestCase]
        public void RodData_LengthCalculation()
        {
            var rod = new RodData
            {
                Id = 1,
                Start = new Vector2(0, 0),
                End = new Vector2(3, 4)
            };
            AssertThat(rod.Length).IsEqual(5f);
        }

        [TestCase]
        public void ClearAll_RemovesEverything()
        {
            var solver = new LinkageSolver();
            solver.Wheels.Add(new WheelData { Id = 1, Center = Vector2.Zero, Radius = 10 });
            solver.Rods.Add(new RodData { Id = 2, Start = Vector2.Zero, End = Vector2.One });
            solver.Pivots.Add(new PivotData { Id = 3, Position = Vector2.Zero });
            solver.Pens.Add(new PenData { Id = 4, AttachedWheelId = 1, Offset = Vector2.One });

            solver.ClearAll();

            AssertThat(solver.Wheels.Count).IsEqual(0);
            AssertThat(solver.Rods.Count).IsEqual(0);
            AssertThat(solver.Pivots.Count).IsEqual(0);
            AssertThat(solver.Pens.Count).IsEqual(0);
        }

        [TestCase]
        public void GetConnectionPoint_WheelCenter()
        {
            var solver = new LinkageSolver();
            var center = new Vector2(100, 200);
            solver.Wheels.Add(new WheelData { Id = 1, Center = center, Radius = 50 });

            var point = solver.GetConnectionPoint("wheel", 1, "center");
            AssertThat(point).IsNotNull();
            AssertThat(point!.Value.X).IsEqual(100f);
            AssertThat(point!.Value.Y).IsEqual(200f);
        }

        [TestCase]
        public void GetConnectionPoint_RodEndpoints()
        {
            var solver = new LinkageSolver();
            solver.Rods.Add(new RodData { Id = 1, Start = new Vector2(10, 20), End = new Vector2(30, 40) });

            var start = solver.GetConnectionPoint("rod", 1, "start");
            var end = solver.GetConnectionPoint("rod", 1, "end");

            AssertThat(start).IsNotNull();
            AssertThat(start!.Value.X).IsEqual(10f);
            AssertThat(end).IsNotNull();
            AssertThat(end!.Value.X).IsEqual(30f);
        }

        [TestCase]
        public void PenWorldPosition_NoRotation()
        {
            var solver = new LinkageSolver();
            var wheel = new WheelData { Id = 1, Center = new Vector2(100, 100), Radius = 50, Rotation = 0 };
            var pen = new PenData { Id = 2, AttachedWheelId = 1, Offset = new Vector2(30, 0) };

            var pos = solver.GetPenWorldPosition(pen, wheel);
            AssertThat(pos.X).IsEqual(130f);
            AssertThat(pos.Y).IsEqual(100f);
        }

        [TestCase]
        public void Step_RecordsTracePoints()
        {
            var solver = new LinkageSolver();
            var wheel = new WheelData
            {
                Id = 1,
                Center = new Vector2(100, 100),
                Radius = 50,
                IsDriver = true,
                Speed = 1.0f
            };
            solver.Wheels.Add(wheel);

            var pen = new PenData
            {
                Id = 2,
                AttachedWheelId = 1,
                Offset = new Vector2(30, 0),
                InkColor = Colors.Blue
            };
            solver.Pens.Add(pen);

            solver.Step(0.1f);
            AssertThat(solver.TracePoints.Count).IsEqual(1);
        }

        [TestCase]
        public void ClearTrace_RemovesPoints()
        {
            var solver = new LinkageSolver();
            solver.TracePoints.Add(new TracePoint { Position = Vector2.Zero, Color = Colors.Red });
            solver.TracePoints.Add(new TracePoint { Position = Vector2.One, Color = Colors.Blue });

            solver.ClearTrace();
            AssertThat(solver.TracePoints.Count).IsEqual(0);
        }
    }
}

using Godot;
using System.Collections.Generic;
using System.Linq;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class TrashWorld : Node2D
    {
        [Export] public PackedScene TrashBoxScene { get; set; } = null!;
        [Export] public int BoxCount { get; set; } = 8;

        private MathProblem _currentProblem = null!;
        private int _score = 0;
        private int _boxesDumped = 0;
        private Node2D _boxesContainer = null!;
        private Robot _robot = null!;
        private Camera2D _camera = null!;

        // Sounds
        private AudioStreamPlayer _dingSound = null!;
        private AudioStreamPlayer _wrongSound = null!;
        private AudioStreamPlayer _dumpSound = null!;
        private AudioStreamPlayer _celebrateSound = null!;

        // World layout
        private float _groundSize = 2000f;
        private Vector2 _dumpHolePosition = new Vector2(0, -350f);
        private float _dumpHoleRadius = 50f;

        // Dump hole label
        private Label _dumpLabel = null!;

        // Trash pile
        private List<TrashPileBlock> _trashPile = new();
        private float _pileBaseY;

        // Box values (pre-generated)
        private List<int> _boxValues = null!;
        private List<int> _remainingValues = null!;

        // Decorations
        private List<Vector2> _debrisPositions = null!;
        private List<float> _debrisSizes = null!;
        private List<int> _debrisTypes = null!;
        private List<Vector2> _rustPatchPositions = null!;
        private List<float> _rustPatchSizes = null!;

        // Auto-drive state
        private bool _waitingForDump = false;

        // Celebration state
        private bool _celebrating = false;
        private float _celebrateTimer = 0f;
        private List<ConfettiParticle> _confetti = new();

        private struct TrashPileBlock
        {
            public int Value;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public Color Color;
        }

        private struct ConfettiParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
        }

        public override void _Ready()
        {
            _boxesContainer = GetNode<Node2D>("Boxes");
            _robot = GetNode<Robot>("Robot");
            _camera = GetNode<Camera2D>("Camera2D");

            // Reparent camera to robot
            _camera.Reparent(_robot);
            _camera.Position = Vector2.Zero;

            // Load sounds
            _dingSound = new AudioStreamPlayer();
            _dingSound.Stream = GD.Load<AudioStream>("res://assets/sounds/ding.wav");
            AddChild(_dingSound);

            _wrongSound = new AudioStreamPlayer();
            _wrongSound.Stream = GD.Load<AudioStream>("res://assets/sounds/wrong.wav");
            AddChild(_wrongSound);

            _dumpSound = new AudioStreamPlayer();
            _dumpSound.Stream = GD.Load<AudioStream>("res://assets/sounds/dump.wav");
            AddChild(_dumpSound);

            _celebrateSound = new AudioStreamPlayer();
            _celebrateSound.Stream = GD.Load<AudioStream>("res://assets/sounds/celebrate.wav");
            AddChild(_celebrateSound);

            TrashBoxScene ??= GD.Load<PackedScene>("res://actors/TrashBox.tscn");

            _pileBaseY = _dumpHolePosition.Y + _dumpHoleRadius + 20f;

            // Create DUMP label near the hole
            _dumpLabel = new Label();
            _dumpLabel.Text = "DUMP";
            var dumpSettings = new LabelSettings();
            dumpSettings.FontSize = 20;
            dumpSettings.FontColor = new Color(0.95f, 0.85f, 0.1f);
            dumpSettings.OutlineSize = 2;
            dumpSettings.OutlineColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            _dumpLabel.LabelSettings = dumpSettings;
            _dumpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _dumpLabel.Size = new Vector2(80, 30);
            _dumpLabel.Position = _dumpHolePosition + new Vector2(-40, _dumpHoleRadius + 20f);
            AddChild(_dumpLabel);

            GenerateDecorations();
            SpawnAllBoxes();

            EventBus.Instance!.BoxTouched += OnBoxTouched;

            StartNextProblem();
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.BoxTouched -= OnBoxTouched;
        }

        private void GenerateDecorations()
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 42;
            float half = _groundSize / 2f;

            // Scattered debris (small junk on the ground)
            _debrisPositions = new List<Vector2>();
            _debrisSizes = new List<float>();
            _debrisTypes = new List<int>();
            for (int i = 0; i < 200; i++)
            {
                _debrisPositions.Add(new Vector2(rng.RandfRange(-half, half), rng.RandfRange(-half, half)));
                _debrisSizes.Add(rng.RandfRange(2f, 8f));
                _debrisTypes.Add(rng.RandiRange(0, 3));
            }

            // Rust patches on the ground
            _rustPatchPositions = new List<Vector2>();
            _rustPatchSizes = new List<float>();
            for (int i = 0; i < 100; i++)
            {
                _rustPatchPositions.Add(new Vector2(rng.RandfRange(-half, half), rng.RandfRange(-half, half)));
                _rustPatchSizes.Add(rng.RandfRange(15f, 50f));
            }
        }

        private void SpawnAllBoxes()
        {
            _boxValues = MathProblemGenerator.GenerateBoxValues(BoxCount);
            _remainingValues = new List<int>(_boxValues);

            var placedPositions = new List<Vector2>();
            float minSpacing = 100f;

            foreach (int val in _boxValues)
            {
                Vector2 pos = FindValidSpawnPosition(placedPositions, minSpacing);
                placedPositions.Add(pos);

                var box = TrashBoxScene.Instantiate<TrashBox>();
                _boxesContainer.AddChild(box);
                box.GlobalPosition = pos;
                box.SetValue(val);
            }
        }

        private Vector2 FindValidSpawnPosition(List<Vector2> existing, float minSpacing)
        {
            // Keep boxes in a manageable area around the center
            float spawnRange = 500f;

            for (int attempt = 0; attempt < 200; attempt++)
            {
                var candidate = new Vector2(
                    (float)GD.RandRange(-spawnRange, spawnRange),
                    (float)GD.RandRange(-spawnRange + 200f, spawnRange)
                );

                // Keep away from dump hole
                if (candidate.DistanceTo(_dumpHolePosition) < _dumpHoleRadius + 100f)
                    continue;

                // Keep away from robot start position (center)
                if (candidate.DistanceTo(Vector2.Zero) < 150f)
                    continue;

                bool tooClose = false;
                foreach (var placed in existing)
                {
                    if (candidate.DistanceTo(placed) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    return candidate;
            }

            return new Vector2((float)GD.RandRange(-200, 200), (float)GD.RandRange(100, 400));
        }

        private void OnBoxTouched(int value, Node2D boxNode)
        {
            if (_waitingForDump || _celebrating) return;

            if (value == _currentProblem.Answer)
            {
                _score += 10;
                _dingSound.Play();
                EventBus.Instance!.EmitCorrectAnswer(_score);

                // Attach box to robot
                if (boxNode is TrashBox trashBox)
                {
                    trashBox.Collect();
                    _robot.AttachBox(trashBox);
                }

                // Auto-drive to dump hole
                _waitingForDump = true;
                _robot.StartAutoDrive(_dumpHolePosition);
            }
            else
            {
                _wrongSound.Play();
                EventBus.Instance!.EmitWrongAnswer();

                if (boxNode is TrashBox trashBox)
                    trashBox.PlayShake();
            }
        }

        private void StartNextProblem()
        {
            if (_remainingValues.Count == 0)
            {
                StartCelebration();
                return;
            }

            // Pick a random remaining box value as the answer
            int index = (int)(GD.Randi() % (uint)_remainingValues.Count);
            int targetAnswer = _remainingValues[index];

            _currentProblem = MathProblemGenerator.GenerateForAnswer(targetAnswer);
            EventBus.Instance!.EmitNewProblem(_currentProblem.A, _currentProblem.B, _currentProblem.Answer);
        }

        private void DumpBox(int value)
        {
            _remainingValues.Remove(value);
            _boxesDumped++;

            // Add to trash pile visual
            float blockWidth = 50f + (float)GD.RandRange(-10f, 10f);
            float blockHeight = 30f + (float)GD.RandRange(-5f, 5f);

            // Stack upward from the pile base
            float stackY = _pileBaseY;
            foreach (var existing in _trashPile)
            {
                stackY = Mathf.Min(stackY, existing.Y - existing.Height);
            }

            Color[] pileColors = {
                new Color(0.72f, 0.52f, 0.3f),
                new Color(0.6f, 0.55f, 0.45f),
                new Color(0.55f, 0.5f, 0.4f),
                new Color(0.65f, 0.58f, 0.42f),
                new Color(0.5f, 0.48f, 0.4f),
            };

            float blockX = _dumpHolePosition.X + (float)GD.RandRange(-15f, 15f);

            _trashPile.Add(new TrashPileBlock
            {
                Value = value,
                X = blockX,
                Y = stackY,
                Width = blockWidth,
                Height = blockHeight,
                Color = pileColors[_boxesDumped % pileColors.Length],
            });

            // Add a label for the number on the pile block
            var pileLabel = new Label();
            pileLabel.Text = value.ToString();
            var pileLabelSettings = new LabelSettings();
            pileLabelSettings.FontSize = 16;
            pileLabelSettings.FontColor = new Color(1, 1, 1, 0.7f);
            pileLabelSettings.OutlineSize = 1;
            pileLabelSettings.OutlineColor = new Color(0, 0, 0, 0.5f);
            pileLabel.LabelSettings = pileLabelSettings;
            pileLabel.HorizontalAlignment = HorizontalAlignment.Center;
            pileLabel.Size = new Vector2(50, 25);
            pileLabel.Position = new Vector2(blockX - 25f, stackY - blockHeight + 3f);
            AddChild(pileLabel);

            _dumpSound.Play();
            EventBus.Instance!.EmitBoxDumped(value, _boxesDumped, BoxCount);
        }

        private void StartCelebration()
        {
            _celebrating = true;
            _celebrateTimer = 0f;
            _celebrateSound.Play();
            EventBus.Instance!.EmitAllBoxesCollected();

            // Spawn a burst of confetti
            SpawnConfettiBurst(200);
        }

        private void SpawnConfettiBurst(int count)
        {
            Color[] palette = {
                new Color(1f, 0.2f, 0.3f),
                new Color(1f, 0.85f, 0.1f),
                new Color(0.2f, 0.9f, 0.3f),
                new Color(0.3f, 0.6f, 1f),
                new Color(1f, 0.5f, 0.1f),
                new Color(0.9f, 0.3f, 0.9f),
                new Color(0.3f, 1f, 0.9f),
                new Color(1f, 1f, 0.3f),
            };

            for (int i = 0; i < count; i++)
            {
                float angle = (float)GD.RandRange(0, Mathf.Tau);
                float speed = (float)GD.RandRange(100, 500);
                _confetti.Add(new ConfettiParticle
                {
                    Position = _robot.GlobalPosition,
                    Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed - 200f),
                    Color = palette[(int)(GD.Randi() % (uint)palette.Length)],
                    Life = (float)GD.RandRange(2f, 4f),
                    MaxLife = 4f,
                    Size = (float)GD.RandRange(4f, 10f),
                    Rotation = (float)GD.RandRange(0, Mathf.Tau),
                    RotationSpeed = (float)GD.RandRange(-5f, 5f),
                });
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Check if robot arrived at dump hole during auto-drive
            if (_waitingForDump && !_robot.IsAutoDriving)
            {
                _waitingForDump = false;

                // Dump the box
                DumpBox(_currentProblem.Answer);

                // Detach and remove the carried box
                _robot.DetachBox();

                // Remove the collected TrashBox node
                foreach (var child in _boxesContainer.GetChildren())
                {
                    if (child is TrashBox tb && tb.IsCollected)
                    {
                        tb.QueueFree();
                        break;
                    }
                }

                // Start next problem after a short delay
                GetTree().CreateTimer(0.8).Timeout += StartNextProblem;
            }

            // Update confetti
            if (_celebrating)
            {
                _celebrateTimer += dt;

                // Spawn more confetti periodically during celebration
                if (_celebrateTimer < 3f && GD.Randf() < 0.3f)
                {
                    SpawnConfettiBurst(5);
                }

                for (int i = _confetti.Count - 1; i >= 0; i--)
                {
                    var c = _confetti[i];
                    c.Life -= dt;
                    if (c.Life <= 0)
                    {
                        _confetti.RemoveAt(i);
                        continue;
                    }
                    c.Position += c.Velocity * dt;
                    c.Velocity += new Vector2(0, 150f) * dt; // gravity
                    c.Rotation += c.RotationSpeed * dt;
                    _confetti[i] = c;
                }
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            float half = _groundSize / 2f;

            // Ground - dusty/industrial
            Color groundColor = new Color(0.45f, 0.42f, 0.38f);
            DrawRect(new Rect2(-half, -half, _groundSize, _groundSize), groundColor);

            // Rust patches for variety
            Color[] rustShades = {
                new Color(0.5f, 0.45f, 0.38f),
                new Color(0.42f, 0.4f, 0.35f),
                new Color(0.48f, 0.43f, 0.36f),
            };
            for (int i = 0; i < _rustPatchPositions.Count; i++)
            {
                DrawCircle(_rustPatchPositions[i], _rustPatchSizes[i],
                    rustShades[i % rustShades.Length]);
            }

            // Debris
            Color[] debrisColors = {
                new Color(0.55f, 0.5f, 0.4f),
                new Color(0.4f, 0.4f, 0.38f),
                new Color(0.6f, 0.45f, 0.3f),
                new Color(0.35f, 0.35f, 0.32f),
            };
            for (int i = 0; i < _debrisPositions.Count; i++)
            {
                DrawCircle(_debrisPositions[i], _debrisSizes[i], debrisColors[_debrisTypes[i]]);
            }

            // --- Dump hole ---
            DrawDumpHole();

            // --- Trash pile ---
            DrawTrashPile();

            // --- Ground border ---
            Color borderColor = new Color(0.35f, 0.32f, 0.28f);
            DrawRect(new Rect2(-half, -half, _groundSize, _groundSize), borderColor, false, 12f);

            // --- Confetti ---
            foreach (var c in _confetti)
            {
                float alpha = Mathf.Clamp(c.Life / c.MaxLife, 0f, 1f);
                var color = new Color(c.Color, alpha);
                DrawCircle(c.Position, c.Size, color);
            }
        }

        private void DrawDumpHole()
        {
            // Outer ring / warning stripes
            Color warningYellow = new Color(0.95f, 0.85f, 0.1f);
            Color warningBlack = new Color(0.15f, 0.15f, 0.12f);

            // Warning ring
            DrawArc(_dumpHolePosition, _dumpHoleRadius + 12f, 0, Mathf.Tau, 32, warningYellow, 8f);
            // Stripe marks
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (Mathf.Tau / 8f);
                var start = _dumpHolePosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_dumpHoleRadius + 8f);
                var end = _dumpHolePosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_dumpHoleRadius + 16f);
                DrawLine(start, end, warningBlack, 3f);
            }

            // Hole itself - dark gradient rings
            DrawCircle(_dumpHolePosition, _dumpHoleRadius, new Color(0.08f, 0.08f, 0.06f));
            DrawCircle(_dumpHolePosition, _dumpHoleRadius * 0.7f, new Color(0.04f, 0.04f, 0.03f));
            DrawCircle(_dumpHolePosition, _dumpHoleRadius * 0.4f, new Color(0.02f, 0.02f, 0.01f));

            // Edge highlight
            DrawArc(_dumpHolePosition, _dumpHoleRadius, 0, Mathf.Tau, 32, new Color(0.3f, 0.28f, 0.25f), 3f);

            // Arrow pointing to hole (if not celebrating)
            if (!_celebrating && !_waitingForDump)
            {
                // Small label above the hole
                // (drawn as simple shapes since we can't draw text in _Draw)
            }
        }

        private void DrawTrashPile()
        {
            foreach (var block in _trashPile)
            {
                float bx = block.X - block.Width / 2f;
                float by = block.Y - block.Height;

                // Block body
                DrawRect(new Rect2(bx, by, block.Width, block.Height), block.Color);

                // Block outline
                DrawRect(new Rect2(bx, by, block.Width, block.Height),
                    new Color(0.3f, 0.25f, 0.2f), false, 1.5f);

                // Number on block
                // (simplified - just a small mark)
                var centerMark = new Vector2(block.X, by + block.Height / 2f);
                DrawCircle(centerMark, 3f, new Color(1, 1, 1, 0.4f));

                // Highlight on top
                DrawLine(
                    new Vector2(bx + 2f, by + 2f),
                    new Vector2(bx + block.Width - 2f, by + 2f),
                    new Color(1, 1, 1, 0.2f), 1.5f);
            }
        }
    }
}

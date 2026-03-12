using Godot;
using System.Collections.Generic;
using TeaLeaves.Systems;

namespace TeaLeaves
{
    public partial class MathWorld : Node2D
    {
        [Export] public PackedScene NumberPickupScene { get; set; } = null!;
        [Export] public int NumberCount { get; set; } = 6;
        [Export] public float SpawnRadius { get; set; } = 280f;

        private MathProblem _currentProblem = null!;
        private int _score = 0;
        private Node2D _numbersContainer = null!;
        private AudioStreamPlayer _correctSound = null!;
        private AudioStreamPlayer _wrongSound = null!;
        private AudioStreamPlayer _celebrateSound = null!;
        private Player _player = null!;
        private Camera2D _camera = null!;

        private float _groundSize = 4000f;

        // Decoration storage
        private List<Vector2> _flowerPositions = null!;
        private List<Color> _flowerColors = null!;
        private List<float> _flowerSizes = null!;
        private List<Vector2> _grassTuftPositions = null!;
        private List<float> _grassTuftAngles = null!;
        private List<Vector2> _rockPositions = null!;
        private List<float> _rockSizes = null!;
        private List<Vector2> _grassPatchPositions = null!;
        private List<float> _grassPatchSizes = null!;
        private List<int> _grassPatchShade = null!;

        public override void _Ready()
        {
            _numbersContainer = GetNode<Node2D>("Numbers");
            _player = GetNode<Player>("Player");
            _camera = GetNode<Camera2D>("Camera2D");

            // Reparent camera to player so it follows automatically
            _camera.Reparent(_player);
            _camera.Position = Vector2.Zero;

            // Load sounds
            _correctSound = new AudioStreamPlayer();
            _correctSound.Stream = GD.Load<AudioStream>("res://assets/sounds/correct.wav");
            AddChild(_correctSound);

            _wrongSound = new AudioStreamPlayer();
            _wrongSound.Stream = GD.Load<AudioStream>("res://assets/sounds/wrong.wav");
            AddChild(_wrongSound);

            _celebrateSound = new AudioStreamPlayer();
            _celebrateSound.Stream = GD.Load<AudioStream>("res://assets/sounds/celebrate.wav");
            AddChild(_celebrateSound);

            // Load the NumberPickup scene if not set via export
            NumberPickupScene ??= GD.Load<PackedScene>("res://actors/NumberPickup.tscn");

            // Generate decorations with a fixed seed for consistency
            GenerateDecorations();

            // Subscribe to events
            EventBus.Instance!.NumberTouched += OnNumberTouched;

            // Start first problem
            StartNewProblem();
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.NumberTouched -= OnNumberTouched;
        }

        private void GenerateDecorations()
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 12345;

            float half = _groundSize / 2f;

            // Flowers: colorful dots scattered around
            _flowerPositions = new List<Vector2>();
            _flowerColors = new List<Color>();
            _flowerSizes = new List<float>();
            Color[] flowerPalette = new Color[]
            {
                new Color(1f, 0.3f, 0.4f),    // pink/red
                new Color(1f, 0.85f, 0.2f),   // yellow
                new Color(0.9f, 0.5f, 0.9f),  // purple
                new Color(1f, 0.6f, 0.2f),    // orange
                new Color(0.4f, 0.7f, 1f),    // light blue
                new Color(1f, 1f, 0.6f),      // pale yellow
            };

            for (int i = 0; i < 300; i++)
            {
                float x = rng.RandfRange(-half, half);
                float y = rng.RandfRange(-half, half);
                _flowerPositions.Add(new Vector2(x, y));
                _flowerColors.Add(flowerPalette[rng.RandiRange(0, flowerPalette.Length - 1)]);
                _flowerSizes.Add(rng.RandfRange(3f, 7f));
            }

            // Grass tufts: small line clusters
            _grassTuftPositions = new List<Vector2>();
            _grassTuftAngles = new List<float>();
            for (int i = 0; i < 400; i++)
            {
                float x = rng.RandfRange(-half, half);
                float y = rng.RandfRange(-half, half);
                _grassTuftPositions.Add(new Vector2(x, y));
                _grassTuftAngles.Add(rng.RandfRange(-0.4f, 0.4f));
            }

            // Rocks: small grey circles
            _rockPositions = new List<Vector2>();
            _rockSizes = new List<float>();
            for (int i = 0; i < 80; i++)
            {
                float x = rng.RandfRange(-half, half);
                float y = rng.RandfRange(-half, half);
                _rockPositions.Add(new Vector2(x, y));
                _rockSizes.Add(rng.RandfRange(4f, 10f));
            }

            // Grass patches: larger circles of varying green shades for ground variety
            _grassPatchPositions = new List<Vector2>();
            _grassPatchSizes = new List<float>();
            _grassPatchShade = new List<int>();
            for (int i = 0; i < 200; i++)
            {
                float x = rng.RandfRange(-half, half);
                float y = rng.RandfRange(-half, half);
                _grassPatchPositions.Add(new Vector2(x, y));
                _grassPatchSizes.Add(rng.RandfRange(20f, 60f));
                _grassPatchShade.Add(rng.RandiRange(0, 2));
            }
        }

        private void OnNumberTouched(int value)
        {
            if (value == _currentProblem.Answer)
            {
                _score += 10;
                _correctSound.Play();
                _celebrateSound.Play();
                EventBus.Instance!.EmitCorrectAnswer(_score);

                ClearNumbers();
                GetTree().CreateTimer(1.5).Timeout += StartNewProblem;
            }
            else
            {
                _wrongSound.Play();
                EventBus.Instance!.EmitWrongAnswer();
            }
        }

        private void StartNewProblem()
        {
            _currentProblem = MathProblemGenerator.Generate(maxAddend: 9);
            var distractors = MathProblemGenerator.GenerateDistractors(
                _currentProblem.Answer, NumberCount - 1);

            EventBus.Instance!.EmitNewProblem(_currentProblem.A, _currentProblem.B, _currentProblem.Answer);

            SpawnNumbers(_currentProblem.Answer, distractors);
        }

        private void SpawnNumbers(int correctAnswer, List<int> distractors)
        {
            var values = new List<int> { correctAnswer };
            values.AddRange(distractors);

            // Shuffle using Fisher-Yates
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = (int)(GD.Randi() % (uint)(i + 1));
                (values[i], values[j]) = (values[j], values[i]);
            }

            var placedPositions = new List<Vector2>();
            float minSpacing = 80f;
            float minPlayerDist = 120f;

            foreach (var val in values)
            {
                Vector2 pos = FindValidSpawnPosition(placedPositions, minSpacing, minPlayerDist);
                placedPositions.Add(pos);

                var pickup = NumberPickupScene.Instantiate<NumberPickup>();
                _numbersContainer.AddChild(pickup);
                pickup.GlobalPosition = pos;
                pickup.SetValue(val, val == correctAnswer);
            }
        }

        private Vector2 FindValidSpawnPosition(List<Vector2> existing, float minSpacing, float minPlayerDist)
        {
            Vector2 playerPos = _player.GlobalPosition;
            int maxAttempts = 100;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float angle = (float)GD.RandRange(0, Mathf.Tau);
                float dist = (float)GD.RandRange(minPlayerDist, SpawnRadius);
                Vector2 candidate = playerPos + new Vector2(
                    Mathf.Cos(angle) * dist,
                    Mathf.Sin(angle) * dist
                );

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

            // Fallback: just pick a random position even if spacing isn't perfect
            float fallbackAngle = (float)GD.RandRange(0, Mathf.Tau);
            float fallbackDist = (float)GD.RandRange(minPlayerDist, SpawnRadius);
            return playerPos + new Vector2(
                Mathf.Cos(fallbackAngle) * fallbackDist,
                Mathf.Sin(fallbackAngle) * fallbackDist
            );
        }

        private void ClearNumbers()
        {
            foreach (var child in _numbersContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        public override void _Draw()
        {
            float half = _groundSize / 2f;

            // Base ground color
            Color baseGreen = new Color(0.35f, 0.75f, 0.3f);
            DrawRect(new Rect2(-half, -half, _groundSize, _groundSize), baseGreen);

            // Three shades of green for grass patches
            Color[] greenShades = new Color[]
            {
                new Color(0.3f, 0.7f, 0.25f),   // darker green
                new Color(0.4f, 0.8f, 0.35f),   // lighter green
                new Color(0.32f, 0.68f, 0.28f),  // muted green
            };

            // Draw grass patches for ground variety
            for (int i = 0; i < _grassPatchPositions.Count; i++)
            {
                Color shade = greenShades[_grassPatchShade[i]];
                DrawCircle(_grassPatchPositions[i], _grassPatchSizes[i], shade);
            }

            // Draw grass tufts (small lines)
            Color tuftColor = new Color(0.25f, 0.6f, 0.2f);
            Color tuftColorLight = new Color(0.45f, 0.85f, 0.4f);
            for (int i = 0; i < _grassTuftPositions.Count; i++)
            {
                Vector2 pos = _grassTuftPositions[i];
                float ang = _grassTuftAngles[i];
                Color c = (i % 2 == 0) ? tuftColor : tuftColorLight;

                // Draw 3 little blades of grass
                Vector2 tipLeft = pos + new Vector2(-4f + ang * 5f, -10f);
                Vector2 tipCenter = pos + new Vector2(ang * 3f, -13f);
                Vector2 tipRight = pos + new Vector2(4f + ang * 5f, -10f);

                DrawLine(pos, tipLeft, c, 1.5f);
                DrawLine(pos, tipCenter, c, 1.5f);
                DrawLine(pos, tipRight, c, 1.5f);
            }

            // Draw rocks
            Color rockColor = new Color(0.55f, 0.55f, 0.5f);
            Color rockHighlight = new Color(0.65f, 0.65f, 0.6f);
            for (int i = 0; i < _rockPositions.Count; i++)
            {
                DrawCircle(_rockPositions[i], _rockSizes[i], rockColor);
                // Small highlight
                DrawCircle(_rockPositions[i] + new Vector2(-1f, -1f), _rockSizes[i] * 0.5f, rockHighlight);
            }

            // Draw flowers
            for (int i = 0; i < _flowerPositions.Count; i++)
            {
                Vector2 pos = _flowerPositions[i];
                float size = _flowerSizes[i];
                Color color = _flowerColors[i];

                // Draw petals (small circles around center)
                float petalDist = size * 0.6f;
                for (int p = 0; p < 5; p++)
                {
                    float pAngle = p * (Mathf.Tau / 5f);
                    Vector2 petalPos = pos + new Vector2(
                        Mathf.Cos(pAngle) * petalDist,
                        Mathf.Sin(pAngle) * petalDist
                    );
                    DrawCircle(petalPos, size * 0.45f, color);
                }

                // Flower center
                Color centerColor = new Color(1f, 0.95f, 0.4f);
                DrawCircle(pos, size * 0.35f, centerColor);
            }

            // Draw a soft border around the edge so the player knows the boundary
            Color borderColor = new Color(0.2f, 0.5f, 0.15f);
            float borderWidth = 12f;
            DrawRect(new Rect2(-half, -half, _groundSize, _groundSize), borderColor, false, borderWidth);
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }
    }
}

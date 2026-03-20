using Godot;
using System;
using System.Collections.Generic;
using TeaLeaves.Systems;

namespace TeaLeaves;

public partial class WreckingBallGame : Node2D
{
    // Layout
    private const float GroundY = 950f;
    private const float CraneTrackY = 60f;
    private const float CraneHookY = 130f;
    private const float CraneSpeed = 500f;
    private const float RopeLength = 600f;

    // Ball
    private const float BallRadius = 42f;
    private const float BallMass = 60f;

    // Building
    private const float BuildingCenterX = 1250f;
    private const float BrickWidth = 55f;
    private const float BrickHeight = 25f;
    private const int BuildingRows = 16;
    private const int BaseColumns = 8;

    // Pump assist: horizontal force applied to ball when crane moves
    private const float PumpForce = 2000f;

    // State
    private Vector2 _cranePos;
    private WreckingBall _ball = null!;
    private Line2D _chainLine = null!;
    private Camera2D _camera = null!;
    private readonly List<BrickData> _bricks = new();
    private int _totalBricks;
    private int _destroyedCount;
    private int _score;
    private float _shakeTimer;
    private float _shakeIntensity;
    private readonly RandomNumberGenerator _shakeRng = new();

    private bool _buildingUnfrozen;
    private AudioStreamPlayer _impactSound = null!;

    // Brick dragging
    private RigidBody2D? _draggedBrick;
    private Vector2 _dragOffset;

    // Crane visual node (draws on top of everything)
    private CraneRenderer _craneRenderer = null!;

    private struct BrickData
    {
        public RigidBody2D Body;
        public Vector2 OriginalPos;
    }

    // Brick colors — warm masonry tones
    private static readonly Color[] BrickColors =
    {
        new("#CD853F"), new("#D2691E"), new("#B8860B"),
        new("#A0522D"), new("#8B4513"), new("#BC8F8F"),
        new("#C4A882"), new("#B07040"),
    };

    public override void _Ready()
    {
        _cranePos = new Vector2(350f, CraneHookY);
        _shakeRng.Randomize();

        CreateBackground();
        CreateGround();
        CreateBuilding();
        CreateBallAndChain();
        CreateCamera();

        // Impact sound
        _impactSound = new AudioStreamPlayer();
        _impactSound.Stream = GD.Load<AudioStream>("res://assets/sounds/pop.mp3");
        _impactSound.VolumeDb = -5f;
        AddChild(_impactSound);

        // Crane/rope renderer on top of everything
        _craneRenderer = new CraneRenderer(this) { ZIndex = 10 };
        AddChild(_craneRenderer);

        _totalBricks = _bricks.Count;
        EventBus.Instance?.EmitBlocksUpdate(_totalBricks - _destroyedCount, _totalBricks, _score);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Move crane
        float dir = Input.GetAxis("move_left", "move_right");
        _cranePos.X += dir * CraneSpeed * dt;
        _cranePos.X = Mathf.Clamp(_cranePos.X, 80f, 1840f);

        // Pump assist — apply horizontal force when crane moves
        if (Mathf.Abs(dir) > 0.1f && _ball != null)
        {
            _ball.ApplyForce(new Vector2(dir * PumpForce, 0));
        }

        // Unfreeze building when ball approaches
        if (!_buildingUnfrozen && _ball != null)
        {
            float buildingLeft = BuildingCenterX - BaseColumns * BrickWidth / 2f;
            float buildingRight = BuildingCenterX + BaseColumns * BrickWidth / 2f;
            float buildingTop = GroundY - BuildingRows * BrickHeight;
            Vector2 bp = _ball.GlobalPosition;
            float margin = BallRadius + 10f;

            if (bp.X + margin > buildingLeft && bp.X - margin < buildingRight &&
                bp.Y + margin > buildingTop && bp.Y - margin < GroundY)
            {
                _buildingUnfrozen = true;
                for (int i = 0; i < _bricks.Count; i++)
                {
                    var b = _bricks[i].Body;
                    if (b != null && IsInstanceValid(b))
                        b.Freeze = false;
                }
            }
        }

        // Update chain visual
        UpdateChainLine();

        // Screen shake
        if (_shakeTimer > 0)
        {
            _shakeTimer -= dt;
            float mag = _shakeIntensity * Mathf.Max(_shakeTimer, 0);
            _camera.Offset = new Vector2(
                _shakeRng.RandfRange(-1, 1) * mag,
                _shakeRng.RandfRange(-1, 1) * mag
            );
            if (_shakeTimer <= 0) _camera.Offset = Vector2.Zero;
        }

        // Check bricks
        CheckBricks();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            GetTree().ReloadCurrentScene();
            return;
        }

        // Brick dragging with mouse
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                // Try to pick up a brick under the cursor
                var spaceState = GetWorld2D().DirectSpaceState;
                var query = new PhysicsPointQueryParameters2D
                {
                    Position = GetGlobalMousePosition(),
                    CollisionMask = 8, // building layer
                    CollideWithBodies = true,
                };
                var results = spaceState.IntersectPoint(query);

                if (results.Count > 0)
                {
                    var body = results[0]["collider"].As<RigidBody2D>();
                    if (body != null)
                    {
                        _draggedBrick = body;
                        _dragOffset = body.GlobalPosition - GetGlobalMousePosition();
                        _draggedBrick.Freeze = true;
                        _draggedBrick.FreezeMode = RigidBody2D.FreezeModeEnum.Kinematic;
                        _draggedBrick.CollisionLayer = 0; // ghost while dragging
                    }
                }
            }
            else if (_draggedBrick != null)
            {
                // Release the brick — let physics take over
                _draggedBrick.CollisionLayer = 8;
                _draggedBrick.Freeze = false;
                _draggedBrick.LinearVelocity = Vector2.Zero;
                _draggedBrick.AngularVelocity = 0;
                _draggedBrick = null;
            }
        }

        if (@event is InputEventMouseMotion && _draggedBrick != null)
        {
            _draggedBrick.GlobalPosition = GetGlobalMousePosition() + _dragOffset;
        }
    }

    // ───────────────────────── Creation helpers ─────────────────────────

    private void CreateBackground()
    {
        Color skyTop = new("#4A90D9");
        Color skyBottom = new("#87CEEB");
        int steps = 20;
        float stepH = GroundY / steps;
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            var rect = new ColorRect
            {
                Position = new Vector2(0, i * stepH),
                Size = new Vector2(1920, stepH + 1),
                Color = skyTop.Lerp(skyBottom, t),
                ZIndex = -10,
            };
            AddChild(rect);
        }

        // Clouds
        var rng = new RandomNumberGenerator { Seed = 42 };
        for (int i = 0; i < 8; i++)
        {
            float cx = rng.RandfRange(100, 1820);
            float cy = rng.RandfRange(80, 350);
            for (int j = 0; j < 5; j++)
            {
                float ox = rng.RandfRange(-50, 50);
                float oy = rng.RandfRange(-18, 18);
                float r = rng.RandfRange(22, 45);
                var cloud = new Polygon2D
                {
                    Color = new Color(1, 1, 1, 0.65f),
                    Polygon = MakeCirclePoly(r, 16),
                    Position = new Vector2(cx + ox, cy + oy),
                    ZIndex = -9,
                };
                AddChild(cloud);
            }
        }
    }

    private void CreateGround()
    {
        // Grass strip
        AddChild(new ColorRect
        {
            Position = new Vector2(0, GroundY - 6),
            Size = new Vector2(1920, 18),
            Color = new("#5B8731"),
            ZIndex = -1,
        });
        // Dirt
        AddChild(new ColorRect
        {
            Position = new Vector2(0, GroundY + 12),
            Size = new Vector2(1920, 200),
            Color = new("#8B7355"),
            ZIndex = -1,
        });

        // Physics body
        var ground = new StaticBody2D { Position = new Vector2(960, GroundY) };
        ground.CollisionLayer = 1;
        ground.CollisionMask = 0;

        // Floor
        var floorShape = new CollisionShape2D();
        var floorRect = new RectangleShape2D { Size = new Vector2(2400, 40) };
        floorShape.Shape = floorRect;
        floorShape.Position = new Vector2(0, 20);
        ground.AddChild(floorShape);

        // Left wall
        var leftWall = new CollisionShape2D();
        leftWall.Shape = new RectangleShape2D { Size = new Vector2(40, 2000) };
        leftWall.Position = new Vector2(-1220, -500);
        ground.AddChild(leftWall);

        // Right wall
        var rightWall = new CollisionShape2D();
        rightWall.Shape = new RectangleShape2D { Size = new Vector2(40, 2000) };
        rightWall.Position = new Vector2(1220, -500);
        ground.AddChild(rightWall);

        AddChild(ground);
    }

    private void CreateBuilding()
    {
        var container = new Node2D { Name = "Building" };
        AddChild(container);

        var rng = new RandomNumberGenerator { Seed = 12345 };

        for (int row = 0; row < BuildingRows; row++)
        {
            int cols = BaseColumns - row / 5;
            if (cols < 3) cols = 3;

            // Zero-gap positioning: bricks sit flush on each other for stability
            float totalW = cols * BrickWidth;
            float startX = BuildingCenterX - totalW / 2f;
            // Row 0 bottom sits exactly on GroundY
            float centerY = GroundY - row * BrickHeight - BrickHeight / 2f;

            for (int col = 0; col < cols; col++)
            {
                float centerX = startX + col * BrickWidth + BrickWidth / 2f;
                int ci = (row * 3 + col) % BrickColors.Length;
                Color c = BrickColors[ci].Lightened(rng.RandfRange(-0.05f, 0.08f));
                CreateBrick(container, centerX, centerY, BrickWidth, BrickHeight, c);
            }
        }
    }

    private void CreateBrick(Node parent, float x, float y, float w, float h, Color color)
    {
        var brick = new RigidBody2D
        {
            Mass = 5f,
            GravityScale = 1f,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.05f, Friction = 1.0f },
            CollisionLayer = 8,     // building
            CollisionMask = 1 | 4 | 8, // ground + ball + building
            LinearDamp = 2.0f,
            AngularDamp = 2.0f,
            Freeze = true,
            FreezeMode = RigidBody2D.FreezeModeEnum.Static,
            CanSleep = true,
        };

        var shape = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(w, h) },
        };
        brick.AddChild(shape);

        // Mortar background (dark border, slightly smaller than collision)
        float m = 1.5f; // mortar thickness
        var face = new Polygon2D
        {
            Polygon = new[] {
                new Vector2(-w / 2 + m, -h / 2 + m), new Vector2(w / 2 - m, -h / 2 + m),
                new Vector2(w / 2 - m, h / 2 - m),   new Vector2(-w / 2 + m, h / 2 - m),
            },
            Color = color,
        };
        brick.AddChild(face);

        // Inner highlight
        float b = 3.5f;
        var highlight = new Polygon2D
        {
            Polygon = new[] {
                new Vector2(-w / 2 + b, -h / 2 + b), new Vector2(w / 2 - b, -h / 2 + b),
                new Vector2(w / 2 - b, h / 2 - b),   new Vector2(-w / 2 + b, h / 2 - b),
            },
            Color = color.Lightened(0.12f),
        };
        brick.AddChild(highlight);

        parent.AddChild(brick);
        brick.GlobalPosition = new Vector2(x, y);

        _bricks.Add(new BrickData { Body = brick, OriginalPos = new Vector2(x, y) });
    }

    private void CreateBallAndChain()
    {
        // Wrecking ball
        _ball = new WreckingBall
        {
            Mass = BallMass,
            GravityScale = 1f,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.15f, Friction = 0.4f },
            CollisionLayer = 4,         // ball
            CollisionMask = 1 | 8,      // ground + building
            ContactMonitor = true,
            MaxContactsReported = 8,
            ContinuousCd = RigidBody2D.CcdMode.CastRay,
        };

        var ballShape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = BallRadius },
        };
        _ball.AddChild(ballShape);

        AddChild(_ball);
        _ball.GlobalPosition = _cranePos + new Vector2(0, RopeLength);
        _ball.Init(RopeLength, () => _cranePos);
        _ball.BodyEntered += OnBallBodyEntered;

        // Chain visual
        _chainLine = new Line2D
        {
            Width = 5f,
            DefaultColor = new Color("#4A4A4A"),
            ZIndex = 9,
        };
        AddChild(_chainLine);
    }

    private void CreateCamera()
    {
        _camera = new Camera2D
        {
            Position = new Vector2(960, 540),
        };
        AddChild(_camera);
        _camera.MakeCurrent();
    }

    // ───────────────────────── Runtime ─────────────────────────

    private void UpdateChainLine()
    {
        if (_ball == null || _chainLine == null) return;
        Vector2 start = _cranePos;
        Vector2 end = _ball.GlobalPosition;
        int links = 12;
        _chainLine.ClearPoints();
        for (int i = 0; i <= links; i++)
        {
            float t = (float)i / links;
            _chainLine.AddPoint(start.Lerp(end, t));
        }
    }

    private void OnBallBodyEntered(Node body)
    {
        if (body is RigidBody2D)
        {
            float speed = _ball.LinearVelocity.Length();
            if (speed > 80f)
            {
                _shakeTimer = Mathf.Clamp(speed / 800f, 0.15f, 0.5f);
                _shakeIntensity = Mathf.Clamp(speed / 60f, 3f, 18f);
                if (!_impactSound.Playing)
                {
                    _impactSound.PitchScale = Mathf.Clamp(speed / 400f, 0.6f, 1.4f);
                    _impactSound.Play();
                }
            }
        }
    }

    private void CheckBricks()
    {
        int destroyed = 0;
        for (int i = 0; i < _bricks.Count; i++)
        {
            var info = _bricks[i];
            if (info.Body == null || !IsInstanceValid(info.Body))
            {
                destroyed++;
                continue;
            }

            float dist = info.Body.GlobalPosition.DistanceTo(info.OriginalPos);
            if (dist > 50f || info.Body.GlobalPosition.Y > GroundY + 80)
            {
                destroyed++;
            }

            // Free bricks far below screen to save resources
            if (info.Body.GlobalPosition.Y > 1400f)
            {
                info.Body.QueueFree();
            }
        }

        if (destroyed != _destroyedCount)
        {
            _destroyedCount = destroyed;
            _score = destroyed * 10;
            EventBus.Instance?.EmitBlocksUpdate(
                _totalBricks - _destroyedCount, _totalBricks, _score);

            if (_destroyedCount >= (int)(_totalBricks * 0.9f))
            {
                EventBus.Instance?.EmitGameWon(_score);
            }
        }
    }

    private static Vector2[] MakeCirclePoly(float radius, int segments)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.Tau / segments;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }

    // ═══════════════════════ Crane renderer (draws on top) ═══════════════════════

    private partial class CraneRenderer : Node2D
    {
        private readonly WreckingBallGame _g;

        public CraneRenderer(WreckingBallGame game) { _g = game; }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            Color trackDark = new("#3A4A5A");
            Color track = new("#607080");
            Color ropeDark = new("#4A4A4A");
            Color ballMain = new("#2A2A2A");
            Color ballHi = new("#505050");

            // Track beam
            DrawRect(new Rect2(0, CraneTrackY - 16, 1920, 32), trackDark);
            DrawRect(new Rect2(0, CraneTrackY - 12, 1920, 24), track);

            Vector2 cp = _g._cranePos;

            // Trolley
            DrawRect(new Rect2(cp.X - 24, CraneTrackY - 20, 48, 40), trackDark);
            DrawRect(new Rect2(cp.X - 20, CraneTrackY - 16, 40, 32), track);
            // Wheels
            DrawCircle(new Vector2(cp.X - 14, CraneTrackY + 18), 6, trackDark);
            DrawCircle(new Vector2(cp.X + 14, CraneTrackY + 18), 6, trackDark);

            // Vertical arm
            float armTop = CraneTrackY + 20;
            DrawRect(new Rect2(cp.X - 6, armTop, 12, cp.Y - armTop), trackDark);
            DrawRect(new Rect2(cp.X - 4, armTop, 8, cp.Y - armTop), track);

            // Hook circle
            DrawCircle(cp, 9, trackDark);
            DrawCircle(cp, 6, track);

            if (_g._ball == null) return;
            Vector2 ballPos = _g._ball.GlobalPosition;

            // Chain link circles along rope
            float chainDist = cp.DistanceTo(ballPos);
            int linkCount = Mathf.Max((int)(chainDist / 28f), 1);
            for (int i = 0; i <= linkCount; i++)
            {
                float t = (float)i / linkCount;
                Vector2 p = cp.Lerp(ballPos, t);
                DrawCircle(p, 6, ropeDark);
                DrawCircle(p, 3.5f, track);
            }

            // Wrecking ball
            DrawCircle(ballPos, BallRadius + 2, new Color(0.08f, 0.08f, 0.08f));
            DrawCircle(ballPos, BallRadius, ballMain);
            // Highlight
            DrawCircle(ballPos + new Vector2(-12, -12), BallRadius * 0.28f, ballHi);
            // Rim
            DrawArc(ballPos, BallRadius, 0, Mathf.Tau, 48,
                new Color(0.15f, 0.15f, 0.15f), 2.5f);
        }
    }
}

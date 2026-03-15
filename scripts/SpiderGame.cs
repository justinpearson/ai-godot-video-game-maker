using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaLeaves
{
    /// <summary>
    /// Main game controller for Itsy Bitsy Spider.
    /// Manages the house, two water spouts, clouds, spiders, rain, and scoring.
    /// </summary>
    public partial class SpiderGame : Node2D
    {
        // Game state
        private int _score;
        private int _lives = 5;
        private bool _gameOver;
        private float _spawnTimer;
        private float _spawnInterval = 2.5f;
        private float _difficultyTimer;
        private float _minSpawnInterval = 0.6f;
        private RandomNumberGenerator _rng = new();

        // Spouts (left=0, right=1)
        private Vector2[] _spoutTop = new Vector2[2];
        private Vector2[] _spoutBottom = new Vector2[2];
        private Vector2[] _cloudCenter = new Vector2[2];
        private bool[] _isRaining = new bool[2];
        private float[] _rainTimer = new float[2];
        private const float RainDuration = 1.8f;

        // Spiders
        private List<SpiderData> _spiders = new();

        // Rain particles
        private List<RainDrop> _rainDrops = new();

        // Splash particles
        private List<SplashParticle> _splashes = new();

        // Layout constants
        private const float HouseWidth = 400;
        private const float HouseHeight = 350;
        private const float RoofHeight = 180;
        private const float SpoutWidth = 30;
        private const float SpoutLength = 420;
        private const float CloudWidth = 120;
        private const float CloudHeight = 70;

        // Colors
        private static readonly Color HouseColor = new(0.95f, 0.85f, 0.7f);
        private static readonly Color RoofColor = new(0.7f, 0.25f, 0.2f);
        private static readonly Color SpoutColor = new(0.6f, 0.65f, 0.7f);
        private static readonly Color SpoutDark = new(0.45f, 0.5f, 0.55f);
        private static readonly Color CloudColor = new(0.85f, 0.88f, 0.92f);
        private static readonly Color CloudDark = new(0.55f, 0.6f, 0.7f);
        private static readonly Color RainCloudColor = new(0.45f, 0.5f, 0.6f);
        private static readonly Color SkyColor = new(0.55f, 0.78f, 0.95f);
        private static readonly Color GrassColor = new(0.35f, 0.7f, 0.3f);
        private static readonly Color GrassDark = new(0.25f, 0.55f, 0.2f);
        private static readonly Color WindowColor = new(0.6f, 0.82f, 0.95f);
        private static readonly Color DoorColor = new(0.5f, 0.3f, 0.15f);
        private static readonly Color RainColor = new(0.4f, 0.6f, 0.9f, 0.7f);
        private static readonly Color SpiderBlack = new(0.1f, 0.1f, 0.1f);
        private static readonly Color SpiderBrown = new(0.35f, 0.2f, 0.1f);

        // Sounds
        private AudioStreamPlayer? _rainSound;
        private AudioStreamPlayer? _splashSound;
        private AudioStreamPlayer? _alertSound;
        private AudioStreamPlayer? _gameOverSound;

        // Center of screen
        private Vector2 _center;
        private float _groundY;

        public override void _Ready()
        {
            _center = new Vector2(960, 540);
            _groundY = _center.Y + HouseHeight / 2 + 40;

            // Left spout
            float leftSpoutX = _center.X - HouseWidth / 2 - SpoutWidth / 2;
            _spoutBottom[0] = new Vector2(leftSpoutX, _groundY);
            _spoutTop[0] = new Vector2(leftSpoutX, _groundY - SpoutLength);
            _cloudCenter[0] = new Vector2(leftSpoutX, _groundY - SpoutLength - CloudHeight - 20);

            // Right spout
            float rightSpoutX = _center.X + HouseWidth / 2 + SpoutWidth / 2;
            _spoutBottom[1] = new Vector2(rightSpoutX, _groundY);
            _spoutTop[1] = new Vector2(rightSpoutX, _groundY - SpoutLength);
            _cloudCenter[1] = new Vector2(rightSpoutX, _groundY - SpoutLength - CloudHeight - 20);

            // Load sounds
            _rainSound = CreateSoundPlayer("res://assets/sounds/rain.wav");
            _splashSound = CreateSoundPlayer("res://assets/sounds/splash.wav");
            _alertSound = CreateSoundPlayer("res://assets/sounds/alert.wav");
            _gameOverSound = CreateSoundPlayer("res://assets/sounds/gameover.wav");

            _rng.Randomize();
        }

        private AudioStreamPlayer? CreateSoundPlayer(string path)
        {
            if (!FileAccess.FileExists(path)) return null;
            var player = new AudioStreamPlayer();
            player.Stream = GD.Load<AudioStream>(path);
            player.VolumeDb = -8;
            AddChild(player);
            return player;
        }

        public override void _Process(double delta)
        {
            if (_gameOver) { QueueRedraw(); return; }

            var dt = (float)delta;

            UpdateRain(dt);
            UpdateSpiders(dt);
            UpdateRainDrops(dt);
            UpdateSplashes(dt);
            UpdateSpawning(dt);
            UpdateDifficulty(dt);

            QueueRedraw();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_gameOver)
            {
                // Restart on click
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    RestartGame();
                }
                return;
            }

            if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
            {
                var pos = click.GlobalPosition;
                for (int i = 0; i < 2; i++)
                {
                    if (IsInsideCloud(pos, i))
                    {
                        StartRain(i);
                    }
                }
            }
        }

        private bool IsInsideCloud(Vector2 pos, int index)
        {
            var cc = _cloudCenter[index];
            // Generous elliptical hit area
            var dx = (pos.X - cc.X) / (CloudWidth * 0.7f);
            var dy = (pos.Y - cc.Y) / (CloudHeight * 0.6f);
            return dx * dx + dy * dy <= 1.0f;
        }

        // ==================== RAIN ====================

        private void StartRain(int spoutIndex)
        {
            _isRaining[spoutIndex] = true;
            _rainTimer[spoutIndex] = RainDuration;
            _rainSound?.Play();
            SpawnRainDrops(spoutIndex);
        }

        private void UpdateRain(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_isRaining[i])
                {
                    _rainTimer[i] -= dt;

                    // Continuously spawn rain drops while raining
                    if (_rainTimer[i] > 0)
                    {
                        SpawnRainDrops(i, 3);
                    }
                    else
                    {
                        _isRaining[i] = false;
                    }
                }
            }
        }

        private void SpawnRainDrops(int spoutIndex, int count = 15)
        {
            var top = _spoutTop[spoutIndex];
            var bottom = _spoutBottom[spoutIndex];

            for (int i = 0; i < count; i++)
            {
                var startY = top.Y + _rng.RandfRange(0, 40);
                _rainDrops.Add(new RainDrop
                {
                    Position = new Vector2(top.X + _rng.RandfRange(-SpoutWidth / 2, SpoutWidth / 2), startY),
                    Velocity = new Vector2(_rng.RandfRange(-15, 15), _rng.RandfRange(300, 600)),
                    Size = _rng.RandfRange(2, 5),
                    SpoutIndex = spoutIndex,
                    Life = 1.0f
                });
            }
        }

        private void UpdateRainDrops(float dt)
        {
            for (int i = _rainDrops.Count - 1; i >= 0; i--)
            {
                var drop = _rainDrops[i];
                drop.Position += drop.Velocity * dt;
                drop.Velocity += new Vector2(0, 400 * dt); // gravity
                drop.Life -= dt * 0.5f;

                // Constrain to spout width
                var spoutX = _spoutTop[drop.SpoutIndex].X;
                drop.Position = new Vector2(
                    Mathf.Clamp(drop.Position.X, spoutX - SpoutWidth / 2, spoutX + SpoutWidth / 2),
                    drop.Position.Y
                );

                _rainDrops[i] = drop;

                if (drop.Position.Y > _groundY || drop.Life <= 0)
                {
                    // Splash at bottom
                    SpawnSplash(drop.Position);
                    _rainDrops.RemoveAt(i);
                }
            }
        }

        // ==================== SPLASHES ====================

        private void SpawnSplash(Vector2 pos)
        {
            for (int i = 0; i < 3; i++)
            {
                _splashes.Add(new SplashParticle
                {
                    Position = pos,
                    Velocity = new Vector2(_rng.RandfRange(-80, 80), _rng.RandfRange(-120, -40)),
                    Life = 0.4f,
                    Size = _rng.RandfRange(1.5f, 3.5f)
                });
            }
        }

        private void UpdateSplashes(float dt)
        {
            for (int i = _splashes.Count - 1; i >= 0; i--)
            {
                var s = _splashes[i];
                s.Position += s.Velocity * dt;
                s.Velocity += new Vector2(0, 300 * dt);
                s.Life -= dt;
                _splashes[i] = s;

                if (s.Life <= 0) _splashes.RemoveAt(i);
            }
        }

        // ==================== SPIDERS ====================

        private void UpdateSpawning(float dt)
        {
            _spawnTimer -= dt;
            if (_spawnTimer <= 0)
            {
                int spoutIndex = _rng.RandiRange(0, 1);
                SpawnSpider(spoutIndex);
                _spawnTimer = _spawnInterval;
            }
        }

        private void UpdateDifficulty(float dt)
        {
            _difficultyTimer += dt;
            // Gradually decrease spawn interval
            _spawnInterval = Mathf.Max(_minSpawnInterval, 2.5f - _difficultyTimer * 0.03f);
        }

        private void SpawnSpider(int spoutIndex)
        {
            _spiders.Add(new SpiderData
            {
                SpoutIndex = spoutIndex,
                Progress = 0, // 0 = bottom, 1 = top
                Speed = _rng.RandfRange(0.06f, 0.14f),
                LegPhase = _rng.Randf() * Mathf.Tau,
                Size = _rng.RandfRange(12, 20),
                ColorTint = _rng.Randf() > 0.5f ? SpiderBlack : SpiderBrown,
                WashOutVelocity = Vector2.Zero,
                IsWashedOut = false,
                WashOutPos = Vector2.Zero
            });
        }

        private void UpdateSpiders(float dt)
        {
            for (int i = _spiders.Count - 1; i >= 0; i--)
            {
                var spider = _spiders[i];

                if (spider.IsWashedOut)
                {
                    // Falling down animation
                    spider.WashOutPos += spider.WashOutVelocity * dt;
                    spider.WashOutVelocity += new Vector2(0, 500 * dt);
                    spider.WashOutLife -= dt;

                    _spiders[i] = spider;
                    if (spider.WashOutLife <= 0)
                    {
                        _spiders.RemoveAt(i);
                    }
                    continue;
                }

                // Climbing up
                spider.Progress += spider.Speed * dt;
                spider.LegPhase += dt * 8;

                // Check if washed out by rain
                if (_isRaining[spider.SpoutIndex])
                {
                    WashOutSpider(ref spider);
                    _spiders[i] = spider;
                    _score += 10;
                    EventBus.Instance?.EmitSpiderWashedOut();
                    EventBus.Instance?.EmitScoreChanged(_score);
                    _splashSound?.Play();
                    continue;
                }

                _spiders[i] = spider;

                // Reached the top!
                if (spider.Progress >= 1.0f)
                {
                    _spiders.RemoveAt(i);
                    _lives--;
                    _alertSound?.Play();
                    EventBus.Instance?.EmitSpiderReachedTop(spider.SpoutIndex);
                    EventBus.Instance?.EmitLivesChanged(_lives);

                    if (_lives <= 0)
                    {
                        _gameOver = true;
                        _gameOverSound?.Play();
                        EventBus.Instance?.EmitGameOver(_score);
                    }
                }
            }
        }

        private void WashOutSpider(ref SpiderData spider)
        {
            var spoutTop = _spoutTop[spider.SpoutIndex];
            var spoutBottom = _spoutBottom[spider.SpoutIndex];
            var spiderPos = spoutBottom.Lerp(spoutTop, spider.Progress);

            spider.IsWashedOut = true;
            spider.WashOutPos = spiderPos;
            spider.WashOutVelocity = new Vector2(
                _rng.RandfRange(-100, 100),
                _rng.RandfRange(100, 300)
            );
            spider.WashOutLife = 1.2f;

            // Spawn extra splash where spider was
            for (int j = 0; j < 5; j++)
            {
                SpawnSplash(spiderPos);
            }
        }

        private void RestartGame()
        {
            _score = 0;
            _lives = 5;
            _gameOver = false;
            _spawnTimer = 1.0f;
            _spawnInterval = 2.5f;
            _difficultyTimer = 0;
            _spiders.Clear();
            _rainDrops.Clear();
            _splashes.Clear();
            _isRaining[0] = false;
            _isRaining[1] = false;

            EventBus.Instance?.EmitScoreChanged(0);
            EventBus.Instance?.EmitLivesChanged(5);
        }

        // ==================== DRAWING ====================

        public override void _Draw()
        {
            DrawSky();
            DrawGrass();
            DrawHouse();
            DrawSpouts();
            DrawRainDrops();
            DrawSpiders();
            DrawSplashes();
            DrawClouds();
            DrawHUD();

            if (_gameOver)
            {
                DrawGameOver();
            }
        }

        private void DrawSky()
        {
            // Gradient sky
            for (int y = 0; y < 1080; y += 4)
            {
                float t = y / 1080f;
                var skyTop = new Color(0.4f, 0.65f, 0.95f);
                var skyBottom = new Color(0.7f, 0.85f, 0.98f);
                var color = skyTop.Lerp(skyBottom, t);
                DrawRect(new Rect2(0, y, 1920, 4), color, true);
            }
        }

        private void DrawGrass()
        {
            // Ground
            DrawRect(new Rect2(0, _groundY, 1920, 1080 - _groundY), GrassColor, true);

            // Grass tufts
            var seed = 42;
            for (int i = 0; i < 60; i++)
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                float x = (seed % 1920);
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                float h = 8 + (seed % 20);
                for (int b = -1; b <= 1; b++)
                {
                    var basePos = new Vector2(x + b * 4, _groundY);
                    var tipPos = new Vector2(x + b * 8, _groundY - h);
                    DrawLine(basePos, tipPos, GrassDark, 2);
                }
            }
        }

        private void DrawHouse()
        {
            float hx = _center.X - HouseWidth / 2;
            float hy = _center.Y - HouseHeight / 2 + 40;

            // House body
            DrawRect(new Rect2(hx, hy, HouseWidth, HouseHeight), HouseColor, true);
            DrawRect(new Rect2(hx, hy, HouseWidth, HouseHeight), new Color(0.6f, 0.5f, 0.4f), false, 3);

            // Roof
            var roofPeak = new Vector2(_center.X, hy - RoofHeight);
            var roofLeft = new Vector2(hx - 30, hy);
            var roofRight = new Vector2(hx + HouseWidth + 30, hy);
            DrawPolygon(new Vector2[] { roofPeak, roofLeft, roofRight }, new Color[] { RoofColor, RoofColor, RoofColor });
            DrawLine(roofPeak, roofLeft, new Color(0.5f, 0.15f, 0.1f), 3);
            DrawLine(roofPeak, roofRight, new Color(0.5f, 0.15f, 0.1f), 3);
            DrawLine(roofLeft, roofRight, new Color(0.5f, 0.15f, 0.1f), 3);

            // Windows (2 on each side)
            float windowSize = 60;
            float windowY = hy + 50;
            DrawWindow(new Vector2(hx + 60, windowY), windowSize);
            DrawWindow(new Vector2(hx + HouseWidth - 60 - windowSize, windowY), windowSize);

            // Door
            float doorW = 70;
            float doorH = 110;
            float doorX = _center.X - doorW / 2;
            float doorY = hy + HouseHeight - doorH;
            DrawRect(new Rect2(doorX, doorY, doorW, doorH), DoorColor, true);
            DrawRect(new Rect2(doorX, doorY, doorW, doorH), new Color(0.3f, 0.18f, 0.08f), false, 2);
            // Doorknob
            DrawCircle(new Vector2(doorX + doorW - 15, doorY + doorH / 2), 4, new Color(0.8f, 0.7f, 0.2f));

            // Chimney
            float chimX = _center.X + 60;
            float chimW = 35;
            float chimH = 80;
            float chimY = hy - RoofHeight * 0.4f;
            DrawRect(new Rect2(chimX, chimY, chimW, chimH), new Color(0.6f, 0.3f, 0.25f), true);
            DrawRect(new Rect2(chimX - 3, chimY, chimW + 6, 8), new Color(0.5f, 0.25f, 0.2f), true);
        }

        private void DrawWindow(Vector2 pos, float size)
        {
            DrawRect(new Rect2(pos, new Vector2(size, size)), WindowColor, true);
            DrawRect(new Rect2(pos, new Vector2(size, size)), new Color(0.4f, 0.35f, 0.3f), false, 2);
            // Cross panes
            DrawLine(new Vector2(pos.X + size / 2, pos.Y), new Vector2(pos.X + size / 2, pos.Y + size), new Color(0.4f, 0.35f, 0.3f), 2);
            DrawLine(new Vector2(pos.X, pos.Y + size / 2), new Vector2(pos.X + size, pos.Y + size / 2), new Color(0.4f, 0.35f, 0.3f), 2);
        }

        private void DrawSpouts()
        {
            for (int i = 0; i < 2; i++)
            {
                var top = _spoutTop[i];
                var bottom = _spoutBottom[i];

                // Spout body (3D-ish pipe)
                var rect = new Rect2(top.X - SpoutWidth / 2, top.Y, SpoutWidth, bottom.Y - top.Y);
                DrawRect(rect, SpoutColor, true);

                // Dark edge for 3D effect
                DrawLine(new Vector2(top.X - SpoutWidth / 2, top.Y), new Vector2(bottom.X - SpoutWidth / 2, bottom.Y), SpoutDark, 2);
                DrawLine(new Vector2(top.X + SpoutWidth / 2, top.Y), new Vector2(bottom.X + SpoutWidth / 2, bottom.Y), SpoutDark, 2);

                // Highlight stripe
                DrawLine(
                    new Vector2(top.X - SpoutWidth / 4, top.Y),
                    new Vector2(bottom.X - SpoutWidth / 4, bottom.Y),
                    new Color(0.75f, 0.78f, 0.82f), 3
                );

                // Bracket at top connecting to roof
                DrawRect(new Rect2(top.X - SpoutWidth / 2 - 5, top.Y - 5, SpoutWidth + 10, 10), SpoutDark, true);

                // Bracket at bottom
                DrawRect(new Rect2(bottom.X - SpoutWidth / 2 - 5, bottom.Y - 5, SpoutWidth + 10, 10), SpoutDark, true);

                // Rain flow overlay when raining
                if (_isRaining[i])
                {
                    var flowColor = new Color(0.3f, 0.5f, 0.85f, 0.35f);
                    DrawRect(new Rect2(top.X - SpoutWidth / 3, top.Y, SpoutWidth * 0.66f, bottom.Y - top.Y), flowColor, true);
                }
            }
        }

        private void DrawClouds()
        {
            for (int i = 0; i < 2; i++)
            {
                var cc = _cloudCenter[i];
                bool raining = _isRaining[i];
                var mainColor = raining ? RainCloudColor : CloudColor;
                var shadowColor = raining ? new Color(0.35f, 0.4f, 0.5f) : CloudDark;

                // Cloud body (overlapping circles)
                DrawCircle(cc + new Vector2(-30, 8), 35, shadowColor);
                DrawCircle(cc + new Vector2(30, 8), 30, shadowColor);
                DrawCircle(cc + new Vector2(0, -5), 40, mainColor);
                DrawCircle(cc + new Vector2(-35, 5), 32, mainColor);
                DrawCircle(cc + new Vector2(35, 5), 28, mainColor);
                DrawCircle(cc + new Vector2(-15, -15), 25, new Color(mainColor, 0.9f));
                DrawCircle(cc + new Vector2(20, -12), 22, new Color(mainColor, 0.9f));

                // Highlight on top
                if (!raining)
                {
                    DrawCircle(cc + new Vector2(-10, -22), 15, new Color(1, 1, 1, 0.3f));
                }

                // "Click me" indicator — subtle pulsing glow
                if (!raining)
                {
                    var pulse = (Mathf.Sin((float)Time.GetTicksMsec() / 500.0f) + 1) * 0.5f;
                    DrawArc(cc, CloudWidth * 0.6f, 0, Mathf.Tau, 32, new Color(1, 1, 0.5f, 0.1f + pulse * 0.15f), 2);
                }
            }
        }

        private void DrawRainDrops()
        {
            foreach (var drop in _rainDrops)
            {
                var alpha = Mathf.Clamp(drop.Life, 0, 1);
                var color = new Color(RainColor.R, RainColor.G, RainColor.B, alpha * 0.8f);
                // Elongated drop
                DrawLine(drop.Position, drop.Position + new Vector2(0, drop.Size * 3), color, drop.Size);
            }
        }

        private void DrawSplashes()
        {
            foreach (var s in _splashes)
            {
                var alpha = Mathf.Clamp(s.Life / 0.4f, 0, 1);
                DrawCircle(s.Position, s.Size * alpha, new Color(0.5f, 0.7f, 1.0f, alpha * 0.6f));
            }
        }

        private void DrawSpiders()
        {
            foreach (var spider in _spiders)
            {
                Vector2 pos;
                float alpha = 1.0f;

                if (spider.IsWashedOut)
                {
                    pos = spider.WashOutPos;
                    alpha = Mathf.Clamp(spider.WashOutLife / 1.2f, 0, 1);
                }
                else
                {
                    var spoutTop = _spoutTop[spider.SpoutIndex];
                    var spoutBottom = _spoutBottom[spider.SpoutIndex];
                    pos = spoutBottom.Lerp(spoutTop, spider.Progress);
                }

                DrawSpider(pos, spider.Size, spider.LegPhase, spider.ColorTint, alpha, spider.IsWashedOut);
            }
        }

        private void DrawSpider(Vector2 pos, float size, float legPhase, Color tint, float alpha, bool washedOut)
        {
            var color = new Color(tint.R, tint.G, tint.B, alpha);
            var legColor = new Color(tint.R * 0.8f, tint.G * 0.8f, tint.B * 0.8f, alpha);

            float rotation = washedOut ? legPhase * 2 : 0;

            // 8 legs (4 per side)
            for (int side = -1; side <= 1; side += 2)
            {
                for (int leg = 0; leg < 4; leg++)
                {
                    float angle = (leg - 1.5f) * 0.4f + rotation;
                    float legAnim = Mathf.Sin(legPhase + leg * 1.2f) * 0.3f;
                    angle += legAnim;

                    var mid = pos + new Vector2(
                        Mathf.Cos(angle + side * 0.5f) * size * 0.9f * side,
                        Mathf.Sin(angle) * size * 0.5f - size * 0.3f
                    );
                    var tip = mid + new Vector2(
                        Mathf.Cos(angle + side * 0.3f + legAnim * 0.5f) * size * 0.7f * side,
                        Mathf.Abs(Mathf.Sin(angle + legAnim)) * size * 0.5f + size * 0.1f
                    );

                    DrawLine(pos, mid, legColor, 1.5f);
                    DrawLine(mid, tip, legColor, 1.0f);
                }
            }

            // Body (abdomen + head)
            DrawCircle(pos + new Vector2(0, size * 0.15f), size * 0.55f, color); // abdomen
            DrawCircle(pos - new Vector2(0, size * 0.3f), size * 0.35f, color);  // head

            // Eyes
            var eyeColor = new Color(1, 1, 1, alpha);
            var pupilColor = new Color(0, 0, 0, alpha);
            float eyeSize = size * 0.12f;
            DrawCircle(pos - new Vector2(size * 0.15f, size * 0.35f), eyeSize, eyeColor);
            DrawCircle(pos - new Vector2(-size * 0.15f, size * 0.35f), eyeSize, eyeColor);
            DrawCircle(pos - new Vector2(size * 0.15f, size * 0.35f), eyeSize * 0.5f, pupilColor);
            DrawCircle(pos - new Vector2(-size * 0.15f, size * 0.35f), eyeSize * 0.5f, pupilColor);
        }

        private void DrawHUD()
        {
            // Score
            DrawString(ThemeDB.FallbackFont, new Vector2(30, 40), $"Score: {_score}",
                HorizontalAlignment.Left, -1, 28, Colors.White);

            // Lives (hearts)
            for (int i = 0; i < _lives; i++)
            {
                DrawCircle(new Vector2(1800 - i * 35, 30), 12, new Color(0.9f, 0.2f, 0.2f));
                DrawCircle(new Vector2(1800 - i * 35 - 6, 24), 7, new Color(0.9f, 0.2f, 0.2f));
                DrawCircle(new Vector2(1800 - i * 35 + 6, 24), 7, new Color(0.9f, 0.2f, 0.2f));
            }

            // Instructions
            DrawString(ThemeDB.FallbackFont, new Vector2(960, 1050),
                "Click the clouds to make rain and wash out the spiders!",
                HorizontalAlignment.Center, -1, 18, new Color(1, 1, 1, 0.7f));
        }

        private void DrawGameOver()
        {
            // Overlay
            DrawRect(new Rect2(0, 0, 1920, 1080), new Color(0, 0, 0, 0.6f), true);

            // Game over text
            DrawString(ThemeDB.FallbackFont, new Vector2(960, 400), "GAME OVER",
                HorizontalAlignment.Center, -1, 64, Colors.White);
            DrawString(ThemeDB.FallbackFont, new Vector2(960, 480), $"Final Score: {_score}",
                HorizontalAlignment.Center, -1, 36, new Color(1, 0.9f, 0.3f));
            DrawString(ThemeDB.FallbackFont, new Vector2(960, 560), "Click anywhere to play again",
                HorizontalAlignment.Center, -1, 22, new Color(1, 1, 1, 0.7f));
        }
    }

    // ==================== DATA CLASSES ====================

    public struct SpiderData
    {
        public int SpoutIndex;
        public float Progress; // 0=bottom, 1=top
        public float Speed;
        public float LegPhase;
        public float Size;
        public Color ColorTint;
        public bool IsWashedOut;
        public Vector2 WashOutPos;
        public Vector2 WashOutVelocity;
        public float WashOutLife;
    }

    public struct RainDrop
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int SpoutIndex;
        public float Life;
    }

    public struct SplashParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float Size;
    }
}

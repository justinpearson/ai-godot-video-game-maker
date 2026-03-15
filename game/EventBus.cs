using Godot;

namespace TeaLeaves
{
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; } = null!;

        public delegate void SpiderReachedTopHandler(int spoutIndex);
        public event SpiderReachedTopHandler? SpiderReachedTop;

        public delegate void SpiderWashedOutHandler();
        public event SpiderWashedOutHandler? SpiderWashedOut;

        public delegate void ScoreChangedHandler(int score);
        public event ScoreChangedHandler? ScoreChanged;

        public delegate void LivesChangedHandler(int lives);
        public event LivesChangedHandler? LivesChanged;

        public delegate void GameOverHandler(int finalScore);
        public event GameOverHandler? GameOver;

        public override void _Ready()
        {
            Instance = this;
        }

        public void EmitSpiderReachedTop(int spoutIndex) => SpiderReachedTop?.Invoke(spoutIndex);
        public void EmitSpiderWashedOut() => SpiderWashedOut?.Invoke();
        public void EmitScoreChanged(int score) => ScoreChanged?.Invoke(score);
        public void EmitLivesChanged(int lives) => LivesChanged?.Invoke(lives);
        public void EmitGameOver(int finalScore) => GameOver?.Invoke(finalScore);
    }
}

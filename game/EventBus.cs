using Godot;

namespace TeaLeaves.Systems
{
    public partial class EventBus : Node
    {
        public static EventBus? Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
        }

        // ── Math Adventure (legacy, kept so old scripts compile) ──

        public delegate void NumberTouchedHandler(int value, Node2D pickup);
        public event NumberTouchedHandler? NumberTouched;
        public void EmitNumberTouched(int value, Node2D pickup) { NumberTouched?.Invoke(value, pickup); }

        public delegate void CorrectAnswerHandler(int newScore);
        public event CorrectAnswerHandler? CorrectAnswer;
        public void EmitCorrectAnswer(int newScore) { CorrectAnswer?.Invoke(newScore); }

        public delegate void WrongAnswerHandler();
        public event WrongAnswerHandler? WrongAnswer;
        public void EmitWrongAnswer() { WrongAnswer?.Invoke(); }

        public delegate void NewProblemHandler(int a, int b, int answer);
        public event NewProblemHandler? NewProblem;
        public void EmitNewProblem(int a, int b, int answer) { NewProblem?.Invoke(a, b, answer); }

        // ── Wrecking Ball ──

        public delegate void BlocksUpdateHandler(int remaining, int total, int score);
        public event BlocksUpdateHandler? BlocksUpdate;
        public void EmitBlocksUpdate(int remaining, int total, int score) { BlocksUpdate?.Invoke(remaining, total, score); }

        public delegate void GameWonHandler(int finalScore);
        public event GameWonHandler? GameWon;
        public void EmitGameWon(int finalScore) { GameWon?.Invoke(finalScore); }
    }
}

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

        // BoxTouched — player touched a trash box
        public delegate void BoxTouchedHandler(int value, Node2D box);
        public event BoxTouchedHandler? BoxTouched;
        public void EmitBoxTouched(int value, Node2D box) { BoxTouched?.Invoke(value, box); }

        // CorrectAnswer — correct box collected
        public delegate void CorrectAnswerHandler(int newScore);
        public event CorrectAnswerHandler? CorrectAnswer;
        public void EmitCorrectAnswer(int newScore) { CorrectAnswer?.Invoke(newScore); }

        // WrongAnswer — wrong box touched
        public delegate void WrongAnswerHandler();
        public event WrongAnswerHandler? WrongAnswer;
        public void EmitWrongAnswer() { WrongAnswer?.Invoke(); }

        // NewProblem — new math problem generated
        public delegate void NewProblemHandler(int a, int b, int answer);
        public event NewProblemHandler? NewProblem;
        public void EmitNewProblem(int a, int b, int answer) { NewProblem?.Invoke(a, b, answer); }

        // BoxDumped — box was dumped into the trash pile
        public delegate void BoxDumpedHandler(int value, int totalDumped, int totalBoxes);
        public event BoxDumpedHandler? BoxDumped;
        public void EmitBoxDumped(int value, int totalDumped, int totalBoxes) { BoxDumped?.Invoke(value, totalDumped, totalBoxes); }

        // AllBoxesCollected — game complete!
        public delegate void AllBoxesCollectedHandler();
        public event AllBoxesCollectedHandler? AllBoxesCollected;
        public void EmitAllBoxesCollected() { AllBoxesCollected?.Invoke(); }
    }
}

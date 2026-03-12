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

        // NumberTouched — player touched a number pickup
        public delegate void NumberTouchedHandler(int value);
        public event NumberTouchedHandler? NumberTouched;
        public void EmitNumberTouched(int value) { NumberTouched?.Invoke(value); }

        // CorrectAnswer — correct answer chosen
        public delegate void CorrectAnswerHandler(int newScore);
        public event CorrectAnswerHandler? CorrectAnswer;
        public void EmitCorrectAnswer(int newScore) { CorrectAnswer?.Invoke(newScore); }

        // WrongAnswer — wrong answer chosen
        public delegate void WrongAnswerHandler();
        public event WrongAnswerHandler? WrongAnswer;
        public void EmitWrongAnswer() { WrongAnswer?.Invoke(); }

        // NewProblem — new math problem generated
        public delegate void NewProblemHandler(int a, int b, int answer);
        public event NewProblemHandler? NewProblem;
        public void EmitNewProblem(int a, int b, int answer) { NewProblem?.Invoke(a, b, answer); }
    }
}

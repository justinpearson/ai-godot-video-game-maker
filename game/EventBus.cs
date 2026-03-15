using Godot;

namespace TeaLeaves
{
    /// <summary>
    /// Typed EventBus for cross-system communication.
    /// Subscribe in _Ready(), unsubscribe in _ExitTree().
    /// </summary>
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; } = null!;

        // Linkage element events
        public delegate void ElementAddedHandler(string elementType);
        public event ElementAddedHandler? ElementAdded;

        public delegate void ElementDeletedHandler(int count);
        public event ElementDeletedHandler? ElementDeleted;

        public delegate void SelectionChangedHandler(int selectedCount);
        public event SelectionChangedHandler? SelectionChanged;

        public delegate void ToolChangedHandler(string toolName);
        public event ToolChangedHandler? ToolChanged;

        public delegate void PlaybackToggledHandler(bool isPlaying);
        public event PlaybackToggledHandler? PlaybackToggled;

        public override void _Ready()
        {
            Instance = this;
        }

        public void EmitElementAdded(string elementType) => ElementAdded?.Invoke(elementType);
        public void EmitElementDeleted(int count) => ElementDeleted?.Invoke(count);
        public void EmitSelectionChanged(int selectedCount) => SelectionChanged?.Invoke(selectedCount);
        public void EmitToolChanged(string toolName) => ToolChanged?.Invoke(toolName);
        public void EmitPlaybackToggled(bool isPlaying) => PlaybackToggled?.Invoke(isPlaying);
    }
}

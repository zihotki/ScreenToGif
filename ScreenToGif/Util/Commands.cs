using System.Windows.Input;

namespace ScreenToGif.Util
{
    /// <summary>
    /// Custom Commands.
    /// </summary>
    public static class Commands
    {
        /// <summary>
        /// New Recording Command, Ctrl + N
        /// </summary>
        public static RoutedUICommand NewRecording { get; set; } = new RoutedUICommand("New Recording", "NewRecording", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.N, ModifierKeys.Control, "Ctrl + N") });

        /// <summary>
        /// New Webcam Recording Command, Ctrl + W
        /// </summary>
        public static RoutedUICommand NewWebcamRecording { get; set; } = new RoutedUICommand("New Webcam Recording", "NewWebcamRecording", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.W, ModifierKeys.Control, "Ctrl + W") });

        /// <summary>
        /// New Board Recording Command, Ctrl + B
        /// </summary>
        public static RoutedUICommand NewBoardRecording { get; set; } = new RoutedUICommand("New Board Recording", "Board", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.B, ModifierKeys.Control, "Ctrl + B") });

        /// <summary>
        /// Save as Command, Ctrl + S
        /// </summary>
        public static RoutedUICommand SaveAs { get; set; } = new RoutedUICommand("Save As", "SaveAs", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control, "Ctrl + S") });

        /// <summary>
        /// Discart Project Command, Ctrl + Delete
        /// </summary>
        public static RoutedUICommand DiscardProject { get; set; } = new RoutedUICommand("Discard Project", "DiscardProject", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Delete, ModifierKeys.Control, "Ctrl + Delete") });

        /// <summary>
        /// OverrideDelay Command, Alt + O
        /// </summary>
        public static RoutedUICommand OverrideDelay { get; set; } = new RoutedUICommand("Override Delay", "OverrideDelay", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Alt, "Alt + O") });

        /// <summary>
        /// IncreaseDecreaseDelay Command, Alt + Y
        /// </summary>
        public static RoutedUICommand ChangeDelay { get; set; } = new RoutedUICommand("Change Delay", "IncreaseDecreaseDelay", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Y, ModifierKeys.Alt, "Alt + Y") });

        /// <summary>
        /// ScaleDelay Command, Alt + 5
        /// </summary>
        public static RoutedUICommand ScaleDelay { get; set; } = new RoutedUICommand("Scale Delay", "ScaleDelay", typeof(Commands),
              new InputGestureCollection { new KeyGesture(Key.D5, ModifierKeys.Alt, "Alt + 5") });

        /// <summary>
        /// Zoom100 Command, Alt + 0
        /// </summary>
        public static RoutedUICommand Zoom100 { get; set; } = new RoutedUICommand("Set Zoom to 100%", "Zoom100", typeof(Commands),
              new InputGestureCollection { new KeyGesture(Key.D0, ModifierKeys.Alt, "Alt + 0") });

        /// <summary>
        /// SizeToContent Command, Alt + 1
        /// </summary>
        public static RoutedUICommand SizeToContent { get; set; } = new RoutedUICommand("Size to Content", "SizeToContent", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.D1, ModifierKeys.Alt, "Alt + 1") });

        /// <summary>
        /// FitImage Command, Alt + -
        /// </summary>
        public static RoutedUICommand FitImage { get; set; } = new RoutedUICommand("Fit Image", "FitImage", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.OemMinus, ModifierKeys.Alt, "Alt + -") });

        /// <summary>
        /// FirstFrame Command, Home
        /// </summary>
        public static RoutedUICommand FirstFrame { get; set; } = new RoutedUICommand("Select First Frame", "FirstFrame", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Home, ModifierKeys.None, "Home") });

        /// <summary>
        /// PreviousFrame Command, PageUp
        /// </summary>
        public static RoutedUICommand PreviousFrame { get; set; } = new RoutedUICommand("Select Previous Frame", "PreviousFrame", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.PageUp, ModifierKeys.None, "PageUp") });

        /// <summary>
        /// Play Command, Alt + P
        /// </summary>
        public static RoutedUICommand Play { get; set; } = new RoutedUICommand("Play", "Play", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.P, ModifierKeys.Alt, "Alt + P"), new KeyGesture(Key.Space) });

        /// <summary>
        /// NextFrame Command, PageDown
        /// </summary>
        public static RoutedUICommand NextFrame { get; set; } = new RoutedUICommand("Select Next Frame", "NextFrame", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.PageDown, ModifierKeys.None, "PageDown") });

        /// <summary>
        /// LastFrame Command, End
        /// </summary>
        public static RoutedUICommand LastFrame { get; set; } = new RoutedUICommand("Select Last Frame", "LastFrame", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.End, ModifierKeys.None, "End") });

        /// <summary>
        /// Reset Command, Ctrl + R
        /// </summary>
        public static RoutedUICommand Reset { get; set; } = new RoutedUICommand("Reset", "Reset", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.R, ModifierKeys.Control, "Ctrl + R") });

        /// <summary>
        /// DeletePrevious Command, Alt + Left
        /// </summary>
        public static RoutedUICommand DeletePrevious { get; set; } = new RoutedUICommand("Delete All Previous Frames", "DeletePrevious", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Left, ModifierKeys.Alt, "Alt + Left") });

        /// <summary>
        /// DeleteNext Command, Alt + Right
        /// </summary>
        public static RoutedUICommand DeleteNext { get; set; } = new RoutedUICommand("Delete All Next Frames", "DeleteNext", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Right, ModifierKeys.Alt, "Alt + Right") });

        /// <summary>
        /// Go To Command, Ctrl + G
        /// </summary>
        public static RoutedUICommand GoTo { get; set; } = new RoutedUICommand("Go To the Selected Frame", "GoTo", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.G, ModifierKeys.Control, "Ctrl + G") });

        /// <summary>
        /// Options Command, Ctrl + Alt + O
        /// </summary>
        public static RoutedUICommand Options { get; set; } = new RoutedUICommand("Options", "Options", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control | ModifierKeys.Alt, "Ctrl + Alt + O") });

        /// <summary>
        /// Enable/Disable Thin mode Command, No Input
        /// </summary>
        public static RoutedUICommand EnableThinMode { get; set; } = new RoutedUICommand("Enable Thin Mode", "EnableThinMode", typeof(Commands));

        /// <summary>
        /// Enable/Disable Snap to Window Command, "Ctrl + Alt + Z"
        /// </summary>
        public static RoutedUICommand EnableSnapToWindow { get; set; } = new RoutedUICommand("Enable Snap To Window", "EnableSnapToWindow", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Alt, "Ctrl + Alt + Z") });

        /// <summary>
        /// Ok Action Command, Alt + E
        /// </summary>
        public static RoutedUICommand OkAction { get; set; } = new RoutedUICommand("Ok", "OkAction", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.E, ModifierKeys.Alt, "Alt + E"), new KeyGesture(Key.Enter) });

        /// <summary>
        /// Cancel Action Command, Esc
        /// </summary>
        public static RoutedUICommand CancelAction { get; set; } = new RoutedUICommand("Cancel", "CancelAction", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.Escape, ModifierKeys.None, "Esc") });

        public static readonly RoutedUICommand Exit = new RoutedUICommand("Exit", "Exit", typeof(Commands),
            new InputGestureCollection { new KeyGesture(Key.F4, ModifierKeys.Alt) });
    }
}

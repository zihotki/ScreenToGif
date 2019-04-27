namespace ScreenToGif.Util
{
    /// <summary>
    /// Animation export type.
    /// </summary>
    public enum Export
    {
        /// <summary>
        /// Graphics Interchange Format.
        /// </summary>
        Gif,

        /// <summary>
        /// Animated Portable Network Graphics.
        /// </summary>
        Apng,

        /// <summary>
        /// Any type of video.
        /// </summary>
        Video
    }

    /// <summary>
    /// The types of Panel of the Editor window.
    /// Positive values means that there's no preview overlay.
    /// </summary>
    public enum PanelType
    {
        /// <summary>
        /// Override Delay Panel.
        /// </summary>
        OverrideDelay = 6,

        /// <summary>
        /// Change Delay Panel.
        /// </summary>
        IncreaseDecreaseDelay = 7,

        ScaleDelay = 8,

        /// <summary>
        /// Reduce Frame Count Panel.
        /// </summary>
        ReduceFrames = 11,

        /// <summary>
        /// Mouse Clicks Panel.
        /// </summary>
        MouseClicks = 14,

        
        /// <summary>
        /// Border Panel.
        /// </summary>
        Border = -8,

        /// <summary>
        /// Progress Panel.
        /// </summary>
        Progress = -10,

        /// <summary>
        /// Key Strokes Panel.
        /// </summary>
        KeyStrokes = -11,

    }

    /// <summary>
    /// Stage status of the recording process.
    /// </summary>
    public enum Stage
    {
        /// <summary>
        /// Recording stopped.
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// Recording active.
        /// </summary>
        Recording = 1,

        /// <summary>
        /// Recording paused.
        /// </summary>
        Paused = 2,

        /// <summary>
        /// The recording is being discarded.
        /// </summary>
        Discarding = 5
    }

    /// <summary>
    /// EncoderListBox Item Status.
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// Processing encoding/uploading status.
        /// </summary>
        Processing,

        /// <summary>
        /// The Encoding was canceled. So aparently "cancelled" (with two L's) is also a valid grammar. Huh, that's strange.
        /// </summary>
        Canceled,

        /// <summary>
        /// An error hapenned with the encoding process.
        /// </summary>
        Error,

        /// <summary>
        /// Encoding done.
        /// </summary>
        Completed,

        /// <summary>
        /// File deleted or Moved.
        /// </summary>
        FileDeletedOrMoved
    }

    /// <summary>
    /// Exit actions after closing the Recording Window.
    /// </summary>
    public enum ExitAction
    {
        /// <summary>
        /// Return to the StartUp Window.
        /// </summary>
        Return = 0,

        /// <summary>
        /// Something was recorded. Go to the Editor.
        /// </summary>
        Recorded = 1,

        /// <summary>
        /// Exit the application.
        /// </summary>
        Exit = 2,
    }

    /// <summary>
    /// Type of delay change action.
    /// </summary>
    public enum DelayChangeType
    {
        Override,
        IncreaseDecrease,
        Scale
    }

    /// <summary>
    /// Type of the gif encoder.
    /// </summary>
    public enum GifEncoderType
    {
        Legacy,
        ScreenToGif,
        PaintNet,
        FFmpeg,
        Gifski
    }

    /// <summary>
    /// Type of the apng encoder.
    /// </summary>
    public enum ApngEncoderType
    {
        ScreenToGif,
        FFmpeg,
    }

    /// <summary>
    /// Type of color quantization methods of the gif encoder.
    /// </summary>
    public enum ColorQuantizationType
    {
        Ordered,
        NeuQuant,
        Octree,
        Grayscale,
    }

    /// <summary>
    /// Type of the video encoder.
    /// </summary>
    public enum VideoEncoderType
    {
        AviStandalone,
        Ffmpg,
    }

    /// <summary>
    /// The type of directory, used to decide the icon of the folder inside the SelectFolderDialog.
    /// </summary>
    public enum DirectoryType
    {
        ThisComputer,
        Drive,
        Folder,
        File,

        Desktop,
        Documents,
        Images,
        Music,
        Videos,
        Downloads
    }

    /// <summary>
    /// The type of path.
    /// </summary>
    public enum PathType
    {
        VirtualFolder,
        Folder,
        File
    }

    /// <summary>
    /// The type of the output.
    /// </summary>
    public enum OutputType
    {
        Video,
        Gif,
        Apng,
    }

    /// <summary>
    /// Specifies the placement of the adorner in related to the adorned control.
    /// </summary>
    public enum AdornerPlacement
    {
        Inside,
        Outside
    }

    /// <summary>
    /// Specifies the type of copy operation.
    /// </summary>
    public enum CopyType
    {
        File,
        FolderPath,
        FilePath,
        Link
    }
    
    /// <summary>
    /// Specifies the status of the image card control.
    /// </summary>
    public enum ExtrasStatus
    {
        NotAvailable,
        Available,
        Processing,
        Ready,
        Error
    }

    /// <summary>
    /// Event flags for mouse-related events.
    /// </summary>
    public enum MouseEventType
    {
        MouseMove,
        IconRightMouseDown,
        IconLeftMouseDown,
        IconRightMouseUp,
        IconLeftMouseUp,
        IconMiddleMouseDown,
        IconMiddleMouseUp,
        IconDoubleClick
    }


    /// <summary>
    /// Dialog Icons.
    /// </summary>
    public enum Icons
    {
        /// <summary>
        /// Information. Blue.
        /// </summary>
        Info,

        /// <summary>
        /// Warning, yellow.
        /// </summary>
        Warning,

        /// <summary>
        /// Error, red.
        /// </summary>
        Error,

        /// <summary>
        /// Success, green.
        /// </summary>
        Success,

        /// <summary>
        /// A question mark, blue.
        /// </summary>
        Question,
    }

    public enum StatusType : int
    {
        None = 0,
        Info,
        Update,
        Warning,
        Error
    }

    /// <summary>
    /// The types of source of project creation.
    /// </summary>
    public enum ProjectByType
    {
        Unknown = 0,
        ScreenRecorder = 1,
        Editor = 4,
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenToGif.Controls;
using ScreenToGif.ImageUtil;
using ScreenToGif.ImageUtil.Gif.Decoder;
using ScreenToGif.Model;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Encoder = ScreenToGif.Windows.Other.Encoder;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListViewItem = System.Windows.Controls.ListViewItem;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Size = System.Windows.Size;
using System.Text.RegularExpressions;

namespace ScreenToGif.Windows
{
    public partial class Editor : Window, INotification
    {
        #region Properties

        public static readonly DependencyProperty FilledListProperty = DependencyProperty.Register("FilledList", typeof(bool), typeof(Editor), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty NotPreviewingProperty = DependencyProperty.Register("NotPreviewing", typeof(bool), typeof(Editor), new FrameworkPropertyMetadata(true));
        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register("IsLoading", typeof(bool), typeof(Editor), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty TotalDurationProperty = DependencyProperty.Register("TotalDuration", typeof(TimeSpan), typeof(Editor));
        public static readonly DependencyProperty FrameSizeProperty = DependencyProperty.Register("FrameSize", typeof(System.Windows.Size), typeof(Editor));
        public static readonly DependencyProperty FrameScaleProperty = DependencyProperty.Register("FrameScale", typeof(int), typeof(Editor));
        public static readonly DependencyProperty AverageDelayProperty = DependencyProperty.Register("AverageDelay", typeof(double), typeof(Editor));
        public static readonly DependencyProperty FrameDpiProperty = DependencyProperty.Register("FrameDpi", typeof(double), typeof(Editor));
        public static readonly DependencyProperty IsCancelableProperty = DependencyProperty.Register("IsCancelable", typeof(bool), typeof(Editor), new FrameworkPropertyMetadata(false));

        /// <summary>
        /// True if there is a value inside the list of frames.
        /// </summary>
        public bool FilledList
        {
            get => (bool)GetValue(FilledListProperty);
            set => SetValue(FilledListProperty, value);
        }

        /// <summary>
        /// True if not in preview mode.
        /// </summary>
        public bool NotPreviewing
        {
            get => (bool)GetValue(NotPreviewingProperty);
            set => SetValue(NotPreviewingProperty, value);
        }

        /// <summary>
        /// True if loading frames.
        /// </summary>
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        /// <summary>
        /// The total duration of the animation. Used by the statistics tab.
        /// </summary>
        private TimeSpan TotalDuration
        {
            get => (TimeSpan)GetValue(TotalDurationProperty);
            set => SetValue(TotalDurationProperty, value);
        }

        /// <summary>
        /// The size of the frames. Used by the statistics tab.
        /// </summary>
        private Size FrameSize
        {
            get => (System.Windows.Size)GetValue(FrameSizeProperty);
            set => SetValue(FrameSizeProperty, value);
        }

        /// <summary>
        /// The scale of the frames in %. Used by the statistics tab.
        /// </summary>
        private int FrameScale
        {
            get => (int)GetValue(FrameScaleProperty);
            set => SetValue(FrameScaleProperty, value);
        }

        /// <summary>
        /// The average delay of the animation. Used by the statistics tab.
        /// </summary>
        private double AverageDelay
        {
            get => (double)GetValue(AverageDelayProperty);
            set => SetValue(AverageDelayProperty, value);
        }

        /// <summary>
        /// The DPI of the frames. Used by the statistics tab.
        /// </summary>
        private double FrameDpi
        {
            get => (double)GetValue(FrameDpiProperty);
            set => SetValue(FrameDpiProperty, value);
        }

        /// <summary>
        /// True if the current recording being loaded can be cancelled.
        /// </summary>
        public bool IsCancelable
        {
            get => (bool)GetValue(IsCancelableProperty);
            set => SetValue(IsCancelableProperty, value);
        }

        #endregion

        #region Variables

        /// <summary>
        /// The current project.
        /// </summary>
        public ProjectInfo Project { get; set; }

        /// <summary>
        /// The clipboard.
        /// </summary>
        public List<FrameInfo> ClipboardFrames { get; set; }

        /// <summary>
        /// Last selected frame index. Used to track users last selection and decide which frame to show.
        /// </summary>
        private int LastSelected { get; set; } = -1;

        /// <summary>
        /// True if the user was selecting frames using the FirstFrame/Previous/Next/LastFrame commands or the scroll wheel.
        /// </summary>
        private bool WasChangingSelection { get; set; }

        /// <summary>
        /// True if the user was previewing the recording.
        /// </summary>
        private bool WasPreviewing { get; set; }

        /// <summary>
        /// True if the PC is sleeping.
        /// </summary>
        private bool Slept { get; set; }

        private readonly System.Windows.Forms.Timer _timerPreview = new System.Windows.Forms.Timer();

        private Action<object, RoutedEventArgs> _applyAction = null;

        private bool _abortLoading;

        #endregion

        public Editor()
        {
            InitializeComponent();

            #region Adjust the position

            //Tries to adjust the position/size of the window, centers on screen otherwise.
            if (!UpdatePositioning())
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

            #endregion

            #region UWP restrictions

#if UWP

            CustomCommandsCheckBox.Visibility = Visibility.Collapsed;
            CustomCommandsTextBox.Visibility = Visibility.Collapsed;
            CustomCommandsApngCheckBox.Visibility = Visibility.Collapsed;
            CustomCommandsApngTextBox.Visibility = Visibility.Collapsed;
            CustomCommandsVideoCheckBox.Visibility = Visibility.Collapsed;
            CustomCommandsVideoTextBox.Visibility = Visibility.Collapsed;
            CustomCommandsPsdCheckBox.Visibility = Visibility.Collapsed;
            CustomCommandsPsdTextBox.Visibility = Visibility.Collapsed;

#endif

            #endregion
        }

        #region Main Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.PowerModeChanged += System_PowerModeChanged;
            SystemEvents.DisplaySettingsChanged += System_DisplaySettingsChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;

            ScrollSynchronizer.SetScrollGroup(ZoomBoxControl.GetScrollViewer(), "Canvas");
            ScrollSynchronizer.SetScrollGroup(MainScrollViewer, "Canvas");

            #region Temporary folder

            //If never configurated.
            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                UserSettings.All.TemporaryFolder = Path.GetTempPath();

            #endregion

            #region Load

            if (Project != null)
            {
                ShowProgress(FindResource("Editor.Preparing").ToString(), Project.Frames.Count, true);

                Cursor = Cursors.AppStarting;
                IsLoading = true;
                IsCancelable = true;

                ActionStack.Project = Project;

                _loadFramesDel = Load;
                _loadFramesDel.BeginInvoke(LoadCallback, null);
                return;
            }

            #endregion

            #region Open With...

            if (Argument.FileNames.Any())
            {
                #region Validation

                var extensionList = Argument.FileNames.Select(Path.GetExtension).ToList();

                var media = new[] { "jpg", "jpeg", "gif", "bmp", "png", "avi", "mp4", "wmv" };

                var projectCount = extensionList.Count(x => !string.IsNullOrEmpty(x) && (x.Equals("stg") || x.Equals("zip")));
                var mediaCount = extensionList.Count(x => !string.IsNullOrEmpty(x) && media.Contains(x));

                //TODO: Later I need to implement another validation for multiple video files.

                if (projectCount != 0 && mediaCount != 0)
                {
                    Dispatcher.Invoke(() => StatusList.Warning(FindResource("Editor.InvalidLoadingFiles").ToString()));
                    return;
                }

                if (projectCount > 0)
                {
                    Dispatcher.Invoke(() => StatusList.Warning(FindResource("Editor.InvalidLoadingProjects").ToString()));
                    return;
                }

                #endregion

                //_importFramesDel = ImportFrom;
                //_importFramesDel.BeginInvoke(Argument.FileNames, ImportFromCallback, null);
                return;
            }

            #endregion

            WelcomeTextBlock.Text = StringResource(Humanizer.WelcomeInfo());
            SymbolTextBlock.Text = Humanizer.Welcome();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (UserSettings.All.EditorExtendChrome)
                Glass.ExtendGlassFrame(this, new Thickness(0, 126, 0, 0));
            else
                Glass.RetractGlassFrame(this);

            //RibbonTabControl.UpdateVisual();

            //Returns the preview if was playing before the deactivation of the window.
            if (WasPreviewing)
            {
                WasPreviewing = false;
                PlayPause();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            //RibbonTabControl.UpdateVisual(false);

            //Pauses the recording preview.
            if (_timerPreview.Enabled)
            {
                WasPreviewing = true;
                Pause();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.SystemKey == Key.LeftAlt)
                e.Handled = true;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //TODO: What if there's any processing happening? I need to try to stop.

            Pause();

            if (Project != null && Project.Any)
            {
                Project.Persist();

                if (UserSettings.All.NotifyWhileClosingEditor && !Dialog.Ask(LocalizationHelper.Get("Editor.Exiting.Title"), LocalizationHelper.Get("Editor.Exiting.Instruction"),
                        LocalizationHelper.Get(UserSettings.All.AutomaticCleanUp ? "Editor.Exiting.Message2" : "Editor.Exiting.Message")))
                {
                    e.Cancel = true;
                    return;
                }

                Project.Clear();

                //Remove the ActionStack.
                ActionStack.Clear();
            }

            //Manually get the position/size of the window, so it's possible opening multiple instances.
            UserSettings.All.EditorTop = Top;
            UserSettings.All.EditorLeft = Left;
            UserSettings.All.EditorWidth = Width;
            UserSettings.All.EditorHeight = Height;
            UserSettings.All.EditorWindowState = WindowState;
            UserSettings.Save();

            Encoder.TryClose();

            SystemEvents.PowerModeChanged -= System_PowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= System_DisplaySettingsChanged;
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        }


        private void ZoomBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Alt)
            {
                #region Translate the Element (Scroll)

                if (sender.GetType() == typeof(ScrollViewer))
                {
                    switch (Keyboard.Modifiers)
                    {
                        case ModifierKeys.Alt:

                            var verDelta = e.Delta > 0 ? -10.5 : 10.5;
                            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + verDelta);

                            break;
                        case ModifierKeys.Shift:

                            var horDelta = e.Delta > 0 ? -10.5 : 10.5;
                            MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset + horDelta);

                            break;
                    }

                    return;
                }

                #endregion

                e.Handled = false;
                return;
            }

            WasChangingSelection = true;

            if (e.Delta > 0)
            {
                if (FrameListView.SelectedIndex == -1 || FrameListView.SelectedIndex == FrameListView.Items.Count - 1)
                {
                    FrameListView.SelectedIndex = 0;
                    return;
                }

                //Show next frame.
                FrameListView.SelectedIndex++;
            }
            else
            {
                if (FrameListView.SelectedIndex == -1 || FrameListView.SelectedIndex == 0)
                {
                    FrameListView.SelectedIndex = FrameListView.Items.Count - 1;
                    return;
                }

                //Show previous frame.
                FrameListView.SelectedIndex--;
            }
        }


        private void System_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                Slept = true;
                Pause();
                GC.Collect();
                return;
            }

            Slept = false;
        }

        private void System_DisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdatePositioning(false);
        }

        private void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Slept)
                return;

            //If the window color changes, update the tabs style.
           // if (e.PropertyName == "WindowGlassColor")
            //    RibbonTabControl.UpdateVisual(IsActive);
        }

        #endregion

        #region Frame Selection

        private void FrameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            #region If nothing selected

            if (FrameListView.SelectedIndex == -1)
            {
                ZoomBoxControl.ImageSource = null;
                return;
            }

            #endregion

            if (LastSelected == -1 || _timerPreview.Enabled || WasChangingSelection 
                || LastSelected >= FrameListView.Items.Count || (e.AddedItems.Count > 0 && e.RemovedItems.Count > 0))
                LastSelected = FrameListView.SelectedIndex;

            FrameListBoxItem current;

            if (_timerPreview.Enabled || WasChangingSelection)
            {
                current = FrameListView.Items[FrameListView.SelectedIndex] as FrameListBoxItem;
            }
            else
            {
                //TODO: Test with other key shortcuts, because Ctrl + Z/Y was breaking this code.
                var focused = Keyboard.FocusedElement as FrameListBoxItem;

                //current = FrameListView.Items.GetItemAt(LastSelected) as FrameListBoxItem;
                if (focused != null && focused.IsVisible && (Keyboard.IsKeyDown(Key.LeftShift) 
                    || Keyboard.IsKeyDown(Key.RightShift) || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                    current = focused;
                else
                    current = FrameListView.Items.OfType<FrameListBoxItem>().FirstOrDefault(x => x.IsFocused || x.IsSelected);
            }

            //If there's no focused item.
            if (current == null)
            {
                if (FrameListView.Items.Count - 1 > LastSelected)
                    FrameListView.SelectedIndex = LastSelected;
                else
                    FrameListView.SelectedIndex = LastSelected = FrameListView.Items.Count - 1;

                if (FrameListView.SelectedIndex > -1)
                    current = FrameListView.Items[FrameListView.SelectedIndex] as FrameListBoxItem;
            }

            if (current != null)
            {
                if (!current.IsFocused && !_timerPreview.Enabled)// && !WasChangingSelection)
                    current.Focus();

                var currentIndex = FrameListView.Items.IndexOf(current);

                if (currentIndex > -1)
                {
                    ZoomBoxControl.ImageSource = Project.Frames[currentIndex].Path;
                    FrameListView.ScrollIntoView(current);
                }
            }

            WasChangingSelection = false;
        }

        private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameListBoxItem item)// && !WasChangingSelection)
            {
                LastSelected = item.FrameNumber;
                Keyboard.Focus(item);
            }
        }

        #endregion


        #region File Tab

        #region New/Open

        private void NewRecording_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsLoading && !e.Handled && Application.Current.Windows.OfType<Window>().All(a => !(a is RecorderWindow));
        }

        private void NewRecording_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            Pause();
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
            Encoder.Minimize();
            ClosePanel(removeEvent: true);

            if (UserSettings.All.NewRecorder)
            {
                var recorder = new RecorderNew();
                recorder.ShowDialog();

                if (recorder.Project?.Any == true)
                {
                    LoadProject(recorder.Project);
                    ShowHint("Hint.NewRecording");
                }
            }
            else
            {
                var recorder = new Recorder();
                recorder.ShowDialog();

                if (recorder.Project?.Any == true)
                {
                    LoadProject(recorder.Project);
                    ShowHint("Hint.NewRecording");
                }
            }

            Encoder.Restore();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
        }

        #endregion


        #region File

        private void File_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Project != null && Project.Any && !IsLoading && !e.Handled;
        }

        private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            // TODO: save
        }

        

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusList.Remove(StatusType.Warning);

            /*
             try
            {
                var output = GetOutputFolder();
                var name = GetOutputFilename();
                var extension = GetOutputExtension();

                var pickLocation = GetPickLocation();
                var overwrite = GetOverwriteOnSave();
                var projectToo = GetSaveAsProjectToo();
                var copyType = GetCopyType();
                var executeCommands = GetExecuteCustomCommands();
                var commands = GetCustomCommands();

                //put datetime into filename which is saved between two questions marks
                GetOutputFilenameNoRegExp(ref name);

                #region Common validations

                if (!pickLocation)
                {
                    StatusList.Warning(StringResource("S.SaveAs.Warning.Type"));
                    return;
                }

                if (UserSettings.All.SaveType == Export.Video)
                {
                    if (UserSettings.All.VideoEncoder == VideoEncoderType.Ffmpg)
                    {
                        if (!Util.Other.IsFfmpegPresent())
                        {
                            StatusList.Warning(StringResource("Editor.Warning.Ffmpeg"), null, () => App.MainViewModel.OpenOptions.Execute(7));
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(UserSettings.All.FfmpegLocation) && UserSettings.All.FfmpegLocation.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                        {
                            StatusList.Warning(StringResource("Extras.FfmpegLocation.Invalid"));
                            return;
                        }
                    }
                    else
                        UserSettings.All.LatestVideoExtension = ".avi";

                    if (!new[] { ".avi", ".mp4", ".wmv", ".webm" }.Contains(UserSettings.All.LatestVideoExtension))
                        UserSettings.All.LatestVideoExtension = (string)FileTypeVideoComboBox.SelectedItem;

                    extension = GetOutputExtension();
                }
                else if (UserSettings.All.SaveType == Export.Gif)
                {
                    if (UserSettings.All.GifEncoder == GifEncoderType.FFmpeg)
                    {
                        if (!Util.Other.IsFfmpegPresent())
                        {
                            StatusList.Warning(StringResource("Editor.Warning.Ffmpeg"), null, () => App.MainViewModel.OpenOptions.Execute(7));
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(UserSettings.All.FfmpegLocation) && UserSettings.All.FfmpegLocation.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                        {
                            StatusList.Warning(StringResource("Extras.FfmpegLocation.Invalid"));
                            return;
                        }
                    }
                    else if (UserSettings.All.GifEncoder == GifEncoderType.Gifski)
                    {
                        if (!Util.Other.IsGifskiPresent())
                        {
                            StatusList.Warning(StringResource("Editor.Warning.Gifski"), null, () => App.MainViewModel.OpenOptions.Execute(7));
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(UserSettings.All.GifskiLocation) && UserSettings.All.GifskiLocation.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                        {
                            StatusList.Warning(StringResource("Extras.GifskiLocation.Invalid"));
                            return;
                        }
                    }
                }
                else if (UserSettings.All.SaveType == Export.Apng)
                {
                    if (UserSettings.All.ApngEncoder == ApngEncoderType.FFmpeg)
                    {
                        if (!Util.Other.IsFfmpegPresent())
                        {
                            StatusList.Warning(StringResource("Editor.Warning.Ffmpeg"), null, () => App.MainViewModel.OpenOptions.Execute(7));
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(UserSettings.All.FfmpegLocation) && UserSettings.All.FfmpegLocation.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                        {
                            StatusList.Warning(StringResource("Extras.FfmpegLocation.Invalid"));
                            return;
                        }
                    }

                    extension = GetOutputExtension();
                }

                if (pickLocation)
                {
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Folder"));
                        return;
                    }

                    if (output.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Folder.Invalid"));
                        return;
                    }

                    if (!Directory.Exists(output))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Folder.NotExists"));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Filename"));
                        return;
                    }

                    if (name.ToCharArray().Any(x => Path.GetInvalidFileNameChars().Contains(x)))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Filename.Invalid"));
                        return;
                    }

                    if (!overwrite && File.Exists(Path.Combine(output, name + GetOutputExtension())))
                    {
                        FileExistsGrid.Visibility = Visibility.Visible;
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Overwrite"));
                        return;
                    }

                    if (projectToo)
                    {
                        if (!overwrite && File.Exists(Path.Combine(output, name + (UserSettings.All.LatestProjectExtension ?? ".stg"))))
                        {
                            StatusList.Warning(StringResource("S.SaveAs.Warning.Overwrite"));
                            return;
                        }
                    }
                }

                //When only copying to the clipboard or uploading.
                if (!pickLocation)
                {
                    //If somehow this happens, try again.
                    if (File.Exists(Path.Combine(output, name)))
                        name = GetOutputFilename();
                }

                if (executeCommands)
                {
                    if (string.IsNullOrWhiteSpace(commands))
                    {
                        StatusList.Warning(StringResource("S.SaveAs.Warning.Commands.Empty"));
                        return;
                    }
                }

                #endregion

                var filename = Path.Combine(output, name + extension);
                var param = new Parameters
                {
                    Type = UserSettings.All.SaveType,
                    Filename = filename,
                    CopyType = copyType,
                    ExecuteCommands = executeCommands,
                    PostCommands = commands
                };

                switch (UserSettings.All.SaveType)
                {
                    case Export.Gif:
                        param.EncoderType = UserSettings.All.GifEncoder;
                        param.DetectUnchangedPixels = UserSettings.All.DetectUnchanged;
                        param.DummyColor = UserSettings.All.DetectUnchanged && UserSettings.All.PaintTransparent ? UserSettings.All.ChromaKey : new Color?();
                        param.Quality = UserSettings.All.Quality;
                        param.UseGlobalColorTable = false;
                        param.MaximumNumberColors = UserSettings.All.MaximumColors;
                        param.RepeatCount = UserSettings.All.Looped ? (UserSettings.All.RepeatForever ? 0 : UserSettings.All.RepeatCount) : -1;
                        param.Command = "-vsync 2 -safe 0 -f concat -i \"{0}\" {1} -y \"{2}\"";
                        param.ExtraParameters = UserSettings.All.ExtraParametersGif;
                        break;
                    case Export.Apng:
                        param.ApngEncoder = UserSettings.All.ApngEncoder;
                        param.DetectUnchangedPixels = UserSettings.All.DetectUnchangedApng;
                        param.DummyColor = UserSettings.All.DetectUnchangedApng && UserSettings.All.PaintTransparentApng ? Colors.Transparent : new Color?();
                        param.RepeatCount = UserSettings.All.LoopedApng ? (UserSettings.All.RepeatForeverApng ? 0 : UserSettings.All.RepeatCountApng) : -1;
                        param.Command = "-vsync 2 -safe 0 -f concat -i \"{0}\" {1} -plays {2} -f apng -y \"{3}\"";
                        param.ExtraParameters = UserSettings.All.ExtraParametersApngFFmpeg;
                        break;
                    case Export.Video:
                        var size = Project.Frames[0].Path.SizeOf();

                        param.VideoEncoder = FfmpegEncoderRadioButton.IsChecked == true ? VideoEncoderType.Ffmpg : VideoEncoderType.AviStandalone;
                        param.VideoQuality = (uint)AviQualitySlider.Value;
                        param.FlipVideo = UserSettings.All.FlipVideo;
                        param.Command = "-vsync 2 -safe 0 -f concat -i \"{0}\" {1} -y \"{2}\"";
                        param.Height = size.Height.DivisibleByTwo();
                        param.Width = size.Width.DivisibleByTwo();
                        param.ExtraParameters = UserSettings.All.ExtraParameters;
                        param.Framerate = UserSettings.All.OutputFramerate;
                        break;
                    case Export.Images:

                        if (!UserSettings.All.ZipImages)
                        {
                            //TODO: Check the verification for existing files. For the 4 types of files.
                            if (FrameListView.SelectedItems.Count > 1 && !Dialog.Ask(LocalizationHelper.Get("S.SaveAs.Frames.Confirmation.Title"),
                                    LocalizationHelper.Get("S.SaveAs.Frames.Confirmation.Instruction"), LocalizationHelper.GetWithFormat("S.SaveAs.Frames.Confirmation.Message", FrameListView.SelectedItems.Count)))
                            {
                                StatusList.Warning(StringResource("S.SaveAs.Warning.Canceled"));
                                return;
                            }

                            foreach (var index in SelectedFramesIndex())
                            {
                                //Validation.
                                if (File.Exists(Path.Combine(UserSettings.All.LatestImageOutputFolder, UserSettings.All.LatestImageFilename + " " + index + ".png")))
                                {
                                    FileExistsGrid.Visibility = Visibility.Visible;
                                    StatusList.Warning(StringResource("S.SaveAs.Warning.Overwrite") + " - " + UserSettings.All.LatestImageFilename + " " + index + ".png");
                                    return;
                                }
                            }

                            foreach (var index in SelectedFramesIndex())
                            {
                                var fileName = Path.Combine(UserSettings.All.LatestImageOutputFolder, UserSettings.All.LatestImageFilename + " " + index + ".png");

                                File.Copy(FrameListView.Items.OfType<FrameListBoxItem>().ToList()[index].Image, fileName);
                            }
                        }
                        else
                        {
                            var fileName = Path.Combine(UserSettings.All.LatestImageOutputFolder, UserSettings.All.LatestImageFilename + ".zip");

                            //Check if file exists.
                            if (!UserSettings.All.OverwriteOnSave)
                            {
                                if (File.Exists(fileName))
                                {
                                    FileExistsGrid.Visibility = Visibility.Visible;
                                    StatusList.Warning(StringResource("S.SaveAs.Warning.Overwrite"));
                                    return;
                                }
                            }

                            if (File.Exists(fileName))
                                File.Delete(fileName);

                            var exportDirectory = Path.Combine(Path.GetDirectoryName(Project.Frames.First().Path), "Export");

                            if (Directory.Exists(exportDirectory))
                                Directory.Delete(exportDirectory, true);

                            var dir = Directory.CreateDirectory(exportDirectory);

                            foreach (var frame in FrameListView.SelectedItems.OfType<FrameListBoxItem>())
                                File.Copy(frame.Image, Path.Combine(dir.FullName, Path.GetFileName(frame.Image)), true);

                            ZipFile.CreateFromDirectory(dir.FullName, fileName);

                            Directory.Delete(dir.FullName, true);
                        }
                        break;
                    case Export.Project:
                        _saveProjectDel = SaveProjectAsync;
                        _saveProjectDel.BeginInvoke(filename, SaveProjectCallback, null);
                        break;
                    case Export.Photoshop:
                        var size2 = Project.Frames[0].Path.SizeOf();

                        param.Height = size2.Height;
                        param.Width = size2.Width;
                        param.Compress = UserSettings.All.CompressImage;
                        param.SaveTimeline = UserSettings.All.SaveTimeline;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                //Save, using the encoder.
                if (UserSettings.All.SaveType != Export.Images && UserSettings.All.SaveType != Export.Project)
                {
                    _saveDel = SaveAsync;
                    _saveDel.BeginInvoke(Project.Frames, param, this.Scale(), projectToo, UserSettings.All.SaveType == Export.Gif && UserSettings.All.GifEncoder == GifEncoderType.Gifski, SaveCallback, null);
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Save As");

                ErrorDialog.Ok("ScreenToGif", "Error while trying to save", ex.Message, ex);
                ClosePanel();
            }

            ClosePanel();
            */
        }



        private void DiscardProject_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Discard(UserSettings.All.NotifyProjectDiscard);
        }

        #endregion

        #endregion

        #region Home Tab

        #region Action Stack

        private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActionStack.CanUndo() && !IsLoading && !e.Handled;
        }

        private void Reset_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActionStack.CanReset() && !IsLoading && !e.Handled;
        }

        private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActionStack.CanRedo() && !IsLoading && !e.Handled;
        }

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ClosePanel();

            Project.Frames = ActionStack.Undo(Project.Frames.CopyList());
            LoadProject(Project, false, false);

            ShowHint("Hint.Undo");
        }

        private void Reset_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ClosePanel();

            Project.Frames = ActionStack.Reset(Project.Frames.CopyList());
            LoadProject(Project, false, false);

            ShowHint("Hint.Reset");
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ClosePanel();

            Project.Frames = ActionStack.Redo(Project.Frames.CopyList());
            LoadProject(Project, false, false);

            ShowHint("Hint.Redo");
        }

        #endregion

        #region Zoom

        private void Zoom_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Project != null && Project.Any && !IsLoading && !OverlayGrid.IsVisible && FrameListView.SelectedIndex != -1;
        }

        private void Zoom100_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ZoomBoxControl.Zoom = 1.0;

            ShowHint("Hint.Zoom", false, 100);
        }

        private void SizeToContent_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            ZoomBoxControl.UpdateLayout();

            var size = ZoomBoxControl.GetElementSize(true);

            //Tried to get the size again.
            if (size.Width < 2)
                size = Project.Frames[0].Path.NonScaledSize();

            //If failed again, abort.
            if (size.Width < 2)
                return;

            var scale = this.Scale();

            var borderHeight = ActualHeight - MainGrid.ActualHeight;
            var borderWidth = ActualWidth - MainGrid.ActualWidth;

            //Bug: I need to take into consideration that the RibbonTabControl.ActualHeight can change, because the tab headers can occupy 2 rows.
            var width = (size.Width * ZoomBoxControl.Zoom / ZoomBoxControl.ScaleDiff + 60) + borderWidth;
            var height = (size.Height * ZoomBoxControl.Zoom / ZoomBoxControl.ScaleDiff + (Menu.ActualHeight + FrameListView.ActualHeight + LowerGrid.ActualHeight)) + borderHeight;

            //If image is too small, size to the minimum size.
            if (width < 770)
                width = 770;

            if (height < 575)
                height = 575;

            var screen = Monitor.AllMonitorsScaled(scale).FirstOrDefault(x => x.Bounds.Contains(new System.Windows.Point(Left, Top))) ??
                         Monitor.AllMonitorsScaled(scale).FirstOrDefault(x => x.IsPrimary);

            if (screen != null)
            {
                //If the resulting size is too big, fit the window on the available working area.
                if (screen.WorkingArea.Width < width)
                    width = screen.WorkingArea.Width;

                if (screen.WorkingArea.Height < height)
                    height = screen.WorkingArea.Height;

                //If the window overflows, put back in place.
                if (Left + width > screen.WorkingArea.Right)
                    Left = screen.WorkingArea.Right - width;

                if (Top + height > screen.WorkingArea.Bottom)
                    Top = screen.WorkingArea.Bottom - height;
            }

            Width = width;
            Height = height;
        }

        private void FitImage_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //Reset the zoom, to get the actual image size.
            ZoomBoxControl.Zoom = 1;
            ZoomBoxControl.UpdateLayout();

            var size = ZoomBoxControl.GetElementSize();

            if (size.Width < 2)
                size = Project.Frames[0].Path.ScaledSize();

            #region Calculate the Zoom

            var zoomHeight = 1D;
            var zoomWidth = 1D;

            if (size.Width > ZoomBoxControl.ActualWidth)
                zoomWidth = ZoomBoxControl.ActualWidth / size.Width;// * this.Scale();

            if (size.Height > ZoomBoxControl.ActualHeight)
                zoomHeight = ZoomBoxControl.ActualHeight / size.Height;// * this.Scale();

            #endregion

            #region Apply the zoom

            if (zoomHeight > 0 && zoomHeight < zoomWidth)
                ZoomBoxControl.Zoom = zoomHeight;
            else if (zoomWidth > 0 && zoomWidth < zoomHeight)
                ZoomBoxControl.Zoom = zoomWidth;
            else
                ZoomBoxControl.Zoom = 1;

            #endregion

            ShowHint("Hint.Zoom", false, Convert.ToInt32(ZoomBoxControl.Zoom * 100));

            GC.Collect(1);
        }

        #endregion

        #region Select

        private void Selection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsLoading && FrameListView != null && FrameListView.HasItems;
        }

        private void SelectAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            FrameListView.SelectAll();

            ShowHint("Hint.SelectAll");
        }

        private void GoTo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            var go = new GoTo(Project.Frames.Count - 1) { Owner = this };
            var result = go.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            var item = FrameListView.Items[go.Selected] as FrameListBoxItem;

            if (item == null)
                return;

            Keyboard.Focus(item);

            FrameListView.ScrollIntoView(item);
            FrameListView.SelectedIndex = go.Selected;

            ShowHint("Hint.SelectSingle", false, go.Selected);
        }

        private void InverseSelection_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            foreach (ListViewItem item in FrameListView.Items)
            {
                item.IsSelected = !item.IsSelected;
            }

            ShowHint("Hint.SelectInverse");
        }

        private void DeselectAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ClosePanel();

            FrameListView.SelectedIndex = -1;

            ShowHint("Hint.Deselect");
        }

        #endregion

        #endregion

        #region Playback Tab

        private void Playback_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Project != null && Project.Frames.Count > 1 && !IsLoading && _applyAction == null;
        }

        private void FirstFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            WasChangingSelection = true;
            FrameListView.SelectedIndex = 0;
        }

        private void PreviousFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            WasChangingSelection = true;

            if (FrameListView.SelectedIndex == -1 || FrameListView.SelectedIndex == 0)
            {
                FrameListView.SelectedIndex = FrameListView.Items.Count - 1;
                return;
            }

            //Show previous frame.
            FrameListView.SelectedIndex--;
        }

        private void Play_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlayPause();
        }

        private void NextFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            WasChangingSelection = true;

            if (FrameListView.SelectedIndex == -1 || FrameListView.SelectedIndex == FrameListView.Items.Count - 1)
            {
                FrameListView.SelectedIndex = 0;
                return;
            }

            //Show next frame.
            FrameListView.SelectedIndex++;
        }

        private void LastFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            WasChangingSelection = true;
            FrameListView.SelectedIndex = FrameListView.Items.Count - 1;
        }

        #endregion

        #region Edit Tab

        #region Frames

        private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = FrameListView != null && FrameListView.SelectedItem != null && !IsLoading;
        }

        private void DeletePrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = FrameListView != null && FrameListView.SelectedItem != null && !IsLoading &&
                FrameListView.SelectedIndex > 0;
        }

        private void DeleteNext_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = FrameListView != null && FrameListView.SelectedItem != null && !IsLoading &&
                FrameListView.SelectedIndex < FrameListView.Items.Count - 1;
        }

        
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            try
            {
                #region Validation

                if (Project.Frames.Count == FrameListView.SelectedItems.Count)
                {
                    //If the user wants to delete all frames, discard the project.
                    if (!UserSettings.All.NotifyProjectDiscard ||
                        Dialog.Ask(this.TextResource("Editor.DeleteAll.Title"), this.TextResource("Editor.DeleteAll.Instruction"), this.TextResource("Editor.DeleteAll.Message"), false))
                        Discard(false);

                    return;
                }

                if (UserSettings.All.NotifyFrameDeletion)
                {
                    if (!Dialog.Ask(this.TextResource("Editor.DeleteFrames.Title"), this.TextResource("Editor.DeleteFrames.Instruction"),
                        string.Format(this.TextResource("Editor.DeleteFrames.Message"), FrameListView.SelectedItems.Count)))
                        return;
                }

                #endregion

                var selected = FrameListView.SelectedItems.OfType<FrameListBoxItem>().ToList();
                var selectedOrdered = selected.OrderByDescending(x => x.FrameNumber).ToList();
                var list = selectedOrdered.Select(item => Project.Frames[item.FrameNumber]).ToList();

                ActionStack.SaveState(ActionStack.EditAction.Remove, Project.Frames, SelectedFramesIndex());

                FrameListView.SelectedItem = null;

                list.ForEach(x => File.Delete(x.Path));
                selectedOrdered.ForEach(x => Project.Frames.RemoveAt(x.FrameNumber));
                selectedOrdered.ForEach(x => FrameListView.Items.Remove(x));

                AdjustFrameNumbers(selectedOrdered.Last().FrameNumber);

                SelectNear(selectedOrdered.Last().FrameNumber);

                Project.Persist();
                UpdateStatistics();
                ShowHint("Hint.DeleteFrames", false, selected.Count);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error While Trying to Delete Frames");

                ErrorDialog.Ok(FindResource("Editor.Title") as string, "Error while trying to delete frames", ex.Message, ex);
            }
        }

        private void DeletePrevious_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            if (UserSettings.All.NotifyFrameDeletion)
            {
                if (!Dialog.Ask(this.TextResource("Editor.DeleteFrames.Title"), this.TextResource("Editor.DeleteFrames.Instruction"),
                    string.Format(this.TextResource("Editor.DeleteFrames.Message"), FrameListView.SelectedIndex)))
                    return;
            }

            ActionStack.SaveState(ActionStack.EditAction.Remove, Project.Frames, Util.Other.CreateIndexList(0, FrameListView.SelectedIndex - 1));

            var count = FrameListView.SelectedIndex;

            for (var index = FrameListView.SelectedIndex - 1; index >= 0; index--)
                DeleteFrame(index);

            AdjustFrameNumbers(0);
            SelectNear(0);

            Project.Persist();
            UpdateStatistics();
            ShowHint("Hint.DeleteFrames", false, count);
        }

        private void DeleteNext_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();

            if (UserSettings.All.NotifyFrameDeletion)
            {
                if (!Dialog.Ask(this.TextResource("Editor.DeleteFrames.Title"), this.TextResource("Editor.DeleteFrames.Instruction"),
                    string.Format(this.TextResource("Editor.DeleteFrames.Message"), FrameListView.Items.Count - FrameListView.SelectedIndex - 1)))
                    return;
            }

            var countList = FrameListView.Items.Count - 1; //So we have a fixed value.

            ActionStack.SaveState(ActionStack.EditAction.Remove, Project.Frames, Util.Other.CreateIndexList2(FrameListView.SelectedIndex + 1, FrameListView.Items.Count - FrameListView.SelectedIndex - 1));

            var count = FrameListView.Items.Count - FrameListView.SelectedIndex - 1;

            for (var i = countList; i > FrameListView.SelectedIndex; i--) //From the end to the middle.
            {
                DeleteFrame(i);
            }

            SelectNear(FrameListView.Items.Count - 1);

            Project.Persist();
            UpdateStatistics();
            ShowHint("Hint.DeleteFrames", false, count);
        }



        private void Reduce_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = FrameListView != null && !IsLoading && FrameListView.Items.Count > 5;
        }

        private void Reduce_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ShowPanel(PanelType.ReduceFrames, StringResource("Editor.Edit.Frames.Reduce"), "Vector.RemoveImage", ApplyReduceFrameCountButton_Click);
        }

        private void ApplyReduceFrameCountButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.AppStarting;

            _reduceFrameDel = ReduceFrameCount;
            _reduceFrameDel.BeginInvoke(ReduceFactorIntegerUpDown.Value, ReduceCountIntegerUpDown.Value, ReduceFrameCountCallback, null);

            ClosePanel();
        }

        #endregion

        #region Delay (Duration)

        private void OverrideDelay_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ShowPanel(PanelType.OverrideDelay, StringResource("Editor.Edit.Delay.Override"), "Vector.OverrideDelay", ApplyOverrideDelayButton_Click);
        }

        private void ApplyOverrideDelayButton_Click(object sender, RoutedEventArgs e)
        {
            ActionStack.SaveState(ActionStack.EditAction.Properties, Project.Frames, SelectedFramesIndex());

            Cursor = Cursors.AppStarting;

            _delayFramesDel = Delay;
            _delayFramesDel.BeginInvoke(DelayChangeType.Override, NewDelayIntegerUpDown.Value, DelayCallback, null);

            ClosePanel();
        }


        private void IncreaseDecreaseDelay_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ShowPanel(PanelType.IncreaseDecreaseDelay, StringResource("Editor.Edit.Delay.IncreaseDecrease"), "Vector.IncreaseDecreaseDelay", ApplyIncreaseDecreaseDelayButtonClick);
        }

        private void ApplyIncreaseDecreaseDelayButtonClick(object sender, RoutedEventArgs e)
        {
            if (IncreaseDecreaseDelayIntegerUpDown.Value == 0)
            {
                ClosePanel();
                return;
            }

            ActionStack.SaveState(ActionStack.EditAction.Properties, Project.Frames, SelectedFramesIndex());

            Cursor = Cursors.AppStarting;

            _delayFramesDel = Delay;
            _delayFramesDel.BeginInvoke(DelayChangeType.IncreaseDecrease, IncreaseDecreaseDelayIntegerUpDown.Value, DelayCallback, null);

            ClosePanel();
        }

        private void ScaleDelay_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pause();
            ShowPanel(PanelType.ScaleDelay, StringResource("Editor.Edit.Delay.Scale"), "Vector.ScaleDelay", ApplyScaleDelayButtonClick);
        }

        private void ApplyScaleDelayButtonClick(object sender, RoutedEventArgs e)
        {
            if (ScaleDelayIntegerUpDown.Value == 0 || ScaleDelayIntegerUpDown.Value == 100)
            {
                ClosePanel();
                return;
            }

            ActionStack.SaveState(ActionStack.EditAction.Properties, Project.Frames, SelectedFramesIndex());

            Cursor = Cursors.AppStarting;

            _delayFramesDel = Delay;
            _delayFramesDel.BeginInvoke(DelayChangeType.Scale, ScaleDelayIntegerUpDown.Value, DelayCallback, null);

            ClosePanel();
        }

      #endregion

      #endregion


        #region Other Events

  /*      #region Panel

        private void PanelAction_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            e.CanExecute = _applyAction != null || ClipboardGrid.IsVisible || LoadRecentGrid.IsVisible;// && ActionGrid.Width > 50 && ActionLowerGrid.IsVisible;
        }

        private void Ok_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ApplyButton.Focus();

            _applyAction?.Invoke(sender, e);

            //If the StatusBand started displaying the message, it means that the action failed.
            if (!StatusList.Children.OfType<StatusBand>().Any(a => a.Starting))
                _applyAction = null;
        }

        private void Cancel_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _applyAction = null;

            ClosePanel(true);
        }

        #endregion */

        #region Frame ListView

        private void ListFramesSelection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = FrameListView.SelectedIndex != -1 && !IsLoading;
        }

        private void OpenImage_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start(Project.Frames[FrameListView.SelectedIndex].Path);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Open Image");
                Dialog.Ok("Open Image", "Impossible to open image.", ex.Message);
            }
        }

        private void ExploreFolder_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{Project.Frames[FrameListView.SelectedIndex].Path}\"");
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Open Image Folder");
                Dialog.Ok("Open Image", "Impossible to open the image folder.", ex.Message);
            }
        }

        private void ExportImages_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ImagesRadioButton.IsChecked = true;

            SaveAs_Executed(sender, e);
        }

        private void FrameListView_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                PlayPause();

                //Avoids the selection of the frame by using the Space key.
                e.Handled = true;
            }

            if (e.Key == Key.PageDown)
            {
                NextFrame_Executed(sender, null);
                e.Handled = true;
            }

            if (e.Key == Key.PageUp)
            {
                PreviousFrame_Executed(sender, null);
                e.Handled = true;
            }
        }

        #endregion

        private void TimerPreview_Tick(object sender, EventArgs e)
        {
            _timerPreview.Tick -= TimerPreview_Tick;

            if (Project.Frames.Count - 1 == FrameListView.SelectedIndex)
                FrameListView.SelectedIndex = 0;
            else
                FrameListView.SelectedIndex++;

            if (Project.Frames[FrameListView.SelectedIndex].Delay == 0)
                Project.Frames[FrameListView.SelectedIndex].Delay = 10;

            //Sets the interval for this frame. If this frame has 500ms, the next frame will take 500ms to show.
            _timerPreview.Interval = Project.Frames[FrameListView.SelectedIndex].Delay;
            _timerPreview.Tick += TimerPreview_Tick;

            GC.Collect(2);
        }

        private void Control_DragEnter(object sender, DragEventArgs e)
        {
            Pause();

            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void Control_Drop(object sender, DragEventArgs e)
        {
            Pause();

            var fileNames = e.Data.GetData(DataFormats.FileDrop) as string[];

            if (fileNames == null) return;
            if (fileNames.Length == 0) return;

            #region Validation

            var extensionList = fileNames.Select(s => Path.GetExtension(s).ToLowerInvariant()).ToList();

            var media = new[] { ".jpg", ".jpeg", ".gif", ".bmp", ".png", ".avi", ".mp4", ".wmv" };

            var projectCount = extensionList.Count(x => !string.IsNullOrEmpty(x) && (x.Equals(".stg") || x.Equals(".zip")));
            var mediaCount = extensionList.Count(x => !string.IsNullOrEmpty(x) && media.Contains(Path.GetExtension(x)));

            if (projectCount != 0 && mediaCount != 0)
            {
                Dialog.Ok(StringResource("Editor.DragDrop.Invalid.Title"),
                    StringResource("Editor.DragDrop.MultipleFiles.Instruction"),
                    StringResource("Editor.DragDrop.MultipleFiles.Message"), Icons.Warning);
                return;
            }

            if (mediaCount == 0 && projectCount == 0)
            {
                Dialog.Ok(StringResource("Editor.DragDrop.Invalid.Title"),
                    StringResource("Editor.DragDrop.Invalid.Instruction"),
                    StringResource("Editor.DragDrop.Invalid.Message"), Icons.Warning);
                return;
            }

            //if (projectCount > 0)
            //{
            //    Dialog.Ok(StringResource("Editor.DragDrop.Invalid.Title"),
            //        StringResource("Editor.DragDrop.InvalidProject.Instruction"),
            //        StringResource("Editor.DragDrop.InvalidProject.Message"), Dialog.Icons.Warning);
            //    return;
            //}

            #endregion

            #region Importing options

            //If inserted into new recording or forced into new one.
            if (Project == null || !Project.Any || e.KeyStates == DragDropKeyStates.ControlKey || projectCount > 0)
                _importFramesDel = ImportFrom;
            else
                _importFramesDel = InsertImportFrom;

            #endregion

            _importFramesDel.BeginInvoke(fileNames.ToList(), ImportFromCallback, null);
        }

        private void CancelLoadingButton_Click(object sender, RoutedEventArgs e)
        {
            _abortLoading = true;
            CancelLoadingButton.IsEnabled = false;
        }

        private void InkCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !(sender is InkCanvas canvas))
                return;

            //This event only exists because the InkPanel eats the Esc key used in Commands.
            //if something is not selected, run the command to close the panel.
            if (canvas.ActiveEditingMode != InkCanvasEditingMode.Select && canvas.GetSelectedStrokes().Any())
                CancelCommandBinding.Command.Execute(null);
        }

        #endregion

        #region Methods

        #region Load

        #region Async Loading

        private delegate bool LoadFrames();

        private LoadFrames _loadFramesDel;

        /// <summary>
        /// Loads the new frames and clears the old ones.
        /// </summary>
        /// <param name="newProject">The project to load.</param>
        /// <param name="isNew">True if this is a new project.</param>
        /// <param name="clear">True if should clear the current list of frames.</param>
        /// <param name="createFlag">True if it should create a flag for single use, a mutex.</param>
        internal void LoadProject(ProjectInfo newProject, bool isNew = true, bool clear = true, bool createFlag = false)
        {
            Cursor = Cursors.AppStarting;
            IsLoading = true;

            FrameListView.Items.Clear();
            ZoomBoxControl.Zoom = 1;

            #region Discard

            if (clear || isNew)
            {
                //Clear clipboard data.
                ClipboardListBox.Items.Clear();
                Util.Clipboard.Items?.Clear();
            }

            //TODO: Settings to choose if the project will be discarded.
            if (clear && Project != null && Project.Any)
            {
                Project.Persist();

                if (!UserSettings.All.NotifyProjectDiscard || Dialog.Ask(LocalizationHelper.Get("Editor.DiscardProject.Title"), LocalizationHelper.Get("Editor.DiscardPreviousProject.Instruction"),
                        LocalizationHelper.Get("Editor.DiscardPreviousProject.Message"), false))
                {
                    _discardFramesDel = Discard;
                    _discardFramesDel.BeginInvoke(Project, DiscardAndLoadCallback, null);

                    Project = newProject;

                    ActionStack.Clear();
                    ActionStack.Project = Project;
                    return;
                }

                Project.Clear();
                ActionStack.Clear();
            }

            #endregion

            if (isNew)
            {
                if (!clear) //Already clears the project above if flag 'clear' is true.
                    Project?.Clear();

                Project = newProject;

                if (createFlag)
                    Project.CreateMutex();

                ActionStack.Clear();
                ActionStack.Project = Project;
            }

            _loadFramesDel = Load;
            _loadFramesDel.BeginInvoke(LoadCallback, null);
        }

        private bool Load()
        {
            try
            {
                ShowProgress(DispatcherStringResource("Editor.LoadingFrames"), Project.Frames.Count);

                if (!Project.IsNew)
                    Project.Persist();

                var corruptedList = new List<FrameInfo>();
                var count = 0;

                #region Check if there's any missing frames (and remove them)

                foreach (var frame in Project.Frames)
                {
                    if (_abortLoading)
                        break;

                    if (!File.Exists(frame.Path))
                        corruptedList.Add(frame);
                }

                if (_abortLoading)
                {
                    _abortLoading = false;
                    return false;
                }

                //Remove the corrupted frames.
                foreach (var frame in corruptedList)
                    Project.Frames.Remove(frame);

                if (Project.Frames.Count == 0)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        Dialog.Ok(LocalizationHelper.Get("Editor.LoadingFrames"), LocalizationHelper.Get("Editor.LoadingFrames.ProjectCorrupted.Instruction"),
                            LocalizationHelper.Get("Editor.LoadingFrames.ProjectCorrupted.Message"));
                    });
                    return false;
                }

                #endregion

                //If the project was never loaded inside the editor. Projects created with any version older than 2.14 won't enter here.
                if (Project.IsNew)
                {
                    Project.IsNew = false;
                    Project.Persist();

                    var tasks = UserSettings.All.AutomatedTasksList?.Cast<DefaultTaskModel>().ToList() ?? new List<DefaultTaskModel>();

                    if (tasks.Any())
                    {
                        #region Initialize the previewer

                        Dispatcher.Invoke(() =>
                        {
                            ZoomBoxControl.LoadFromPath(Project.Frames[0].Path);

                            OverlayGrid.Visibility = Visibility.Visible;
                            OverlayGrid.Opacity = 1;
                            OverlayGrid.UpdateLayout();

                            var size = ZoomBoxControl.GetElementSize();

                            CaptionOverlayGrid.Width = size.Width;
                            CaptionOverlayGrid.Height = size.Height;
                            CaptionOverlayGrid.UpdateLayout();
                        });

                        #endregion

                        foreach (var task in tasks)
                        {
                            if (_abortLoading)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ZoomBoxControl.ImageSource = null;

                                    OverlayGrid.Visibility = Visibility.Collapsed;
                                    OverlayGrid.Opacity = 0;
                                });

                                _abortLoading = false;
                                return false;
                            }

                            try
                            {
                                //TODO: Abort loading from inside the tasks too.
                                switch (task.TaskType)
                                {
                                    case DefaultTaskModel.TaskTypeEnum.MouseClicks:
                                        if (Project.CreatedBy == ProjectByType.ScreenRecorder && UserSettings.All.DetectMouseClicks)
                                            MouseClicksAsync(task as MouseClicksModel ?? MouseClicksModel.FromSettings());
                                        break;

                                    case DefaultTaskModel.TaskTypeEnum.KeyStrokes:
                                        if (Project.CreatedBy == ProjectByType.ScreenRecorder)
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                KeyStrokesGrid.Visibility = Visibility.Visible;
                                                KeyStrokesLabel.Text = "Ctrl + C";
                                                KeyStrokesLabel.MinHeight = 0;
                                            });
                                            KeyStrokesAsync(task as KeyStrokesModel ?? KeyStrokesModel.FromSettings());
                                            Dispatcher.Invoke(() => KeyStrokesGrid.Visibility = Visibility.Collapsed);
                                        }
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                LogWriter.Log(e, "Error while applying automatic task");
                                Dispatcher.Invoke(() => { ExceptionDialog.Ok(e, "ScreenToGif", "Error while executing a task", e.Message); });
                            }
                        }

                        #region Reset the previer state

                        Dispatcher.Invoke(() =>
                        {
                            ZoomBoxControl.ImageSource = null;

                            OverlayGrid.Visibility = Visibility.Collapsed;
                            OverlayGrid.Opacity = 0;
                        });

                        #endregion
                    }
                }

                #region Load frames into the ListView

                foreach (var frame in Project.Frames)
                {
                    if (_abortLoading)
                        break;

                    frame.Index = count++;

                    Dispatcher.Invoke(() =>
                    {
                        var item = new FrameListBoxItem
                        {
                            FrameNumber = frame.Index,
                            Image = frame.Path,
                            Delay = frame.Delay
                        };

                        FrameListView.Items.Add(item);

                        UpdateProgress(item.FrameNumber);
                    });
                }

                if (_abortLoading)
                {
                    _abortLoading = false;
                    return false;
                }

                if (corruptedList.Any())
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        Dialog.Ok(LocalizationHelper.Get("Editor.LoadingFrames"), LocalizationHelper.Get("Editor.LoadingFrames.FramesCorrupted.Instruction"),
                            LocalizationHelper.Get("Editor.LoadingFrames.FramesCorrupted.Message"));
                    });
                }

                #endregion

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Frame Loading");

                return false;
            }
        }

        private void LoadCallback(IAsyncResult ar)
        {
            var result = _loadFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(delegate
            {
                Cursor = Cursors.Arrow;
                IsLoading = false;
                IsCancelable = false;

                if (Project.Any)
                    FilledList = true;

                if (!result)
                {
                    CancelLoadingButton.IsEnabled = true; //TODO: Is this right?

                    _discardFramesDel = Discard;
                    _discardFramesDel.BeginInvoke(Project, DiscardCallback, null);
                    return;
                }

                FrameListView.SelectedIndex = -1;
                FrameListView.SelectedIndex = 0; //TODO: Get the latest selected frame is it's the same project.
                ZoomBoxControl.PixelSize = Project.Frames[0].Path.ScaledSize();
                ZoomBoxControl.ImageScale = Project.Frames[0].Path.ScaleOf();
                ZoomBoxControl.RefreshImage();

                //ListBoxSelector.SetIsEnabled(FrameListView, true);
                //new ListBoxSelector(FrameListView);

                HideProgress();
                UpdateStatistics();

                WelcomeGrid.BeginStoryboard(this.FindStoryboard("HideWelcomeBorderStoryboard"), HandoffBehavior.Compose);

                CommandManager.InvalidateRequerySuggested();

                SetFocusOnCurrentFrame();

                //Adjust the window size based on the frame size.
                if (UserSettings.All.AutomaticallySizeOnContent && SizeToContentCommand.Command != null && SizeToContentCommand.Command.CanExecute(null))
                    SizeToContentCommand.Command.Execute(null);

                //Adjust the frame zoom based on the window size.
                if (UserSettings.All.AutomaticallyFitImage && FitImageCommand.Command != null && FitImageCommand.Command.CanExecute(null))
                    FitImageCommand.Command.Execute(null);
            });
        }

        #endregion

        #region Async Selective Loading

        private delegate bool LoadSelectedFrames(int start, int? end);

        private LoadSelectedFrames _loadSelectedFramesDel;

        private void LoadSelectedStarter(int start, int? end = null)
        {
            Cursor = Cursors.AppStarting;
            IsLoading = true;
            ShowProgress(StringResource("Editor.UpdatingFrames"), Project.Frames.Count, true);

            //Persists the project to the disk.
            Task.Factory.StartNew(() => Project.Persist(), TaskCreationOptions.PreferFairness);

            _loadSelectedFramesDel = LoadSelected;
            _loadSelectedFramesDel.BeginInvoke(start, end, LoadSelectedCallback, null);
        }

        private bool LoadSelected(int start, int? end)
        {
            end = end ?? Project.Frames.Count - 1;
            UpdateProgress(0);

            try
            {
                //For each changed frame.
                for (var index = start; index <= end; index++)
                {
                    //Check if within limits.
                    if (index <= FrameListView.Items.Count - 1)
                    {
                        #region Edit the existing frame

                        Dispatcher.Invoke(() =>
                        {
                            var frame = (FrameListBoxItem)FrameListView.Items[index];

                            //TODO: Check if ListFrames.Count == FrameListView.Items.Count
                            frame.FrameNumber = index;
                            frame.Delay = Project.Frames[index].Delay;
                            frame.Image = null; //To update the image.
                            frame.Image = Project.Frames[index].Path;
                            frame.UpdateLayout();
                            frame.InvalidateVisual();
                        });

                        Project.Frames[index].Index = index;

                        #endregion
                    }
                    else
                    {
                        #region Create another frame

                        Dispatcher.Invoke(() =>
                        {
                            var item = new FrameListBoxItem
                            {
                                FrameNumber = index,
                                Image = Project.Frames[index].Path,
                                Delay = Project.Frames[index].Delay
                            };

                            FrameListView.Items.Add(item);
                        });

                        Project.Frames[index].Index = index;

                        #endregion
                    }

                    UpdateProgress(index);
                }

                if (Project.Frames.Count > 0)
                    Dispatcher.Invoke(() => { FilledList = true; });

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Frame Loading Error");
                return false;
            }
        }

        private void LoadSelectedCallback(IAsyncResult ar)
        {
            try
            {
                var result = _loadSelectedFramesDel.EndInvoke(ar);
            }
            catch (Exception)
            { }

            Dispatcher.Invoke(delegate
            {
                Cursor = Cursors.Arrow;
                HideProgress();

                if (LastSelected != -1)
                {
                    ZoomBoxControl.ImageSource = null;

                    var valid = Project.ValidIndex(LastSelected);

                    if (valid > -1)
                    {
                        ZoomBoxControl.ImageSource = Project.Frames[valid].Path;
                        ZoomBoxControl.PixelSize = Project.Frames[0].Path.ScaledSize();
                        ZoomBoxControl.ImageScale = Project.Frames[0].Path.ScaleOf();
                        ZoomBoxControl.RefreshImage();
                        FrameListView.ScrollIntoView(FrameListView.Items[valid]);
                    }
                }

                UpdateStatistics();

                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();

                SetFocusOnCurrentFrame();
            });
        }

        #endregion

/*
        #region Async Import

        private delegate bool ImportFrames(List<string> fileList);

        private ImportFrames _importFramesDel;

        private List<FrameInfo> InsertInternal(string fileName, string pathTemp)
        {
            List<FrameInfo> listFrames;

            try
            {
                switch (fileName.Split('.').Last().ToLowerInvariant())
                {
                    case "stg":
                    case "zip":

                        listFrames = ImportFromProject(fileName, pathTemp);
                        break;

                    case "gif":

                        listFrames = ImportFromGif(fileName, pathTemp);
                        break;

                    case "mp4":
                    case "wmv":
                    case "avi":

                        listFrames = ImportFromVideo(fileName, pathTemp);
                        break;

                    default:

                        listFrames = ImportFromImage(fileName, pathTemp);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Import Error");

                return new List<FrameInfo>();
            }

            return listFrames;
        }

        private bool ImportFrom(List<string> fileList)
        {
            #region Disable UI

            Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.AppStarting;
                IsLoading = true;
            });

            #endregion

            ShowProgress(DispatcherStringResource("Editor.PreparingImport"), 100);

            var project = new ProjectInfo().CreateProjectFolder(ProjectByType.Editor);

            //Adds each image to a list.
            foreach (var file in fileList)
            {
                if (Dispatcher.HasShutdownStarted)
                    return false;

                project.Frames.AddRange(InsertInternal(file, project.FullPath) ?? new List<FrameInfo>());
            }

            if (project.Frames.Count == 0)
            {
                if (Dispatcher.HasShutdownStarted)
                    return false;

                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    IsLoading = false;

                    if (project.Any)
                        FilledList = true;

                    HideProgress();

                    CommandManager.InvalidateRequerySuggested();
                    SetFocusOnCurrentFrame();
                });

                return false;
            }

            Dispatcher.Invoke(() => LoadProject(project));

            return true;
        }

        private void ImportFromCallback(IAsyncResult ar)
        {
            _importFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(delegate
            {
                ClosePanel(removeEvent: true);

                CommandManager.InvalidateRequerySuggested();
            });

            GC.Collect();
        }

        private bool InsertImportFrom(List<string> fileList)
        {
            #region Disable UI

            Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.AppStarting;
                IsLoading = true;
            });

            #endregion

            ShowProgress(DispatcherStringResource("Editor.PreparingImport"), 100);

            var project = new ProjectInfo().CreateProjectFolder(ProjectByType.Editor);

            //Adds each image to a list.
            foreach (var file in fileList)
                project.Frames.AddRange(InsertInternal(file, project.FullPath) ?? new List<FrameInfo>());

            if (!project.Any)
            {
                project.ReleaseMutex();

                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    IsLoading = false;
                });

                return false;
            }

            return Dispatcher.Invoke(() =>
            {
                #region Insert

                //TODO: Treat multi-sized set of images...
                var insert = new Insert(Project.Frames, project.Frames, FrameListView.SelectedIndex) { Owner = this };
                var result = insert.ShowDialog();

                project.ReleaseMutex();

                if (result.HasValue && result.Value)
                {
                    ActionStack.SaveState(ActionStack.EditAction.Add, FrameListView.SelectedIndex, project.Frames.Count);

                    Project.Frames = insert.ActualList;
                    LoadSelectedStarter(FrameListView.SelectedIndex, Project.Frames.Count - 1); //Check

                    return true;
                }

                HideProgress();

                if (LastSelected != -1)
                {
                    ZoomBoxControl.ImageSource = null;
                    ZoomBoxControl.ImageSource = Project.Frames[LastSelected].Path;

                    FrameListView.ScrollIntoView(FrameListView.Items[LastSelected]);
                }

                #region Enabled the UI

                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    IsLoading = false;
                });

                #endregion

                return false;

                #endregion
            });
        }

        private void InsertImportFromCallback(IAsyncResult ar)
        {
            var result = _importFramesDel.EndInvoke(ar);

            GC.Collect();

            if (!result)
                Dispatcher.Invoke(delegate
                {
                    Cursor = Cursors.Arrow;
                    IsLoading = false;

                    ClosePanel(removeEvent: true);

                    FrameListView.Focus();
                    CommandManager.InvalidateRequerySuggested();
                });
        }

        #endregion

        
        private List<FrameInfo> ImportFromProject(string sourceFileName, string pathTemp)
        {
            try
            {
                //Extract to the folder of the newly created project.
                ZipFile.ExtractToDirectory(sourceFileName, pathTemp);

                List<FrameInfo> list;

                if (File.Exists(Path.Combine(pathTemp, "Project.json")))
                {
                    //Read as text.
                    var json = File.ReadAllText(Path.Combine(pathTemp, "Project.json"));

                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var ser = new DataContractJsonSerializer(typeof(ProjectInfo));
                        var project = ser.ReadObject(ms) as ProjectInfo;
                        list = project.Frames;
                    }
                }
                else
                {
                    if (File.Exists(Path.Combine(pathTemp, "List.sb")))
                        throw new Exception("Project not compatible with this version");

                    throw new FileNotFoundException("Impossible to open project.", "List.sb");
                }

                //Shows the ProgressBar
                ShowProgress("Importing Frames", list.Count);

                var count = 0;
                foreach (var frame in list)
                {
                    //Change the file path to the current one.
                    frame.Path = Path.Combine(pathTemp, Path.GetFileName(frame.Path));

                    count++;
                    UpdateProgress(count);
                }

                return list;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Importing project");
                Dispatcher.Invoke(() => Dialog.Ok("ScreenToGif", "Impossible to load project", ex.Message));
                return new List<FrameInfo>();
            }
        }

        private List<FrameInfo> ImportFromGif(string sourceFileName, string pathTemp)
        {
            ShowProgress(DispatcherStringResource("Editor.ImportingFrames"), 50, true);

            var listFrames = new List<FrameInfo>();

            var decoder = ImageMethods.GetDecoder(sourceFileName, out var gifMetadata) as GifBitmapDecoder;

            ShowProgress(DispatcherStringResource("Editor.ImportingFrames"), decoder.Frames.Count);

            if (decoder.Frames.Count <= 0)
                return listFrames;

            var fullSize = ImageMethods.GetFullSize(decoder, gifMetadata);
            var index = 0;

            BitmapSource baseFrame = null;
            foreach (var rawFrame in decoder.Frames)
            {
                var metadata = ImageMethods.GetFrameMetadata(decoder, gifMetadata, index);

                var bitmapSource = ImageMethods.MakeFrame(fullSize, rawFrame, metadata, baseFrame);

                #region Disposal Method

                switch (metadata.DisposalMethod)
                {
                    case FrameDisposalMethod.None:
                    case FrameDisposalMethod.DoNotDispose:
                        baseFrame = bitmapSource;
                        break;
                    case FrameDisposalMethod.RestoreBackground:
                        baseFrame = ImageMethods.IsFullFrame(metadata, fullSize) ? null : ImageMethods.ClearArea(bitmapSource, metadata);
                        break;
                    case FrameDisposalMethod.RestorePrevious:
                        //Reuse same base frame.
                        break;
                }

                #endregion

                #region Each Frame

                var fileName = Path.Combine(pathTemp, $"{index} {DateTime.Now:hh-mm-ss-FFFF}.png");

                using (var stream = new FileStream(fileName, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    encoder.Save(stream);
                    stream.Close();
                }

                //It should not throw a overflow exception because of the maximum value for the milliseconds.
                var frame = new FrameInfo(fileName, (int)metadata.Delay.TotalMilliseconds);
                listFrames.Add(frame);

                UpdateProgress(index);

                GC.Collect(1);

                #endregion

                index++;
            }

            return listFrames;
        }

        private List<FrameInfo> ImportFromImage(string sourceFileName, string pathTemp)
        {
            var fileName = Path.Combine(pathTemp, $"{0} {DateTime.Now:hh-mm-ss-FFFF}.png");

            #region Save the Image to the Recording Folder

            BitmapSource bitmap = new BitmapImage(new Uri(sourceFileName));

            if (bitmap.Format != PixelFormats.Bgra32)
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
                stream.Close();
            }

            GC.Collect();

            #endregion

            return new List<FrameInfo> { new FrameInfo(fileName, 66) };
        }

        private List<FrameInfo> ImportFromVideo(string fileName, string pathTemp)
        {
            var delay = 66;

            var frameList = Dispatcher.Invoke(() =>
            {
                var videoSource = new VideoSource(fileName) { Owner = this };
                var result = videoSource.ShowDialog();

                delay = videoSource.Delay;

                if (result.HasValue && result.Value)
                    return videoSource.FrameList;

                return null;
            });

            //return frameList ?? new List<FrameInfo>();

            if (frameList == null)
                return new List<FrameInfo>();

            ShowProgress(DispatcherStringResource("Editor.ImportingFrames"), frameList.Count);

            #region Saves the Frames to the Disk

            var frameInfoList = new List<FrameInfo>();
            var count = 0;

            foreach (var frame in frameList)
            {
                var frameName = Path.Combine(pathTemp, $"{count} {DateTime.Now:hh-mm-ss-FFFF}.png");

                using (var stream = new FileStream(frameName, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(frame);
                    encoder.Save(stream);
                    stream.Close();
                }

                var frameInfo = new FrameInfo(frameName, delay);
                frameInfoList.Add(frameInfo);

                GC.Collect(1, GCCollectionMode.Forced);
                count++;

                UpdateProgress(count);
            }

            frameList.Clear();
            GC.Collect();

            #endregion

            return frameInfoList;
        }*/

        #endregion

        #region Playback

        private void PlayPause()
        {
            if (_timerPreview.Enabled)
            {
                _timerPreview.Tick -= TimerPreview_Tick;
                _timerPreview.Stop();

                NotPreviewing = true;
                
                Menu.PlayButton.Text = StringResource("Editor.Playback.Play");
                Menu.PlayButton.Content = FindResource("Vector.Play");
                PlayPauseButton.Content = FindResource("Vector.Play");

                PlayMenuItem.Header = StringResource("Editor.Playback.Play");
                PlayMenuItem.Image = (Canvas)FindResource("Vector.Play");

                SetFocusOnCurrentFrame();
            }
            else
            {
                NotPreviewing = false;
                Menu.PlayButton.Text = StringResource("Editor.Playback.Pause");
                Menu.PlayButton.Content = FindResource("Vector.Pause");
                PlayPauseButton.Content = FindResource("Vector.Pause");

                PlayMenuItem.Header = StringResource("Editor.Playback.Pause");
                PlayMenuItem.Image = (Canvas)FindResource("Vector.Pause");

                #region Starts playing the next frame

                if (Project.Frames.Count - 1 == FrameListView.SelectedIndex)
                {
                    FrameListView.SelectedIndex = 0;
                }
                else
                {
                    FrameListView.SelectedIndex++;
                }

                #endregion

                if (Project.Frames[FrameListView.SelectedIndex].Delay == 0)
                    Project.Frames[FrameListView.SelectedIndex].Delay = 10;

                _timerPreview.Interval = Project.Frames[FrameListView.SelectedIndex].Delay;
                _timerPreview.Tick += TimerPreview_Tick;
                _timerPreview.Start();
            }
        }

        private void Pause()
        {
            if (!_timerPreview.Enabled)
                return;

            _timerPreview.Tick -= TimerPreview_Tick;
            _timerPreview.Stop();

            NotPreviewing = true;
            Menu.PlayButton.Text = StringResource("Editor.Playback.Play");
            Menu.PlayButton.Content = FindResource("Vector.Play");
            PlayPauseButton.Content = FindResource("Vector.Play");

            PlayMenuItem.Header = StringResource("Editor.Playback.Play");
            PlayMenuItem.Image = (Canvas)FindResource("Vector.Play");

            SetFocusOnCurrentFrame();
        }

        #endregion

        #region UI

        #region Progress

        private void ShowProgress(string description, int maximum, bool isIndeterminate = false)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Content = description;
                StatusProgressBar.Maximum = maximum;
                StatusProgressBar.Value = 0;
                StatusProgressBar.IsIndeterminate = isIndeterminate;
                StatusGrid.Visibility = Visibility.Visible;

                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
            }, DispatcherPriority.Loaded);
        }

        private void UpdateProgress(int value)
        {
            Dispatcher.Invoke(() =>
            {
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                StatusProgressBar.IsIndeterminate = false;
                StatusProgressBar.Value = value;
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                StatusGrid.Visibility = Visibility.Collapsed;
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            });
        }

        #endregion

        private void SelectNear(int index)
        {
            FrameListView.Focus();

            if (FrameListView.Items.Count - 1 < index)
            {
                FrameListView.SelectedIndex = FrameListView.Items.Count - 1;
                return;
            }

            FrameListView.SelectedIndex = index;
            FrameListView.ScrollIntoView(FrameListView.SelectedItem);
        }

        private void AdjustFrameNumbers(int startIndex)
        {
            for (var index = startIndex; index < FrameListView.Items.Count; index++)
            {
                Project.Frames[index].Index = index;
                ((FrameListBoxItem)FrameListView.Items[index]).FrameNumber = index;
            }
        }

        private void ShowPanel(PanelType type, string title, string vector, Action<object, RoutedEventArgs> apply = null)
        {
            #region Hide all visible grids

            foreach (var child in ActionInternalGrid.Children.OfType<Grid>().Where(x => x.Visibility == Visibility.Visible))
                child.Visibility = Visibility.Collapsed;

            #endregion

            #region Overlay

            if (Project != null && Project.Any && type < 0)
            {
                ZoomBoxControl.Zoom = 1.0;
                var size = ZoomBoxControl.GetElementSize();

                CaptionOverlayGrid.Width = size.Width;
                CaptionOverlayGrid.Height = size.Height;
            }

            #endregion

            #region Commons

            ActionTitleLabel.Content = title;
            ActionViewBox.Child = FindResource(vector) as Canvas;

            Util.Other.RemoveRoutedEventHandlers(ApplyButton, ButtonBase.ClickEvent);

            if (apply != null)
            {
                ApplyButton.Text = StringResource("Action.Apply");
                ApplyButton.Content = FindResource("Vector.Ok") as Canvas;
                _applyAction = apply;

                ActionLowerGrid.Visibility = Visibility.Visible;
            }
            else
            {
                ActionLowerGrid.Visibility = Visibility.Collapsed;
            }

            #endregion

            #region Type

            switch (type)
            {
                case PanelType.NewAnimation:
                    NewGrid.Visibility = Visibility.Visible;
                    break;
                case PanelType.SaveAs:
                    ApplyButton.Text = StringResource("Action.Save");
                    ApplyButton.Content = FindResource("Vector.Save") as Canvas;
                    SaveGrid.Visibility = Visibility.Visible;
                    break;
                case PanelType.LoadRecent:
                    ApplyButton.Text = StringResource("Action.Open");
                    ApplyButton.Content = FindResource("Vector.Open") as Canvas;
                    LoadRecentGrid.Visibility = Visibility.Visible;

                    //Load list.
                    _loadRecentDel = LoadRecentAsync;
                    _loadRecentDel.BeginInvoke(LoadRecentCallback, null);
                    break;
                case PanelType.Clipboard:
                    ClipboardGrid.Visibility = Visibility.Visible;
                    break;
                case PanelType.Resize:
                    ResizeGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplyAll", true);
                    break;
                case PanelType.FlipRotate:
                    FlipRotateGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.FlipRotate2", true);
                    break;
                case PanelType.Crop:

                    #region Crop

                    CropGrid.Visibility = Visibility.Visible;

                    AddCropToElement(CropAreaGrid);

                    BottomCropNumericUpDown.Scale = TopCropNumericUpDown.Scale = RightCropNumericUpDown.Scale = LeftCropNumericUpDown.Scale = this.Scale();

                    BottomCropNumericUpDown.Value = (int)(CaptionOverlayGrid.Height - (CaptionOverlayGrid.Height * .1));
                    TopCropNumericUpDown.Value = (int)(CaptionOverlayGrid.Height * .1);

                    RightCropNumericUpDown.Value = (int)(CaptionOverlayGrid.Width - (CaptionOverlayGrid.Width * .1));
                    LeftCropNumericUpDown.Value = (int)(CaptionOverlayGrid.Width * .1);

                    ShowHint("Hint.ApplyAll", true);

                    #endregion

                    break;
                case PanelType.Caption:
                    CaptionGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.FreeText:
                    FreeTextGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.TitleFrame:
                    TitleFrameGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.TitleFrame2", true);
                    break;
                case PanelType.KeyStrokes:
                    KeyStrokesLabel.MinHeight = 0;
                    KeyStrokesLabel.Text = "Ctrl + c";
                    KeyStrokesGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplyAll", true);
                    break;
                case PanelType.FreeDrawing:
                    FreeDrawingGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.Shapes:
                    ShapesGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);

                    ShapeProperties_Changed(this, null);
                    ShapeType_SelectionChanged(ShapesListBox, null);
                    break;
                case PanelType.Watermark:

                    #region Watermark

                    //#region Arrange

                    //if (UserSettings.All.WatermarkLeft < 0)
                    //    UserSettings.All.WatermarkLeft = 10;

                    //if (UserSettings.All.WatermarkLeft + 10 > CaptionOverlayGrid.Width)
                    //    UserSettings.All.WatermarkLeft = 10;

                    //if (UserSettings.All.WatermarkTop < 0)
                    //    UserSettings.All.WatermarkTop = 10;

                    //if (UserSettings.All.WatermarkTop + 10 > CaptionOverlayGrid.Height)
                    //    UserSettings.All.WatermarkTop = 10;

                    //#endregion

                    WatermarkGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);

                    #endregion

                    break;
                case PanelType.Border:
                    BorderGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.Obfuscate:
                    ObfuscateOverlaySelectControl.Scale = this.Scale();
                    ObfuscateOverlaySelectControl.Retry();
                    ObfuscateGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;

                case PanelType.Progress:
                    ProgressGrid.Visibility = Visibility.Visible;

                    ChangeProgressTextToCurrent();
                    ShowHint("Hint.ApplyAll", true);
                    break;
                case PanelType.OverrideDelay:
                    OverrideDelayGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.IncreaseDecreaseDelay:
                    IncreaseDecreaseDelayGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.ScaleDelay:
                    ScaleDelayGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplySelected", true);
                    break;
                case PanelType.Cinemagraph:
                    CinemagraphGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.Cinemagraph", true);
                    break;
                case PanelType.Fade:
                    FadeGrid.Visibility = Visibility.Visible;
                    ShowHint("Transitions.Info", true);
                    break;
                case PanelType.Slide:
                    SlideGrid.Visibility = Visibility.Visible;
                    ShowHint("Transitions.Info", true);
                    break;
                case PanelType.ReduceFrames:
                    ReduceGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplyAll", true);
                    break;
                case PanelType.RemoveDuplicates:
                    RemoveDuplicatesGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplyAll", true);
                    break;
                case PanelType.MouseClicks:
                    MouseClicksGrid.Visibility = Visibility.Visible;
                    ShowHint("Hint.ApplyAll", true);
                    break;
            }

            #endregion

            #region Focus

            var visible = ActionInternalGrid.Children.OfType<Grid>().FirstOrDefault(x => x.Visibility == Visibility.Visible);

            if (visible != null)
            {
                visible.Focus();
                visible.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            #endregion

            #region Animate

            if ((type == PanelType.SaveAs || type == PanelType.LoadRecent) && ActionGrid.Width < 300)
                ActionGrid.BeginStoryboard(this.FindStoryboard("ShowExtendedPanelStoryboard"), HandoffBehavior.Compose);
            else if (type != PanelType.SaveAs && type != PanelType.LoadRecent && (ActionGrid.Width < 5 || ActionGrid.Width > 280))
                ActionGrid.BeginStoryboard(this.FindStoryboard("ShowPanelStoryboard"), HandoffBehavior.Compose);

            #endregion

            #region Overlay Grid

            if (OverlayGrid.Opacity < 1 && type < 0)
                OverlayGrid.BeginStoryboard(this.FindStoryboard("ShowOverlayGridStoryboard"), HandoffBehavior.Compose);
            else if (OverlayGrid.Opacity > 0 && type > 0)
                OverlayGrid.BeginStoryboard(this.FindStoryboard("HideOverlayGridStoryboard"), HandoffBehavior.Compose);

            //For when panels don't need to show an overlay.
            if (type > 0)
                ZoomBoxControl.SetZoomAsPrevious();

            #endregion

            CommandManager.InvalidateRequerySuggested();
        }

        private void ClosePanel(bool isCancel = false, bool removeEvent = false)
        {
            StatusList.Remove(StatusType.Warning);

            if (ActionGrid.ActualWidth > 0)
                ZoomBoxControl.ResetToPrevious();

            HideHint();

            if (isCancel)
                SetFocusOnCurrentFrame();

            if (removeEvent)
                _applyAction = null;

            BeginStoryboard(this.FindStoryboard("HidePanelStoryboard"), HandoffBehavior.Compose);
            BeginStoryboard(this.FindStoryboard("HideOverlayGridStoryboard"), HandoffBehavior.Compose);
        }

        private List<int> SelectedFramesIndex()
        {
            return FrameListView.SelectedItems.OfType<FrameListBoxItem>().Select(x => FrameListView.Items.IndexOf(x)).OrderBy(y => y).ToList();
        }

        private bool UpdatePositioning(bool onLoad = true)
        {
            //TODO: When the DPI changes, these values are still from the latest dpi.
            var top = onLoad ? UserSettings.All.EditorTop : Top;
            var left = onLoad ? UserSettings.All.EditorLeft : Left;
            var width = onLoad ? UserSettings.All.EditorWidth : Width;
            var height = onLoad ? UserSettings.All.EditorHeight : Height;
            var state = onLoad ? UserSettings.All.EditorWindowState : WindowState;

            //If the position was never set, let it center on screen. 
            if (double.IsNaN(top) && double.IsNaN(left))
                return false;

            //The catch here is to get the closest monitor from current Top/Left point. 
            var monitors = Monitor.AllMonitorsScaled(this.Scale());
            var closest = monitors.FirstOrDefault(x => x.Bounds.Contains(new Point((int)left, (int)top))) ?? monitors.FirstOrDefault(x => x.IsPrimary);

            if (closest == null)
                return false;

            //To much to the Left.
            if (closest.WorkingArea.Left > left + width - 100)
                left = closest.WorkingArea.Left;

            //Too much to the top.
            if (closest.WorkingArea.Top > top + height - 100)
                top = closest.WorkingArea.Top;

            //Too much to the right.
            if (closest.WorkingArea.Right < left + 100)
                left = closest.WorkingArea.Right - width;

            //Too much to the bottom.
            if (closest.WorkingArea.Bottom < top + 100)
                top = closest.WorkingArea.Bottom - height;

            Top = top;
            Left = left;
            Width = width;
            Height = height;
            WindowState = state;

            return true;
        }

        #endregion

        #region Other

        private void Discard(bool notify = true)
        {
            Pause();

            if (notify && !Dialog.Ask(LocalizationHelper.Get("Editor.DiscardProject.Title"), LocalizationHelper.Get("Editor.DiscardProject.Instruction"), LocalizationHelper.Get("Editor.DiscardProject.Message"), false))
                return;

            #region Prepare UI

            ClosePanel();

            FrameListView.SelectedIndex = -1;
            FrameListView.SelectionChanged -= FrameListView_SelectionChanged;

            FrameListView.Items.Clear();
            ClipboardListBox.Items.Clear();
            Util.Clipboard.Items.Clear();
            ZoomBoxControl.Clear();

            #endregion

            if (Project == null || !Project.Any) return;

            _discardFramesDel = Discard;
            _discardFramesDel.BeginInvoke(Project, DiscardCallback, null);
        }

        private void DeleteFrame(int index)
        {
            //Delete the File from the disk.
            File.Delete(Project.Frames[index].Path);

            //Remove from the list.
            Project.Frames.RemoveAt(index);
            FrameListView.Items.RemoveAt(index);
        }

        private List<FrameInfo> SelectedFrames()
        {
            var selectedIndexList = Dispatcher.Invoke(SelectedFramesIndex);
            return Project.Frames.Where(x => selectedIndexList.Contains(Project.Frames.IndexOf(x))).ToList();
        }

        private string StringResource(string key)
        {
            return FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " ");
        }

        private string DispatcherStringResource(string key)
        {
            return Dispatcher.Invoke(() => FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " "));
        }

        private void ChangeFileNumber(int change)
        {
            //If there's no filename declared, show the default one.
            if (string.IsNullOrWhiteSpace(GetOutputFilename()))
            {
                SetOutputFilename(StringResource("S.SaveAs.File.Animation"));
                return;
            }

            var index = GetOutputFilename().Length;
            int start = -1, end = -1;

            //Detects the last number in a string.
            foreach (var c in GetOutputFilename().Reverse())
            {
                if (char.IsNumber(c))
                {
                    if (end == -1)
                        end = index;

                    start = index - 1;
                }
                else if (start == index)
                    break;

                index--;
            }

            //If there's no number.
            if (end == -1)
            {
                SetOutputFilename(GetOutputFilename() + $" ({change})");
                return;
            }

            //If it's a negative number, include the signal.
            if (start > 0 && GetOutputFilename().Substring(start - 1, 1).Equals("-"))
                start--;

            //Cut, convert, merge.
            if (int.TryParse(GetOutputFilename().Substring(start, end - start), out var number))
            {
                var offset = start + number.ToString().Length;

                SetOutputFilename(GetOutputFilename().Substring(0, start) + (number + change) + GetOutputFilename().Substring(offset, GetOutputFilename().Length - end));
            }
        }

        private void UpdateStatistics()
        {
            TotalDuration = TimeSpan.FromMilliseconds(Project.Frames.Sum(x => x.Delay));
            FrameSize = Project.Frames.Count > 0 ? Project.Frames[0].Path.ScaledSize() : new Size(0, 0);
            FrameScale = Project.Frames.Count > 0 ? Convert.ToInt32(Project.Frames[0].Path.DpiOf() / 96d * 100d) : 0;
            AverageDelay = Project.Frames.Count > 0 ? Project.Frames.Average(x => x.Delay) : 0;
            FrameDpi = Project.Frames.Count > 0 ? Math.Round(Project.Frames[0].Path.DpiOf(), 0) : 0d;
        }

        private void ShowHint(string hint, bool isPermanent = false, params object[] values)
        {
            if (HintTextBlock.Visibility == Visibility.Visible)
                BeginStoryboard(this.FindStoryboard("HideHintStoryboard"), HandoffBehavior.Compose);

            if (values.Length == 0)
                HintTextBlock.Text = TryFindResource(hint) + "";
            else
                HintTextBlock.Text = string.Format(TryFindResource(hint) + "", values);

            BeginStoryboard(this.FindStoryboard(isPermanent ? "ShowPermanentHintStoryboard" : "ShowHintStoryboard"), HandoffBehavior.Compose);
        }

        private void HideHint()
        {
            if (HintTextBlock.Visibility == Visibility.Visible)
                BeginStoryboard(this.FindStoryboard("HideHintStoryboard"), HandoffBehavior.Compose);
        }

        private void SetFocusOnCurrentFrame()
        {
            FrameListView.Focus();

            if (!(FrameListView.SelectedItem is FrameListBoxItem current))
                return;

            Keyboard.Focus(current);
            current.Focus();
        }

        private void ChangeProgressText(long cumulative, long total, int current)
        {
            switch (ProgressPrecisionComboBox.SelectedIndex)
            {
                case 0: //Minutes
                    ProgressHorizontalTextBlock.Text = UserSettings.All.ProgressShowTotal ? TimeSpan.FromMilliseconds(cumulative).ToString(@"m\:ss") + "/" + TimeSpan.FromMilliseconds(total).ToString(@"m\:ss")
                        : TimeSpan.FromMilliseconds(cumulative).ToString(@"m\:ss");
                    break;
                case 1: //Seconds
                    ProgressHorizontalTextBlock.Text = UserSettings.All.ProgressShowTotal ? (int)TimeSpan.FromMilliseconds(cumulative).TotalSeconds + "/" + TimeSpan.FromMilliseconds(total).TotalSeconds + " s"
                        : (int)TimeSpan.FromMilliseconds(cumulative).TotalSeconds + " s";
                    break;
                case 2: //Milliseconds
                    ProgressHorizontalTextBlock.Text = UserSettings.All.ProgressShowTotal ? cumulative + "/" + total + " ms" : cumulative + " ms";
                    break;
                case 3: //Percentage
                    var count = (double)Project.Frames.Count;
                    ProgressHorizontalTextBlock.Text = (current / count * 100).ToString("##0.#", CultureInfo.CurrentUICulture) + (UserSettings.All.ProgressShowTotal ? "/100%" : " %");
                    break;
                case 4: //Frame number
                    ProgressHorizontalTextBlock.Text = UserSettings.All.ProgressShowTotal ? current + "/" + Project.Frames.Count
                        : current.ToString();
                    break;
                case 5: //Custom
                    ProgressHorizontalTextBlock.Text = CustomProgressTextBox.Text
                        .Replace("$ms", cumulative.ToString())
                        .Replace("$s", ((int)TimeSpan.FromMilliseconds(cumulative).TotalSeconds).ToString())
                        .Replace("$m", TimeSpan.FromMilliseconds(cumulative).ToString())
                        .Replace("$p", (current / (double)Project.Frames.Count * 100).ToString("##0.#", CultureInfo.CurrentUICulture))
                        .Replace("$f", current.ToString())
                        .Replace("@ms", total.ToString())
                        .Replace("@s", ((int)TimeSpan.FromMilliseconds(total).TotalSeconds).ToString())
                        .Replace("@m", TimeSpan.FromMilliseconds(total).ToString(@"m\:ss"))
                        .Replace("@p", "100")
                        .Replace("@f", Project.Frames.Count.ToString());
                    break;
            }
        }

        private void ChangeProgressTextToCurrent()
        {
            var total = Project.Frames.Sum(y => y.Delay);
            var cumulative = 0L;

            for (var j = 0; j < FrameListView.SelectedIndex; j++)
                cumulative += Project.Frames[j].Delay;

            ChangeProgressText(cumulative, total, FrameListView.SelectedIndex);
        }

        private string GetOutputFolder()
        {
            if (!GetPickLocation())
                return Path.GetTempPath();

            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.LatestOutputFolder ?? "";
                case Export.Apng:
                    return UserSettings.All.LatestApngOutputFolder ?? "";
                case Export.Video:
                    return UserSettings.All.LatestVideoOutputFolder ?? "";
                case Export.Images:
                    return UserSettings.All.LatestImageOutputFolder ?? "";
                case Export.Project:
                    return UserSettings.All.LatestProjectOutputFolder ?? "";
                case Export.Photoshop:
                    return UserSettings.All.LatestPhotoshopOutputFolder ?? "";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string GetOutputFilename()
        {
            if (!GetPickLocation())
                return Guid.NewGuid() + "";

            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.LatestFilename ?? "";
                case Export.Apng:
                    return UserSettings.All.LatestApngFilename ?? "";
                case Export.Video:
                    return UserSettings.All.LatestVideoFilename ?? "";
                case Export.Images:
                    return UserSettings.All.LatestImageFilename ?? "";
                case Export.Project:
                    return UserSettings.All.LatestProjectFilename ?? "";
                case Export.Photoshop:
                    return UserSettings.All.LatestPhotoshopFilename ?? "";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string GetOutputFilenameNoRegExp(ref string name)
        {
            //put datetime into filename which is saved between two questions marks
            string dateTimeFileNameRegEx = @"[?]([dyhms]+[-_ ]*)+[?]";
            if (Regex.IsMatch(name, dateTimeFileNameRegEx, RegexOptions.IgnoreCase))
            {
                var dateTimeRegExp = Regex.Match(name, dateTimeFileNameRegEx, RegexOptions.IgnoreCase);
                var dateTimeConverted = DateTime.Now.ToString(Regex.Replace(dateTimeRegExp.Value, "[?]", ""));
                name = name.Replace(dateTimeRegExp.ToString(),dateTimeConverted);
            }
            return name;
        }

        private string GetOutputExtension()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.LatestExtension ?? ".gif";
                case Export.Apng:
                    return UserSettings.All.LatestApngExtension ?? ".png";
                case Export.Video:
                    return UserSettings.All.LatestVideoExtension ?? ".mp4";
                case Export.Images:
                    return UserSettings.All.LatestImageExtension ?? ".png";
                case Export.Project:
                    return UserSettings.All.LatestProjectExtension ?? ".stg";
                case Export.Photoshop:
                    return UserSettings.All.LatestPhotoshopExtension ?? ".psd";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetOutputFolder(string folder)
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    UserSettings.All.LatestOutputFolder = folder;
                    break;
                case Export.Apng:
                    UserSettings.All.LatestApngOutputFolder = folder;
                    break;
                case Export.Video:
                    UserSettings.All.LatestVideoOutputFolder = folder;
                    break;
                case Export.Images:
                    UserSettings.All.LatestImageOutputFolder = folder;
                    break;
                case Export.Project:
                    UserSettings.All.LatestProjectOutputFolder = folder;
                    break;
                case Export.Photoshop:
                    UserSettings.All.LatestPhotoshopOutputFolder = folder;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetOutputFilename(string filename)
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    UserSettings.All.LatestFilename = filename;
                    break;
                case Export.Apng:
                    UserSettings.All.LatestApngFilename = filename;
                    break;
                case Export.Video:
                    UserSettings.All.LatestVideoFilename = filename;
                    break;
                case Export.Images:
                    UserSettings.All.LatestImageFilename = filename;
                    break;
                case Export.Project:
                    UserSettings.All.LatestProjectFilename = filename;
                    break;
                case Export.Photoshop:
                    UserSettings.All.LatestPhotoshopFilename = filename;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetOutputExtension(string extension)
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    UserSettings.All.LatestExtension = extension;
                    break;
                case Export.Apng:
                    UserSettings.All.LatestApngExtension = extension;
                    break;
                case Export.Video:
                    UserSettings.All.LatestVideoExtension = extension;
                    break;
                case Export.Images:
                    UserSettings.All.LatestImageExtension = extension;
                    break;
                case Export.Project:
                    UserSettings.All.LatestProjectExtension = extension;
                    break;
                case Export.Photoshop:
                    UserSettings.All.LatestPhotoshopExtension = extension;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool GetPickLocation()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.PickLocation;
                case Export.Apng:
                    return UserSettings.All.PickLocationApng;
                case Export.Video:
                    return UserSettings.All.PickLocationVideo;
                case Export.Photoshop:
                    return UserSettings.All.PickLocationPhotoshop;
                case Export.Project:
                case Export.Images:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool GetOverwriteOnSave()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.OverwriteOnSave;
                case Export.Apng:
                    return UserSettings.All.OverwriteOnSaveApng;
                case Export.Video:
                    return UserSettings.All.OverwriteOnSaveVideo;
                case Export.Project:
                    return UserSettings.All.OverwriteOnSaveProject;
                case Export.Images:
                    return UserSettings.All.OverwriteOnSaveImages;
                case Export.Photoshop:
                    return UserSettings.All.OverwriteOnSavePhotoshop;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool GetSaveAsProjectToo()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.SaveAsProjectToo;
                case Export.Apng:
                    return UserSettings.All.SaveAsProjectTooApng;
                case Export.Video:
                    return UserSettings.All.SaveAsProjectTooVideo;
                case Export.Photoshop:
                    return UserSettings.All.SaveAsProjectTooPhotoshop;
                case Export.Project:
                case Export.Images:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private CopyType GetCopyType()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.LatestCopyType;
                case Export.Apng:
                    return UserSettings.All.LatestCopyTypeApng;
                case Export.Video:
                    return UserSettings.All.LatestCopyTypeVideo;
                case Export.Project:
                    return UserSettings.All.LatestCopyTypeProject;
                case Export.Photoshop:
                    return UserSettings.All.LatestCopyTypePhotoshop;
                case Export.Images:
                    return CopyType.File;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool GetExecuteCustomCommands()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.ExecuteCustomCommands;
                case Export.Apng:
                    return UserSettings.All.ExecuteCustomCommandsApng;
                case Export.Video:
                    return UserSettings.All.ExecuteCustomCommandsVideo;
                case Export.Photoshop:
                    return UserSettings.All.ExecuteCustomCommandsPhotoshop;
                case Export.Project:
                case Export.Images:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string GetCustomCommands()
        {
            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    return UserSettings.All.CustomCommands;
                case Export.Apng:
                    return UserSettings.All.CustomCommandsApng;
                case Export.Video:
                    return UserSettings.All.CustomCommandsVideo;
                case Export.Photoshop:
                    return UserSettings.All.CustomCommandsPhotoshop;
                case Export.Project:
                case Export.Images:
                    return "";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #endregion

        #region Async

        #region Async Load Recent

        private delegate void LoadRecentDelegate();

        private LoadRecentDelegate _loadRecentDel;

        private void LoadRecentAsync()
        {
            ShowProgress(DispatcherStringResource("Recent.EnumeratingProjects"), 100, true);

            Dispatcher.Invoke(() => IsLoading = true);

            #region Enumerate recent projects

            var list = new List<ProjectInfo>();

            try
            {
                Dispatcher.Invoke(() => RecentDataGrid.ItemsSource = null);

                var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

                if (!Directory.Exists(path))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => RecentDataGrid.ItemsSource = null));
                    return;
                }

                var folderList = Directory.GetDirectories(path).Select(x => new DirectoryInfo(x)).ToList();

                foreach (var folder in folderList)
                {
                    var file = Path.Combine(folder.FullName, "Project.json");

                    if (!File.Exists(file))
                        continue;

                    var json = File.ReadAllText(Path.Combine(file));

                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var ser = new DataContractJsonSerializer(typeof(ProjectInfo));
                        var project = ser.ReadObject(ms) as ProjectInfo;

                        //Ignore empty projects.
                        if (project == null || project.Frames.Count == 0 || !project.Frames.Any(x => File.Exists(x.Path)))
                            continue;

                        list.Add(project);
                    }
                }

                //Waits the animation to complete before filling the grid.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    RecentDataGrid.ItemsSource = list;

                    if (RecentDataGrid.Items.Count > 0)
                    {
                        RecentDataGrid.Sort(ListSortDirection.Descending, "CreationDate");
                        RecentDataGrid.FocusOnFirstCell();
                    }
                }));
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Loading list of recent");

                Dispatcher.Invoke(() => ErrorDialog.Ok("ScreenToGif", "Error while enumerating recent projects", ex.Message, ex));
            }

            #endregion
        }

        private void LoadRecentCallback(IAsyncResult ar)
        {
            _loadRecentDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.Arrow;
                IsLoading = false;

                HideProgress();

                CommandManager.InvalidateRequerySuggested();
            });

            GC.Collect();
        }

        #endregion

        #region Async Save

        private delegate bool SaveDelegate(List<FrameInfo> listFrames, Parameters param, double scale, bool projectToo, bool usePadding = false);

        private SaveDelegate _saveDel;

        private bool SaveAsync(List<FrameInfo> listFrames, Parameters param, double scale, bool projectToo, bool usePadding = false)
        {
            ShowProgress(DispatcherStringResource("S.Editor.PreparingSaving"), Project.Frames.Count, true);

            Dispatcher.Invoke(() => IsLoading = true);

            #region Save

            try
            {
                Dispatcher.Invoke(() => Encoder.AddItem(listFrames.CopyToEncode(usePadding), param, scale));
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Preparing to save the recording");

                Dispatcher.Invoke(() => Dialog.Ok("Error While Saving", "Error while preparing to save", ex.Message));
            }

            //Save as project too.
            if (projectToo)
            {
                _saveProjectDel = SaveProjectAsync;
                _saveProjectDel.BeginInvoke(Path.Combine(GetOutputFolder(), GetOutputFilename() + (UserSettings.All.LatestProjectExtension ?? ".stg")), SaveProjectCallback, null);
            }

            return projectToo;

            #endregion
        }

        private void SaveCallback(IAsyncResult ar)
        {
            var result = _saveDel.EndInvoke(ar);

            if (!result)
                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    IsLoading = false;

                    HideProgress();

                    CommandManager.InvalidateRequerySuggested();
                });

            GC.Collect();
        }

        #endregion

        #region Async Project

        private delegate void SaveProjectDelegate(string fileName);

        private SaveProjectDelegate _saveProjectDel;

        private void SaveProjectAsync(string fileName)
        {
            ShowProgress(DispatcherStringResource("Editor.ExportingRecording"), Project.Frames.Count);

            Dispatcher.Invoke(() => IsLoading = true);

            #region Export as Project

            try
            {
                //Serialize the current list of frames.
                Project.Persist();

                if (File.Exists(fileName))
                    File.Delete(fileName);

                var exportDir = Path.Combine(Project.FullPath, "Export");

                if (Directory.Exists(exportDir))
                    Directory.Delete(exportDir, true);

                var dir = Directory.CreateDirectory(exportDir);

                File.Copy(Project.ProjectPath, Path.Combine(exportDir, "Project.json"));

                var count = 0;
                foreach (var frameInfo in Project.Frames)
                {
                    File.Copy(frameInfo.Path, Path.Combine(dir.FullName, Path.GetFileName(frameInfo.Path)), true);
                    UpdateProgress(count++);
                }

                ZipFile.CreateFromDirectory(dir.FullName, fileName);

                Directory.Delete(dir.FullName, true);

                #region Copy

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var data = new DataObject();

                        switch (UserSettings.All.LatestCopyTypeProject)
                        {
                            case CopyType.File:
                                data.SetText(fileName, TextDataFormat.Text);
                                data.SetFileDropList(new StringCollection { fileName });
                                break;
                            case CopyType.FolderPath:
                                data.SetText(Path.GetDirectoryName(fileName) ?? fileName, TextDataFormat.Text);
                                break;
                            default:
                                data.SetText(fileName, TextDataFormat.Text);
                                break;
                        }

                        System.Windows.Clipboard.SetDataObject(data, true);
                    }
                    catch (Exception e)
                    {
                        LogWriter.Log(e, "It was not possible to copy the project.");
                    }
                });

                #endregion
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Exporting Recording as a Project");

                Dispatcher.Invoke(() => Dialog.Ok("Error While Saving", "Error while Saving as Project", ex.Message));
            }

            #endregion
        }

        private void SaveProjectCallback(IAsyncResult ar)
        {
            _saveProjectDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.Arrow;
                IsLoading = false;

                HideProgress();

                CommandManager.InvalidateRequerySuggested();
            });

            GC.Collect();
        }

        #endregion

        #region Async Discard

        private delegate void DiscardFrames(ProjectInfo project);

        private DiscardFrames _discardFramesDel;

        private void Discard(ProjectInfo project)
        {
            ShowProgress(DispatcherStringResource("Editor.DiscardingFrames"), project.Frames.Count);

            Dispatcher.Invoke(() => IsLoading = true);

            try
            {
                var count = 0;
                foreach (var frame in project.Frames)
                {
                    File.Delete(frame.Path);

                    UpdateProgress(count++);
                }

                var folderList = Directory.EnumerateDirectories(project.FullPath).ToList();

                ShowProgress(DispatcherStringResource("Editor.DiscardingFolders"), folderList.Count);

                count = 0;
                foreach (var folder in folderList)
                {
                    if (!folder.Contains("Encode "))
                        Directory.Delete(folder, true);

                    UpdateProgress(count++);
                }

                //Deletes the JSON file.
                if (File.Exists(project.ProjectPath))
                    File.Delete(project.ProjectPath);
            }
            catch (IOException io)
            {
                LogWriter.Log(io, "Error while trying to Discard the Project");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Dialog.Ok("Discard Error", "Error while trying to discard the project", ex.Message));
                LogWriter.Log(ex, "Error while trying to Discard the Project");
            }

            ActionStack.Clear();
            project.Clear();

            HideProgress();
        }

        private void DiscardCallback(IAsyncResult ar)
        {
            _discardFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                WelcomeGrid.BeginStoryboard(this.FindStoryboard("ShowWelcomeBorderStoryboard"), HandoffBehavior.Compose);

                FilledList = false;
                IsLoading = false;

                WelcomeTextBlock.Text = StringResource(Humanizer.WelcomeInfo());
                SymbolTextBlock.Text = Humanizer.Welcome();

                UpdateStatistics();

                FrameListView.SelectionChanged += FrameListView_SelectionChanged;

                CommandManager.InvalidateRequerySuggested();
            });

            GC.Collect();
        }

        private void DiscardAndLoadCallback(IAsyncResult ar)
        {
            _discardFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                FilledList = false;

                FrameListView.SelectionChanged += FrameListView_SelectionChanged;
            });

            _loadFramesDel = Load;
            _loadFramesDel.BeginInvoke(LoadCallback, null);

            GC.Collect();
        }

        #endregion

        #region Async Resize

        private delegate void ResizeFrames(int width, int height, double dpi);

        private ResizeFrames _resizeFramesDel;

        private void Resize(int width, int height, double dpi)
        {
            ShowProgress(DispatcherStringResource("Editor.ResizingFrames"), Project.Frames.Count);

            Dispatcher.Invoke(() => IsLoading = true);

            var count = 0;
            foreach (var frame in Project.Frames)
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(ImageMethods.ResizeImage((BitmapImage)frame.Path.SourceFrom(), width, height, 0, dpi));

                using (Stream stm = File.OpenWrite(frame.Path))
                    png.Save(stm);

                UpdateProgress(count++);
            }
        }

        private void ResizeCallback(IAsyncResult ar)
        {
            _resizeFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(0, Project.Frames.Count - 1);
            });
        }

        #endregion

        #region Async Crop

        private delegate void CropFrames(Int32Rect rect);

        private CropFrames _cropFramesDel;

        private void Crop(Int32Rect rect)
        {
            ShowProgress(DispatcherStringResource("Editor.CroppingFrames"), Project.Frames.Count);

            Dispatcher.Invoke(() => IsLoading = true);

            var count = 0;
            foreach (var frame in Project.Frames)
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(frame.Path.CropFrom(rect)));

                using (Stream stm = File.OpenWrite(frame.Path))
                    png.Save(stm);

                UpdateProgress(count++);
            }
        }

        private void CropCallback(IAsyncResult ar)
        {
            _cropFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(0, Project.Frames.Count - 1);
            });
        }

        #endregion

        #region Async Progress

        private delegate void Progress();

        private Progress _progressDel;

        private void ProgressAsync()
        {
            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), Project.Frames.Count);

            var total = Project.Frames.Sum(y => y.Delay);

            var count = 0;
            foreach (var frame in Project.Frames)
            {
                var image = frame.Path.SourceFrom();

                var render = Dispatcher.Invoke(() =>
                {
                    if (UserSettings.All.ProgressType == ProgressType.Bar)
                    {
                        #region Bar

                        //Set the size of the bar as the percentage of the total size: Current/Total * Available size
                        ProgressHorizontalRectangle.Width = count / (double)Project.Frames.Count * ProgressOverlayGrid.RenderSize.Width;
                        ProgressVerticalRectangle.Height = count / (double)Project.Frames.Count * ProgressOverlayGrid.RenderSize.Height;

                        //Assures that the UIElement is up to the changes.
                        ProgressHorizontalRectangle.Arrange(new Rect(ProgressOverlayGrid.RenderSize));
                        ProgressVerticalRectangle.Arrange(new Rect(ProgressOverlayGrid.RenderSize));

                        //Renders the current Visual.
                        return ProgressOverlayGrid.GetScaledRender(ZoomBoxControl.ScaleDiff, ZoomBoxControl.ImageDpi, ZoomBoxControl.GetImageSize());

                        #endregion
                    }

                    #region Text

                    //Calculates the cumulative total milliseconds.
                    var cumulative = 0L;

                    for (var j = 0; j < count; j++)
                        cumulative += Project.Frames[j].Delay;

                    //Type of the representation.
                    ChangeProgressText(cumulative, total, count);

                    //Assures that the UIElement is up to the changes.
                    ProgressHorizontalTextBlock.Arrange(new Rect(ProgressOverlayGrid.RenderSize));

                    //Renders the current Visual.
                    return ProgressOverlayGrid.GetScaledRender(ZoomBoxControl.ScaleDiff, ZoomBoxControl.ImageDpi, ZoomBoxControl.GetImageSize());

                    #endregion
                });


                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height));
                    drawingContext.DrawImage(render, new Rect(0, 0, render.Width, render.Height));
                }

                // Converts the Visual (DrawingVisual) into a BitmapSource
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, render.DpiX, render.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                // Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                // Saves the image into a file using the encoder
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                UpdateProgress(count++);
            }
        }

        private void ProgressCallback(IAsyncResult ar)
        {
            _progressDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(0, Project.Frames.Count - 1);
            });
        }

        #endregion

        #region Async Merge Frames

        private delegate List<int> OverlayFrames(RenderTargetBitmap render, double dpi, bool forAll = false);

        private OverlayFrames _overlayFramesDel;

        private List<int> OverlayAsync(RenderTargetBitmap render, double dpi, bool forAll = false)
        {
            var frameList = forAll ? Project.Frames : SelectedFrames();
            var selectedList = Dispatcher.Invoke(() =>
            {
                IsLoading = true;

                return forAll ? Project.Frames.Select(x => Project.Frames.IndexOf(x)).ToList() : SelectedFramesIndex();
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), frameList.Count);

            var count = 0;
            foreach (var frame in frameList)
            {
                var image = frame.Path.SourceFrom();

                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height));
                    drawingContext.DrawImage(render, new Rect(0, 0, render.Width, render.Height));
                }

                //Converts the Visual (DrawingVisual) into a BitmapSource.
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, dpi, dpi, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder.
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                //Saves the image into a file using the encoder.
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                UpdateProgress(count++);
            }

            return selectedList;
        }

        private void OverlayCallback(IAsyncResult ar)
        {
            var selected = _overlayFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                ShowHint("Hint.Overlay");

                LoadSelectedStarter(selected.Min(), selected.Max());
            });
        }


        //Currently not being used.
        private delegate List<int> OverlayMultipleFrames(List<RenderTargetBitmap> render, double dpi, bool forAll = false);

        private OverlayMultipleFrames _overlayMultipleFramesDel;

        private List<int> OverlayMultiple(List<RenderTargetBitmap> renderList, double dpi, bool forAll = false)
        {
            var frameList = forAll ? Project.Frames : SelectedFrames();
            var selectedList = Dispatcher.Invoke(() =>
            {
                IsLoading = true;

                return forAll ? Project.Frames.Select(x => Project.Frames.IndexOf(x)).ToList() : SelectedFramesIndex();
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), frameList.Count);

            var count = 0;
            foreach (var frame in frameList)
            {
                var image = frame.Path.SourceFrom();

                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height));
                    drawingContext.DrawImage(renderList[count], new Rect(0, 0, renderList[count].Width, renderList[count].Height));
                }

                // Converts the Visual (DrawingVisual) into a BitmapSource
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, dpi, dpi, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                // Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                // Saves the image into a file using the encoder
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                UpdateProgress(count++);
            }

            return selectedList;
        }

        private void OverlayMultipleCallback(IAsyncResult ar)
        {
            var selected = _overlayMultipleFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                ShowHint("Hint.Overlay");

                LoadSelectedStarter(selected.Min(), selected.Max());
            });
        }

        #endregion

        #region Async Title Frame

        private delegate int TitleFrameAction(RenderTargetBitmap render, int selected, double dpi);

        private TitleFrameAction _titleFrameDel;

        private int TitleFrame(RenderTargetBitmap render, int selected, double dpi)
        {
            ShowProgress(DispatcherStringResource("Editor.CreatingTitleFrame"), 1, true);

            Dispatcher.Invoke(() => IsLoading = true);

            #region Save Image

            var name = Path.GetFileNameWithoutExtension(Project.Frames[selected].Path);
            var folder = Path.GetDirectoryName(Project.Frames[selected].Path);
            var fileName = Path.Combine(folder, $"{name} TF {DateTime.Now:hh-mm-ss}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(render));

            //Saves the image into a file using the encoder.
            using (Stream stream = File.Create(fileName))
                encoder.Save(stream);

            GC.Collect();

            #endregion

            //Adds to the List
            Project.Frames.Insert(selected, new FrameInfo(fileName, UserSettings.All.TitleFrameDelay));

            return selected;
        }

        private void TitleFrameCallback(IAsyncResult ar)
        {
            var selected = _titleFrameDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                ShowHint("Hint.TitleFrame");

                LoadSelectedStarter(selected, Project.Frames.Count - 1);
            });
        }

        #endregion

        #region Async Keystrokes

        private delegate void KeyStrokesDelegate(KeyStrokesModel model);

        private KeyStrokesDelegate _keyStrokesDelegate;

        private void KeyStrokesAsync(KeyStrokesModel model)
        {
            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
                KeyStrokesInternalGrid.MinHeight = model.KeyStrokesFontFamily.LineSpacing * model.KeyStrokesFontSize + model.KeyStrokesMargin * 2;
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), Project.Frames.Count);

            var auxList = Project.Frames.CopyList();

            #region Make the keystrokes start earlier

            if (model.KeyStrokesEarlier)
            {
                //Checks if there is a key stroke that needs to be shown earlier, into the previous frames.
                for (var outer = 0; outer < auxList.Count; outer++)
                {
                    //For each frame, check if any other key pressed later wants to be shown on this frame.

                    var amount = 0;

                    //If it's the last item of the list.
                    //if (outer == auxList.Count - 1 && amount <= model.KeyStrokesEarlierBy)
                    //    auxList[outer].KeyList.InsertRange(auxList[outer].KeyList.Count, auxList[0].KeyList);

                    //Check next itens.
                    for (var inner = outer + 1; inner < auxList.Count - 1; inner++)
                    {
                        //For each frame, check if a next frame needs to show their key strokes.
                        amount += auxList[inner].Delay;

                        //If next item bleeds into this frame, insert on the list.
                        if (inner < auxList.Count - 1 && auxList[inner + 1].Delay <= model.KeyStrokesEarlierBy)
                            auxList[outer].KeyList.InsertRange(auxList[outer].KeyList.Count, auxList[inner].KeyList);

                        //Stops veryfying the previous frames if the delay sum is greater than the maximum.
                        if (amount >= model.KeyStrokesEarlierBy)
                            break;
                    }
                }
            }

            #endregion

            #region Expand the keystrokes

            if (model.KeyStrokesExtended)
            {
                //Checks from the end to the start, if there is a key stroke that needs to 'bleed'/'delayed' into the next frames.
                //I need to check from the end to the start so the keystrokes stays in position. 
                for (var outer = auxList.Count - 1; outer > 0; outer--)
                {
                    var amount = 0;

                    if (outer == 1 && amount <= model.KeyStrokesDelay)
                    {
                        var listA = auxList[0].KeyList.TakeWhile((_, i) => !auxList[0].KeyList.Skip(i).SequenceEqual(auxList[1].KeyList.Take(auxList[0].KeyList.Count - i))).Concat(auxList[1].KeyList);

                        auxList[1].KeyList = new List<SimpleKeyGesture>(listA);
                    }

                    //Check previous itens.
                    for (var inner = outer - 1; inner >= 0; inner--)
                    {
                        //For each frame, check if a previous frame needs to show their key strokes.
                        amount += auxList[inner].Delay;

                        //If previous item bleeds into this frame, insert on the list.
                        if (inner > 0 && auxList[inner - 1].Delay <= model.KeyStrokesDelay)
                        {
                            var listA = auxList[inner].KeyList.TakeWhile((_, i) => !auxList[inner].KeyList.Skip(i).SequenceEqual(auxList[outer].KeyList.Take(auxList[inner].KeyList.Count - i))).Concat(auxList[outer].KeyList);

                            auxList[outer].KeyList = new List<SimpleKeyGesture>(listA);
                        }

                        //Stops veryfying the previous frames if the delay sum is greater than the maximum.
                        if (amount >= model.KeyStrokesDelay)
                            break;
                    }
                }
            }

            #endregion

            var count = 0;
            foreach (var frame in auxList)
            {
                if (!frame.KeyList.Any())
                {
                    UpdateProgress(count++);
                    continue;
                }

                var image = frame.Path.SourceFrom();

                var render = Dispatcher.Invoke(() =>
                {
                    #region Removes any duplicated modifier key

                    var keyList = new List<SimpleKeyGesture>();
                    for (var i = 0; i < frame.KeyList.Count; i++)
                    {
                        //Ignore Control, Shift, Alt and Windows keys if not acting as modifiers.
                        if (model.KeyStrokesIgnoreNonModifiers && (frame.KeyList[i].Key >= Key.LeftShift && frame.KeyList[i].Key <= Key.RightAlt || frame.KeyList[i].Key == Key.LWin || frame.KeyList[i].Key == Key.RWin))
                            continue;

                        //If there's another key ahead on the same frame.
                        if (frame.KeyList.Count > i + 1)
                        {
                            //If this frame being added will be repeated next, ignore.
                            if (frame.KeyList[i + 1].Key == frame.KeyList[i].Key && frame.KeyList[i + 1].Modifiers == frame.KeyList[i].Modifiers)
                                continue;

                            //TODO: If there's a key between the current key and the one that is repeated, they are going to be shown.

                            //If this frame being added will be repeated within the next key presses as a modifier, ignore.
                            if ((frame.KeyList[i].Key == Key.LeftCtrl || frame.KeyList[i].Key == Key.RightCtrl) && (frame.KeyList[i + 1].Modifiers & ModifierKeys.Control) != 0)
                                continue;

                            if ((frame.KeyList[i].Key == Key.LeftShift || frame.KeyList[i].Key == Key.RightShift) && (frame.KeyList[i + 1].Modifiers & ModifierKeys.Shift) != 0)
                                continue;

                            if ((frame.KeyList[i].Key == Key.LeftAlt || frame.KeyList[i].Key == Key.RightAlt) && (frame.KeyList[i + 1].Modifiers & ModifierKeys.Alt) != 0)
                                continue;

                            if ((frame.KeyList[i].Key == Key.LWin || frame.KeyList[i].Key == Key.RWin) && (frame.KeyList[i + 1].Modifiers & ModifierKeys.Windows) != 0)
                                continue;
                        }

                        //Removes the previous modifier key, if a combination is next to it: "LeftCtrl Control + A" will be "Control + A". (This checks if the next modifier is not present as a current key).
                        if (i + 1 > frame.KeyList.Count - 1 || !(frame.KeyList[i].Key != Key.Left && frame.KeyList[i].Key != Key.Right && frame.KeyList[i + 1].Modifiers.ToString().Contains(frame.KeyList[i].Key.ToString().Remove("Left", "Right").Replace("Ctrl", "Control").TrimStart('L').TrimStart('R'))))
                            keyList.Add(frame.KeyList[i]);
                    }
                    
                    #endregion

                    if (keyList.Count == 0)
                        return null;

                    //Update text with key strokes.
                    KeyStrokesLabel.Text = keyList.Select(x => "" + Native.GetSelectKeyText(x.Key, x.Modifiers, x.IsUppercase)).Aggregate((p, n) => p + model.KeyStrokesSeparator + n);
                    KeyStrokesLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    KeyStrokesLabel.Arrange(new Rect(KeyStrokesLabel.DesiredSize));
                    KeyStrokesLabel.UpdateLayout();

                    //Renders the current Visual.
                    KeyStrokesOverlayGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    KeyStrokesOverlayGrid.Arrange(new Rect(KeyStrokesOverlayGrid.DesiredSize));
                    KeyStrokesOverlayGrid.UpdateLayout();

                    return KeyStrokesOverlayGrid.GetScaledRender(ZoomBoxControl.ScaleDiff, ZoomBoxControl.ImageDpi, ZoomBoxControl.GetImageSize());

                    
                }, DispatcherPriority.Normal);

                if (render == null)
                {
                    UpdateProgress(count++);
                    continue;
                }

                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height));
                    drawingContext.DrawImage(render, new Rect(0, 0, render.Width, render.Height));
                }

                //Converts the Visual (DrawingVisual) into a BitmapSource.
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, render.DpiX, render.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder.
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                //Saves the image into a file using the encoder.
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                //GC.Collect(1);
                GC.WaitForPendingFinalizers();
                GC.Collect(1);

                UpdateProgress(count++);
            }
        }

        private void KeyStrokesCallback(IAsyncResult ar)
        {
            _keyStrokesDelegate.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(0, Project.Frames.Count - 1);
            });
        }

        #endregion

        #region Async Reduce Frames

        private delegate void ReduceFrame(int factor, int removeCount);

        private ReduceFrame _reduceFrameDel;

        private void ReduceFrameCount(int factor, int removeCount)
        {
            var removeList = new List<int>();

            //Gets the list of frames to be removed.
            for (var i = factor - 1; i < Project.Frames.Count - 1; i += factor + removeCount)
                removeList.AddRange(Util.Other.CreateIndexList2(i + 1, removeCount));

            //Only allow removing frames within the possible range.
            removeList = removeList.Where(x => x < Project.Frames.Count).ToList();

            var alterList = (from item in removeList where item - 1 >= 0 select item - 1).ToList(); //.Union(removeList)

            ActionStack.SaveState(ActionStack.EditAction.RemoveAndAlter, Project.Frames, removeList, alterList);

            for (var i = removeList.Count - 1; i >= 0; i--)
            {
                var removeIndex = removeList[i];

                Project.Frames[removeIndex - 1].Delay += Project.Frames[removeIndex].Delay;

                File.Delete(Project.Frames[removeIndex].Path);
                Project.Frames.RemoveAt(removeIndex);
            }
        }

        private void ReduceFrameCountCallback(IAsyncResult ar)
        {
            _reduceFrameDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                for (var i = FrameListView.Items.Count - 1; i >= Project.Frames.Count; i--)
                    FrameListView.Items.RemoveAt(i);

                SelectNear(LastSelected);
                Project.Persist();

                LoadSelectedStarter(ReduceFactorIntegerUpDown.Value - 1, Project.Frames.Count - 1);

                ShowHint("Hint.Reduce");
            });
        }

        #endregion

        #region Async Delay

        private delegate void DelayFrames(DelayChangeType type, int delay);

        private DelayFrames _delayFramesDel;

        private void Delay(DelayChangeType type, int delay)
        {
            var frameList = SelectedFrames();

            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
                Cursor = Cursors.AppStarting;
            });

            ShowProgress(DispatcherStringResource("Editor.ChangingDelay"), frameList.Count);

            var count = 0;
            foreach (var frameInfo in frameList)
            {
                if (type == DelayChangeType.Override)
                {
                    frameInfo.Delay = delay;
                }
                else if (type == DelayChangeType.IncreaseDecrease)
                {
                    frameInfo.Delay += delay;

                    if (frameInfo.Delay < 10)
                        frameInfo.Delay = 10;
                }
                else
                {
                    frameInfo.Delay = frameInfo.Delay * delay / 100;

                    if (frameInfo.Delay < 10)
                        frameInfo.Delay = 10;
                }

                #region Update UI

                var index = Project.Frames.IndexOf(frameInfo);
                Dispatcher.Invoke(() => ((FrameListBoxItem)FrameListView.Items[index]).Delay = frameInfo.Delay);

                #endregion

                UpdateProgress(count++);
            }
        }

        private void DelayCallback(IAsyncResult ar)
        {
            _delayFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.Arrow;

                UpdateStatistics();
                HideProgress();
                IsLoading = false;

                ShowHint("Hint.Delay");

                CommandManager.InvalidateRequerySuggested();
                SetFocusOnCurrentFrame();
            });
        }

        #endregion

        #region Async Transitions

        private delegate int Transition(int selected, int frameCount, object optional);

        private Transition _transitionDel;

        private int Fade(int selected, int frameCount, object optional)
        {
            ShowProgress(DispatcherStringResource("Editor.ApplyingTransition"), Project.Frames.Count - selected + frameCount);

            Dispatcher.Invoke(() => IsLoading = true);

            //Calculate opacity increment.
            var increment = 1F / (frameCount + 1);
            var previousName = Path.GetFileNameWithoutExtension(Project.Frames[selected].Path);
            var previousFolder = Path.GetDirectoryName(Project.Frames[selected].Path);

            #region Images

            //var size = Dispatcher.Invoke(() => FrameSize);
            var dpi = Dispatcher.Invoke(this.Dpi);

            var previousImage = Project.Frames[selected].Path.SourceFrom();
            var nextImage = UserSettings.All.FadeToType == FadeToType.NextFrame ? Project.Frames[Project.Frames.Count - 1 == selected ? 0 : selected + 1].Path.SourceFrom() :
                ImageMethods.CreateEmtpyBitmapSource(UserSettings.All.FadeToColor, previousImage.PixelWidth, previousImage.PixelHeight, dpi, PixelFormats.Indexed1);

            var nextBrush = new ImageBrush
            {
                ImageSource = nextImage,
                Stretch = Stretch.Uniform,
                TileMode = TileMode.None,
                Opacity = increment,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            #endregion

            #region Creates and Save each Transition Frame

            for (var index = 0; index < frameCount; index++)
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(previousImage, new Rect(0, 0, previousImage.Width, previousImage.Height));
                    drawingContext.DrawRectangle(nextBrush, null, new Rect(0, 0, nextImage.Width, nextImage.Height));
                }

                // Converts the Visual (DrawingVisual) into a BitmapSource
                var bmp = new RenderTargetBitmap(previousImage.PixelWidth, previousImage.PixelHeight, previousImage.DpiX, previousImage.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Increase the opacity for the next frame.
                nextBrush.Opacity += increment;

                //TODO: Fix filenaming.
                //TODO: This transition doesn't preserve any other frame info, such as keys pressed and mouse clicks.
                var fileName = Path.Combine(previousFolder, $"{previousName} T {index} {DateTime.Now:hh-mm-ss fff}.png");
                Project.Frames.Insert(selected + index + 1, new FrameInfo(fileName, UserSettings.All.FadeTransitionDelay));

                // Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                // Saves the image into a file using the encoder
                using (Stream stream = File.Create(fileName))
                    encoder.Save(stream);

                UpdateProgress(index);
            }

            #endregion

            return selected;
        }

        private int Slide(int selected, int frameCount, object optional)
        {
            ShowProgress(DispatcherStringResource("Editor.ApplyingTransition"), Project.Frames.Count - selected + frameCount);

            Dispatcher.Invoke(() => IsLoading = true);

            var previousName = Path.GetFileNameWithoutExtension(Project.Frames[selected].Path);
            var previousFolder = Path.GetDirectoryName(Project.Frames[selected].Path);

            #region Images

            var previousImage = Project.Frames[selected].Path.SourceFrom();
            var nextImage = Project.Frames[(Project.Frames.Count - 1) == selected ? 0 : selected + 1].Path.SourceFrom();

            var nextBrush = new ImageBrush
            {
                ImageSource = nextImage,
                Stretch = Stretch.Uniform,
                TileMode = TileMode.None,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            #endregion

            //Calculate Translate Transform increment.
            var increment = previousImage.Width / (frameCount + 1);
            var transf = increment;

            //Calculate the Opacity increment.
            var alphaIncrement = 1F / (frameCount + 1);
            nextBrush.Opacity = alphaIncrement;

            #region Creates and Save each Transition Frame

            for (var index = 0; index < frameCount; index++)
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(previousImage, new Rect(0, 0, previousImage.Width, previousImage.Height));
                    drawingContext.DrawRectangle(nextBrush, null, new Rect(previousImage.Width - transf, 0, nextImage.Width, nextImage.Height));
                }

                // Converts the Visual (DrawingVisual) into a BitmapSource
                var bmp = new RenderTargetBitmap(previousImage.PixelWidth, previousImage.PixelHeight, previousImage.DpiX, previousImage.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Increase the translation and opacity for the next frame.
                transf += increment;
                nextBrush.Opacity += alphaIncrement;

                //TODO: Fix filenaming.
                //TODO: This transition doesn't preserve any other frame info, such as keys pressed and mouse clicks.
                var fileName = Path.Combine(previousFolder, $"{previousName} T {index} {DateTime.Now:hh-mm-ss fff}.png");
                Project.Frames.Insert(selected + index + 1, new FrameInfo(fileName, UserSettings.All.FadeTransitionDelay));

                // Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                // Saves the image into a file using the encoder
                using (Stream stream = File.Create(fileName))
                    encoder.Save(stream);

                UpdateProgress(index);
            }

            #endregion

            return selected;
        }

        private void TransitionCallback(IAsyncResult ar)
        {
            var selected = _transitionDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(selected, Project.Frames.Count - 1);

                ShowHint("Hint.Transition");
            });
        }

        #endregion

        #region Async Obfuscate

        private delegate List<int> ObfuscateFrames(Rect rect, int pixelSize, bool useMedian, double dpi, double screenDpi, bool forAll = false);

        private ObfuscateFrames _obfuscateFramesDel;

        private List<int> ObfuscateAsync(Rect rect, int pixelSize, bool useMedian, double dpi, double screenScale, bool forAll = false)
        {
            var frameList = forAll ? Project.Frames : SelectedFrames();
            var selectedList = Dispatcher.Invoke(() =>
            {
                IsLoading = true;

                return forAll ? Project.Frames.Select(x => Project.Frames.IndexOf(x)).ToList() : SelectedFramesIndex();
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), frameList.Count);

            var size = frameList[0].Path.ScaledSize();

            rect = rect.Scale(screenScale).Limit(size.Width, size.Height);

            var count = 0;
            foreach (var frame in frameList)
            {
                var image = frame.Path.SourceFrom();
                var render = ImageMethods.Pixelate(image, new Int32Rect((int)(rect.X + 1d * screenScale), (int)(rect.Y + 1 * screenScale),
                    (int)(rect.Width - 1 * screenScale), (int)(rect.Height - 1 * screenScale)), pixelSize, useMedian);

                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height));
                    drawingContext.DrawImage(render, new Rect((rect.X + 1d) / screenScale * ZoomBoxControl.ScaleDiff, (rect.Y + 1d) / screenScale * ZoomBoxControl.ScaleDiff, render.Width, render.Height));
                }

                //Converts the Visual (DrawingVisual) into a BitmapSource.
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, dpi, dpi, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder.
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                //Saves the image into a file using the encoder.
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                UpdateProgress(count++);
            }

            return selectedList;
        }

        private void ObfuscateCallback(IAsyncResult ar)
        {
            var selected = _obfuscateFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                ShowHint("Hint.Overlay");

                LoadSelectedStarter(selected.Min(), selected.Max());
            });
        }

        #endregion

        #region Async Mouse Clicks

        private delegate void MouseClicksDelegate(MouseClicksModel model);

        private MouseClicksDelegate _mouseClicksDelegate;

        private void MouseClicksAsync(MouseClicksModel model)
        {
            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), Project.Frames.Count);

            var auxList = Project.Frames.CopyList();

            var count = 0;
            foreach (var frame in auxList)
            {
                if (!frame.WasClicked)
                {
                    UpdateProgress(count++);
                    continue;
                }

                var image = frame.Path.SourceFrom();
                var scale = Math.Round(image.DpiX / 96d, 2);

                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(image, new Rect(0, 0, image.Width, image.Height)); // - UserSettings.All.MouseClicksWidth/2d   // - UserSettings.All.MouseClicksHeight/2d
                    drawingContext.DrawEllipse(new SolidColorBrush(model.ForegroundColor), null, new System.Windows.Point(frame.CursorX / scale, frame.CursorY / scale), model.Width, model.Height);
                }

                //KeyStrokesOverlayGrid.GetScaledRender(ZoomBoxControl.ScaleDiff, ZoomBoxControl.ImageDpi, ZoomBoxControl.GetImageSize());

                //Converts the Visual (DrawingVisual) into a BitmapSource.
                var bmp = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, image.DpiX, image.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //Creates a PngBitmapEncoder and adds the BitmapSource to the frames of the encoder.
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                //Saves the image into a file using the encoder.
                using (Stream stream = File.Create(frame.Path))
                    encoder.Save(stream);

                GC.WaitForPendingFinalizers();
                GC.Collect(1);

                UpdateProgress(count++);
            }
        }

        private void MouseClicksCallback(IAsyncResult ar)
        {
            _mouseClicksDelegate.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {
                LoadSelectedStarter(0, Project.Frames.Count - 1);
            });
        }

        #endregion

        #endregion

        public void NotificationUpdated()
        {
            //RibbonTabControl.UpdateNotifications();
        }
    }
}
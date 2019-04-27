using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenToGif.Controls;
using ScreenToGif.ImageUtil;
using ScreenToGif.Model;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;
using Cursors = System.Windows.Input.Cursors;
using Encoder = ScreenToGif.Windows.Other.Encoder;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenToGif.Windows
{
    public partial class Editor : Window, INotification
    {
        public ActionStack Stack
        {
            get => _stack;
            set
            {
                if (_stack != null)
                {
                    throw new InvalidOperationException("This shouldn't happen");
                }

                _stack = value;
            }
        }

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

        #endregion

        #region Variables

        /// <summary>
        /// Last selected frame index. Used to track users last selection and decide which frame to show.
        /// </summary>
        private int _lastSelected = -1;

        /// <summary>
        /// True if the user was selecting frames using the FirstFrame/Previous/Next/LastFrame commands or the scroll wheel.
        /// </summary>
        private bool _wasChangingSelection;

        /// <summary>
        /// True if the user was previewing the recording.
        /// </summary>
        private bool _wasPreviewing;

        /// <summary>
        /// True if the PC is sleeping.
        /// </summary>
        private bool _slept;

        private readonly System.Windows.Forms.Timer _timerPreview = new System.Windows.Forms.Timer();

        private Action<object, RoutedEventArgs> _applyAction = null;

        #endregion

        public Editor(ActionStack projectStack)
        {
            Stack = projectStack;

            InitializeComponent();

            //TODO: decide whether this is the best or not
            WindowState = WindowState.Maximized;
            Cursor = Cursors.AppStarting;
            IsLoading = true;
        }

        #region Main Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.PowerModeChanged += System_PowerModeChanged;

            ScrollSynchronizer.SetScrollGroup(ZoomBoxControl.GetScrollViewer(), "Canvas");
            ScrollSynchronizer.SetScrollGroup(MainScrollViewer, "Canvas");

            //If never configurated.
            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
            {
                UserSettings.All.TemporaryFolder = Path.GetTempPath();
            }

            Cursor = Cursors.Arrow;
            IsLoading = false;



            Dispatcher.Invoke(delegate
            {
                Cursor = Cursors.Arrow;
                IsLoading = false;

                if (Stack.Project.AnyFrames)
                    FilledList = true;

                FrameListView.SelectedIndex = -1;
                FrameListView.SelectedIndex = 0;
                ZoomBoxControl.PixelSize = Stack.Project.Frames[0].FullPath.ScaledSize();
                ZoomBoxControl.ImageScale = Stack.Project.Frames[0].FullPath.ScaleOf();
                ZoomBoxControl.RefreshImage();

                HideProgress();

                CommandManager.InvalidateRequerySuggested();

                SetFocusOnCurrentFrame();
            });
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (_wasPreviewing)
            {
                _wasPreviewing = false;
                PlaybackPlayPause();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_timerPreview.Enabled)
            {
                _wasPreviewing = true;
                PlaybackPause();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.SystemKey == Key.LeftAlt)
                e.Handled = true;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            PlaybackPause();

            Stack?.Dispose();

            Encoder.TryClose();

            SystemEvents.PowerModeChanged -= System_PowerModeChanged;
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

            _wasChangingSelection = true;

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
                _slept = true;
                PlaybackPause();
                return;
            }

            _slept = false;
        }

        #endregion

        #region New

        private void NewRecording_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsLoading
                && !e.Handled
                && Application.Current.Windows.OfType<Window>().All(a => !(a is RecorderWindow));
        }

        private void NewRecording_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            PlaybackPause();
            Encoder.TryClose();

            // TODO: let App know that a new recording should be created.
            Close();
        }
        #endregion

        #region Save

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack?.Project.AnyFrames == true && !IsLoading && !e.Handled;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            // TODO: save
        }

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusList.Remove(StatusType.Warning);

            // TODO: save
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

                                File.Copy(FrameListView.Items.OfType<FrameInfo>().ToList()[index].Image, fileName);
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

                            foreach (var frame in FrameListView.SelectedItems.OfType<FrameInfo>())
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

        #endregion

        #region Action Stack

        private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack.CanUndo() && !IsLoading && !e.Handled;
        }

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            Stack.Undo();

            ShowHint("Hint.Undo");
        }

        private void Reset_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack.CanReset() && !IsLoading && !e.Handled;
        }

        private void Reset_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            Stack.Reset();

            ShowHint("Hint.Reset");
        }

        private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack.CanRedo() && !IsLoading && !e.Handled;
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            Stack.Redo();

            ShowHint("Hint.Redo");
        }

        #endregion

        #region Zoom

        private void Zoom_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack?.Project.AnyFrames == true && !IsLoading && !OverlayGrid.IsVisible && FrameListView.SelectedIndex != -1;
        }

        private void Zoom100_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ZoomBoxControl.Zoom = 1.0;

            ShowHint("Hint.Zoom", false, 100);
        }

        #endregion

        #region Playback Tab

        private void Playback_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Stack?.Project.AnyFrames == true && !IsLoading && _applyAction == null;
        }

        private void FirstFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            _wasChangingSelection = true;
            FrameListView.SelectedIndex = 0;
        }

        private void PreviousFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            _wasChangingSelection = true;

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
            PlaybackPlayPause();
        }

        private void NextFrame_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();

            _wasChangingSelection = true;

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
            PlaybackPause();

            _wasChangingSelection = true;
            FrameListView.SelectedIndex = FrameListView.Items.Count - 1;
        }

        #endregion

        #region Other Events

        private void FrameListView_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                PlaybackPlayPause();

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

        private void TimerPreview_Tick(object sender, EventArgs e)
        {
            _timerPreview.Tick -= TimerPreview_Tick;

            if (Stack.Project.Frames.Count - 1 == FrameListView.SelectedIndex)
                FrameListView.SelectedIndex = 0;
            else
                FrameListView.SelectedIndex++;

            //Sets the interval for this frame. If this frame has 500ms, the next frame will take 500ms to show.
            _timerPreview.Interval = Stack.Project.Frames[FrameListView.SelectedIndex].Delay;
            _timerPreview.Tick += TimerPreview_Tick;

            GC.Collect(2);
        }


        #endregion

        #region Methods

        private void DisposeStack(ActionStack oldStack)
        {

            // TODO: delete files & async
            oldStack.Dispose();

            PlaybackPause();

            FrameListView.SelectedIndex = -1;

            FrameListView.Items.Clear();
            ZoomBoxControl.Clear();
        }


        #region Playback

        private void PlaybackPlayPause()
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

                if (Stack.Project.Frames.Count - 1 == FrameListView.SelectedIndex)
                {
                    FrameListView.SelectedIndex = 0;
                }
                else
                {
                    FrameListView.SelectedIndex++;
                }

                #endregion

                _timerPreview.Interval = Stack.Project.Frames[FrameListView.SelectedIndex].Delay;
                _timerPreview.Tick += TimerPreview_Tick;
                _timerPreview.Start();
            }
        }

        private void PlaybackPause()
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

        #endregion

        #region Other

        private List<FrameInfo> SelectedFrames()
        {
            return FrameListView.SelectedItems.OfType<FrameInfo>().ToList();
        }

        private string StringResource(string key)
        {
            return FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " ");
        }

        private string DispatcherStringResource(string key)
        {
            return Dispatcher.Invoke(() => FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " "));
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

            // TODO: scroll into view?
        }

        private string GetOutputFilename()
        {
            return DateTime.Now.ToString("yyyy MMM dd HH-mm-ss");
        }

        private string GetOutputFilenameNoRegExp(ref string name)
        {
            //put datetime into filename which is saved between two questions marks
            string dateTimeFileNameRegEx = @"[?]([dyhms]+[-_ ]*)+[?]";
            if (Regex.IsMatch(name, dateTimeFileNameRegEx, RegexOptions.IgnoreCase))
            {
                var dateTimeRegExp = Regex.Match(name, dateTimeFileNameRegEx, RegexOptions.IgnoreCase);
                var dateTimeConverted = DateTime.Now.ToString(Regex.Replace(dateTimeRegExp.Value, "[?]", ""));
                name = name.Replace(dateTimeRegExp.ToString(), dateTimeConverted);
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #endregion

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
            PlaybackPause();

            try
            {
                var selected = FrameListView.SelectedItems.OfType<FrameInfo>().ToList();

                Stack.Remove(SelectedFrames());

                // SelectNear(selectedOrdered.Last().FrameNumber);

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
            PlaybackPause();

            var count = FrameListView.SelectedIndex;

            Stack.Remove(Stack.Project.Frames.Take(count).ToList());

            SelectNear(0);

            ShowHint("Hint.DeleteFrames", false, count);
        }

        private void DeleteNext_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PlaybackPause();


            var count = FrameListView.Items.Count - FrameListView.SelectedIndex - 1;

            Stack.Remove(Stack.Project.Frames.Skip(FrameListView.SelectedIndex).ToList());

            SelectNear(FrameListView.Items.Count - 1);

            ShowHint("Hint.DeleteFrames", false, count);
        }

        #endregion

        #region Async

        #region Async Save

        private delegate bool SaveDelegate(Parameters param, double scale, bool projectToo);

        private SaveDelegate _saveDel;
        private ActionStack _stack;

        private void Save(Parameters param, double scale)
        {
            ShowProgress(DispatcherStringResource("S.Editor.PreparingSaving"), Stack.Project.Frames.Count, true);

            Dispatcher.Invoke(() => IsLoading = true);

            try
            {
                Dispatcher.Invoke(() => Encoder.AddItem(Stack.Project, param, scale));
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Preparing to save the recording");

                Dispatcher.Invoke(() => Dialog.Ok("Error While Saving", "Error while preparing to save", ex.Message));
            }

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

        #region Async Discard

        /*
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
                    UpdateProgress(count++);
                }

                var folderList = Directory.EnumerateDirectories(project.FullPathOfProject).ToList();

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

            ActionStack.Dispose();
            project.Clear();

            HideProgress();

            FilledList = false;
            IsLoading = false;

            FrameListView.SelectionChanged += FrameListView_SelectionChanged;

            CommandManager.InvalidateRequerySuggested();
        }

        private void DiscardCallback(IAsyncResult ar)
        {
            _discardFramesDel.EndInvoke(ar);

            Dispatcher.Invoke(() =>
            {

            });

            GC.Collect();
        }
        */

        #endregion

        #region Async Progress

        private delegate void ProgressDelegate();

        private void Progress()
        {
            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
            });

            ShowProgress(DispatcherStringResource("Editor.ApplyingOverlay"), Stack.Project.Frames.Count);

            var total = Stack.Project.Frames.Sum(y => y.Delay);

            var count = 0;
            foreach (var frame in Stack.Project.Frames)
            {
                var image = frame.FullPath.SourceFrom();

                var render = Dispatcher.Invoke(() =>
                {
                    //Set the size of the bar as the percentage of the total size: Current/Total * Available size
                    ProgressHorizontalRectangle.Width = count / (double)Stack.Project.Frames.Count * ProgressOverlayGrid.RenderSize.Width;
                    ProgressVerticalRectangle.Height = count / (double)Stack.Project.Frames.Count * ProgressOverlayGrid.RenderSize.Height;

                    //Assures that the UIElement is up to the changes.
                    ProgressHorizontalRectangle.Arrange(new Rect(ProgressOverlayGrid.RenderSize));
                    ProgressVerticalRectangle.Arrange(new Rect(ProgressOverlayGrid.RenderSize));

                    //Renders the current Visual.
                    return ProgressOverlayGrid.GetScaledRender(ZoomBoxControl.ScaleDiff, ZoomBoxControl.ImageDpi, ZoomBoxControl.GetImageSize());
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
                using (Stream stream = File.Create(frame.FullPath))
                    encoder.Save(stream);

                UpdateProgress(count++);
            }
        }

        #endregion

        #endregion

        public void NotificationUpdated()
        {
            //RibbonTabControl.UpdateNotifications();
        }
    }
}
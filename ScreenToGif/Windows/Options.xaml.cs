using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using ScreenToGif.Controls;
using ScreenToGif.Model;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;
using Application = System.Windows.Application;
using DialogResultWinForms = System.Windows.Forms.DialogResult;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace ScreenToGif.Windows
{
    public partial class Options : Window, INotification
    {
        #region Variables

        /// <summary>
        /// The Path of the Temp folder.
        /// </summary>
        private List<DirectoryInfo> _folderList = new List<DirectoryInfo>();

        /// <summary>
        /// The file count of the Temp folder.
        /// </summary>
        private int _fileCount;

        /// <summary>
        /// .
        /// </summary>
        private ObservableCollection<DefaultTaskModel> _effectList;

        #endregion

        public Options()
        {
            InitializeComponent();

#if UWP
                //PaypalLabel.Visibility = Visibility.Collapsed;
                UpdatesCheckBox.Visibility = Visibility.Collapsed;
                StoreTextBlock.Visibility = Visibility.Visible;
#endif
        }

        public Options(int index) : this()
        {
            SelectTab(index);
        }

        #region App Settings

        private void NotificationIconCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (App.NotifyIcon != null)
            {
                App.NotifyIcon.Visibility = UserSettings.All.ShowNotificationIcon ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Shortcuts

        private void ShortcutsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Global.IgnoreHotKeys = ShortcutsPanel.IsVisible;
        }

        private void Globals_OnKeyChanged(object sender, KeyChangedEventArgs e)
        {
            Recorders_OnKeyChanged(sender, e);

            if (e.Cancel)
                return;

            //Unregister old shortcut.
            HotKeyCollection.Default.Remove(e.PreviousModifiers, e.PreviousKey);

            //Registers all shortcuts and updates the input gesture text.
            App.RegisterShortcuts();
        }

        private void Recorders_OnKeyChanged(object sender, KeyChangedEventArgs e)
        {
            if (!(sender is KeyBox box))
                return;

            var list = new List<Tuple<Key, ModifierKeys>>
            {
                new Tuple<Key, ModifierKeys>(UserSettings.All.RecorderShortcut, UserSettings.All.RecorderModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.EditorShortcut, UserSettings.All.EditorModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.OptionsShortcut, UserSettings.All.OptionsModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.ExitShortcut, UserSettings.All.ExitModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.StartPauseShortcut, UserSettings.All.StartPauseModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.StopShortcut, UserSettings.All.StopModifiers),
                new Tuple<Key, ModifierKeys>(UserSettings.All.DiscardShortcut, UserSettings.All.DiscardModifiers)
            };

            //If this new shortcut is already in use.
            if (box.MainKey != Key.None && list.Count(c => c.Item1 == box.MainKey && c.Item2 == box.ModifierKeys) > 1)
            {
                box.MainKey = e.PreviousKey;
                box.ModifierKeys = e.PreviousModifiers;
                e.Cancel = true;
            }
        }

        #endregion

        #region Temp Files

        private void TempPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (TempPanel.Visibility != Visibility.Visible)
                return;

            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                UserSettings.All.TemporaryFolder = Path.GetTempPath();

            _tempDel = CheckTemp;
            _tempDel.BeginInvoke(e, CheckTempCallBack, null);

            NotificationUpdated();

            #region Settings

            //Paths.
            TempPanel.AppDataPathTextBlock.Text = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif"), "Settings.xaml");
            TempPanel.LocalPathTextBlock.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml");

            //Remove all text decorations (Strikethrough).
            TempPanel.AppDataPathTextBlock.TextDecorations.Clear();
            TempPanel.LocalPathTextBlock.TextDecorations.Clear();

            //Clear the tooltips.
            TempPanel.AppDataPathTextBlock.ClearValue(ToolTipProperty);
            TempPanel.LocalPathTextBlock.ClearValue(ToolTipProperty);

            //AppData.
            if (!File.Exists(TempPanel.AppDataPathTextBlock.Text))
            {
                TempPanel.AppDataPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Brushes.DarkSlateGray, 1),
                    0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                TempPanel.AppDataPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }

            //Local.
            if (!File.Exists(TempPanel.LocalPathTextBlock.Text))
            {
                TempPanel.LocalPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Brushes.DarkSlateGray, 1),
                    0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                TempPanel.LocalPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }

            #endregion
        }

        private void ChooseLogsLocation_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = true };

            if (!string.IsNullOrWhiteSpace(UserSettings.All.LogsFolder))
                folderDialog.SelectedPath = UserSettings.All.LogsFolder;

            if (folderDialog.ShowDialog() == DialogResultWinForms.OK)
                UserSettings.All.LogsFolder = folderDialog.SelectedPath;
        }

        private void ChooseLocation_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = true };

            if (!string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                folderDialog.SelectedPath = UserSettings.All.TemporaryFolder;

            if (folderDialog.ShowDialog() == DialogResultWinForms.OK)
                UserSettings.All.TemporaryFolder = folderDialog.SelectedPath;
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                Process.Start(path);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while trying to open the Temp Folder.");
            }
        }

        private async void ClearTempButton_Click(object sender, RoutedEventArgs e)
        {
            TempPanel.ClearTempButton.IsEnabled = false;

            try
            {
                var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

                if (!Directory.Exists(path))
                {
                    _folderList.Clear();
                    TempPanel.TempSeparator.TextRight = LocalizationHelper.Get("TempFiles.FilesAndFolders.None");
                    return;
                }

                _folderList = await Task.Factory.StartNew(() => Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).Select(x => new DirectoryInfo(x)).ToList());

                if (Dialog.Ask("ScreenToGif", LocalizationHelper.Get("TempFiles.KeepRecent"), LocalizationHelper.Get("TempFiles.KeepRecent.Info")))
                    _folderList = await Task.Factory.StartNew(() => _folderList.Where(w => (DateTime.Now - w.CreationTime).Days > (UserSettings.All.AutomaticCleanUpDays > 0 ? UserSettings.All.AutomaticCleanUpDays : 5)).ToList());

                foreach (var folder in _folderList)
                {
                    if (MutexList.IsInUse(folder.Name))
                        continue;

                    Directory.Delete(folder.FullName, true);
                }

                _folderList = await Task.Factory.StartNew(() => Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).Select(x => new DirectoryInfo(x)).ToList());
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while cleaning the Temp folder");
            }
            finally
            {
                App.MainViewModel.CheckDiskSpace();
            }

            TempPanel.TempSeparator.TextRight = string.Format(LocalizationHelper.Get("TempFiles.FilesAndFolders.Count", "{0} folders and {1} files"), _folderList.Count.ToString("##,##0"),
                _folderList.Sum(folder => Directory.EnumerateFiles(folder.FullName).Count()).ToString("##,##0"));

            TempPanel.ClearTempButton.IsEnabled = _folderList.Any();
        }

        private void RemoveAppDataSettings_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = IsLoaded && File.Exists(TempPanel.AppDataPathTextBlock.Text);
        }

        private void OpenAppDataSettings_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif", "Settings.xaml"));
                else
                    Process.Start("explorer.exe", $"/select,\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif", "Settings.xaml")}\"");
            }
            catch (Exception ex)
            {
                Dialog.Ok("Open AppData Settings Folder", "Impossible to open where the AppData settings is located", ex.Message);
            }
        }

        private void RemoveAppDataSettings_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                UserSettings.RemoveAppDataSettings();

                TempPanel.AppDataPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough,
                    new Pen(Brushes.DarkSlateGray, 1), 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                TempPanel.AppDataPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }
            catch (Exception ex)
            {
                Dialog.Ok("Remove AppData Settings", "Impossible to remove AppData settings", ex.Message);
            }
        }

        #region Async

        private delegate void TempDelegate(DependencyPropertyChangedEventArgs e);

        private TempDelegate _tempDel;

        private void CheckTemp(DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;

            _folderList = new List<DirectoryInfo>();

            var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

            if (!Directory.Exists(path)) return;

            _folderList = Directory.GetDirectories(path).Select(x => new DirectoryInfo(x)).ToList();

            _fileCount = _folderList.Sum(folder => Directory.EnumerateFiles(folder.FullName).Count());
        }

        private void CheckTempCallBack(IAsyncResult r)
        {
            try
            {
                _tempDel.EndInvoke(r);

                Dispatcher.Invoke(() =>
                {
                    App.MainViewModel.CheckDiskSpace();

                    TempPanel.TempSeparator.TextRight = string.Format(LocalizationHelper.Get("TempFiles.FilesAndFolders.Count", "{0} folders and {1} files"), _folderList.Count.ToString("##,##0"), _fileCount.ToString("##,##0"));

                    TempPanel.ClearTempButton.IsEnabled = _folderList.Any();
                });
            }
            catch (Exception)
            { }
        }

        #endregion

        #endregion

        #region Extras

        private void ExtrasPanel_Loaded(object sender, RoutedEventArgs e)
        {
            CheckTools();
        }

        private async void FfmpegImageCard_Click(object sender, RoutedEventArgs e)
        {
            CheckTools();

            if (!string.IsNullOrWhiteSpace(UserSettings.All.FfmpegLocation) && File.Exists(UserSettings.All.FfmpegLocation))
            {
                Native.ShowFileProperties(Path.GetFullPath(UserSettings.All.FfmpegLocation));
            }
        }

        private async void GifskiImageCard_Click(object sender, RoutedEventArgs e)
        {
            CheckTools();

            if (!string.IsNullOrWhiteSpace(UserSettings.All.GifskiLocation) && File.Exists(UserSettings.All.GifskiLocation))
            {
                Native.ShowFileProperties(Path.GetFullPath(UserSettings.All.GifskiLocation));
                return;
            }
        }

        private void LocationTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CheckTools();
        }

        private void SelectFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var output = UserSettings.All.FfmpegLocation ?? "";

            if (output.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                output = "";

            //It's only a relative path if not null/empty and there's no root folder declared.
            var isRelative = !string.IsNullOrWhiteSpace(output) && !Path.IsPathRooted(output);
            var notAlt = !string.IsNullOrWhiteSpace(output) && (UserSettings.All.FfmpegLocation ?? "").Contains(Path.DirectorySeparatorChar);

            //Gets the current directory folder, where the file is located. If empty, it means that the path is relative.
            var directory = !string.IsNullOrWhiteSpace(output) ? Path.GetDirectoryName(output) : "";

            if (!string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(directory))
                directory = AppDomain.CurrentDomain.BaseDirectory;

            var initial = Directory.Exists(directory) ? directory : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var ofd = new OpenFileDialog
            {
                FileName = "ffmpeg",
                Filter = "FFmpeg executable (*.exe)|*.exe", //TODO: Localize.
                Title = LocalizationHelper.Get("Extras.FfmpegLocation.Select"),
                InitialDirectory = isRelative ? Path.GetFullPath(initial) : initial,
                DefaultExt = ".exe"
            };

            var result = ofd.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            UserSettings.All.FfmpegLocation = ofd.FileName;

            //Converts to a relative path again.
            if (isRelative && !string.IsNullOrWhiteSpace(UserSettings.All.FfmpegLocation))
            {
                var selected = new Uri(UserSettings.All.FfmpegLocation);
                var baseFolder = new Uri(AppDomain.CurrentDomain.BaseDirectory);
                var relativeFolder = Uri.UnescapeDataString(baseFolder.MakeRelativeUri(selected).ToString());

                //This app even returns you the correct slashes/backslashes.
                UserSettings.All.FfmpegLocation = notAlt ? relativeFolder : relativeFolder.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            CheckTools();
        }

        private void SelectGifski_Click(object sender, RoutedEventArgs e)
        {
            var output = UserSettings.All.GifskiLocation ?? "";

            if (output.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                output = "";

            //It's only a relative path if not null/empty and there's no root folder declared.
            var isRelative = !string.IsNullOrWhiteSpace(output) && !Path.IsPathRooted(output);
            var notAlt = !string.IsNullOrWhiteSpace(output) && (UserSettings.All.GifskiLocation ?? "").Contains(Path.DirectorySeparatorChar);

            //Gets the current directory folder, where the file is located. If empty, it means that the path is relative.
            var directory = !string.IsNullOrWhiteSpace(output) ? Path.GetDirectoryName(output) : "";

            if (!string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(directory))
                directory = AppDomain.CurrentDomain.BaseDirectory;

            var initial = Directory.Exists(directory) ? directory : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var ofd = new OpenFileDialog
            {
                FileName = "gifski",
                Filter = "Gifski library (*.dll)|*.dll", //TODO: Localize.
                Title = LocalizationHelper.Get("Extras.GifskiLocation.Select"),
                InitialDirectory = isRelative ? Path.GetFullPath(initial) : initial,
                DefaultExt = ".dll"
            };

            var result = ofd.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            UserSettings.All.GifskiLocation = ofd.FileName;

            //Converts to a relative path again.
            if (isRelative && !string.IsNullOrWhiteSpace(UserSettings.All.GifskiLocation))
            {
                var selected = new Uri(UserSettings.All.GifskiLocation);
                var baseFolder = new Uri(AppDomain.CurrentDomain.BaseDirectory);
                var relativeFolder = Uri.UnescapeDataString(baseFolder.MakeRelativeUri(selected).ToString());

                //This app even returns you the correct slashes/backslashes.
                UserSettings.All.GifskiLocation = notAlt ? relativeFolder : relativeFolder.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            CheckTools();
        }

        private void ExtrasHyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(e.Uri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Erro while trying to navigate to the license website.");
            }
        }

        private void CheckTools()
        {
            if (!IsLoaded)
                return;

            try
            {
                if (Util.Other.IsFfmpegPresent(true))
                {
                    var info = new FileInfo(UserSettings.All.FfmpegLocation);
                    info.Refresh();

                    ExtrasPanel.FfmpegImageCard.Status = ExtrasStatus.Ready;
                    ExtrasPanel.FfmpegImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Ready", "{0}"), Humanizer.BytesToString(info.Length));
                }
                else
                {
                    ExtrasPanel.FfmpegImageCard.Status = ExtrasStatus.Available;
                    ExtrasPanel.FfmpegImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Download", "{0}"), "~ 43,7 MB");
                }

                if (Util.Other.IsGifskiPresent(true))
                {
                    var info = new FileInfo(UserSettings.All.GifskiLocation);
                    info.Refresh();

                    ExtrasPanel.GifskiImageCard.Status = ExtrasStatus.Ready;
                    ExtrasPanel.GifskiImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Ready", "{0}"), Humanizer.BytesToString(info.Length));
                }
                else
                {
                    ExtrasPanel.GifskiImageCard.Status = ExtrasStatus.Available;
                    ExtrasPanel.GifskiImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Download", "{0}"), "~ 1 MB");
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Checking the existance of external tools.");
                StatusBand.Error("It was not possible to check the existence of the external tools.");
            }
        }

        #endregion

        #region Other

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Global.IgnoreHotKeys = false;

            RenderOptions.ProcessRenderMode = UserSettings.All.DisableHardwareAcceleration ? RenderMode.SoftwareOnly : RenderMode.Default;
        
            UserSettings.Save();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            UserSettings.Save();

            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        #endregion


        public void NotificationUpdated()
        {
            if (!string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder) && Global.AvailableDiskSpace < 2000000000)
                TempPanel.LowSpaceTextBlock.Visibility = Visibility.Visible;
            else
                TempPanel.LowSpaceTextBlock.Visibility = Visibility.Collapsed;
        }

        internal void SelectTab(int index)
        {
            if (index <= -1 || index >= OptionsStackPanel.Children.Count - 1) return;

            if (OptionsStackPanel.Children[index] is RadioButton radio)
                radio.IsChecked = true;
        }
    }
}
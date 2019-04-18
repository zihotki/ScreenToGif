using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;
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

namespace ScreenToGif.Windows.UserControls
{
    /// <summary>
    /// Interaction logic for OptionsTempFiles.xaml
    /// </summary>
    public partial class OptionsTempFiles : UserControl
    {
        /// <summary>
        /// The Path of the Temp folder.
        /// </summary>
        private List<DirectoryInfo> _folderList = new List<DirectoryInfo>();

        /// <summary>
        /// The file count of the Temp folder.
        /// </summary>
        private int _fileCount;

        public OptionsTempFiles()
        {
            InitializeComponent();
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
            ClearTempButton.IsEnabled = false;

            try
            {
                var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

                if (!Directory.Exists(path))
                {
                    _folderList.Clear();
                    return;
                }

                _folderList = await Task.Factory.StartNew(() => Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).Select(x => new DirectoryInfo(x)).ToList());

                if (Dialog.Ask("ScreenToGif", LocalizationHelper.Get("TempFiles.KeepRecent"), 
                        LocalizationHelper.Get("TempFiles.KeepRecent.Info")))
                {
                    _folderList = await Task.Factory.StartNew(() =>
                          _folderList.Where(w => (DateTime.Now - w.CreationTime).Days > 5).ToList());
                }

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

            ClearTempButton.IsEnabled = _folderList.Any();
        }

        private void TempPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility != Visibility.Visible)
                return;

            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                UserSettings.All.TemporaryFolder = Path.GetTempPath();

            _tempDel = CheckTemp;
            _tempDel.BeginInvoke(e, CheckTempCallBack, null);

            NotificationUpdated();

            #region Settings

            //Paths.
            AppDataPathTextBlock.Text = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif"), "Settings.xaml");
            LocalPathTextBlock.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml");

            //Remove all text decorations (Strikethrough).
            AppDataPathTextBlock.TextDecorations.Clear();
            LocalPathTextBlock.TextDecorations.Clear();

            //Clear the tooltips.
            AppDataPathTextBlock.ClearValue(ToolTipProperty);
            LocalPathTextBlock.ClearValue(ToolTipProperty);

            //AppData.
            if (!File.Exists(AppDataPathTextBlock.Text))
            {
                AppDataPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Brushes.DarkSlateGray, 1),
                    0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                AppDataPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }

            //Local.
            if (!File.Exists(LocalPathTextBlock.Text))
            {
                LocalPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Brushes.DarkSlateGray, 1),
                    0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                LocalPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }

            #endregion
        }

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

                    ClearTempButton.IsEnabled = _folderList.Any();
                });
            }
            catch (Exception)
            { }
        }

        public void NotificationUpdated()
        {
            if (!string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder) && Global.AvailableDiskSpace < 2000000000)
                LowSpaceTextBlock.Visibility = Visibility.Visible;
            else
                LowSpaceTextBlock.Visibility = Visibility.Collapsed;
        }
    }
}

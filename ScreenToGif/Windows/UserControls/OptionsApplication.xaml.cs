using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using ScreenToGif.Util;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace ScreenToGif.Windows.UserControls
{
    /// <summary>
    /// Interaction logic for OptionsApplication.xaml
    /// </summary>
    public partial class OptionsApplication : UserControl
    {
        public OptionsApplication()
        {
            InitializeComponent();
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

                    FfmpegImageCard.Status = ExtrasStatus.Ready;
                    FfmpegImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Ready", "{0}"), Humanizer.BytesToString(info.Length));
                }
                else
                {
                    FfmpegImageCard.Status = ExtrasStatus.Available;
                    FfmpegImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Download", "{0}"), "~ 43,7 MB");
                }

                if (Util.Other.IsGifskiPresent(true))
                {
                    var info = new FileInfo(UserSettings.All.GifskiLocation);
                    info.Refresh();

                    GifskiImageCard.Status = ExtrasStatus.Ready;
                    GifskiImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Ready", "{0}"), Humanizer.BytesToString(info.Length));
                }
                else
                {
                    GifskiImageCard.Status = ExtrasStatus.Available;
                    GifskiImageCard.Description = string.Format(LocalizationHelper.Get("Extras.Download", "{0}"), "~ 1 MB");
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Checking the existance of external tools.");
            }
        }

    }
}

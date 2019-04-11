using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ScreenToGif.Windows.UserControls
{
    /// <summary>
    /// Interaction logic for OptionsSave.xaml
    /// </summary>
    public partial class OptionsSave : UserControl
    {
        public OptionsSave()
        {
            InitializeComponent();
        }

        private void SaveType_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            switch (UserSettings.All.SaveType)
            {
                case Export.Gif:
                    UserSettings.All.LatestExtension = ".gif";
                    break;
                case Export.Video:

                    if (SystemEncoderRadioButton.IsChecked == true)
                    {
                        UserSettings.All.LatestVideoExtension = ".avi";
                        FileTypeVideoComboBox.IsEnabled = false;
                    }
                    else
                    {
                        FileTypeVideoComboBox.IsEnabled = true;

                        if (FileTypeVideoComboBox.Items == null || !FileTypeVideoComboBox.Items.OfType<string>().Contains(UserSettings.All.LatestVideoExtension))
                            UserSettings.All.LatestVideoExtension = ".mp4";
                    }

                    break;
            }

            FilenameTextBox_TextChanged(null, null);
        }



        private void VideoEncoderRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SaveType_Checked(sender, e);
        }

        private void TransparentColorButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            var colorDialog = new ColorSelector(UserSettings.All.ChromaKey, false)
            {
                Owner = window
            };
            var result = colorDialog.ShowDialog();

            if (result.HasValue && result.Value)
            {
                UserSettings.All.ChromaKey = colorDialog.SelectedColor;
            }
        }

        private void ChooseLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var output = GetOutputFolder();

                if (output.ToCharArray().Any(x => Path.GetInvalidPathChars().Contains(x)))
                    output = "";

                //It's only a relative path if not null/empty and there's no root folder declared.
                var isRelative = !string.IsNullOrWhiteSpace(output) && !Path.IsPathRooted(output);
                var notAlt = !string.IsNullOrWhiteSpace(output) && GetOutputFolder().Contains(Path.DirectorySeparatorChar);

                var initial = Directory.Exists(output) ? output : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                var sfd = new SaveFileDialog
                {
                    FileName = GetOutputFilename(),
                    InitialDirectory = isRelative ? Path.GetFullPath(initial) : initial
                };

                #region Extensions

                switch (UserSettings.All.SaveType)
                {
                    case Export.Gif:
                        sfd.Filter = "Gif animation (.gif)|*.gif";
                        sfd.DefaultExt = ".gif";
                        break;
                    case Export.Apng:
                        sfd.Filter = "Animated PNG|*.png|Animated PNG|*.apng";
                        sfd.DefaultExt = UserSettings.All.LatestApngExtension ?? ".png";
                        break;
                    case Export.Video:
                        sfd.Filter = FfmpegEncoderRadioButton.IsChecked == true ? "Avi video (.avi)|*.avi|Mp4 video (.mp4)|*.mp4|WebM video|*.webm|Windows media video|*.wmv" : "Avi video (.avi)|*.avi";
                        sfd.DefaultExt = FfmpegEncoderRadioButton.IsChecked == true ? FileTypeVideoComboBox.SelectedItem as string : ".avi";
                        sfd.FilterIndex = FfmpegEncoderRadioButton.IsChecked == true ? FileTypeVideoComboBox.SelectedIndex + 1 : 0;
                        break;
                }

                #endregion

                var result = sfd.ShowDialog();

                if (!result.HasValue || !result.Value) return;

                SetOutputFolder(Path.GetDirectoryName(sfd.FileName));
                SetOutputFilename(Path.GetFileNameWithoutExtension(sfd.FileName));
                UserSettings.All.OverwriteOnSave = FileExistsGrid.Visibility == Visibility.Visible;
                SetOutputExtension(Path.GetExtension(sfd.FileName));

                //Converts to a relative path again.
                if (isRelative && !string.IsNullOrWhiteSpace(GetOutputFolder()))
                {
                    var selected = new Uri(GetOutputFolder());
                    var baseFolder = new Uri(AppDomain.CurrentDomain.BaseDirectory);
                    var relativeFolder = Uri.UnescapeDataString(baseFolder.MakeRelativeUri(selected).ToString());

                    //This app even returns you the correct slashes/backslashes.
                    SetOutputFolder(notAlt ? relativeFolder : relativeFolder.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }
            }
            catch (ArgumentException sx)
            {
                LogWriter.Log(sx, "Error while trying to choose the output path and filename.", GetOutputFolder() + GetOutputFilename());

                SetOutputFolder("");
                SetOutputFilename("");
                throw;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while trying to choose the output path and filename.", GetOutputFolder() + GetOutputFilename());
                throw;
            }
        }

        private void FilenameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            try
            {
                var exists = File.Exists(Path.Combine(GetOutputFolder(), GetOutputFilename() + GetOutputExtension()));

                FileExistsGrid.Visibility = exists && GetPickLocation() ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Check if exists");
                FileExistsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void FileHyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(Path.Combine(GetOutputFolder(), GetOutputFilename() + GetOutputExtension()));
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Open file that already exists using the hyperlink");
            }
        }

        #region helper methods

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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private string StringResource(string key)
        {
            return FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " ");
        }

        private string DispatcherStringResource(string key)
        {
            return Dispatcher.Invoke(() => FindResource(key).ToString().Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " "));
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
    }
}

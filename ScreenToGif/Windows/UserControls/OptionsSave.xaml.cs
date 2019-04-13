using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
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

                var initial = Directory.Exists(output)
                    ? output
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    InitialDirectory = initial
                };

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    SetOutputFolder(dialog.FileName);
                }
            }
            catch (ArgumentException sx)
            {
                LogWriter.Log(sx, "Error while trying to choose the output path.", GetOutputFolder());

                SetOutputFolder("");
                throw;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while trying to choose the output path.", GetOutputFolder());
                throw;
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

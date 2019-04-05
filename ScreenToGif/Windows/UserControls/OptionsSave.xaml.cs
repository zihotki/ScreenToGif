using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
                case Export.Images:
                    UserSettings.All.LatestImageExtension = UserSettings.All.ZipImages ? ".zip" : ".png";
                    break;
                case Export.Project:
                    if (UserSettings.All.LatestProjectExtension != ".stg" && UserSettings.All.LatestProjectExtension != ".zip")
                        UserSettings.All.LatestProjectExtension = ".stg";
                    break;
                case Export.Photoshop:
                    UserSettings.All.LatestPhotoshopExtension = ".psd";
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
            var colorDialog = new ColorSelector(UserSettings.All.ChromaKey, false) { Owner = this };
            var result = colorDialog.ShowDialog();

            if (result.HasValue && result.Value)
                UserSettings.All.ChromaKey = colorDialog.SelectedColor;
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
                    case Export.Images:
                        sfd.Filter = UserSettings.All.ZipImages ? "Zip, all selected images (.zip)|*.zip" : "Png image, all selected images (.png)|*.png";
                        sfd.DefaultExt = UserSettings.All.ZipImages ? ".zip" : ".png";
                        break;
                    case Export.Project:
                        sfd.Filter = "Project (.stg)|*.stg|Project as Zip (.zip)|*.zip";
                        sfd.DefaultExt = ".stg";
                        break;
                    case Export.Photoshop:
                        sfd.Filter = "PSD File (.psd)|*.psd";
                        sfd.DefaultExt = ".psd";
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

        private void IncreaseNumber_Click(object sender, RoutedEventArgs e)
        {
            ChangeFileNumber(1);
        }

        private void DecreaseNumber_Click(object sender, RoutedEventArgs e)
        {
            ChangeFileNumber(-1);
        }

        private void FilenameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            try
            {
                var exists = File.Exists(Path.Combine(GetOutputFolder(), GetOutputFilename() + GetOutputExtension()));

                FileExistsGrid.Visibility = exists && GetPickLocation() ? Visibility.Visible : Visibility.Collapsed;
                StatusList.Remove(StatusType.Warning);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Check if exists");
                StatusList.Warning("Filename inconsistency: " + ex.Message);
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
    }
}

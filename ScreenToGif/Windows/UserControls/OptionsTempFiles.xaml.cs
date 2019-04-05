using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;

namespace ScreenToGif.Windows.UserControls
{
    /// <summary>
    /// Interaction logic for OptionsTempFiles.xaml
    /// </summary>
    public partial class OptionsTempFiles : UserControl
    {
        public OptionsTempFiles()
        {
            InitializeComponent();
        }

        private void CreateLocalSettings_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                UserSettings.CreateLocalSettings();

                LocalPathTextBlock.TextDecorations.Clear();
                LocalPathTextBlock.ClearValue(ToolTipProperty);
            }
            catch (Exception ex)
            {
                Dialog.Ok("Create Local Settings", "Impossible to create local settings", ex.Message);
            }
        }

        private void CreateLocalSettings_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = IsLoaded && !File.Exists(LocalPathTextBlock.Text);
        }

        private void OpenLocalSettings_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml"));
                else
                    Process.Start("explorer.exe", $"/select,\"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml")}\"");
            }
            catch (Exception ex)
            {
                Dialog.Ok("Open AppData Local Folder", "Impossible to open where the Local settings file is located", ex.Message);
            }
        }


        private void RemoveLocalSettings_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                UserSettings.RemoveLocalSettings();

                LocalPathTextBlock.TextDecorations.Add(new TextDecoration(TextDecorationLocation.Strikethrough,
                    new Pen(Brushes.DarkSlateGray, 1), 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));

                LocalPathTextBlock.SetResourceReference(ToolTipProperty, "TempFiles.NotExists");
            }
            catch (Exception ex)
            {
                Dialog.Ok("Remove Local Settings", "Impossible to remove local settings", ex.Message);
            }
        }


        private void RemoveLocalSettings_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = IsLoaded && File.Exists(LocalPathTextBlock.Text);
        }
    }
}

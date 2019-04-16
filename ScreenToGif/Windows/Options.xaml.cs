using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenToGif.Util;
using Application = System.Windows.Application;

namespace ScreenToGif.Windows
{
    public partial class Options : Window, INotification
    {
        public Options()
        {
            InitializeComponent();
        }

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

        public void NotificationUpdated()
        {
            TempPanel.NotificationUpdated();
        }
    }
}
using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using ScreenToGif.Util;

namespace ScreenToGif.Windows.UserControls
{
    /// <summary>
    /// Interaction logic for OptionsExtras.xaml
    /// </summary>
    public partial class OptionsExtras : UserControl
    {
        public OptionsExtras()
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
    }
}

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
    /// Interaction logic for OptionsShortcuts.xaml
    /// </summary>
    public partial class OptionsShortcuts : UserControl
    {
        public OptionsShortcuts()
        {
            InitializeComponent();
        }

        private void ShortcutsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Global.IgnoreHotKeys = IsVisible;
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
    }
}

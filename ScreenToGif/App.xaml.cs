using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenToGif.Model;
using ScreenToGif.Util;
using ScreenToGif.Windows.Other;

namespace ScreenToGif
{
    public partial class App
    {
        internal static ApplicationViewModel MainViewModel { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Global.StartupDateTime = DateTime.Now;

            //Unhandled Exceptions.
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //Increases the duration of the tooltip display.
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));

            LocalizationHelper.SelectCulture("en");

            if (UserSettings.All.DisableHardwareAcceleration)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            #region Net Framework

            var array = Type.GetType("System.Array");
            var method = array?.GetMethod("Empty");

            if (array == null || method == null)
            {
                var ask = Dialog.Ask("Missing Dependency", "Net Framework 4.6.1 is not present", "In order to properly use this app, you need to download the correct version of the .Net Framework. Open the web page to download?");

                if (ask)
                {
                    Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=49981");
                    return;
                }
            }

            #endregion

            #region Net Framework HotFixes

            //Only runs on Windows 7 SP1.
            if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 1)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var search = new ManagementObjectSearcher("SELECT HotFixID FROM Win32_QuickFixEngineering WHERE HotFixID = 'KB4055002'").Get();
                        Global.IsHotFix4055002Installed = search.Count > 0;
                    }
                    catch (Exception ex)
                    {
                        LogWriter.Log(ex, "Error while trying to know if a hot fix was installed.");
                    }
                });
            }

            #endregion

            MainViewModel = (ApplicationViewModel)FindResource("AppViewModel") ?? new ApplicationViewModel();

            RegisterShortcuts();

            //var select = new SelectFolderDialog(); select.ShowDialog(); return;
            //var select = new TestField(); select.ShowDialog(); return;
            //var select = new Encoder(); select.ShowDialog(); return;

            #region Tasks

            Task.Factory.StartNew(MainViewModel.ClearTemporaryFilesTask, TaskCreationOptions.LongRunning);

            #endregion

            MainViewModel.OpenRecorder.Execute(null);
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            //TODO: Use a try catch for each one.

            MutexList.RemoveAll();

            UserSettings.Save();

            HotKeyCollection.Default.Dispose();
        }

        private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogWriter.Log(e.Exception, "On Dispacher Unhandled Exception - Unknown");

            try
            {
                ShowException(e.Exception);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while displaying the error.");
                //Ignored.
            }

            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!(e.ExceptionObject is Exception exception)) return;

            LogWriter.Log(exception, "Current Domain Unhandled Exception - Unknown");

            try
            {
                ShowException(exception);
            }
            catch (Exception)
            {
                //Ignored.
            }
        }

        internal static void RegisterShortcuts()
        {
            //TODO: If startup/editor is open and focused, should I let the hotkeys work? 

            //Registers all shortcuts. 
            var screen = HotKeyCollection.Default.TryRegisterHotKey(UserSettings.All.RecorderModifiers, UserSettings.All.RecorderShortcut, () =>
                { if (!Global.IgnoreHotKeys && MainViewModel.OpenRecorder.CanExecute(null)) MainViewModel.OpenRecorder.Execute(null); }, true);

            var options = HotKeyCollection.Default.TryRegisterHotKey(UserSettings.All.OptionsModifiers, UserSettings.All.OptionsShortcut, () =>
                { if (!Global.IgnoreHotKeys && MainViewModel.OpenOptions.CanExecute(null)) MainViewModel.OpenOptions.Execute(null); }, true);

            var exit = HotKeyCollection.Default.TryRegisterHotKey(UserSettings.All.ExitModifiers, UserSettings.All.ExitShortcut, () =>
                { if (!Global.IgnoreHotKeys && MainViewModel.ExitApplication.CanExecute(null)) MainViewModel.ExitApplication.Execute(null); }, true);

            //Updates the input gesture text of each command.
            MainViewModel.RecorderGesture = screen ? Native.GetSelectKeyText(UserSettings.All.RecorderShortcut, UserSettings.All.RecorderModifiers, true, true) : "";
            MainViewModel.OptionsGesture = options ? Native.GetSelectKeyText(UserSettings.All.OptionsShortcut, UserSettings.All.OptionsModifiers, true, true) : "";
            MainViewModel.ExitGesture = exit ? Native.GetSelectKeyText(UserSettings.All.ExitShortcut, UserSettings.All.ExitModifiers, true, true) : "";
        }

        internal void ShowException(Exception exception)
        {
            if (Global.IsHotFix4055002Installed && exception is XamlParseException && exception.InnerException is TargetInvocationException)
            {
                ExceptionDialog.Ok(exception, "ScreenToGif", "Error while rendering visuals", exception.Message);
            }
            else
            {
                ExceptionDialog.Ok(exception, "ScreenToGif", "Unhandled exception", exception.Message);
            }
        }
    }
}
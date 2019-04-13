using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using ScreenToGif.Controls;
using ScreenToGif.Util;
using ScreenToGif.Windows;
using ScreenToGif.Windows.Other;

namespace ScreenToGif.Model
{
    internal class ApplicationViewModel : ApplicationModel
    {
        #region Commands

        public ICommand OpenRecorder
        {
            get
            {
                return new RelayCommand
                {
                    CanExecutePredicate = o =>
                    {
                        //True if all windows are not Recorders.
                        return Application.Current.Windows.OfType<Window>().All(a => !(a is RecorderWindow));
                    },
                    ExecuteAction = a =>
                    {
                        var caller = a as Window;
                        caller?.Hide();

                        var recorder = new Recorder();
                        recorder.Closed += (sender, args) =>
                        {
                            var window = sender as Recorder;

                            if (window?.Project != null && window.Project.Any)
                            {
                                ShowEditor(window.Project);
                                caller?.Close();
                            }
                            else
                            {
                                caller?.Show();
                            }
                        };

                        Application.Current.MainWindow = recorder;
                        recorder.Show();
                    }
                };
            }
        }

        public ICommand OpenEditor
        {
            get
            {
                return new RelayCommand
                {
                    CanExecutePredicate = a => true,
                    ExecuteAction = a =>
                    {
                        var caller = a as Window;

                        ShowEditor();

                        caller?.Close();
                    }
                };
            }
        }

        public ICommand OpenOptions
        {
            get
            {
                return new RelayCommand
                {
                    CanExecutePredicate = a => true,
                    ExecuteAction = a =>
                    {
                        var options = Application.Current.Windows.OfType<Options>().FirstOrDefault();
                        options = new Options();
                        options.ShowDialog();
                    }
                };
            }
        }

        public ICommand ExitApplication
        {
            get
            {
                return new RelayCommand
                {
                    CanExecutePredicate = o =>
                    {
                        //TODO: Check if there's anything open or anything happening with editors.
                        return Application.Current.Windows.OfType<RecorderWindow>().All(a => a.Stage != Stage.Recording);
                    },
                    ExecuteAction = a =>
                    {
                        if (UserSettings.All.NotifyWhileClosingApp && !Dialog.Ask(LocalizationHelper.Get("Application.Exiting.Title"), LocalizationHelper.Get("Application.Exiting.Instruction"), LocalizationHelper.Get("Application.Exiting.Message")))
                            return;

                        Application.Current.Shutdown(69);
                    }
                };
            }
        }

        #endregion

        #region Methods

        private void ShowEditor(ProjectInfo project = null)
        {
            var editor = new Editor { Project = project };
            editor.Show();

            Application.Current.MainWindow = editor;
        }

        internal void ClearTemporaryFilesTask()
        {
            try
            {
                if (!UserSettings.All.AutomaticCleanUp || Global.IsCurrentlyDeletingFiles || string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                    return;

                Global.IsCurrentlyDeletingFiles = true;

                var path = Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording");

                if (!Directory.Exists(path))
                    return;

                var list = Directory.GetDirectories(path).Select(x => new DirectoryInfo(x))
                    .Where(w => (DateTime.Now - w.CreationTime).TotalDays > (UserSettings.All.AutomaticCleanUpDays > 0 ? UserSettings.All.AutomaticCleanUpDays : 5)).ToList();

                //var list = Directory.GetDirectories(path).Select(x => new DirectoryInfo(x));
                
                foreach (var folder in list)
                {
                    if (MutexList.IsInUse(folder.Name))
                        continue;

                    Directory.Delete(folder.FullName, true);
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Automatic clean up");
            }
            finally
            {
                Global.IsCurrentlyDeletingFiles = false;
                CheckDiskSpace();
            }
        }

        internal void CheckDiskSpace()
        {
            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
                return;

            try
            {
                var isRelative = !string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder) && !Path.IsPathRooted(UserSettings.All.TemporaryFolder);
                var drive = new DriveInfo((isRelative ? Path.GetFullPath(UserSettings.All.TemporaryFolder) : UserSettings.All.TemporaryFolder).Substring(0, 1));

                Global.AvailableDiskSpacePercentage = drive.AvailableFreeSpace * 100d / drive.TotalSize; //Get the percentage of space left.
                Global.AvailableDiskSpace = drive.AvailableFreeSpace;

                //If there's less than 2GB left.
                if (drive.AvailableFreeSpace < 2000000000)
                    Application.Current.Dispatcher.Invoke(() => NotificationManager.AddNotification(LocalizationHelper.GetWithFormat("Editor.Warning.LowSpace", Math.Round(Global.AvailableDiskSpacePercentage, 2)),
                        StatusType.Warning, "disk", () => App.MainViewModel.OpenOptions.Execute(5)));
                else
                    Application.Current.Dispatcher.Invoke(() => NotificationManager.RemoveNotification(r => r.Tag == "disk"));
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Error while checking the space left in disk");
            }
        }

        #endregion
    }

    internal class RelayCommand : ICommand
    {
        public Predicate<object> CanExecutePredicate { get; set; }
        public Action<object> ExecuteAction { get; set; }

        public RelayCommand(Predicate<object> canExecute, Action<object> execute)
        {
            CanExecutePredicate = canExecute;
            ExecuteAction = execute;
        }

        public RelayCommand()
        { }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            return CanExecutePredicate == null || CanExecutePredicate(parameter);
        }

        public void Execute(object parameter)
        {
            ExecuteAction(parameter);
        }
    }

    internal class AdvancedRelayCommand : RoutedUICommand, ICommand
    {
        public Predicate<object> CanExecutePredicate { get; set; }
        public Action<object> ExecuteAction { get; set; }

        public AdvancedRelayCommand()
        { }

        public AdvancedRelayCommand(string text, string name, Type ownerType, InputGestureCollection inputGestures) : base(text, name, ownerType, inputGestures)
        { }

        bool ICommand.CanExecute(object parameter)
        {
            return CanExecutePredicate == null || CanExecutePredicate(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            ExecuteAction(parameter);
        }

        //public bool CanExecute(object parameter)
        //{
        //    return CanExecutePredicate == null || CanExecutePredicate(parameter);
        //}

        //public void Execute(object parameter)
        //{
        //    ExecuteAction(parameter);
        //}
    }
}
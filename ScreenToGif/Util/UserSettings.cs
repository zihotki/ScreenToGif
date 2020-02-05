using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Xaml;
using System.Xml;
using XamlParseException = System.Windows.Markup.XamlParseException;
using XamlReader = System.Windows.Markup.XamlReader;
using XamlWriter = System.Windows.Markup.XamlWriter;

namespace ScreenToGif.Util
{
    internal sealed class UserSettings : INotifyPropertyChanged
    {
        #region Variables

        private static ResourceDictionary _local;
        private static ResourceDictionary _appData;
        private static readonly ResourceDictionary Default;

        public event PropertyChangedEventHandler PropertyChanged;

        public static UserSettings All { get; } = new UserSettings();

        public string Version => Assembly.GetEntryAssembly().GetName().Version?.ToStringShort() ?? "0.0";

        #endregion

        static UserSettings()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            //Paths.
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml");
            var appData = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif"), "Settings.xaml");

            //Only creates an empty AppData settings file if there's no local settings defined.
            if (!File.Exists(local) && !File.Exists(appData))
            {
                var directory = Path.GetDirectoryName(appData);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                //Just creates an empty file without writting anything. 
                File.Create(appData).Dispose();
            }

            //Loads AppData settings.
            if (File.Exists(appData))
            {
                _appData = LoadOrDefault(appData);
                Application.Current.Resources.MergedDictionaries.Add(_appData);
            }

            //Loads Local settings.
            if (File.Exists(local))
            {
                _local = LoadOrDefault(local);
                Application.Current.Resources.MergedDictionaries.Add(_local);
            }

            //Reads the default settings (It's loaded by default).
            Default = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source.OriginalString.EndsWith("/Settings.xaml"));
        }

        #region Methods

        public static void Save()
        {
            //Only writes if there's something changed. Should not write the default dictionary.
            if (_local == null && _appData == null)
                return;

            //Filename: Local or AppData.
            var filename = _local != null ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml") :
                Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif"), "Settings.xaml");
            var backup = _local != null ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml.bak") :
                Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif"), "Settings.xaml.bak");

            #region Create folder

            var folder = Path.GetDirectoryName(filename);

            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            #endregion

            //Create backup.
            if (File.Exists(filename))
                File.Copy(filename, backup, true);

            try
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    CheckCharacters = true,
                    CloseOutput = true,
                    ConformanceLevel = ConformanceLevel.Fragment,
                    Encoding = Encoding.UTF8,
                };

                using (var writer = XmlWriter.Create(filename, settings))
                    XamlWriter.Save(_local ?? _appData, writer);

                if (File.ReadAllText(filename).All(x => x == '\0'))
                    File.Copy(backup, filename, true);

                File.Delete(backup);
            }
            catch (Exception e)
            {
                LogWriter.Log(e, "Saving settings");
            }
        }

        private static object GetValue([CallerMemberName] string key = "", object defaultValue = null)
        {
            if (Default == null)
                return defaultValue;

            if (Application.Current == null || Application.Current.Resources == null)
                return Default[key];

            if (Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key];

            return Default[key] ?? defaultValue;
        }

        private static void SetValue(object value, [CallerMemberName] string key = "")
        {
            //Updates or inserts the value to the Local resource.
            if (_local != null)
            {
                if (_local.Contains(key))
                {
                    _local[key] = value;

                    //If the value is being set to null, remove it.
                    if (value == null && (!Default.Contains(key) || Default[key] == null))
                        _local.Remove(key);
                }
                else
                    _local.Add(key, value);
            }

            //Updates or inserts the value to the AppData resource.
            if (_appData != null)
            {
                if (_appData.Contains(key))
                {
                    _appData[key] = value;

                    //If the value is being set to null, remove it.
                    if (value == null && (!Default.Contains(key) || Default[key] == null))
                        _appData.Remove(key);
                }
                else
                    _appData.Add(key, value);
            }

            //Updates/Adds the current value of the resource.
            if (Application.Current.Resources.Contains(key))
                Application.Current.Resources[key] = value;
            else
                Application.Current.Resources.Add(key, value);

            All.OnPropertyChanged(key);
        }

        private static ResourceDictionary LoadOrDefault(string path, int trial = 0, XamlObjectWriterException exception = null)
        {
            ResourceDictionary resource = null;

            try
            {
                if (!File.Exists(path))
                    return new ResourceDictionary();

                if (exception != null)
                {
                    var content = File.ReadAllLines(path).ToList();
                    content.RemoveAt(exception.LineNumber - 1);

                    File.WriteAllLines(path, content);
                }

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    try
                    {
                        //Read in ResourceDictionary File
                        resource = (ResourceDictionary)XamlReader.Load(fs);
                    }
                    catch (XamlParseException xx)
                    {
                        if (xx.InnerException is XamlObjectWriterException inner && trial < 5)
                            return LoadOrDefault(path, trial + 1, inner);

                        resource = new ResourceDictionary();
                    }
                    catch (Exception ex)
                    {
                        //Sets a default value if null.
                        resource = new ResourceDictionary();
                    }
                }

                //Tries to load the resource from disk. 
                //resource = new ResourceDictionary {Source = new Uri(path, UriKind.RelativeOrAbsolute)};
            }
            catch (Exception)
            {
                //Sets a default value if null.
                resource = new ResourceDictionary();
            }

            return resource;
        }

        public static void CreateLocalSettings()
        {
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml");

            if (!File.Exists(local))
                File.Create(local).Dispose();

            _local = LoadOrDefault(local);
        }

        public static void RemoveLocalSettings()
        {
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.xaml");

            if (File.Exists(local))
                File.Delete(local);

            _local = null; //TODO: Should I remove from the merged dictionaries?
        }

        public static void RemoveAppDataSettings()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenToGif", "Settings.xaml");

            if (File.Exists(appData))
                File.Delete(appData);

            _appData = null; //TODO: Should I remove from the merged dictionaries?
        }

        private void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Startup

        public double StartupTop
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double StartupLeft
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double StartupHeight
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double StartupWidth
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public WindowState StartupWindowState
        {
            get => (WindowState)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Recorder

        public Rect SelectedRegion
        {
            get => (Rect)GetValue();
            set => SetValue(value);
        }

        public int RecorderModeIndex
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public int LatestFps
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public double RecorderLeft
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double RecorderTop
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public int RecorderWidth
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public int RecorderHeight
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Options • Application
        
        public bool UsePreStart
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public int PreStartValue
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public int SnapshotDefaultDelay
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public bool FixedFrameRate
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool AsyncRecording
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool Magnifier
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool NotifyFrameDeletion
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool NotifyWhileClosingApp
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool DrawOutlineOutside
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool DisableHardwareAcceleration
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Options • Interface

        public Color GridColor1
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color GridColor2
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Rect GridSize
        {
            get => (Rect)GetValue();
            set => SetValue(value);
        }

        public Color RecorderBackground
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color RecorderForeground
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color BoardGridBackground
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color BoardGridColor1
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color BoardGridColor2
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Rect BoardGridSize
        {
            get => (Rect)GetValue();
            set => SetValue(value);
        }

        public bool EditorExtendChrome
        {
            get => (bool)GetValue(defaultValue: false);
            set => SetValue(value);
        }

        public bool RecorderThinMode
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool TripleClickSelection
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool AutomaticallySizeOnContent
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool AutomaticallyFitImage
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Options • Shortcuts

        public Key RecorderShortcut
        {
            get => (Key)GetValue(defaultValue: Key.None);
            set => SetValue(value);
        }

        public ModifierKeys RecorderModifiers
        {
            get => (ModifierKeys)GetValue(defaultValue: ModifierKeys.None);
            set => SetValue(value);
        }

        public Key EditorShortcut
        {
            get => (Key)GetValue(defaultValue: Key.None);
            set => SetValue(value);
        }

        public ModifierKeys EditorModifiers
        {
            get => (ModifierKeys)GetValue(defaultValue: ModifierKeys.None);
            set => SetValue(value);
        }

        public Key OptionsShortcut
        {
            get => (Key)GetValue(defaultValue: Key.None);
            set => SetValue(value);
        }

        public ModifierKeys OptionsModifiers
        {
            get => (ModifierKeys)GetValue(defaultValue: ModifierKeys.None);
            set => SetValue(value);
        }

        public Key ExitShortcut
        {
            get => (Key)GetValue(defaultValue: Key.None);
            set => SetValue(value);
        }

        public ModifierKeys ExitModifiers
        {
            get => (ModifierKeys)GetValue(defaultValue: ModifierKeys.None);
            set => SetValue(value);
        }


        public Key StartPauseShortcut
        {
            get => (Key)GetValue();
            set => SetValue(value);
        }

        public ModifierKeys StartPauseModifiers
        {
            get => (ModifierKeys)GetValue(defaultValue: ModifierKeys.None);
            set => SetValue(value);
        }

        public Key StopShortcut
        {
            get => (Key)GetValue();
            set => SetValue(value);
        }

        public ModifierKeys StopModifiers
        {
            get => (ModifierKeys)GetValue();
            set => SetValue(value);
        }

        public Key DiscardShortcut
        {
            get => (Key)GetValue();
            set => SetValue(value);
        }

        public ModifierKeys DiscardModifiers
        {
            get => (ModifierKeys)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Options • Temporary Files

        public string LogsFolder
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string TemporaryFolder
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public bool AutomaticCleanUp
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public int AutomaticCleanUpDays
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Options • Save As

        public Export SaveType
        {
            get => (Export)GetValue();
            set => SetValue(value);
        }

        public GifEncoderType GifEncoder
        {
            get => (GifEncoderType)GetValue();
            set => SetValue(value);
        }

        public ApngEncoderType ApngEncoder
        {
            get => (ApngEncoderType)GetValue();
            set => SetValue(value);
        }

        public VideoEncoderType VideoEncoder
        {
            get => (VideoEncoderType)GetValue();
            set => SetValue(value);
        }

        //Gif.
        public int Quality
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public int GifskiQuality
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public int MaximumColors
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public bool Looped
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool RepeatForever
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public int RepeatCount
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public ColorQuantizationType ColorQuantization
        {
            get => (ColorQuantizationType)GetValue();
            set => SetValue(value);
        }

        public bool DetectUnchanged
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool PaintTransparent
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public Color ChromaKey
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public string ExtraParametersGif
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string LatestOutputFolder
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string LatestExtension
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        //Gif > Save options.

        public bool ExecuteCustomCommands
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public string CustomCommands
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        //Apng.
        public bool DetectUnchangedApng
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool PaintTransparentApng
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public bool LoopedApng
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public int RepeatCountApng
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public bool RepeatForeverApng
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public string ExtraParametersApngFFmpeg
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string LatestApngOutputFolder
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string LatestApngExtension
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        //Apng > Save options.

        public bool ExecuteCustomCommandsApng
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public string CustomCommandsApng
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        //Video
        public int AviQuality
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public bool FlipVideo
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public int OutputFramerate
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public string ExtraParameters
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string LatestVideoOutputFolder
        {
            get => (string)GetValue();
            set => SetValue(value);
        }
                
        public string LatestVideoExtension
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        //Video > Save options.

        public bool ExecuteCustomCommandsVideo
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public string CustomCommandsVideo
        {
            get => (string)GetValue();
            set => SetValue(value);
        }


        #endregion

        #region Options • Extras

        public string FfmpegLocation
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public string GifskiLocation
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Editor

        public double EditorTop
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double EditorLeft
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double EditorHeight
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public double EditorWidth
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public WindowState EditorWindowState
        {
            get => (WindowState)GetValue();
            set => SetValue(value);
        }

        #endregion

        #region Editor • Progress

        public ProgressType ProgressType
        {
            get => (ProgressType)GetValue();
            set => SetValue(value);
        }

        public bool IsProgressFontGroupExpanded
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public FontFamily ProgressFontFamily
        {
            get => (FontFamily)GetValue();
            set => SetValue(value);
        }

        public FontStyle ProgressFontStyle
        {
            get => (FontStyle)GetValue();
            set => SetValue(value);
        }

        public FontWeight ProgressFontWeight
        {
            get => (FontWeight)GetValue();
            set => SetValue(value);
        }

        public double ProgressFontSize
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public Color ProgressFontColor
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public Color ProgressColor
        {
            get => (Color)GetValue();
            set => SetValue(value);
        }

        public int ProgressPrecision
        {
            get => (int)GetValue();
            set => SetValue(value);
        }

        public bool ProgressShowTotal
        {
            get => (bool)GetValue();
            set => SetValue(value);
        }

        public string ProgressFormat
        {
            get => (string)GetValue();
            set => SetValue(value);
        }

        public double ProgressThickness
        {
            get => (double)GetValue();
            set => SetValue(value);
        }

        public VerticalAlignment ProgressVerticalAligment
        {
            get => (VerticalAlignment)GetValue();
            set => SetValue(value);
        }

        public HorizontalAlignment ProgressHorizontalAligment
        {
            get => (HorizontalAlignment)GetValue();
            set => SetValue(value);
        }

        public Orientation ProgressOrientation
        {
            get => (Orientation)GetValue();
            set => SetValue(value);
        }

        #endregion
    }
}
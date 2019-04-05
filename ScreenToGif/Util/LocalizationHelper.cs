using ScreenToGif.Windows.Other;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace ScreenToGif.Util
{
    /// <summary>
    /// Deals with localization behaviors.
    /// </summary>
    public static class LocalizationHelper
    {
        public static void SelectCulture(string culture)
        {
            #region Validation

            //If none selected, fallback to english.
            if (string.IsNullOrEmpty(culture))
                culture = "en";

            if (culture.Equals("auto") || culture.Length < 2)
            {
                var ci = CultureInfo.InstalledUICulture;
                culture = ci.Name;
            }

            #endregion

            //Copy all MergedDictionarys into a auxiliar list.
            var dictionaryList = Application.Current.Resources.MergedDictionaries.ToList();

            #region Selected Culture

            //Search for the specified culture.
            var requestedCulture = $"/Resources/Localization/StringResources.{culture}.xaml";
            var requestedResource = dictionaryList.FirstOrDefault(d => d.Source?.OriginalString == requestedCulture);

            #endregion

            #region Generic Branch Fallback

            //Fallback to a more generic version of the language. Example: pt-BR to pt.
            while (requestedResource == null && !string.IsNullOrEmpty(culture))
            {
                culture = CultureInfo.GetCultureInfo(culture).Parent.Name;
                requestedCulture = $"/Resources/Localization/StringResources.{culture}.xaml";
                requestedResource = dictionaryList.FirstOrDefault(d => d.Source?.OriginalString == requestedCulture);
            }

            #endregion

            #region English Fallback

            //If not present, fall back to english.
            if (requestedResource == null)
            {
                culture = "en";
                requestedCulture = "/Resources/Localization/StringResources.en.xaml";
                requestedResource = dictionaryList.FirstOrDefault(d => d.Source?.OriginalString == requestedCulture);
            }

            #endregion

            //If we have the requested resource, remove it from the list and place at the end.
            //Then this language will be our current string table.
            Application.Current.Resources.MergedDictionaries.Remove(requestedResource);
            Application.Current.Resources.MergedDictionaries.Add(requestedResource);

            //Inform the threads of the new culture.
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

            #region English Fallback of the Current Language

            //Only non-English resources need a fallback, because the English resource is evergreen. TODO
            if (culture.StartsWith("en"))
                return;

            var englishResource = dictionaryList.FirstOrDefault(d => d.Source?.OriginalString == "/Resources/Localization/StringResources.en.xaml");

            if (englishResource != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(englishResource);
                Application.Current.Resources.MergedDictionaries.Insert(Application.Current.Resources.MergedDictionaries.Count - 1, englishResource);
            }

            #endregion

            GC.Collect(0);

            if (!UserSettings.All.CheckForTranslationUpdates)
                return;
        }       

        public static void SaveDefaultResource(string path)
        {
            //Copy all MergedDictionarys into a auxiliar list.
            var dictionaryList = Application.Current.Resources.MergedDictionaries.ToList();

            try
            {
                //Search for the specified culture.
                var resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source?.OriginalString == "/Resources/Localization/StringResources.en.xaml");

                if (resourceDictionary == null)
                    throw new CultureNotFoundException("String resource not found.");

                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException("Path is null.");

                var settings = new XmlWriterSettings { Indent = true };

                using (var writer = XmlWriter.Create(path, settings))
                    System.Windows.Markup.XamlWriter.Save(resourceDictionary, writer);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Save Xaml Resource Error");

                Dialog.Ok("Impossible to Save", "Impossible to save the Xaml file", ex.Message, Icons.Warning);
            }
        }

        public static void ImportStringResource(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException("Path is null");

                var destination = Path.Combine(Path.GetTempPath(), Path.GetFileName(path));

                if (File.Exists(destination))
                    File.Delete(destination);

                File.WriteAllText(destination, File.ReadAllText(path).Replace("&#x0d;", "\r"));

                using (var fs = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length == 0)
                        throw new InvalidDataException("File is empty");

                    //Reads the ResourceDictionary file
                    var dictionary = (ResourceDictionary)XamlReader.Load(fs);
                    dictionary.Source = new Uri(destination);

                    //Add in newly loaded Resource Dictionary.
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Import Resource");
                //Rethrowing, because it's more useful to catch later
                throw;
            }
        }

        public static List<ResourceDictionary> GetLocalizations()
        {
            //Copy all MergedDictionarys into a auxiliar list.
            var dictionaryList = Application.Current.Resources.MergedDictionaries.ToList();

            return dictionaryList.Where(x => x.Source.OriginalString.Contains("StringResource")).ToList();
        }

        public static bool Move(int selectedIndex, bool toUp = true)
        {
            try
            {
                if (toUp && selectedIndex < 1)
                    return false;

                if (!toUp && selectedIndex > Application.Current.Resources.MergedDictionaries.Count - 1)
                    return false;

                //Recover selected dictionary.
                var dictionaryAux = Application.Current.Resources.MergedDictionaries[selectedIndex];

                //Remove from the current list.
                Application.Current.Resources.MergedDictionaries.Remove(Application.Current.Resources.MergedDictionaries[selectedIndex]);

                //Insert at the upper position.
                Application.Current.Resources.MergedDictionaries.Insert(toUp ? selectedIndex - 1 : selectedIndex + 1, dictionaryAux);

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Move Resource", selectedIndex);
                return false;
            }
        }

        public static void SaveSelected(int selectedIndex, string path)
        {
            try
            {
                if (selectedIndex < 0 || selectedIndex > Application.Current.Resources.MergedDictionaries.Count - 1)
                    throw new IndexOutOfRangeException("Index out of range while trying to save the resource dictionary.");

                var settings = new XmlWriterSettings { Indent = true };

                using (var writer = XmlWriter.Create(path, settings))
                    System.Windows.Markup.XamlWriter.Save(Application.Current.Resources.MergedDictionaries[selectedIndex], writer);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Save Resource", selectedIndex);
                //Rethrowing, because it's more useful to catch later
                throw;
            }
        }

        public static bool Remove(int selectedIndex)
        {
            try
            {
                if (selectedIndex == -1 || selectedIndex > Application.Current.Resources.MergedDictionaries.Count - 1)
                    return false;

                if (Application.Current.Resources.MergedDictionaries[selectedIndex].Source.OriginalString.Contains("StringResources.xaml"))
                    return false;

                //Remove from the current list.
                Application.Current.Resources.MergedDictionaries.RemoveAt(selectedIndex);

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Remove Resource", selectedIndex);
                return false;
            }
        }

        /// <summary>
        /// Gets a resource as string.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <param name="removeNewLines">If true, it removes any kind of new lines.</param>
        /// <returns>A string resource, usually a localized string.</returns>
        public static string Get(string key, bool removeNewLines = false)
        {
            if (removeNewLines)
                return (Application.Current.TryFindResource(key) as string ?? "").Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " ");

            return Application.Current.TryFindResource(key) as string;
        }

        /// <summary>
        /// Gets a resource as string and applies the format.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <param name="values">The values for the string format.</param>
        /// <returns>A string resource, usually a localized string.</returns>
        public static string GetWithFormat(string key, params object[] values)
        {
            return string.Format(Thread.CurrentThread.CurrentUICulture, Application.Current.TryFindResource(key) as string ?? "", values);
        }

        /// <summary>
        /// Gets a resource as string.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <param name="defaultValue">The default value in english.</param>
        /// <param name="removeNewLines">If true, it removes any kind of new lines.</param>
        /// <returns>A string resource, usually a localized string.</returns>
        public static string Get(string key, string defaultValue, bool removeNewLines = false)
        {
            if (removeNewLines)
                return (Application.Current.TryFindResource(key) as string ?? defaultValue).Replace("\n", " ").Replace("\\n", " ").Replace("\r", " ").Replace("&#10;", " ").Replace("&#x0d;", " ");

            return Application.Current.TryFindResource(key) as string ?? defaultValue;
        }

        /// <summary>
        /// Gets a resource as string and applies the format.
        /// </summary>
        /// <param name="key">The key of the string resource.</param>
        /// <param name="defaultValue">The default value in english.</param>
        /// <param name="values">The values for the string format.</param>
        /// <returns>A string resource, usually a localized string.</returns>
        public static string GetWithFormat(string key, string defaultValue, params object[] values)
        {
            return string.Format(Thread.CurrentThread.CurrentUICulture, Application.Current.TryFindResource(key) as string ?? defaultValue, values);
        }
    }
}
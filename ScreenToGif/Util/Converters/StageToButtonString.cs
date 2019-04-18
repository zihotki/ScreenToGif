﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScreenToGif.Util.Converters
{
    public class StageToButtonString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var stage = value as Stage?;

            if (!stage.HasValue)
                return Application.Current.FindResource("Recorder.Record");

            switch (stage)
            {
                case Stage.Stopped:
                    return Application.Current.FindResource("Recorder.Record");
                case Stage.Recording:
                    return Application.Current.FindResource("Recorder.Pause");
                case Stage.Paused:
                    return Application.Current.FindResource("Recorder.Continue");
            }

            return Application.Current.FindResource("Recorder.Record");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}

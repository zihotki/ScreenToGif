using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using ScreenToGif.Util;

namespace ScreenToGif.Model
{
    public class ProjectInfo
    {
        public string RelativePath { get; }

        public DateTime CreationDate { get; } = DateTime.Now;

        public ObservableCollection<FrameInfo> Frames { get; } = new ObservableCollection<FrameInfo>();

        public string FullPathOfProject => Path.Combine(UserSettings.All.TemporaryFolder, "ScreenToGif", "Recording", RelativePath);

        public bool AnyFrames => Frames?.Any() == true;

        public Int32Rect FrameSize { get; }

        public ProjectInfo(Int32Rect frameSize)
        {
            //Check if the parameter exists.
            if (string.IsNullOrWhiteSpace(UserSettings.All.TemporaryFolder))
            {
                UserSettings.All.TemporaryFolder = Path.GetTempPath();
            }

            RelativePath = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + Path.DirectorySeparatorChar;
            FrameSize = frameSize;

            Directory.CreateDirectory(FullPathOfProject);
            MutexList.Add(RelativePath);
        }

        public void Clear()
        {
            Frames?.Clear();

            MutexList.Remove(RelativePath);
        }

        public string FilenameOf(int index)
        {
            return Path.Combine(FullPathOfProject, index.ToString());
        }

        public int GetClosestValidIndex(int index)
        {
            return (Frames.LastOrDefault(x => index > x.Index)
                ?? Frames.FirstOrDefault())
                ?.Index
                ?? -1;
        }
    }
}
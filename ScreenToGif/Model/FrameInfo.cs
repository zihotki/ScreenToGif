namespace ScreenToGif.Model
{
    public class FrameInfo
    {
        public string FullPath { get; }

        public int Delay { get; }

        public int Index { get; }

        public FrameInfo(string fullPath, int index, int delay)
        {
            FullPath = fullPath;
            Delay = delay;
            Index = index;
        }
    }
}
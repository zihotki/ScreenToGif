using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScreenToGif.Model;

namespace ScreenToGif.Util
{
    /// <summary>
    /// Do, Undo, Redo stack.
    /// </summary>
    public static class ActionStack
    {
        private static readonly Stack<StateChange> StackToUndo = new Stack<StateChange>();
        private static readonly Stack<StateChange> StackToRedo = new Stack<StateChange>();

        private static ProjectInfo _project;

        public static ProjectInfo Project
        {
            get { return _project; }
            set
            {
                _project = value;
                Reset();
            }
        }
        
        private class StateChange
        {
            public List<FrameInfo> RemovedFrames { get; set; }
        }

        public static void Remove(List<FrameInfo> frames)
        {
            StackToUndo.Push(new StateChange { RemovedFrames = frames });
        }

        public static void Undo()
        {
            if (StackToUndo.Count > 0)
            {
                var operation = StackToUndo.Pop();
                StackToRedo.Push(operation);

                var framesToUndo = operation.RemovedFrames;
                Project.Frames = Project.Frames
                    .Union(framesToUndo)
                    .OrderBy(x => x.Index)
                    .ToList();
            }
        }

        public static void Redo()
        {
            if (StackToRedo.Count > 0)
            {
                var operation = StackToRedo.Pop();
                StackToUndo.Push(operation);

                var framesToRedo = operation.RemovedFrames;
                Project.Frames = Project.Frames
                    .Except(framesToRedo)
                    .OrderBy(x => x.Index)
                    .ToList();
            }
        }

        public static void Reset()
        {
            while (CanUndo())
            {
                Undo();
            }

            StackToRedo.Clear();
        }

        public static void Clear()
        {
            StackToRedo.Clear();
            StackToUndo.Clear();
        }

        public static bool CanUndo()
        {
            return StackToUndo.Count > 0;
        }

        public static bool CanRedo()
        {
            return StackToRedo.Count > 0;
        }

        public static bool CanReset()
        {
            return StackToUndo.Count > 0;
        }
    }
}

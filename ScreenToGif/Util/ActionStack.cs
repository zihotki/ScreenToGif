using System.Collections.Generic;
using System.Linq;
using ScreenToGif.Model;

namespace ScreenToGif.Util
{
    public class ActionStack
    {
        private readonly Stack<StateChange> _stackToUndo = new Stack<StateChange>();
        private readonly Stack<StateChange> _stackToRedo = new Stack<StateChange>();

        public ProjectInfo Project { get; }

        private readonly List<FrameInfo> _allFrames;

        public ActionStack(ProjectInfo project)
        {
            Project = project;
            _allFrames = project.Frames.ToList();
        }

        public void Remove(List<FrameInfo> frames)
        {
            _stackToUndo.Push(new StateChange { RemovedFrames = frames });
        }

        public void Undo()
        {
            if (_stackToUndo.Count > 0)
            {
                var operation = _stackToUndo.Pop();
                _stackToRedo.Push(operation);

                var framesToUndo = operation.RemovedFrames;

                // TODO:
                /*Project.Frames = Project.Frames
                    .Union(framesToUndo)
                    .OrderBy(x => x.Index)
                    .ToList();*/
            }
        }

        public void Redo()
        {
            if (_stackToRedo.Count > 0)
            {
                var operation = _stackToRedo.Pop();
                _stackToUndo.Push(operation);

                var framesToRedo = operation.RemovedFrames;
                // TODO:
                /*
                Project.Frames = Project.Frames
                    .Except(framesToRedo)
                    .OrderBy(x => x.Index)
                    .ToList();*/
            }
        }

        public void Reset()
        {
            if (CanUndo())
            {
                // TODO
                _stackToRedo.Clear();
                _stackToUndo.Clear();
            }
            while (CanUndo())
            {
                Undo();
            }

            _stackToRedo.Clear();
        }

        public void Dispose()
        {
            _stackToRedo.Clear();
            _stackToUndo.Clear();
            Project.Clear();
        }

        public bool CanUndo()
        {
            return _stackToUndo.Count > 0;
        }

        public bool CanRedo()
        {
            return _stackToRedo.Count > 0;
        }

        public bool CanReset()
        {
            return _stackToUndo.Count > 0;
        }

        private class StateChange
        {
            public List<FrameInfo> RemovedFrames { get; set; }
        }
    }
}

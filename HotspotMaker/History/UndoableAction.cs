using System;

namespace HotspotMaker.History
{
    public class UndoableAction
    {
        private Action DoAction { get; }
        private Action UndoAction { get; }


        public UndoableAction(Action doAction, Action undoAction)
        {
            DoAction = doAction;
            UndoAction = undoAction;
        }

        public void Do()
            => DoAction();

        public void Undo()
            => UndoAction();
    }
}

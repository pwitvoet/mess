using System;
using System.Collections.Generic;
using System.Linq;

namespace HotspotMaker.History
{
    // TODO: Handle exceptions that are thrown by do/undo functions!

    /// <summary>
    /// An undo system tracks changes, and allows those changes to be undone and redone.
    /// </summary>
    public class UndoSystem
    {
        public event Action? OnActionDone;
        public event Action? OnActionUndone;
        public event Action? OnActionRedone;

        public bool IsUndoAvailable => UndoableActions.Any();
        public bool IsRedoAvailable => RedoableActions.Any();


        // TODO: Internal list of undo/redo actions!
        private Stack<UndoableAction> UndoableActions { get; } = new();
        private Stack<UndoableAction> RedoableActions { get; } = new();


        /// <summary>
        /// Executes the given 'do' action, and stores the do and undo actions so they can be undone and redone in the future.
        /// </summary>
        public void PerformUndoableAction(Action doAction, Action undoAction)
        {
            var undoableAction = new UndoableAction(doAction, undoAction);
            undoableAction.Do();
            UndoableActions.Push(undoableAction);
            RedoableActions.Clear();

            OnActionDone?.Invoke();
        }

        /// <summary>
        /// Executes the given 'do' action, and stores the do and undo actions so they can be undone and redone in the future.
        /// <para>
        /// This replaces the top-most undoable action. Use this for actions that consist of many small steps, such as resizing
        /// an object or changing a name, where each step changes the state of an object, but all the steps together should be
        /// treated as a single undoable action. Call <see cref="PerformUndoableAction(Action, Action)"/> for the first step,
        /// and <see cref="ReplaceCurrentUndoableAction(Action, Action)"/> for each subsequent step.
        /// </para>
        /// </summary>
        public void ReplaceCurrentUndoableAction(Action doAction, Action undoAction)
        {
            if (UndoableActions.Any())
                UndoableActions.Pop();

            PerformUndoableAction(doAction, undoAction);
        }

        /// <summary>
        /// Rolls back the most recent undoable action.
        /// </summary>
        public void UndoLastAction()
        {
            if (UndoableActions.Any())
            {
                var undoableAction = UndoableActions.Pop();
                undoableAction.Undo();
                RedoableActions.Push(undoableAction);

                OnActionUndone?.Invoke();
            }
        }

        /// <summary>
        /// Performs the most recently undone action again.
        /// </summary>
        public void RedoLastAction()
        {
            if (RedoableActions.Any())
            {
                var undoableAction = RedoableActions.Pop();
                undoableAction.Do();
                UndoableActions.Push(undoableAction);

                OnActionRedone?.Invoke();
            }
        }
    }
}

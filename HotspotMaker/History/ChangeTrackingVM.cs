using HotspotMaker.Util.UI;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotMaker.History
{
    /// <summary>
    /// Base class for view models that need undo/redo support.
    /// </summary>
    public abstract class ChangeTrackingVM : INotifyPropertyChanged, IFocusTrackingVM
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        void IFocusTrackingVM.FocustLost(string propertyName)
        {
            if (_ongoingActionPropertyName == propertyName)
                StopOngoingAction();
        }


        public virtual bool IsModified => CurrentStateID != UnmodifiedStateID;


        // Internal state:
        protected UndoSystem UndoSystem { get; }
        private bool SuppressChangeTracking { get; set; }

        private int _currentStateID;
        private int CurrentStateID
        {
            get => _currentStateID;
            set { _currentStateID = value; RaisePropertyChanged(nameof(IsModified)); }
        }

        private int _unmodifiedStateID;
        private int UnmodifiedStateID
        {
            get => _unmodifiedStateID;
            set { _unmodifiedStateID = value; RaisePropertyChanged(nameof(IsModified)); }
        }

        private string? _ongoingActionPropertyName;
        private Action? _ongoingActionUndo;


        public ChangeTrackingVM(UndoSystem undoSystem)
        {
            UndoSystem = undoSystem;

            undoSystem.OnActionUndone += UndoSystem_OnActionUndone;
            undoSystem.OnActionRedone += UndoSystem_OnActionRedone;
        }

        /// <summary>
        /// Sets the current state of this view model as the unmodified state.
        /// </summary>
        public virtual void MarkAsUnmodified()
        {
            UnmodifiedStateID = CurrentStateID;
        }


        /// <summary>
        /// Performs an undoable action. This will stop any ongoing action.
        /// </summary>
        protected void PerformUndoableAction(Action doAction, Action undoAction)
        {
            var oldStateID = CurrentStateID;
            var newStateID = CurrentStateID + 1;

            StopOngoingAction();
            UndoSystem.PerformUndoableAction(
                () =>
                {
                    doAction();
                    CurrentStateID = newStateID;
                },
                () =>
                {
                    undoAction();
                    CurrentStateID = oldStateID;
                });
        }

        /// <summary>
        /// Performs an undoable action.
        /// Subsequent calls to this method will update the registered undoable action instead of creating multiple undoable actions,
        /// so that all changes can be undone in one go. Also see <see cref="StopOngoingAction"/>.
        /// </summary>
        protected void PerformUndoableActionOngoing(string actionName, Action doAction, Action undoAction)
        {
            if (SuppressChangeTracking)
            {
                doAction();
                return;
            }

            if (_ongoingActionPropertyName != null && _ongoingActionUndo != null && _ongoingActionPropertyName == actionName)
            {
                // NOTE: This was already incremented when the ongoing action got started, so don't increment it again:
                var newStateID = CurrentStateID;

                UndoSystem.ReplaceCurrentUndoableAction(
                    () =>
                    {
                        doAction();
                        CurrentStateID = newStateID;
                    },
                    _ongoingActionUndo);
            }
            else
            {
                var oldStateID = CurrentStateID;
                var newStateID = CurrentStateID + 1;

                _ongoingActionPropertyName = actionName;
                _ongoingActionUndo = () =>
                {
                    undoAction();
                    CurrentStateID = oldStateID;
                };

                UndoSystem.PerformUndoableAction(
                    () =>
                    {
                        doAction();
                        CurrentStateID = newStateID;
                    },
                    _ongoingActionUndo);
            }
        }

        /// <summary>
        /// Updates a property and registers the change with the undo system so that it can be undone.
        /// This will stop any ongoing action.
        /// </summary>
        protected void SetProperty<TValue>(Action<TValue> setter, TValue oldValue, TValue newValue, [CallerMemberName] string? propertyName = null)
        {
            if (SuppressChangeTracking)
            {
                setter(newValue);
                RaisePropertyChanged(propertyName);
                return;
            }

            var oldStateID = CurrentStateID;
            var newStateID = CurrentStateID + 1;

            StopOngoingAction();
            UndoSystem.PerformUndoableAction(
                () =>
                {
                    setter(newValue);
                    CurrentStateID = newStateID;
                    RaisePropertyChanged(propertyName);
                },
                () =>
                {
                    setter(oldValue);
                    CurrentStateID = oldStateID;
                    RaisePropertyChanged(propertyName);
                });
        }

        /// <summary>
        /// Updates a property and registers the change with the undo system so that it can be undone.
        /// Subsequent calls to this method will update the registered undoable action instead of creating multiple undoable actions,
        /// so that all changes can be undone in one go. Also see <see cref="StopOngoingAction"/>.
        /// </summary>
        protected void SetPropertyOngoing<TValue>(Action<TValue> setter, TValue oldValue, TValue newValue, [CallerMemberName] string? propertyName = null)
        {
            if (SuppressChangeTracking)
            {
                setter(newValue);
                RaisePropertyChanged(propertyName);
                return;
            }

            if (_ongoingActionPropertyName != null && _ongoingActionUndo != null && _ongoingActionPropertyName == propertyName)
            {
                // NOTE: This was already incremented when the ongoing action got started, so don't increment it again:
                var newStateID = CurrentStateID;

                UndoSystem.ReplaceCurrentUndoableAction(
                    () =>
                    {
                        setter(newValue);
                        CurrentStateID = newStateID;
                        RaisePropertyChanged(propertyName);
                    },
                    _ongoingActionUndo);
            }
            else
            {
                var oldStateID = CurrentStateID;
                var newStateID = CurrentStateID + 1;

                _ongoingActionPropertyName = propertyName;
                _ongoingActionUndo = () =>
                {
                    setter(oldValue);
                    CurrentStateID = oldStateID;
                    RaisePropertyChanged(propertyName);
                };

                UndoSystem.PerformUndoableAction(
                    () =>
                    {
                        setter(newValue);
                        CurrentStateID = newStateID;
                        RaisePropertyChanged(propertyName);
                    },
                    _ongoingActionUndo);
            }
        }

        /// <summary>
        /// Stops the currently ongoing action, if there is any.
        /// Subsequent calls to <see cref="SetPropertyOngoing{TValue}(Action{TValue}, TValue, TValue, string?)"/> will create a new undoable action.
        /// </summary>
        protected void StopOngoingAction()
        {
            _ongoingActionPropertyName = null;
            _ongoingActionUndo = null;
        }

        /// <summary>
        /// Executes the given action without change tracking. Useful for initializing properties.
        /// </summary>
        protected void WithoutChangeTracking(Action action)
        {
            try
            {
                SuppressChangeTracking = true;
                action();
            }
            finally
            {
                SuppressChangeTracking = false;
            }
        }


        private void UndoSystem_OnActionUndone()
            => StopOngoingAction();

        private void UndoSystem_OnActionRedone()
            => StopOngoingAction();
    }
}

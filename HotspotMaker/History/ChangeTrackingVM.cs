using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotMaker.History
{
    /// <summary>
    /// Base class for view models that need undo/redo support.
    /// </summary>
    public abstract class ChangeTrackingVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        // Internal state:
        protected UndoSystem UndoSystem { get; }
        private bool SuppressChangeTracking { get; set; }

        private string? _ongoingActionPropertyName;
        private Action? _ongoingActionUndo;


        public ChangeTrackingVM(UndoSystem undoSystem)
        {
            UndoSystem = undoSystem;

            undoSystem.OnActionUndone += UndoSystem_OnActionUndone;
            undoSystem.OnActionRedone += UndoSystem_OnActionRedone;
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

            StopOngoingAction();
            UndoSystem.PerformUndoableAction(
                () =>
                {
                    setter(newValue);
                    RaisePropertyChanged(propertyName);
                },
                () =>
                {
                    setter(oldValue);
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
                UndoSystem.ReplaceCurrentUndoableAction(
                    () =>
                    {
                        setter(newValue);
                        RaisePropertyChanged(propertyName);
                    },
                    _ongoingActionUndo);
            }
            else
            {
                _ongoingActionPropertyName = propertyName;
                _ongoingActionUndo = () =>
                {
                    setter(oldValue);
                    RaisePropertyChanged(propertyName);
                };

                UndoSystem.PerformUndoableAction(
                    () =>
                    {
                        setter(newValue);
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

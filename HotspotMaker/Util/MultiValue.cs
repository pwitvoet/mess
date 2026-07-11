using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotMaker.Util
{
    public class MultiValue<TValue> : INotifyPropertyChanged
        where TValue : struct
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        private TValue? _value;
        public TValue? Value
        {
            get => _value;
            set
            {
                _value = value;

                if (value != null)
                    SetValue(value.Value);

                RaisePropertyChanged();
            }
        }

        private bool _hasMultipleValues;
        public bool HasMultipleValues
        {
            get => _hasMultipleValues;
            private set => _hasMultipleValues = value;
        }


        private Action<TValue> SetValue { get; }


        public MultiValue(Action<TValue> setValue)
        {
            SetValue = setValue;
        }

        public void SetSingleValue(TValue value)
        {
            _value = value;
            _hasMultipleValues = false;

            RaisePropertyChanged(nameof(Value));
            RaisePropertyChanged(nameof(HasMultipleValues));
        }

        public void SetMultiValue()
        {
            _value = null;
            _hasMultipleValues = true;

            RaisePropertyChanged(nameof(Value));
            RaisePropertyChanged(nameof(HasMultipleValues));
        }
    }
}

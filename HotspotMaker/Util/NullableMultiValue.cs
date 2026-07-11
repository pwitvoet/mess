using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotMaker.Util
{

    public class NullableMultiValue<TValue> : INotifyPropertyChanged
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
                    SetValue(value);

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


        public NullableMultiValue(Action<TValue> setValue)
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
            _value = default;
            _hasMultipleValues = true;

            RaisePropertyChanged(nameof(Value));
            RaisePropertyChanged(nameof(HasMultipleValues));
        }
    }
}

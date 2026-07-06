using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;
using System.Globalization;

namespace HotspotMaker.Controls
{
    public partial class LabelsTextBox : UserControl
    {
        public static readonly DirectProperty<LabelsTextBox, string[]?> LabelsProperty = AvaloniaProperty.RegisterDirect<LabelsTextBox, string[]?>(
            nameof(Labels),
            o => o.Labels,
            (o, v) => o.Labels = v,
            defaultBindingMode: BindingMode.TwoWay);

        // NOTE: This property is only for internal use:
        public static readonly DirectProperty<LabelsTextBox, string?> LabelsTextProperty = AvaloniaProperty.RegisterDirect<LabelsTextBox, string?>(
            nameof(LabelsText),
            o => o.LabelsText,
            (o, v) => o.LabelsText = v,
            defaultBindingMode: BindingMode.TwoWay);


        private string[]? _labels;
        public string[]? Labels
        {
            get => _labels;
            set
            {
                SetAndRaise(LabelsProperty, ref _labels, value);

                if (!IgnoreLabelsChanges)
                    UpdateInternalTextBox();
            }
        }

        // NOTE: This property is only for internal use:
        private string? _labelsText;
        public string? LabelsText
        {
            get => _labelsText;
            set
            {
                SetAndRaise(LabelsTextProperty, ref _labelsText, value);

                if (!IgnoreInternalTextBoxChanges)
                    UpdateLabels();
            }
        }


        private bool IgnoreLabelsChanges { get; set; }
        private bool IgnoreInternalTextBoxChanges { get; set; }


        public LabelsTextBox()
        {
            InitializeComponent();
        }


        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var text = InternalTextBox.Text ?? "";
            var offsetX = InternalTextBox.Margin.Left + InternalTextBox.BorderThickness.Left + InternalTextBox.Padding.Left;
            var offsetY = InternalTextBox.Margin.Top + InternalTextBox.BorderThickness.Top + InternalTextBox.Padding.Top;

            if (!FontManager.Current.TryMatchCharacter('a', InternalTextBox.FontStyle, InternalTextBox.FontWeight, InternalTextBox.FontStretch, InternalTextBox.FontFamily, CultureInfo.CurrentUICulture, out var typeface))
                typeface = Typeface.Default;
            var fontSize = InternalTextBox.FontSize;

            var id = 0;
            foreach ((var index, var length) in GetWordBoundaries(text))
            {
                var isAtCaret = InternalTextBox.IsFocused && InternalTextBox.CaretIndex >= index && InternalTextBox.CaretIndex <= index + length;
                var word = text.Substring(index, length);

                var precedingText = new FormattedText(text.Substring(0, index), CultureInfo.CurrentUICulture, InternalTextBox.FlowDirection, typeface, fontSize, Brushes.Black);
                var currentWord = new FormattedText(word, CultureInfo.CurrentUICulture, InternalTextBox.FlowDirection, typeface, fontSize, Brushes.Black);

                // TODO: Make label colors configurable (for commonly used labels, at least)!
                var color = (id % 2 == 0) ? Color.FromUInt32(0xFF404040) : Color.FromUInt32(0xFF606060);
                var cornerRadius = 2;
                context.FillRectangle(
                    new SolidColorBrush(color),
                    new Rect(offsetX + precedingText.WidthIncludingTrailingWhitespace - 1, offsetY - 1, currentWord.Width + 2, currentWord.Height + 2),
                    cornerRadius);

                id += 1;
            }
        }


        private void InternalTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!IgnoreInternalTextBoxChanges)
                UpdateLabels();

            InvalidateVisual();
        }

        private void InternalTextBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.CaretIndexProperty)
                InvalidateVisual();
        }

        private void InternalTextBox_GotFocus(object? sender, FocusChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LabelsText) && !LabelsText.EndsWith(' '))
            {
                try
                {
                    // Make it easier to start with a new label by adding a space at the end:
                    IgnoreInternalTextBoxChanges = true;
                    LabelsText += " ";
                }
                finally
                {
                    IgnoreInternalTextBoxChanges = false;
                }
            }

            InvalidateVisual();
        }

        private void InternalTextBox_LostFocus(object? sender, FocusChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void UpdateLabels()
        {
            try
            {
                IgnoreLabelsChanges = true;
                Labels = (InternalTextBox.Text ?? "").Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            }
            finally
            {
                IgnoreLabelsChanges = false;
            }
        }

        private void UpdateInternalTextBox()
        {
            try
            {
                IgnoreInternalTextBoxChanges = true;

                var oldText = _labelsText;
                var newText = Labels != null ? string.Join(" ", Labels) : "";

                _labelsText = newText;
                RaisePropertyChanged(LabelsTextProperty, oldText, newText);
            }
            finally
            {
                IgnoreInternalTextBoxChanges = false;
            }
        }


        private static IEnumerable<(int, int)> GetWordBoundaries(string text)
        {
            var isPreviousWhitespace = true;
            var currentWordStart = 0;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    if (!isPreviousWhitespace)
                        yield return (currentWordStart, i - currentWordStart);

                    isPreviousWhitespace = true;
                }
                else
                {
                    if (isPreviousWhitespace)
                        currentWordStart = i;

                    isPreviousWhitespace = false;
                }
            }

            if (!isPreviousWhitespace)
                yield return (currentWordStart, text.Length - currentWordStart);
        }
    }
}
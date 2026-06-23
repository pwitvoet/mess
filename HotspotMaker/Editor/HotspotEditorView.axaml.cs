using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HotspotMaker.Hotspot;
using System;
using System.ComponentModel;

namespace HotspotMaker.Editor;

public partial class HotspotEditorView : UserControl
{
    public event Action<HotspotRectangleVM>? RectangleClicked;


    // Editor state:
    private HotspotEditorVM? Editor { get; set; }

    private Point _cameraOffset;
    private Point CameraOffset
    {
        get => _cameraOffset;
        set { _cameraOffset = value; InvalidateVisual(); }
    }

    private double _cameraScale = 1.5;
    private double CameraScale
    {
        get => _cameraScale;
        set { _cameraScale = value; InvalidateVisual(); }
    }

    private bool _showGrid = true;
    private bool IsGridEnabled
    {
        get => _showGrid;
        set { _showGrid = value; InvalidateVisual(); }
    }

    private double _gridSize = 16;
    private double GridSize
    {
        get => _gridSize;
        set { _gridSize = value; InvalidateVisual(); }
    }

    private PointerButtons PointerState { get; set; }
    private Point PreviousPointerPosition { get; set; }

    private Size PreviousTextureSize { get; set; }


    // Brushes and pens:
    private Brush BackgroundBrush { get; } = new SolidColorBrush(0xFF404040);
    private Pen GridPen { get; } = new Pen(0x20FFFFFF);

    private Brush RectangleBrush { get; } = new SolidColorBrush(0x60F0F0FF);
    private Pen RectangleBorderPen { get; } = new Pen(0xFFFFFFFF, 2);

    private Brush SelectedRectangleBrush { get; } = new SolidColorBrush(0x60FFF0F0);
    private Pen SelectedRectangleBorderPen { get; } = new Pen(0xFFFF0000, 2);


    public HotspotEditorView()
    {
        InitializeComponent();

        // We want nice crispy pixels when zooming in:
        RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
    }


    public void HandleKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.G)
        {
            // Toggle grid with 'g'
            IsGridEnabled = !IsGridEnabled;

            e.Handled = true;
        }
        else if (e.Key == Key.OemOpenBrackets)
        {
            // Decrease grid size with '['
            if (GridSize > 1)
                GridSize /= 2;

            e.Handled = true;
        }
        else if (e.Key == Key.OemCloseBrackets)
        {
            // Increase grid size with ']'
            if (GridSize < 1024)
                GridSize *= 2;

            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(BackgroundBrush, new Rect(0, 0, Bounds.Width, Bounds.Height));

        var editor = Editor;
        if (editor != null)
        {
            DrawTexture(context, editor);
            DrawHotspotRectangles(context, editor);
        }

        DrawGrid(context);
    }

    private void DrawTexture(DrawingContext context, HotspotEditorVM editor)
    {
        var textureImage = editor.TextureImage;
        if (textureImage == null)
            return;


        context.DrawImage(textureImage, new Rect(CameraOffset.X, CameraOffset.Y, textureImage.Size.Width * CameraScale, textureImage.Size.Height * CameraScale));
    }

    // TODO: Draw the various hotspot rectangle settings! (snap distances as sub-rects, horizontal/vertical tiling as open-ended sides (no edge), tiling/rotating as little icons maybe? etc!)
    private void DrawHotspotRectangles(DrawingContext context, HotspotEditorVM editor)
    {
        var rectangleSet = editor.RectangleSet;
        if (rectangleSet == null)
            return;


        foreach (var rectangle in rectangleSet.Rectangles)
        {
            var isSelected = rectangle == editor.SelectedRectangle;
            context.FillRectangle(
                isSelected ? SelectedRectangleBrush : RectangleBrush,
                new Rect(
                    CameraOffset.X + (rectangle.X * CameraScale),
                    CameraOffset.Y + (rectangle.Y * CameraScale),
                    rectangle.Width * CameraScale,
                    rectangle.Height * CameraScale));

            context.DrawRectangle(
                RectangleBorderPen,
                new Rect(
                    CameraOffset.X + (rectangle.X * CameraScale),
                    CameraOffset.Y + (rectangle.Y * CameraScale),
                    rectangle.Width * CameraScale,
                    rectangle.Height * CameraScale));
        }

        var selectedRectangle = editor.SelectedRectangle;
        if (selectedRectangle != null)
        {
            context.DrawRectangle(
                SelectedRectangleBorderPen,
                new Rect(
                    CameraOffset.X + (selectedRectangle.X * CameraScale),
                    CameraOffset.Y + (selectedRectangle.Y * CameraScale),
                    selectedRectangle.Width * CameraScale,
                    selectedRectangle.Height * CameraScale));
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        if (!IsGridEnabled || GridSize < 1)
            return;


        var cameraSpaceBounds = new Rect(
            -CameraOffset.X / CameraScale,
            -CameraOffset.Y / CameraScale,
            Bounds.Width / CameraScale,
            Bounds.Height / CameraScale);

        var minX = Math.Round(cameraSpaceBounds.X / GridSize) * GridSize;
        var maxX = Math.Round((cameraSpaceBounds.X + cameraSpaceBounds.Width) / GridSize) * GridSize;
        var minY = Math.Round(cameraSpaceBounds.Y / GridSize) * GridSize;
        var maxY = Math.Round((cameraSpaceBounds.Y + cameraSpaceBounds.Height) / GridSize) * GridSize;

        for (var x = minX; x <= maxX; x += GridSize)
        {
            var xPos = Math.Round(CameraOffset.X + x * CameraScale) + 0.5;
            context.DrawLine(GridPen, new Point(xPos, 0), new Point(xPos + 0.5, Bounds.Height));
        }

        for (var y = minY; y <= maxY; y += GridSize)
        {
            var yPos = Math.Round(CameraOffset.Y + y * CameraScale) + 0.5;
            context.DrawLine(GridPen, new Point(0, yPos), new Point(Bounds.Width, yPos));
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (Editor != null)
        {
            Editor.PropertyChanged -= Editor_PropertyChanged;
            Editor.RectanglePropertyChanged -= Editor_RectanglePropertyChanged;
        }

        Editor = DataContext as HotspotEditorVM;

        if (Editor != null)
        {
            Editor.PropertyChanged += Editor_PropertyChanged;
            Editor.RectanglePropertyChanged += Editor_RectanglePropertyChanged;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // Keep the view centered:
        var sizeChange = e.NewSize - e.PreviousSize;
        CameraOffset += new Point(sizeChange.Width / 2, sizeChange.Height / 2);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pointerPosition = e.GetPosition(this);
        UpdatePointerState(pointerPosition, e.Properties);

        var delta = pointerPosition - PreviousPointerPosition;
        PreviousPointerPosition = pointerPosition;

        HandlePointerMovement(pointerPosition, delta);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        UpdatePointerState(e.GetPosition(this), e.Properties);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        UpdatePointerState(e.GetPosition(this), e.Properties);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        HandlePointerWheelChange(e.GetPosition(this), e.Delta);
    }

    // NOTE: This method gets called only when the editor view has focus.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var selectedRectangle = Editor?.SelectedRectangle;
        if (selectedRectangle != null)
        {
            // TODO: Snap to grid instead?
            var distance = IsGridEnabled ? GridSize : 1;
            var movement = new Vector(0, 0);
            if (e.Key == Key.Up)
                movement -= new Vector(0, distance);
            if (e.Key == Key.Down)
                movement += new Vector(0, distance);
            if (e.Key == Key.Left)
                movement -= new Vector(distance, 0);
            if (e.Key == Key.Right)
                movement += new Vector(distance, 0);

            if (movement.X != 0 || movement.Y != 0)
            {
                selectedRectangle.Move(movement);
            }
        }
    }


    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotspotEditorVM.TextureImage))
        {
            // Adjust camera position somewhat if the new texture has a different size:
            var newTextureSize = Editor?.TextureImage?.Size ?? new Size();
            var oldTextureSize = PreviousTextureSize;
            PreviousTextureSize = newTextureSize;

            var cameraMovement = (oldTextureSize - newTextureSize) / 2 * CameraScale;
            CameraOffset += new Point(cameraMovement.Width, cameraMovement.Height);

            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(HotspotEditorVM.RectangleSet) ||
            e.PropertyName == nameof(HotspotEditorVM.SelectedRectangle))
        {
            InvalidateVisual();
        }
    }

    private void Editor_RectanglePropertyChanged(HotspotRectangleVM sender, string? propertyName)
    {
        InvalidateVisual();
    }


    private void UpdatePointerState(Point position, PointerPointProperties pointer)
    {
        CheckButtonState(PointerButtons.Left, pointer.IsLeftButtonPressed);
        CheckButtonState(PointerButtons.Middle, pointer.IsMiddleButtonPressed);
        CheckButtonState(PointerButtons.Right, pointer.IsRightButtonPressed);
        CheckButtonState(PointerButtons.X1, pointer.IsXButton1Pressed);
        CheckButtonState(PointerButtons.X2, pointer.IsXButton2Pressed);


        void CheckButtonState(PointerButtons button, bool isPressed)
        {
            if (PointerState.HasFlag(button) != isPressed)
            {
                if (isPressed)
                    PointerState |= button;
                else
                    PointerState &= ~button;

                HandlePointerButtonChange(position, button, isPressed);
            }
        }
    }


    private void HandlePointerMovement(Point position, Vector delta)
    {
        if (PointerState.HasFlag(PointerButtons.Right))
        {
            CameraOffset += delta;
        }
    }

    private void HandlePointerButtonChange(Point position, PointerButtons button, bool isPressed)
    {
    }

    private void HandlePointerWheelChange(Point position, Vector wheelDelta)
    {
        // Adjust zoom level:
        if (wheelDelta.Y > 0)
        {
            if (CameraScale < 10)
                ChangeCameraScale(position, CameraScale * 1.1);
        }
        else if (wheelDelta.Y < 0)
        {
            if (CameraScale > 0.1)
                ChangeCameraScale(position, CameraScale / 1.1);
        }
    }


    private void ChangeCameraScale(Point position, double newScale)
    {
        var relativePosition = position - CameraOffset;
        var newRelativePosition = (relativePosition / CameraScale) * newScale;

        CameraOffset = position - newRelativePosition;
        CameraScale = newScale;
    }
}

using PainDiagram.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PainDiagram.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (sender, args) =>
        {
            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSavePngPathAsync = PickSavePngPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = InvalidateDrawCanvas;
            }
        };

        InitializeComponent();
    }

    private MainViewModel ViewModel => DataContext as MainViewModel;

    private SKSize CanvasViewSize => new SKSize((float)DrawCanvas.ActualWidth, (float)DrawCanvas.ActualHeight);

    private void InvalidateDrawCanvas()
    {
        if (DrawCanvas.Dispatcher.CheckAccess())
        {
            DrawCanvas.InvalidateVisual();
        }
        else
        {
            DrawCanvas.Dispatcher.BeginInvoke(DrawCanvas.InvalidateVisual);
        }
    }

    private static SKPoint ToSKPoint(Point point) => new SKPoint((float)point.X, (float)point.Y);

    private void DrawCanvas_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        ViewModel?.Session?.Render(e.Surface, e.Info);
    }

    private void DrawCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null || e.ChangedButton != MouseButton.Left) { return; }

        if (session.PointerPressed(ToSKPoint(e.GetPosition(DrawCanvas)), CanvasViewSize))
        {
            DrawCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void DrawCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null || !session.IsPointerActive) { return; }

        session.PointerMoved(ToSKPoint(e.GetPosition(DrawCanvas)), CanvasViewSize);
        e.Handled = true;
    }

    private void DrawCanvas_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null || e.ChangedButton != MouseButton.Left || !session.IsPointerActive) { return; }

        session.PointerReleased();
        DrawCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void DrawCanvas_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        ViewModel?.Session?.PointerCanceled();
    }

    private Task<string> PickSavePngPathAsync(string suggestedFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save PNG as",
            Filter = "PNG image (*.png)|*.png|All files (*.*)|*.*",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = suggestedFileName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            OverwritePrompt = false   //The app does its own replace prompt via SimpleDialog
        };

        var chosen = dialog.ShowDialog(this) == true ? dialog.FileName : null;
        return Task.FromResult(chosen);
    }
}

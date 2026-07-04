using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PainDiagram.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System.Threading.Tasks;

namespace PainDiagram.WinUI.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (sender, args) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSavePngPathAsync = PickSavePngPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = () => DrawCanvas.Invalidate();
            }
        };

        InitializeComponent();
    }

    private MainViewModel ViewModel => DataContext as MainViewModel;

    private SKSize CanvasViewSize => new SKSize((float)DrawCanvas.ActualWidth, (float)DrawCanvas.ActualHeight);

    private static SKPoint ToSKPoint(Windows.Foundation.Point point) => new SKPoint((float)point.X, (float)point.Y);

    private void DrawCanvas_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        ViewModel?.Session?.Render(e.Surface, e.Info);
    }

    private void DrawCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCanvas.Invalidate();
    }

    private void DrawCanvas_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null) { return; }

        var pointerPoint = e.GetCurrentPoint(DrawCanvas);
        if (!pointerPoint.Properties.IsLeftButtonPressed) { return; }

        if (session.PointerPressed(ToSKPoint(pointerPoint.Position), CanvasViewSize))
        {
            DrawCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void DrawCanvas_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null || !session.IsPointerActive) { return; }

        session.PointerMoved(ToSKPoint(e.GetCurrentPoint(DrawCanvas).Position), CanvasViewSize);
        e.Handled = true;
    }

    private void DrawCanvas_OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var session = ViewModel?.Session;
        if (session == null || !session.IsPointerActive) { return; }

        session.PointerReleased();
        DrawCanvas.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void DrawCanvas_OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        ViewModel?.Session?.PointerCanceled();
    }

    private static Task<string> PickSavePngPathAsync(string suggestedFileName)
    {
        //The Win32 dialog (rather than the WinRT FileSavePicker) so the un-suppressible
        //  WinRT overwrite prompt does not double up with the app's own confirmation
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
        var path = Win32SaveFileDialog.PickSavePath(hwnd, suggestedFileName, "Save PNG as");
        return Task.FromResult(path);
    }
}

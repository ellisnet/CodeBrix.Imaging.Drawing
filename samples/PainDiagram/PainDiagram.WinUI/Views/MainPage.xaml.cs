using CodeBrix.Imaging.Drawing;
using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PainDiagram.ViewModels;
using System.Threading.Tasks;

namespace PainDiagram.WinUI.Views;

// ReSharper disable once RedundantExtendsListEntry
public sealed partial class MainPage : Page
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSavePngPathAsync = (fileName) =>
                {
                    //The Win32 dialog (rather than the WinRT FileSavePicker) so the un-suppressible
                    //  WinRT overwrite prompt does not double up with the app's own confirmation
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
                    var path = Win32SaveFileDialog.PickSavePath(hwnd, fileName, "Save PNG as");
                    return Task.FromResult(path);
                };
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = () => DrawCanvas?.Invalidate();
            }
        };

        InitializeComponent();

        #region | Add event handling for our DrawCanvas element |

        DrawCanvas.PaintSurface += (_, e) => ViewModel?.Session?.Render(e.Surface, e.Info);

        DrawCanvas.PointerPressed += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (session == null) { return; }

            var pointerPoint = e.GetCurrentPoint(DrawCanvas);
            if (!pointerPoint.Properties.IsLeftButtonPressed) { return; }

            if (session.PointerPressed(DrawCanvasHelper.GetPointFromPosition(pointerPoint.Position), DrawCanvas.GetViewSize()))
            {
                DrawCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        };

        DrawCanvas.PointerMoved += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerMoved(DrawCanvasHelper.GetPointFromPosition(e.GetCurrentPoint(DrawCanvas).Position), DrawCanvas.GetViewSize());
            e.Handled = true;
        };

        DrawCanvas.PointerReleased += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerReleased();
            DrawCanvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };

        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        DrawCanvas.PointerCaptureLost += (_, _) => ViewModel?.Session?.PointerCanceled();

        DrawCanvas.SizeChanged += (_, _) => DrawCanvas.Invalidate();

        #endregion
    }
}

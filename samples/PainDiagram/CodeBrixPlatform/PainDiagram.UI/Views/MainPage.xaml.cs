using CodeBrix.Imaging.Drawing;
using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PainDiagram.Helpers;
using PainDiagram.ViewModels;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FileSavePicker) lives here
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

// ReSharper disable once CheckNamespace
namespace PainDiagram.Views;

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
                fileSave.PickSavePngPathAsync = PickSavePngPathAsync;
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

    private static async Task<string> PickSavePngPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".png"
        };
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });

        StorageFile file = await picker.PickSaveFileAsync();
        if (file == null) { return null; }

        FileDialogHelper.RemoveEmptyPlaceholder(file.Path);
        return file.Path;
    }
}

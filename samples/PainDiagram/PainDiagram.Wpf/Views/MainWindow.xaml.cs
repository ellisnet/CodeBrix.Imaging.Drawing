using CodeBrix.Imaging.Drawing;
using PainDiagram.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PainDiagram.Views;

public partial class MainWindow : Window
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
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

        #region | Add event handling for our DrawCanvas element |

        DrawCanvas.PaintSurface += (_, e) => ViewModel?.Session?.Render(e.Surface, e.Info);

        DrawCanvas.MouseDown += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (session == null || e.ChangedButton != MouseButton.Left) { return; }

            if (session.PointerPressed(DrawCanvasHelper.GetPointFromPosition(e.GetPosition(DrawCanvas)), DrawCanvas.GetViewSize()))
            {
                DrawCanvas.CaptureMouse();
                e.Handled = true;
            }
        };

        DrawCanvas.MouseMove += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerMoved(DrawCanvasHelper.GetPointFromPosition(e.GetPosition(DrawCanvas)), DrawCanvas.GetViewSize());
            e.Handled = true;
        };

        DrawCanvas.MouseUp += (_, e) =>
        {
            var session = ViewModel?.Session;
            if (e.ChangedButton != MouseButton.Left || session is not { IsPointerActive: true }) { return; }

            session.PointerReleased();
            DrawCanvas.ReleaseMouseCapture();
            e.Handled = true;
        };

        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        DrawCanvas.LostMouseCapture += (_, _) => ViewModel?.Session?.PointerCanceled();

        #endregion
    }

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

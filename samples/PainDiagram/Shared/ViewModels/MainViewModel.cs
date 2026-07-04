using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Platform.Simple;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace PainDiagram.ViewModels;

/// <summary>
/// Lets the hosting page give the view model a native "Save PNG as…" file dialog. Each head
/// wires this up with the file dialog appropriate to its UI stack (the CodeBrix.Platform
/// <c>FileSavePicker</c> on the Skia heads, a Win32 dialog on native WinUI, and
/// <c>SaveFileDialog</c> on WPF).
/// </summary>
public interface IFileSaveBridge
{
    /// <summary>
    /// Shows a "save PNG" dialog seeded with <paramref name="suggestedFileName"/> and returns the
    /// full path the user chose, or <c>null</c> if they cancelled. The head leaves this null when
    /// it has no file dialog (e.g. the Linux framebuffer head), in which case the image is saved
    /// to a default location.
    /// Signature: <c>Func&lt;suggestedFileName, Task&lt;chosenPathOrNull&gt;&gt;</c>.
    /// </summary>
    Func<string, Task<string>> PickSavePngPathAsync { get; set; }
}

/// <summary>
/// Lets the hosting page hand the view model a way to invalidate (repaint) the Skia canvas
/// that displays the drawing. The drawing session raises redraw requests as strokes arrive,
/// and the view model forwards them through this delegate.
/// </summary>
public interface ICanvasInvalidator
{
    /// <summary>Invalidates the hosting page's drawing canvas (null before the page wires it up).</summary>
    Action InvalidateCanvas { get; set; }
}

#if HAS_CODEBRIX
[Microsoft.UI.Xaml.Data.Bindable]
#endif
public class MainViewModel : SimpleViewModel, IFileSaveBridge, ICanvasInvalidator
{
    public const string PainLayerName = "Pain";
    public const string NumbnessLayerName = "Numbness";
    public const string TinglingLayerName = "Tingling";

    //The body-map image is embedded with this logical name by every head that compiles
    //  this file (PainDiagram.Core, PainDiagram.WinUI, and PainDiagram.Wpf)
    private const string BodyMapResourceName = "PainDiagram.Assets.body_map_master.png";

    private const int ExportPixelSize = 1000;

    private DrawingSession _session;

    public MainViewModel()
    {
        if (!IsDesignMode(true))
        {
            Debug.WriteLine("Main view model startup.");

            _session = new DrawingSession(new DrawingSessionOptions
            {
                BackgroundFillColor = SKColors.White,
                SurfaceClearColor = SKColors.White,
            });

            //The same highlighter colors the original NuraPad application used
            _session.AddLayer(PainLayerName, new SKColor(255, 30, 230));
            _session.AddLayer(NumbnessLayerName, new SKColor(30, 128, 204));
            _session.AddLayer(TinglingLayerName, new SKColor(204, 170, 10));

            LoadBodyMapBackground();

            _session.RedrawRequested += (sender, args) => InvalidateCanvas?.Invoke();
            _session.DrawingChanged += (sender, args) => InvokeOnMainThread(() => HasDrawing = _session.HasStrokes);

            StatusText = "Select Pain, Numbness, or Tingling - then draw on the body map with the left mouse button.";
        }
    }

    /// <summary>
    /// The interactive drawing session; the hosting page renders it in its paint handler and
    /// forwards pointer events to it.
    /// </summary>
    public DrawingSession Session => _session;

    private void LoadBodyMapBackground()
    {
        //The view model is compiled into a different assembly on each head, and each of those
        //  assemblies embeds the body-map image under the same logical resource name
        using Stream resourceStream = typeof(MainViewModel).Assembly.GetManifestResourceStream(BodyMapResourceName);
        if (resourceStream == null)
        {
            Debug.WriteLine($"Embedded body-map image not found: {BodyMapResourceName}");
            return;
        }

        using var buffer = new MemoryStream();
        resourceStream.CopyTo(buffer);
        _session.SetBackgroundImage(buffer.ToArray());
    }

    private static string GetSuggestedFileName() => "pain_diagram.png";

    private static string GetDefaultSavePath()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (String.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        return Path.Combine(folder, $"pain_diagram_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    #region | Bindable properties |

    private string _activeLayerName = PainLayerName;
    public string ActiveLayerName
    {
        get => _activeLayerName;
        private set
        {
            SetProperty(ref _activeLayerName, value);
            NotifyPropertyChanged(nameof(PainButtonText));
            NotifyPropertyChanged(nameof(NumbnessButtonText));
            NotifyPropertyChanged(nameof(TinglingButtonText));
        }
    }

    public string PainButtonText => ActiveLayerName == PainLayerName ? "✓ Pain" : "Pain";
    public string NumbnessButtonText => ActiveLayerName == NumbnessLayerName ? "✓ Numbness" : "Numbness";
    public string TinglingButtonText => ActiveLayerName == TinglingLayerName ? "✓ Tingling" : "Tingling";

    private bool _hasDrawing;
    [AffectsCommands(nameof(SaveCommand), nameof(ClearCommand))]
    public bool HasDrawing
    {
        get => _hasDrawing;
        private set => SetProperty(ref _hasDrawing, value);
    }

    private bool _isBusy;
    [AffectsCommands(nameof(SaveCommand), nameof(ClearCommand))]
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    /// <summary>Set by the hosting head (see <see cref="IFileSaveBridge"/>); null on heads with no file dialog.</summary>
    public Func<string, Task<string>> PickSavePngPathAsync { get; set; }

    /// <summary>Set by the hosting page (see <see cref="ICanvasInvalidator"/>).</summary>
    public Action InvalidateCanvas { get; set; }

    #endregion

    #region | Commands and their implementations |

    private void SetActiveLayer(string layerName)
    {
        DrawingLayer layer = _session?.GetLayer(layerName);
        if (layer != null)
        {
            _session.ActiveLayer = layer;
            ActiveLayerName = layerName;
        }
    }

    #region SelectPainCommand / SelectNumbnessCommand / SelectTinglingCommand

    private SimpleCommand _selectPainCommand;
    public SimpleCommand SelectPainCommand =>
        (_selectPainCommand ??= new SimpleCommand(() => true, DoSelectPain));

    private Task DoSelectPain()
    {
        SetActiveLayer(PainLayerName);
        return Task.CompletedTask;
    }

    private SimpleCommand _selectNumbnessCommand;
    public SimpleCommand SelectNumbnessCommand =>
        (_selectNumbnessCommand ??= new SimpleCommand(() => true, DoSelectNumbness));

    private Task DoSelectNumbness()
    {
        SetActiveLayer(NumbnessLayerName);
        return Task.CompletedTask;
    }

    private SimpleCommand _selectTinglingCommand;
    public SimpleCommand SelectTinglingCommand =>
        (_selectTinglingCommand ??= new SimpleCommand(() => true, DoSelectTingling));

    private Task DoSelectTingling()
    {
        SetActiveLayer(TinglingLayerName);
        return Task.CompletedTask;
    }

    #endregion

    #region ClearCommand

    private SimpleCommand _clearCommand;
    public SimpleCommand ClearCommand =>
        (_clearCommand ??= new SimpleCommand(CanClear, DoClear));

    private bool CanClear() => (!IsBusy) && HasDrawing;

    private async Task DoClear()
    {
        if (!CanClear()) { return; }

        var doClear = true;
        if (_session.StrokeCount > 2)
        {
            doClear = await ConfirmDialog(
                "Are you sure you want to clear and start over?",
                "Confirm");
        }

        if (doClear)
        {
            _session.Clear();
            SetActiveLayer(PainLayerName);
            StatusText = "Cleared - draw a new diagram.";
        }
    }

    #endregion

    #region SaveCommand

    private SimpleCommand _saveCommand;
    public SimpleCommand SaveCommand =>
        (_saveCommand ??= new SimpleCommand(CanSave, DoSave));

    private bool CanSave() => (!IsBusy) && HasDrawing;

    private async Task DoSave()
    {
        if (!CanSave()) { return; }

        try
        {
            string outputPath;

            if (PickSavePngPathAsync == null)
            {
                //No native file dialog on this head (e.g. the Linux framebuffer head) -
                //  save to a default location instead
                outputPath = GetDefaultSavePath();
            }
            else
            {
                outputPath = await PickSavePngPathAsync(GetSuggestedFileName());
                if (String.IsNullOrWhiteSpace(outputPath))
                {
                    return; //The user cancelled the dialog
                }
                outputPath = outputPath.Trim();

                //Confirm before clobbering an existing file (the heads' own overwrite
                //  prompts are suppressed so this is the single confirmation)
                if (File.Exists(outputPath))
                {
                    var replace = await ConfirmDialog(
                        $"A file already exists at:\n{outputPath}\n\nDo you want to replace it?",
                        "Replace existing file?");
                    if (!replace)
                    {
                        StatusText = "Save cancelled - the existing file was kept.";
                        return;
                    }
                }
            }

            IsBusy = true;

            byte[] png = _session.ExportPng(new SKSizeI(ExportPixelSize, ExportPixelSize));
            await File.WriteAllBytesAsync(outputPath, png);

            StatusText = $"Saved: {outputPath}";

            //Unlike the original NuraPad application (which always started fresh after
            //  sending, ready for the next patient), clearing is the user's choice here
            var clearDrawing = await ConfirmDialog(
                $"The Pain Diagram image was saved to:\n{outputPath}\n\nDo you want to clear the drawing?",
                "Image saved");
            if (clearDrawing)
            {
                _session.Clear();
                SetActiveLayer(PainLayerName);
            }
        }
        catch (NotSupportedException)
        {
            await ShowError("File dialogs are not supported on this head.");
        }
        catch (Exception e)
        {
            StatusText = "Saving failed.";
            await ShowError($"Error while saving the Pain Diagram image: {e.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        _selectPainCommand?.Dispose();
        _selectPainCommand = null;
        _selectNumbnessCommand?.Dispose();
        _selectNumbnessCommand = null;
        _selectTinglingCommand?.Dispose();
        _selectTinglingCommand = null;
        _clearCommand?.Dispose();
        _clearCommand = null;
        _saveCommand?.Dispose();
        _saveCommand = null;

        PickSavePngPathAsync = null;
        InvalidateCanvas = null;

        _session?.Dispose();
        _session = null;

        base.Dispose();
    }

    #endregion
}

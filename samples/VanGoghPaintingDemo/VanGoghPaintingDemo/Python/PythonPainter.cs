using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CodeBrix.Python;
using VanGoghPaintingDemo.Painting;

namespace VanGoghPaintingDemo.Python;

/// <summary>
/// The original animation: the Python script (assets/Python/paint_vangogh.py), run inside this process by
/// CodeBrix.Python. The script does all of the painting, including the dissolve between paintings, and writes
/// each finished frame straight into the buffer it is handed.
/// <para>
/// REQUIREMENTS: a CPython with its shared library (libpython / pythonXY.dll) installed on this machine, with
/// the numpy, scipy and Pillow packages. The library is found from the <c>--python-dll</c> option, else the
/// PYTHONNET_PYDLL environment variable, else by looking in the usual places.
/// </para>
/// <para>
/// LIFETIME: only one Python painter can ever be created in a process. It starts the Python engine, and its
/// <see cref="Dispose" /> shuts the engine down for good.
/// </para>
/// </summary>
internal sealed unsafe class PythonPainter : IFramePainter
{
    private static readonly object Gate = new object();
    private static bool used;

    private readonly PyObject module;
    private readonly IntPtr threads;
    private bool disposed;

    /// <summary>Starts Python, loads the script and builds (first run) or loads its stroke layouts.</summary>
    /// <param name="pythonDll">The Python shared library to use, or null to find one.</param>
    /// <param name="cacheFolder">Where the script keeps its stroke layouts between runs.</param>
    /// <exception cref="InvalidOperationException">Python, or one of the packages the script needs, is not available.</exception>
    public PythonPainter(string pythonDll, string cacheFolder)
    {
        lock (Gate)
        {
            if (used) throw new InvalidOperationException("The Python engine can only be started once per process.");
            used = true;
        }

        string library = FindLibrary(pythonDll);
        string scripts = Path.Combine(AppContext.BaseDirectory, "assets", "Python");
        if (!File.Exists(Path.Combine(scripts, "paint_vangogh.py"))) throw new InvalidOperationException("The Python script is missing from " + scripts + ".");
        Directory.CreateDirectory(cacheFolder);

        Runtime.PythonDLL = library;
        PythonEngine.Initialize();
        try
        {
            using (Py.GIL())
            {
                using PyObject sys = Py.Import("sys");
                using PyObject path = sys.GetAttr("path");
                using PyString folder = new PyString(scripts);
                using PyInt first = new PyInt(0);
                path.InvokeMethod("insert", first, folder).Dispose();
                module = Py.Import("paint_vangogh");
                using PyString cache = new PyString(cacheFolder);
                module.InvokeMethod("init", cache).Dispose();
            }
        }
        catch (PythonException e)
        {
            PythonEngine.Shutdown();
            throw new InvalidOperationException(
                "The Python script could not start (" + library + "): " + e.Message.Trim()
                + Environment.NewLine + "It needs the numpy, scipy and Pillow packages installed in that Python.", e);
        }

        // Let Python's own background painter thread run whenever this process is not calling into Python.
        threads = PythonEngine.BeginAllowThreads();
    }

    /// <inheritdoc />
    public string Name => "python";

    /// <inheritdoc />
    public void RenderFrame(double seconds, Span<byte> rgba)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (rgba.Length < Timeline.Width * Timeline.Height * 4) throw new ArgumentException("The frame buffer is too small.", nameof(rgba));
        fixed (byte* pixels = rgba)
        {
            using (Py.GIL())
            {
                using PyFloat time = new PyFloat(seconds);
                using PyInt address = new PyInt((long)pixels);
                module.InvokeMethod("frame_into", time, address).Dispose();
            }
        }
    }

    /// <summary>Stops the script's background painter and shuts the Python engine down.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        using (Py.GIL())
        {
            module.InvokeMethod("shutdown").Dispose();
            module.Dispose();
        }

        PythonEngine.EndAllowThreads(threads);
        PythonEngine.Shutdown();
    }

    private static string FindLibrary(string asked)
    {
        foreach (string candidate in new[] { asked, Environment.GetEnvironmentVariable("PYTHONNET_PYDLL") })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (!File.Exists(candidate)) throw new InvalidOperationException("The Python library '" + candidate + "' does not exist.");
            return candidate;
        }

        List<(string Folder, string Pattern)> places = new List<(string, string)>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (string root in new[] { Path.Combine(local, "Programs", "Python"), programs })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string folder in Directory.GetDirectories(root, "Python3*")) places.Add((folder, "python3*.dll"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            foreach (string root in new[] { "/Library/Frameworks", "/opt/homebrew/Frameworks", "/usr/local/Frameworks" })
            {
                string versions = Path.Combine(root, "Python.framework", "Versions");
                if (!Directory.Exists(versions)) continue;
                foreach (string folder in Directory.GetDirectories(versions, "3.*")) places.Add((Path.Combine(folder, "lib"), "libpython3.*.dylib"));
            }
        }
        else
        {
            foreach (string folder in new[] { "/usr/lib/x86_64-linux-gnu", "/usr/lib/aarch64-linux-gnu", "/usr/lib64", "/usr/lib", "/usr/local/lib" })
            {
                places.Add((folder, "libpython3.*.so*"));
            }
        }

        // The newest "3.N" wins; a name like python3.dll (no minor version) is only a forwarding stub, so it is skipped.
        string best = null;
        int bestMinor = -1;
        foreach ((string folder, string pattern) in places)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string file in Directory.GetFiles(folder, pattern).OrderBy(f => f.Length))
            {
                Match match = Regex.Match(Path.GetFileName(file), @"python3\.?(\d+)", RegexOptions.IgnoreCase);
                if (!match.Success) continue;
                int minor = int.Parse(match.Groups[1].Value);
                if (minor > bestMinor)
                {
                    bestMinor = minor;
                    best = file;
                }
            }
        }

        return best ?? throw new InvalidOperationException(
            "No Python shared library was found. Install a CPython with its shared library, or point at one with --python-dll <path> or the PYTHONNET_PYDLL environment variable.");
    }
}

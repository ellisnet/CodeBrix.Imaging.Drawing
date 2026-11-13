using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using AnimationDemoApp.Demo;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Sdl3;
using SkiaSharp;
using static CodeBrix.Sdl3.SDL3;

namespace AnimationDemoApp.Hosting;

/// <summary>
/// The live view: an SDL3 window playing the scenes in real time. Each painter writes its frame into its own
/// buffer; SkiaSharp lays the frame - or, side by side, both painters' frames - out on a raster surface, which
/// is uploaded to a streaming SDL texture and scaled to fit the window (letterboxed). The play state is the
/// library's <see cref="PlaybackController" /> over the catalog's timeline.
/// Keys: Space pause/play (at the end: play again), Left/Right seek 1 s (Shift: previous/next scene), Home
/// restart, D show/hide the debug overlay (timecode, frame, current scene), Esc quit; with two painters, Tab goes
/// to the next view (each painter alone, then side by side).
/// </summary>
internal static unsafe class LiveWindow
{
    private const int Gap = 24;

    /// <summary>Runs the live view until the window is closed.</summary>
    /// <param name="painters">The painters to show: one, or two to compare.</param>
    /// <param name="catalog">The scenes being played (for the timeline and the window title).</param>
    /// <returns>The process exit code.</returns>
    public static int Run(IReadOnlyList<IFramePainter> painters, SceneCatalog catalog)
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Console.Error.WriteLine("SDL_Init failed: " + SDL_GetError());
            return 1;
        }

        int frameBytes = DemoFrame.Bytes;
        SKImageInfo frameInfo = new SKImageInfo(DemoFrame.Width, DemoFrame.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        IntPtr[] frames = new IntPtr[painters.Count];
        SDL_Window* window = null;
        SDL_Renderer* renderer = null;
        SDL_Texture* texture = null;
        IntPtr pixels = IntPtr.Zero;
        SKSurface surface = null;
        try
        {
            for (int i = 0; i < frames.Length; i++) frames[i] = Marshal.AllocHGlobal(frameBytes);

            // The views: each painter alone, then (with two painters) both side by side - which is shown first.
            int views = painters.Count > 1 ? painters.Count + 1 : 1;
            int view = views - 1;

            window = SDL_CreateWindow("Animation Demo", painters.Count > 1 ? (painters.Count * 640) + 12 : 960, painters.Count > 1 ? 380 : 560,
                SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
            if (window == null) return Fail("SDL_CreateWindow");
            renderer = SDL_CreateRenderer(window, (byte*)null);
            if (renderer == null) return Fail("SDL_CreateRenderer");
            SDL_SetRenderVSync(renderer, 1);

            Console.WriteLine("Live view: " + (views > 1 ? "Tab next view, " : string.Empty) + "Space pause/play, Left/Right seek 1 s (Shift: scene), Home restart, D debug overlay, Esc quit.");
            SKImageInfo info = default;
            int shownView = -1;
            PlaybackController playback = new PlaybackController(catalog.Timeline);
            playback.Play();

            // The debug overlay is drawn by an empty session rendered over the finished canvas (the overlay is a live-
            // render preview aid: it never reaches a painter's frames or a still).
            using DrawingSession overlaySession = new DrawingSession();
            DebugOverlay overlay = new DebugOverlay(DemoFonts.Registry, catalog.Timeline) { ShowCues = true, Corner = DebugOverlayCorner.BottomRight };
            Stopwatch clock = Stopwatch.StartNew();
            double last = clock.Elapsed.TotalSeconds;
            double titled = -1;
            bool running = true;
            SDL_Event e;
            while (running)
            {
                while (SDL_PollEvent(&e))
                {
                    SDL_EventType type = (SDL_EventType)e.type;
                    if (type == SDL_EventType.SDL_EVENT_QUIT || type == SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                    {
                        running = false;
                    }
                    else if (type == SDL_EventType.SDL_EVENT_KEY_DOWN)
                    {
                        bool shift = (e.key.mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0;
                        double seconds = playback.Time;
                        int scene = catalog.At(seconds).Index;
                        switch (e.key.key)
                        {
                            case SDL_Keycode.SDLK_ESCAPE:
                                running = false;
                                break;
                            case SDL_Keycode.SDLK_TAB:
                                if (!e.key.repeat) view = (view + 1) % views;
                                break;
                            case SDL_Keycode.SDLK_SPACE:
                                if (!e.key.repeat) playback.Toggle();
                                break;
                            case SDL_Keycode.SDLK_LEFT:
                                if (shift) playback.Seek(catalog.StartOf(seconds - catalog.StartOf(scene) > 0.5 ? scene : scene - 1));
                                else playback.SeekBy(-1);
                                break;
                            case SDL_Keycode.SDLK_RIGHT:
                                if (shift) playback.Seek(scene + 1 < catalog.Scenes.Count ? catalog.StartOf(scene + 1) : catalog.TotalSeconds);
                                else playback.SeekBy(1);
                                break;
                            case SDL_Keycode.SDLK_HOME:
                                playback.Restart();
                                break;
                            case SDL_Keycode.SDLK_D:
                                if (!e.key.repeat) overlaySession.DebugOverlay = overlaySession.DebugOverlay == null ? overlay : null;
                                break;
                        }
                    }
                }

                // A slow frame pauses the animation instead of skipping ahead (the controller caps each step).
                double nowSeconds = clock.Elapsed.TotalSeconds;
                playback.Advance(nowSeconds - last);
                last = nowSeconds;
                double time = playback.Time;

                bool both = view >= painters.Count;
                if (view != shownView)
                {
                    // The surface is one frame wide, or two frames and a gap.
                    surface?.Dispose();
                    if (texture != null) SDL_DestroyTexture(texture);
                    if (pixels != IntPtr.Zero) Marshal.FreeHGlobal(pixels);
                    int width = both ? (painters.Count * DemoFrame.Width) + ((painters.Count - 1) * Gap) : DemoFrame.Width;

                    // ABGR8888 is R,G,B,A in memory on little-endian machines: the same bytes as Skia's Rgba8888.
                    info = new SKImageInfo(width, DemoFrame.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                    pixels = Marshal.AllocHGlobal(info.BytesSize);
                    surface = SKSurface.Create(info, pixels, info.RowBytes) ?? throw new InvalidOperationException("Could not create the frame surface.");
                    texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, info.Width, info.Height);
                    if (texture == null) return Fail("SDL_CreateTexture");
                    SDL_SetTextureScaleMode(texture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);
                    shownView = view;
                    titled = -1;
                }

                SKCanvas canvas = surface.Canvas;
                canvas.Clear(SKColors.Black);
                for (int i = 0; i < painters.Count; i++)
                {
                    if (both || i == view) painters[i].Prepare(time);
                }

                for (int i = 0; i < painters.Count; i++)
                {
                    if (!both && i != view) continue;
                    painters[i].RenderFrame(time, new Span<byte>((void*)frames[i], frameBytes));
                    using SKImage image = SKImage.FromPixels(frameInfo, frames[i], frameInfo.RowBytes);
                    canvas.DrawImage(image, both ? i * (DemoFrame.Width + Gap) : 0, 0, SKSamplingOptions.Default);
                }

                overlaySession.Time = time;
                overlaySession.Render(canvas, info, clearCanvas: false);
                surface.Flush();
                SDL_UpdateTexture(texture, null, pixels, info.RowBytes);

                if (Math.Abs(time - titled) >= 0.1)
                {
                    titled = time;
                    string showing = both ? string.Join("  |  ", Names(painters)) : painters[view].Name;
                    DemoScene current = catalog.Scenes[catalog.At(time).Index];
                    SDL_SetWindowTitle(window, string.Format(CultureInfo.InvariantCulture,
                        "Animation Demo  -  {0}  -  {1}{2:0.0} s of {3:0.0}{4}  -  {5}Space pause  Left/Right seek (Shift: scene)  Home restart  D overlay  Esc quit",
                        current.Title, views > 1 ? showing + "  -  " : string.Empty, time, catalog.TotalSeconds, playback.IsPlaying ? string.Empty : " (paused)", views > 1 ? "Tab view  " : string.Empty));
                }

                int outW;
                int outH;
                SDL_GetRenderOutputSize(renderer, &outW, &outH);
                float scale = Math.Min(outW / (float)info.Width, outH / (float)info.Height);
                SDL_FRect target = new SDL_FRect
                {
                    w = info.Width * scale,
                    h = info.Height * scale,
                };
                target.x = (outW - target.w) / 2;
                target.y = (outH - target.h) / 2;
                SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL_RenderClear(renderer);
                SDL_RenderTexture(renderer, texture, null, &target);
                SDL_RenderPresent(renderer);
            }

            return 0;
        }
        finally
        {
            surface?.Dispose();
            if (texture != null) SDL_DestroyTexture(texture);
            if (renderer != null) SDL_DestroyRenderer(renderer);
            if (window != null) SDL_DestroyWindow(window);
            if (pixels != IntPtr.Zero) Marshal.FreeHGlobal(pixels);
            foreach (IntPtr frame in frames)
            {
                if (frame != IntPtr.Zero) Marshal.FreeHGlobal(frame);
            }

            SDL_Quit();
        }
    }

    private static IEnumerable<string> Names(IReadOnlyList<IFramePainter> painters)
    {
        foreach (IFramePainter painter in painters) yield return painter.Name;
    }

    private static int Fail(string what)
    {
        Console.Error.WriteLine(what + " failed: " + SDL_GetError());
        return 1;
    }
}

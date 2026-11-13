using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using AutumnAviana.Animation;
using AutumnAviana.Scene;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.Rendering;
using CodeBrix.Sdl3;
using static CodeBrix.Sdl3.SDL3;

namespace AutumnAviana.Hosting;

/// <summary>
/// The live view: an SDL3 window playing the animation in real time at 1280x720 (the stills stay 1920x1080).
/// <para>
/// Each frame comes from one of two places. Where the scene is cheap enough to draw live - the leaf fall and the
/// still and finished pictures, the raccoons - the session renders it into a managed bitmap with its layer caches
/// (the settled leaves are only ever added, so just what moves is drawn afresh). Where it is not - the camera pan,
/// which moves every pile leaf - the library's <see cref="CodeBrix.Imaging.Drawing.FrameCache" /> has rendered it
/// ahead on background threads; if a frame is not ready yet (just after a seek) it is drawn live. The RGBA bytes are
/// uploaded to a streaming SDL texture and scaled to fit the window (letterboxed). The play state is the library's
/// <see cref="PlaybackController" />.
/// </para>
/// Keys: Space pause/play (at the end: play again), Left/Right seek 1 s (Shift: 5 s), Home restart, D show/hide the
/// debug overlay (timecode, frame, active cues), Esc quit. The title shows the time.
/// </summary>
internal static unsafe class LiveWindow
{
    /// <summary>The live view's frame width.</summary>
    public const int Width = 1280;

    /// <summary>The live view's frame height.</summary>
    public const int Height = 720;

    /// <summary>
    /// The number of frames the frame cache renders at once: a few, leaving most of the machine to the live frames
    /// (the cache starts the pan seconds ahead, so it does not need to hurry).
    /// </summary>
    public static int CacheWorkers => Math.Clamp((Environment.ProcessorCount / 4) - 1, 2, 3);

    /// <summary>Runs the live view until the window is closed.</summary>
    /// <returns>The process exit code.</returns>
    public static int Run()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Console.Error.WriteLine("SDL_Init failed: " + SDL_GetError());
            return 1;
        }

        SDL_Window* window = null;
        SDL_Renderer* renderer = null;
        SDL_Texture* texture = null;
        try
        {
            window = SDL_CreateWindow("Autumn Aviana", Width, Height, SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
            if (window == null) return Fail("SDL_CreateWindow");
            renderer = SDL_CreateRenderer(window, (byte*)null);
            if (renderer == null) return Fail("SDL_CreateRenderer");
            SDL_SetRenderVSync(renderer, 1);

            // ABGR8888 is R, G, B, A in memory on little-endian machines: the frames' byte order.
            texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, Width, Height);
            if (texture == null) return Fail("SDL_CreateTexture");
            SDL_SetTextureScaleMode(texture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);

            Console.WriteLine("Live view: Space pause/play, Left/Right seek 1 s (Shift 5 s), Home restart, D debug overlay, Esc quit.");
            using AutumnScene scene = new AutumnScene();
            DrawingImageInfo info = new DrawingImageInfo(Width, Height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul);
            using DrawingBitmap bitmap = new DrawingBitmap(info);
            using DrawingCanvas canvas = new DrawingCanvas(bitmap);
            IntPtr pixels = bitmap.GetPixels();
            Span<byte> frame = new Span<byte>((void*)pixels, Width * Height * 4);

            using FrameSource source = new FrameSource(scene, canvas, info, CacheWorkers);

            // The debug overlay is drawn by an empty session rendered over the finished frame, whichever way it was made.
            using DrawingSession overlaySession = new DrawingSession(new DrawingSessionOptions { CalibrationSize = Stage.FrameSize });
            DebugOverlay overlay = new DebugOverlay(scene.Session.Fonts, Timeline.Clock) { ShowCues = true, Corner = DebugOverlayCorner.TopLeft };

            // Frames are large and made continually: keep the garbage collector from pausing the window for long.
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            PlaybackController playback = new PlaybackController(Timeline.Clock);
            playback.Play();
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
                        double step = shift ? 5 : 1;
                        switch (e.key.key)
                        {
                            case SDL_Keycode.SDLK_ESCAPE:
                                running = false;
                                break;
                            case SDL_Keycode.SDLK_SPACE:
                                if (!e.key.repeat) playback.Toggle();
                                break;
                            case SDL_Keycode.SDLK_LEFT:
                                playback.SeekBy(-step);
                                break;
                            case SDL_Keycode.SDLK_RIGHT:
                                playback.SeekBy(step);
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

                // A slow frame pauses the animation rather than skipping it (the controller caps each step).
                double now = clock.Elapsed.TotalSeconds;
                playback.Advance(now - last);
                last = now;
                double time = playback.Time;
                source.Draw(Timeline.Clock.FrameAt(time), time, frame);

                if (overlaySession.DebugOverlay != null)
                {
                    overlaySession.Time = time;
                    overlaySession.Render(canvas, info, clearCanvas: false);
                }

                SDL_UpdateTexture(texture, null, pixels, Width * 4);

                if (Math.Abs(time - titled) >= 0.1 || !playback.IsPlaying)
                {
                    titled = time;
                    SDL_SetWindowTitle(window, string.Format(CultureInfo.InvariantCulture,
                        "Autumn Aviana  -  {0:0.0} s of {1:0.0}{2}  -  Space pause  Left/Right seek (Shift 5 s)  Home restart  D overlay  Esc quit",
                        time, Timeline.TotalSeconds, playback.IsPlaying ? string.Empty : " (paused)"));
                }

                int outW;
                int outH;
                SDL_GetRenderOutputSize(renderer, &outW, &outH);
                float scale = Math.Min(outW / (float)Width, outH / (float)Height);
                SDL_FRect target = new SDL_FRect
                {
                    w = Width * scale,
                    h = Height * scale,
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
            if (texture != null) SDL_DestroyTexture(texture);
            if (renderer != null) SDL_DestroyRenderer(renderer);
            if (window != null) SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }

    private static int Fail(string what)
    {
        Console.Error.WriteLine(what + " failed: " + SDL_GetError());
        return 1;
    }
}

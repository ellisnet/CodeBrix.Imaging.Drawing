using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.SafeAreas;
using CodeBrix.Sdl3;
using SkiaSharp;
using VenusRosetteAnimation.Animation;
using static CodeBrix.Sdl3.SDL3;

namespace VenusRosetteAnimation.Hosting;

/// <summary>
/// The live view: an SDL3 window showing the animation in real time. Every frame is drawn at the scene's full
/// video size (landscape 1920x1080 by default, portrait 1080x1920 with --portrait) by the drawing session into a
/// Skia raster surface, uploaded to a streaming SDL texture and scaled to fit the window (letterboxed). The play state
/// is the library's <see cref="PlaybackController" /> over the animation's clock. Keys: Space pause/play (at the end:
/// play again), Left/Right seek 1 s (Shift: 5 s), Home restart, S show/hide the layout's safe-area overlay, Esc quit.
/// </summary>
internal static unsafe class LiveWindow
{
    /// <summary>Runs the live view until the window is closed.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="showSafeArea">Whether the safe-area overlay starts shown (S toggles it).</param>
    /// <returns>The process exit code.</returns>
    public static int Run(RosetteScene scene, bool showSafeArea)
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Console.Error.WriteLine("SDL_Init failed: " + SDL_GetError());
            return 1;
        }

        SDL_Window* window = null;
        SDL_Renderer* renderer = null;
        SDL_Texture* texture = null;
        IntPtr pixels = IntPtr.Zero;
        try
        {
            int windowWidth = scene.Layout.Width >= scene.Layout.Height ? 1280 : 540;
            int windowHeight = scene.Layout.Width >= scene.Layout.Height ? 720 : 960;
            window = SDL_CreateWindow("Venus Rosette  -  Space pause/play  Left/Right seek  Home restart  S safe area  Esc quit", windowWidth, windowHeight,
                SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
            if (window == null) return Fail("SDL_CreateWindow");
            renderer = SDL_CreateRenderer(window, (byte*)null);
            if (renderer == null) return Fail("SDL_CreateRenderer");
            SDL_SetRenderVSync(renderer, 1);
            texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, scene.Layout.Width, scene.Layout.Height);
            if (texture == null) return Fail("SDL_CreateTexture");
            SDL_SetTextureScaleMode(texture, SDL_ScaleMode.SDL_SCALEMODE_LINEAR);

            // ABGR8888 is R,G,B,A in memory on little-endian machines: the same bytes as Skia's Rgba8888.
            SKImageInfo info = new SKImageInfo(scene.Layout.Width, scene.Layout.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            pixels = Marshal.AllocHGlobal(info.BytesSize);
            using SKSurface surface = SKSurface.Create(info, pixels, info.RowBytes) ?? throw new InvalidOperationException("Could not create the frame surface.");

            Console.WriteLine("Live view: Space pause/play, Left/Right seek 1 s (Shift 5 s), Home restart, S safe area, Esc quit.");
            SafeAreaOverlay overlay = new SafeAreaOverlay(scene.Layout.SafeArea) { Duration = Timeline.TotalSeconds };
            scene.Overlay = showSafeArea ? overlay : null;
            PlaybackController playback = new PlaybackController(Timeline.Clock);
            playback.Play();
            Stopwatch clock = Stopwatch.StartNew();
            double last = clock.Elapsed.TotalSeconds;
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
                            case SDL_Keycode.SDLK_S:
                                if (e.key.repeat) break;
                                scene.Overlay = scene.Overlay == null ? overlay : null;
                                break;
                        }
                    }
                }

                // A slow frame pauses the animation rather than skipping it (the controller caps each step).
                double nowSeconds = clock.Elapsed.TotalSeconds;
                playback.Advance(nowSeconds - last);
                last = nowSeconds;

                scene.Show(playback.Time);
                scene.Render(surface.Canvas, info);
                surface.Flush();
                SDL_UpdateTexture(texture, null, pixels, info.RowBytes);

                int outW;
                int outH;
                SDL_GetRenderOutputSize(renderer, &outW, &outH);
                float scale = Math.Min(outW / (float)scene.Layout.Width, outH / (float)scene.Layout.Height);
                SDL_FRect target = new SDL_FRect
                {
                    w = scene.Layout.Width * scale,
                    h = scene.Layout.Height * scale,
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
            if (pixels != IntPtr.Zero) Marshal.FreeHGlobal(pixels);
            SDL_Quit();
        }
    }

    private static int Fail(string what)
    {
        Console.Error.WriteLine(what + " failed: " + SDL_GetError());
        return 1;
    }
}

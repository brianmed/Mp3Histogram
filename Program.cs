using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using SDL2;

namespace Mp3Histogram
{
    // git clone https://github.com/flibitijibibo/SDL2-CS

    //     <AllowUnsafeBlocks>true</AllowUnsafeBlocks>

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1) {
                Console.WriteLine("Please pass in a mp3 file");
            }

            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0) {
                Console.WriteLine(SDL.SDL_GetError());

                Environment.Exit(1);
            }

            if (SDL_mixer.Mix_Init(SDL_mixer.MIX_InitFlags.MIX_INIT_MP3) < 0) {
                Console.WriteLine(SDL.SDL_GetError());

                Environment.Exit(1);
            }

            IntPtr window = SDL.SDL_CreateWindow("Yay", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, 1024, 600, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (window == IntPtr.Zero) {
                Console.WriteLine(SDL.SDL_GetError());

                Console.WriteLine("Sad: Window");

                Environment.Exit(1);
            }

            IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            if (renderer == IntPtr.Zero) {
                Console.WriteLine(SDL.SDL_GetError());

                Console.WriteLine("Sad: Renderer");

                Environment.Exit(1);
            }

            if (SDL_mixer.Mix_OpenAudio(44100, SDL_mixer.MIX_DEFAULT_FORMAT, 2, 2048) < 0 ) {
                Console.WriteLine(SDL.SDL_GetError());

                Console.WriteLine("Sad: Mixer");

                Environment.Exit(1);
            }

            bool mp3Done = false;

            IntPtr joy = SDL_mixer.Mix_LoadMUS(args[0]);

            if (joy == IntPtr.Zero) {
                Console.WriteLine(SDL.SDL_GetError());

                Console.WriteLine("Sad: Mix_LoadMUS");

                Environment.Exit(1);
            }

            MPGImport.mpg123_init();

            Int32 err = 0;

            GCHandle handleErr = GCHandle.Alloc(err, GCHandleType.Pinned);

            IntPtr ptrErr = GCHandle.ToIntPtr(handleErr);

            IntPtr handle_mpg = MPGImport.mpg123_new(null, ptrErr);
            int x = MPGImport.mpg123_open(handle_mpg, args[0]);
            handleErr.Free();

            MPGImport.mpg123_format_none(handle_mpg);
            MPGImport.mpg123_format(handle_mpg, 44100, 2, (int)MPGImport.mpg123_enc_enum.MPG123_ENC_SIGNED_16);
        
            int FrameSize = MPGImport.mpg123_outblock(handle_mpg);      
            byte[] Buffer = new byte[FrameSize];      
            int lengthSamples = MPGImport.mpg123_length(handle_mpg);

            if (SDL_mixer.Mix_PlayMusic(joy, 0) < 0) {
                Console.WriteLine(SDL.SDL_GetError());

                Console.WriteLine("Sad: Mix_PlayMusic");

                Environment.Exit(1);
            }

            object fun = new object();

            List<Int16> LeftChannels = new List<Int16>();
            List<Int16> RightChannels = new List<Int16>();

            SDL_mixer.Mix_HookMusic((uData, stream, len) => {
                byte[] mixData = new byte[len];
                byte[] managedStream = new byte[len];

                Marshal.Copy(stream, managedStream, 0, len);

                IntPtr done = IntPtr.Zero;

                if (MPGImport.mpg123_read(handle_mpg, managedStream, len, out done) is int hello) {
                    if (hello == -12) {
                        mp3Done = true;
                    } else if (hello == 0) {
                        // foreach (var b in managedStream.Select((value, idx) => (value, idx)).Where((value, idx) => idx % 4 == 0)) {
                            Int16 leftChannel;
                            Int16 rightChannel;

                            byte[] datum = new byte[2];

                            datum[0] = managedStream[0];
                            datum[1] = managedStream[1];
                            leftChannel = BitConverter.ToInt16(datum, 0);

                            datum[0] = managedStream[2];
                            datum[1] = managedStream[3];
                            rightChannel = BitConverter.ToInt16(datum, 0);

                            leftChannel = (Int16) Math.Sqrt(leftChannel);
                            rightChannel = (Int16) Math.Sqrt(rightChannel);

                            lock (fun) {
                                LeftChannels.Add(leftChannel);
                                RightChannels.Add(rightChannel);
                            }
                        // }
                    }
                }

                Marshal.Copy(managedStream, 0, stream, len);
            }, IntPtr.Zero);

            SDL.SDL_Event e;
            bool quit = false;                        

            while (!quit)
            {
                while (SDL.SDL_WaitEventTimeout(out e, 10) != 0)
                {
                    switch (e.type)
                    {
                       case SDL.SDL_EventType.SDL_KEYDOWN:
                           switch (e.key.keysym.sym)
                           {
                                case SDL.SDL_Keycode.SDLK_ESCAPE:
                                    quit = true;

                                break;
                           }

                           break;

                       case SDL.SDL_EventType.SDL_QUIT:
                           quit = true;

                           break;
                    }
                }

                if (mp3Done) {
                    quit = true;
                } else {
                    SDL.SDL_RenderClear(renderer);
                        SDL.SDL_SetRenderDrawColor(renderer, 218, 112, 214, 255);

                    // 40 is perfect.. ;)
                    lock (fun) {
                        foreach (var channel in LeftChannels.Select((value, idx) => (value, idx)))
                        {
                            if (channel.idx > 40) {
                                continue;
                            }

                            SDL.SDL_Rect rect = new SDL.SDL_Rect() {
                                x = channel.idx * 20,
                                y = 300,
                                w = 15,
                                h = -channel.value
                            };

                            SDL.SDL_RenderDrawRect(renderer, ref rect);
                        }

                        if (LeftChannels.Count > 40) {
                            LeftChannels.RemoveAt(0);
                        }

                        foreach (var channel in RightChannels.Select((value, idx) => (value, idx)))
                        {
                            if (channel.idx > 40) {
                                continue;
                            }

                            SDL.SDL_Rect rect = new SDL.SDL_Rect() {
                                x = channel.idx * 20,
                                y = 315,
                                w = 15,
                                h = channel.value
                            };

                            SDL.SDL_RenderDrawRect(renderer, ref rect);
                        }

                        if (RightChannels.Count > 40) {
                            RightChannels.RemoveAt(0);
                        }
                    }

                    SDL.SDL_Rect r = new SDL.SDL_Rect() {
                        x = 0,
                        y = 300,
                        w = 1024,
                        h = 10
                    };

                    // SDL.SDL_RenderClear(renderer);
                    // SDL.SDL_SetRenderDrawColor(renderer, 218, 112, 214, 255);
                    SDL.SDL_RenderDrawRect(renderer, ref r);

                    SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                    SDL.SDL_RenderPresent(renderer);
                }
            }

            SDL_mixer.Mix_FreeMusic(joy);

            SDL.SDL_Quit();
        }
    }
}
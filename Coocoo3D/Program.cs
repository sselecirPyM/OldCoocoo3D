﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Coocoo3D.UI;
using ImGuiNET;
using static SDL2.SDL;

namespace Coocoo3D
{
    class Program
    {
        static void Main(string[] args)
        {
            bool quitRequested = false;
            Core.Coocoo3DMain coocoo3DMain = new Core.Coocoo3DMain();
            IntPtr window = SDL_CreateWindow("Coocoo3D", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, 1024, 768, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            SDL_GetWindowSize(window, out int Width, out int Height);
            SDL_SysWMinfo info = new SDL_SysWMinfo();
            SDL_GetWindowWMInfo(window, ref info);
            IntPtr hwnd = info.info.win.window;
            coocoo3DMain.graphicsDevice.SetSwapChainPanel(hwnd, Width, Height, 1, 1, 96.0f);
            coocoo3DMain.swapChainReady = true;
            #region key map
            Dictionary<uint, int> sdlMouse2ImguiMouse = new Dictionary<uint, int>();
            sdlMouse2ImguiMouse[SDL_BUTTON_LEFT] = 0;
            sdlMouse2ImguiMouse[SDL_BUTTON_MIDDLE] = 2;
            sdlMouse2ImguiMouse[SDL_BUTTON_RIGHT] = 1;
            sdlMouse2ImguiMouse[SDL_BUTTON_X1] = 3;
            sdlMouse2ImguiMouse[SDL_BUTTON_X2] = 4;
            Dictionary<SDL_Keycode, int> sdlKeycode2ImguiKey = new Dictionary<SDL_Keycode, int>();
            for (int i = 'a'; i <= 'z'; i++)
                sdlKeycode2ImguiKey[(SDL_Keycode)i] = i - 32;
            for (int i = '0'; i <= '9'; i++)
                sdlKeycode2ImguiKey[(SDL_Keycode)i] = i;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_BACKSPACE] = (int)ImGuiKey.Backspace;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_DELETE] = (int)ImGuiKey.Delete;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RETURN] = (int)ImGuiKey.Enter;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RETURN2] = (int)ImGuiKey.Enter;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_KP_ENTER] = (int)ImGuiKey.KeyPadEnter;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_ESCAPE] = (int)ImGuiKey.Escape;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_TAB] = (int)ImGuiKey.Tab;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_KP_TAB] = (int)ImGuiKey.Tab;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_PAGEDOWN] = (int)ImGuiKey.PageDown;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_PAGEUP] = (int)ImGuiKey.PageUp;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_AC_HOME] = (int)ImGuiKey.Home;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_HOME] = (int)ImGuiKey.Home;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_END] = (int)ImGuiKey.End;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_SPACE] = (int)ImGuiKey.Space;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_UP] = (int)ImGuiKey.UpArrow;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_DOWN] = (int)ImGuiKey.DownArrow;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_LEFT] = (int)ImGuiKey.LeftArrow;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RIGHT] = (int)ImGuiKey.RightArrow;
            sdlKeycode2ImguiKey[SDL_Keycode.SDLK_INSERT] = (int)ImGuiKey.Insert;
            #endregion
            #region cursors
            Dictionary<ImGuiMouseCursor, IntPtr> cursors = new Dictionary<ImGuiMouseCursor, IntPtr>();
            void createCursor(SDL_SystemCursor systemCursor, ImGuiMouseCursor imguiMouseCursor)
            {
                cursors[imguiMouseCursor] = SDL_CreateSystemCursor(systemCursor);
            }
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW, ImGuiMouseCursor.Arrow);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM, ImGuiMouseCursor.TextInput);
            //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT);
            //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR);
            //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAITARROW);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE, ImGuiMouseCursor.ResizeNWSE);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW, ImGuiMouseCursor.ResizeNESW);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE, ImGuiMouseCursor.ResizeEW);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS, ImGuiMouseCursor.ResizeNS);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL, ImGuiMouseCursor.ResizeAll);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO, ImGuiMouseCursor.NotAllowed);
            createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND, ImGuiMouseCursor.Hand);
            createCursor(SDL_SystemCursor.SDL_NUM_SYSTEM_CURSORS, ImGuiMouseCursor.COUNT);
            #endregion
            var imguiInput = coocoo3DMain.imguiInput;
            while (!quitRequested)
            {
                while (SDL_PollEvent(out var sdlEvent) == 1)
                {
                    switch (sdlEvent.type)
                    {
                        case SDL_EventType.SDL_QUIT:
                            quitRequested = true;
                            break;
                        case SDL_EventType.SDL_WINDOWEVENT:
                            if (sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
                            {
                                Width = sdlEvent.window.data1;
                                Height = sdlEvent.window.data2;
                                coocoo3DMain.RPContext.RequireResize = true;
                                coocoo3DMain.RPContext.NewSize = new Vector2(Width, Height);
                            }
                            break;
                        case SDL_EventType.SDL_KEYDOWN:
                            {
                                if (sdlKeycode2ImguiKey.TryGetValue(sdlEvent.key.keysym.sym, out int imkey))
                                    imguiInput.keydown[imkey] = true;
                            }
                            break;
                        case SDL_EventType.SDL_KEYUP:
                            {
                                if (sdlKeycode2ImguiKey.TryGetValue(sdlEvent.key.keysym.sym, out int imkey))
                                    imguiInput.keydown[imkey] = false;
                                break;
                            }
                        case SDL_EventType.SDL_TEXTINPUT:
                            {
                                string utf8Str;
                                unsafe
                                {
                                    utf8Str = Marshal.PtrToStringUTF8(new IntPtr(sdlEvent.text.text));
                                }
                                foreach (var c in utf8Str)
                                    imguiInput.InputChar(c);
                                break;
                            }
                        case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                            imguiInput.mouseDown[sdlMouse2ImguiMouse[sdlEvent.button.button]] = true;
                            if (sdlEvent.button.button == SDL_BUTTON_LEFT)
                            {

                            }
                            break;
                        case SDL_EventType.SDL_MOUSEBUTTONUP:
                            imguiInput.mouseDown[sdlMouse2ImguiMouse[sdlEvent.button.button]] = false;
                            if (sdlEvent.button.button == SDL_BUTTON_LEFT)
                            {

                            }
                            break;
                        case SDL_EventType.SDL_MOUSEMOTION:
                            {
                                int x = sdlEvent.motion.x;
                                int y = sdlEvent.motion.y;
                                imguiInput.MousePosition(new Vector2(x, y));
                                ImguiInput.mouseMoveDelta.Enqueue(new Vector2(sdlEvent.motion.xrel, sdlEvent.motion.yrel));

                            }
                            break;
                        case SDL_EventType.SDL_MOUSEWHEEL:
                            imguiInput.mouseWheelH += sdlEvent.wheel.x;
                            imguiInput.mouseWheelV += sdlEvent.wheel.y;
                            break;
                    }
                    coocoo3DMain.RequireRender();
                }

                var modState = SDL_GetModState();
                imguiInput.KeyAlt = (int)(modState & SDL_Keymod.KMOD_ALT) != 0;
                imguiInput.KeyShift = (int)(modState & SDL_Keymod.KMOD_SHIFT) != 0;
                imguiInput.KeyControl = (int)(modState & SDL_Keymod.KMOD_CTRL) != 0;
                SDL_SetCursor(cursors[imguiInput.requestCursor]);
                //imguiInput.Update();

                if (imguiInput.WantTextInput)
                    SDL_StartTextInput();
                else
                    SDL_StopTextInput();
                SDL_CaptureMouse(imguiInput.WantCaptureMouse ? SDL_bool.SDL_TRUE : SDL_bool.SDL_FALSE);

                UIHelper.OnFrame(coocoo3DMain);
            }
        }
    }
}

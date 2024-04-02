﻿using Bannerlord.ButterLib.CrashReportWindow.OpenGL;
using Bannerlord.ButterLib.CrashReportWindow.Windowing;

using ImGuiNET;

using System;
using System.Runtime.InteropServices;

namespace Bannerlord.ButterLib.CrashReportWindow.Controller;

using static Gl;
using static Glfw;

partial class ImGuiController
{
    private static readonly byte[] VertexShaderUtf8 = """
                                                      #version 330 core
                                                      layout (location = 0) in vec2 Position;
                                                      layout (location = 1) in vec2 UV;
                                                      layout (location = 2) in vec4 Color;
                                                      uniform mat4 ProjMtx;
                                                      out vec2 Frag_UV;
                                                      out vec4 Frag_Color;
                                                      void main()
                                                      {
                                                          Frag_UV = UV;
                                                          Frag_Color = Color;
                                                          gl_Position = ProjMtx * vec4(Position.xy,0,1);
                                                      } 
                                                      """u8.ToArray();

    private static readonly byte[] FragmentShaderUtf8 = """
                                                        #version 330 core
                                                        in vec2 Frag_UV;
                                                        in vec4 Frag_Color;
                                                        uniform sampler2D Texture;
                                                        layout (location = 0) out vec4 Out_Color;
                                                        void main()
                                                        {
                                                            Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                                                        } 
                                                        """u8.ToArray();

    public void Init()
    {
        _io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        _io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
        _io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        _glfw.GetWindowSize(in _windowPtr.Handle, out var w, out var h);
        _windowsWidth = (uint) w;
        _windowsHeight = (uint) h;

        _glfw.GetFramebufferSize(in _windowPtr.Handle, out var fw, out var fh);
        _frameBufferWidth = (uint) fw;
        _frameBufferHeight = (uint) fh;

        InitGlfw();
        CreateDeviceObjects();

        _io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(SetClipboardTextFnDelegate);
        _io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(GetClipboardTextFnDelegate);
        _io.ClipboardUserData = _windowPtr;
    }

    private void InitGlfw()
    {
        _mouseCursors[(int) ImGuiMouseCursor.Arrow] = _glfw.CreateStandardCursor(GLFW_ARROW_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.TextInput] = _glfw.CreateStandardCursor(GLFW_IBEAM_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.ResizeNS] = _glfw.CreateStandardCursor(GLFW_VRESIZE_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.ResizeEW] = _glfw.CreateStandardCursor(GLFW_HRESIZE_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.Hand] = _glfw.CreateStandardCursor(GLFW_HAND_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.ResizeAll] = _glfw.CreateStandardCursor(GLFW_ARROW_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.ResizeNESW] = _glfw.CreateStandardCursor(GLFW_ARROW_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.ResizeNWSE] = _glfw.CreateStandardCursor(GLFW_ARROW_CURSOR);
        _mouseCursors[(int) ImGuiMouseCursor.NotAllowed] = _glfw.CreateStandardCursor(GLFW_ARROW_CURSOR);

        _glfw.SetWindowSizeCallback(in _windowPtr.Handle, _userCallbackWindowsSize);
        _glfw.SetFramebufferSizeCallback(in _windowPtr.Handle, _userCallbackFramebufferSize);
        _glfw.SetMouseButtonCallback(in _windowPtr.Handle, _userCallbackMouseButton);
        _glfw.SetScrollCallback(in _windowPtr.Handle, _userCallbackScroll);
        _glfw.SetKeyCallback(in _windowPtr.Handle, _userCallbackKey);
        _glfw.SetCharCallback(in _windowPtr.Handle, _userCallbackChar);
    }

    private void CreateDeviceObjects()
    {
        var lastTexture = _gl.GetInteger(GL_TEXTURE_BINDING_2D);
        var lastArrayBuffer = _gl.GetInteger(GL_ARRAY_BUFFER_BINDING);
        var lastVertexArray = _gl.GetInteger(GL_VERTEX_ARRAY_BINDING);

        _shader = new Shader(_gl, VertexShaderUtf8, FragmentShaderUtf8);

        _attribLocationTex = _shader.GetUniformLocation("Texture\0"u8);
        _attribLocationProjMtx = _shader.GetUniformLocation("ProjMtx"u8);
        _attribLocationVtxPos = (uint) _shader.GetAttribLocation("Position\0"u8);
        _attribLocationVtxUv = (uint) _shader.GetAttribLocation("UV\0"u8);
        _attribLocationVtxColor = (uint) _shader.GetAttribLocation("Color\0"u8);

        _vboHandle = _gl.GenBuffer();
        _elementsHandle = _gl.GenBuffer();

        CreateFontsTexture();

        _gl.BindTexture(GL_TEXTURE_2D, (uint) lastTexture);
        _gl.BindBuffer(GL_ARRAY_BUFFER, (uint) lastArrayBuffer);
        _gl.BindVertexArray((uint) lastVertexArray);

        _gl.CheckGlError("End of ImGui setup");
    }

    private void CreateFontsTexture()
    {
        // Build texture atlas
        // Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders.
        // If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory
        _io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var width, out var height);

        // Upload texture to graphics system
        var lastTexture = _gl.GetInteger(GL_TEXTURE_BINDING_2D);

        _fontTexture = new Texture(_gl, (uint) width, (uint) height, pixels);
        _fontTexture.Bind();
        _fontTexture.SetMagFilter(GL_LINEAR);
        _fontTexture.SetMinFilter(GL_LINEAR);
        _gl.PixelStorei(GL_UNPACK_ROW_LENGTH, 0);

        _io.Fonts.SetTexID((IntPtr) _fontTexture.GlTexture);

        _gl.BindTexture(GL_TEXTURE_2D, (uint) lastTexture);
    }

    private void DestroyDeviceObjects()
    {
        if (_vboHandle != 0)
        {
            _gl.DeleteBuffer(_vboHandle);
            _vboHandle = 0;
        }

        if (_elementsHandle != 0)
        {
            _gl.DeleteBuffer(_elementsHandle);
            _elementsHandle = 0;
        }
    }

    private void DestroyShader()
    {
        _shader.Dispose();
    }

    private void DestroyFontsTexture()
    {
        _io.Fonts.SetTexID(IntPtr.Zero);
        _fontTexture.Dispose();
    }

    private void GlfwShutdown()
    {
        for (ImGuiMouseCursor cursorN = 0; cursorN < ImGuiMouseCursor.COUNT; cursorN++)
        {
            _glfw.DestroyCursor(_mouseCursors[(int) cursorN]);
            _mouseCursors[(int) cursorN] = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        DestroyDeviceObjects();
        DestroyShader();
        DestroyFontsTexture();
        GlfwShutdown();
        _imgui.DestroyContext(_context);
    }
}
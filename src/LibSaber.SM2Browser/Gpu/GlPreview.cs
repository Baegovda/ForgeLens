using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using LibSaber.SM2.Assets.Meshes;
using Silk.NET.OpenGL;

namespace LibSaber.SM2Browser.Gpu;

/// <summary>
///   GPU mesh preview. Static interleaved VBO/IBO per vertex pool (uploaded once per mesh/LOD), skinning in the
///   vertex shader from a float bone texture (any bone count), per-pixel lighting with interpolated vertex
///   normals and a per-pixel cotangent frame for tangent-space normal maps, sRGB albedo with full mip chains,
///   trilinear + anisotropic filtering, Toksvig specular AA, MSAA render target resolved into the host
///   framebuffer. Works on GLES 3.0 (ANGLE on Windows, which runs on D3D11) and desktop GL 3.3 core.
/// </summary>
internal sealed unsafe class GlPreview : IDisposable
{
  private const int Stride = 16; // pos3 nrm3 uv2 joint4 weight4
  private const GLEnum MaxAnisotropy = (GLEnum)0x84FE;
  private const GLEnum MaxAnisotropyLimit = (GLEnum)0x84FF;

  private readonly GL _gl;
  private readonly bool _es;
  private uint _program;
  private uint _boneTex;
  private int _boneRows;
  private uint _msFbo, _msColor, _msDepth, _rsFbo, _rsColor;
  private int _fbW, _fbH;
  private uint _lineVao, _lineVbo;
  private readonly Dictionary<object, GpuBatch> _batches = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<PreviewImage, uint> _textures = new(ReferenceEqualityComparer.Instance);
  private readonly HashSet<PreviewImage> _srgb = new(ReferenceEqualityComparer.Instance);
  private object? _owner;
  private readonly Dictionary<string, int> _uniforms = new();

  /// <summary>Bench "before" frames: every part in the opaque pass, as v3 drew them.</summary>
  public bool LegacyOpaque { get; set; }

  public int Samples { get; private set; }
  public float Anisotropy { get; private set; } = 1;
  public string Info { get; private set; } = "";
  /// <summary>True when a texture upload was deferred to keep the frame short; render again.</summary>
  public bool Pending { get; private set; }
  public double LastUploadMs { get; private set; }

  private sealed class GpuBatch
  {
    public uint Vao, Vbo, Ibo, LineIbo;
    public int LineCount;
    public int[] LineStart = Array.Empty<int>();
    public int[] LineLen = Array.Empty<int>();
    public bool Skinned;
  }

  public GlPreview(GL gl, bool es)
  {
    _gl = gl;
    _es = es;
    Init();
  }

  private void Init()
  {
    var header = _es
      ? "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2D;\n"
      : "#version 330 core\n";
    _program = Link(header + VertexSource, header + FragmentSource);
    _gl.GetInteger(GLEnum.MaxSamples, out int maxSamples);
    Samples = Math.Clamp(maxSamples >= 8 ? 8 : maxSamples, 0, 8);
    _gl.GetFloat(MaxAnisotropyLimit, out float aniso);
    if (_gl.GetError() == GLEnum.NoError && aniso >= 1)
      Anisotropy = Math.Min(16f, aniso);
    else
      Anisotropy = 1;
    while (_gl.GetError() != GLEnum.NoError) { }
    _boneTex = _gl.GenTexture();
    _gl.BindTexture(TextureTarget.Texture2D, _boneTex);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    _lineVao = _gl.GenVertexArray();
    _lineVbo = _gl.GenBuffer();
    _gl.BindVertexArray(_lineVao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
    _gl.EnableVertexAttribArray(0);
    _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
    _gl.BindVertexArray(0);
    var renderer = Marshal.PtrToStringAnsi((nint)_gl.GetString(StringName.Renderer)) ?? "?";
    var version = Marshal.PtrToStringAnsi((nint)_gl.GetString(StringName.Version)) ?? "?";
    Info = $"{renderer} | {version} | MSAA {Samples}x | aniso {Anisotropy:0}x";
  }

  private uint Link(string vs, string fs)
  {
    uint Compile(ShaderType type, string source)
    {
      var shader = _gl.CreateShader(type);
      _gl.ShaderSource(shader, source);
      _gl.CompileShader(shader);
      _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var ok);
      if (ok == 0)
        throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
      return shader;
    }

    var v = Compile(ShaderType.VertexShader, vs);
    var f = Compile(ShaderType.FragmentShader, fs);
    var program = _gl.CreateProgram();
    _gl.AttachShader(program, v);
    _gl.AttachShader(program, f);
    _gl.BindAttribLocation(program, 0, "aPos");
    _gl.BindAttribLocation(program, 1, "aNrm");
    _gl.BindAttribLocation(program, 2, "aUv");
    _gl.BindAttribLocation(program, 3, "aJoint");
    _gl.BindAttribLocation(program, 4, "aWeight");
    _gl.LinkProgram(program);
    _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
    if (linked == 0)
      throw new InvalidOperationException($"link failed: {_gl.GetProgramInfoLog(program)}");
    _gl.DetachShader(program, v);
    _gl.DetachShader(program, f);
    _gl.DeleteShader(v);
    _gl.DeleteShader(f);
    return program;
  }

  private int U(string name)
  {
    if (!_uniforms.TryGetValue(name, out var location))
      _uniforms[name] = location = _gl.GetUniformLocation(_program, name);
    return location;
  }

  private void EnsureTargets(int width, int height)
  {
    if (width == _fbW && height == _fbH && _msFbo != 0)
      return;
    DeleteTargets();
    _fbW = width;
    _fbH = height;
    _msFbo = _gl.GenFramebuffer();
    _msColor = _gl.GenRenderbuffer();
    _msDepth = _gl.GenRenderbuffer();
    _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msColor);
    _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)Samples, InternalFormat.Rgba8, (uint)width, (uint)height);
    _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msDepth);
    _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)Samples, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
    _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msColor);
    _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _msDepth);
    var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
    if (status != GLEnum.FramebufferComplete)
      throw new InvalidOperationException($"MSAA framebuffer incomplete: {status}");
    _rsFbo = _gl.GenFramebuffer();
    _rsColor = _gl.GenRenderbuffer();
    _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rsColor);
    _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)width, (uint)height);
    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _rsFbo);
    _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _rsColor);
    status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
    if (status != GLEnum.FramebufferComplete)
      throw new InvalidOperationException($"resolve framebuffer incomplete: {status}");
  }

  private void DeleteTargets()
  {
    if (_msFbo == 0)
      return;
    _gl.DeleteFramebuffer(_msFbo);
    _gl.DeleteFramebuffer(_rsFbo);
    _gl.DeleteRenderbuffer(_msColor);
    _gl.DeleteRenderbuffer(_msDepth);
    _gl.DeleteRenderbuffer(_rsColor);
    _msFbo = _rsFbo = _msColor = _msDepth = _rsColor = 0;
  }

  /// <summary>Drops every buffer and texture of the previous mesh when a different mesh is drawn.</summary>
  private void Own(object owner)
  {
    if (ReferenceEquals(owner, _owner))
      return;
    _owner = owner;
    foreach (var batch in _batches.Values)
    {
      _gl.DeleteVertexArray(batch.Vao);
      _gl.DeleteBuffer(batch.Vbo);
      _gl.DeleteBuffer(batch.Ibo);
      if (batch.LineIbo != 0)
        _gl.DeleteBuffer(batch.LineIbo);
    }

    _batches.Clear();
    foreach (var texture in _textures.Values)
      _gl.DeleteTexture(texture);
    _textures.Clear();
    _srgb.Clear();
  }

  private GpuBatch Upload(PreviewBatch batch)
  {
    if (_batches.TryGetValue(batch.Key, out var existing))
      return existing;
    var count = batch.Positions.Length;
    var data = new float[count * Stride];
    var skin = batch.Skin;
    Parallel.For(0, (count + 8191) / 8192, chunk =>
    {
      var end = Math.Min(count, (chunk + 1) * 8192);
      for (var i = chunk * 8192; i < end; i++)
      {
        var o = i * Stride;
        var p = batch.Positions[i];
        var n = i < batch.Normals.Length ? batch.Normals[i] : Vector3.UnitY;
        var t = i < batch.Uvs.Length ? batch.Uvs[i] : Vector2.Zero;
        data[o] = p.X; data[o + 1] = p.Y; data[o + 2] = p.Z;
        data[o + 3] = n.X; data[o + 4] = n.Y; data[o + 5] = n.Z;
        data[o + 6] = t.X; data[o + 7] = t.Y;
        if (skin is not null)
        {
          var s = skin[i];
          data[o + 8] = s.J0; data[o + 9] = s.J1; data[o + 10] = s.J2; data[o + 11] = s.J3;
          data[o + 12] = s.W0; data[o + 13] = s.W1; data[o + 14] = s.W2; data[o + 15] = s.W3;
        }
      }
    });

    // Out-of-range indices (the CPU path skips those triangles) collapse to vertex 0.
    var indices = new uint[batch.Indices.Length];
    for (var i = 0; i < indices.Length; i++)
    {
      var index = batch.Indices[i];
      indices[i] = (uint)index < (uint)count ? (uint)index : 0u;
    }

    var gpu = new GpuBatch { Skinned = skin is not null };
    gpu.Vao = _gl.GenVertexArray();
    gpu.Vbo = _gl.GenBuffer();
    gpu.Ibo = _gl.GenBuffer();
    _gl.BindVertexArray(gpu.Vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, gpu.Vbo);
    _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, data, BufferUsageARB.StaticDraw);
    _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, gpu.Ibo);
    _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
    uint stride = Stride * 4;
    _gl.EnableVertexAttribArray(0);
    _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
    _gl.EnableVertexAttribArray(1);
    _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
    _gl.EnableVertexAttribArray(2);
    _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
    _gl.EnableVertexAttribArray(3);
    _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
    _gl.EnableVertexAttribArray(4);
    _gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, (void*)48);
    _gl.BindVertexArray(0);
    _batches[batch.Key] = gpu;
    return gpu;
  }

  private void EnsureLines(PreviewBatch batch, GpuBatch gpu)
  {
    if (gpu.LineIbo != 0)
      return;
    var parts = batch.Parts;
    var lines = new List<uint>(batch.Indices.Length * 2);
    gpu.LineStart = new int[parts.Count];
    gpu.LineLen = new int[parts.Count];
    var count = batch.Positions.Length;
    for (var p = 0; p < parts.Count; p++)
    {
      gpu.LineStart[p] = lines.Count;
      var end = parts[p].IndexStart + parts[p].IndexCount;
      for (var i = parts[p].IndexStart; i + 2 < end + 0 && i + 2 < batch.Indices.Length; i += 3)
      {
        int a = batch.Indices[i], b = batch.Indices[i + 1], c = batch.Indices[i + 2];
        if ((uint)a >= (uint)count || (uint)b >= (uint)count || (uint)c >= (uint)count)
          continue;
        lines.Add((uint)a); lines.Add((uint)b);
        lines.Add((uint)b); lines.Add((uint)c);
        lines.Add((uint)c); lines.Add((uint)a);
      }

      gpu.LineLen[p] = lines.Count - gpu.LineStart[p];
    }

    gpu.LineIbo = _gl.GenBuffer();
    _gl.BindVertexArray(0);
    _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, gpu.LineIbo);
    _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, lines.ToArray(), BufferUsageARB.StaticDraw);
    gpu.LineCount = lines.Count;
  }

  private uint Texture(PreviewImage? image, bool srgb, Stopwatch budget)
  {
    if (image is null)
      return 0;
    if (_textures.TryGetValue(image, out var id))
      return id;
    if (budget.Elapsed.TotalMilliseconds > 120)
    {
      Pending = true;
      return 0;
    }

    var start = budget.Elapsed.TotalMilliseconds;
    var w = image.Width;
    var h = image.Height;
    var rgba = new byte[w * h * 4];
    var src = image.Bgra;
    Parallel.For(0, h, y =>
    {
      var row = y * w * 4;
      for (var x = 0; x < w * 4; x += 4)
      {
        rgba[row + x] = src[row + x + 2];
        rgba[row + x + 1] = src[row + x + 1];
        rgba[row + x + 2] = src[row + x];
        rgba[row + x + 3] = src[row + x + 3];
      }
    });
    var levels = 1 + (int)Math.Floor(Math.Log2(Math.Max(w, h)));
    id = _gl.GenTexture();
    _gl.BindTexture(TextureTarget.Texture2D, id);
    _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
    _gl.TexStorage2D(TextureTarget.Texture2D, (uint)levels, srgb ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8, (uint)w, (uint)h);
    _gl.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, 0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
    _gl.GenerateMipmap(TextureTarget.Texture2D);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
    if (Anisotropy > 1)
      _gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)MaxAnisotropy, Anisotropy);
    _textures[image] = id;
    LastUploadMs += budget.Elapsed.TotalMilliseconds - start;
    return id;
  }

  /// <summary>Draws the scene into the MSAA target, resolves it and blits it into <paramref name="targetFb"/>.</summary>
  public void Render(object owner, PreviewScene scene, int width, int height, bool wireframe, int targetFb)
  {
    var budget = Stopwatch.StartNew();
    Pending = false;
    LastUploadMs = 0;
    Own(owner);
    width = Math.Max(1, width);
    height = Math.Max(1, height);
    EnsureTargets(width, height);
    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
    _gl.Viewport(0, 0, (uint)width, (uint)height);
    _gl.ClearColor(28 / 255f, 24 / 255f, 22 / 255f, 1f); // same as the CPU preview
    _gl.ClearDepth(1f);
    _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthFunc(DepthFunction.Less);
    _gl.DepthMask(true);
    _gl.Disable(EnableCap.CullFace);
    _gl.Disable(EnableCap.Blend);
    _gl.Disable(EnableCap.ScissorTest);
    _gl.UseProgram(_program);

    var viewProjection = scene.View * scene.Projection;
    SetMatrix("uVP", viewProjection);
    SetVec3("uKey", scene.KeyLight);
    SetVec3("uFill", scene.FillLight);
    SetVec3("uEye", scene.Eye);
    _gl.Uniform1(U("uAlbedo"), 0);
    _gl.Uniform1(U("uNormal"), 1);
    _gl.Uniform1(U("uSpec"), 2);
    _gl.Uniform1(U("uBones"), 3);

    var skin = scene.SkinMatrices;
    if (skin is { Length: > 0 })
    {
      _gl.ActiveTexture(TextureUnit.Texture3);
      _gl.BindTexture(TextureTarget.Texture2D, _boneTex);
      _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
      fixed (Matrix4x4* pointer = skin)
      {
        if (_boneRows != skin.Length)
        {
          _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, 4, (uint)skin.Length, 0, PixelFormat.Rgba, PixelType.Float, pointer);
          _boneRows = skin.Length;
        }
        else
          _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 4, (uint)skin.Length, PixelFormat.Rgba, PixelType.Float, pointer);
      }

      _gl.Uniform1(U("uBoneCount"), skin.Length);
    }

    _gl.Uniform1(U("uMode"), 0);
    // Pass 1 opaque, pass 2 alpha-tested (alpha-to-coverage under MSAA), pass 3 blended back to front without
    // depth writes. Hair ships as a hard (masked) and a soft (blend) copy of the same cards: the soft pass uses
    // depth LESS, so it only adds the fringes the hard pass cut away.
    DrawPass(scene, skin, budget, PreviewBlend.Opaque);
    if (Samples > 1)
      _gl.Enable(EnableCap.SampleAlphaToCoverage);
    DrawPass(scene, skin, budget, PreviewBlend.Masked);
    _gl.Disable(EnableCap.SampleAlphaToCoverage);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
    _gl.DepthMask(false);
    DrawPass(scene, skin, budget, PreviewBlend.Translucent);
    _gl.DepthMask(true);
    _gl.Disable(EnableCap.Blend);

    // Overlays: flat colour, no depth test (as the CPU preview draws them on top).
    _gl.Disable(EnableCap.DepthTest);
    _gl.Uniform1(U("uMode"), 1);
    if (wireframe)
    {
      SetVec3("uColor", new Vector3(230, 220, 210) / 255f);
      foreach (var batch in scene.Batches)
      {
        var gpu = Upload(batch);
        EnsureLines(batch, gpu);
        _gl.Uniform1(U("uSkin"), skin is { Length: > 0 } && gpu.Skinned ? 1 : 0);
        _gl.BindVertexArray(gpu.Vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, gpu.LineIbo);
        for (var p = 0; p < batch.Parts.Count && p < gpu.LineStart.Length; p++)
        {
          if (batch.Parts[p].Draw && gpu.LineLen[p] > 0)
            _gl.DrawElements(PrimitiveType.Lines, (uint)gpu.LineLen[p], DrawElementsType.UnsignedInt, (void*)(gpu.LineStart[p] * 4L));
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, gpu.Ibo);
      }
    }

    if (scene.BoneLines.Count > 0)
    {
      var points = new float[scene.BoneLines.Count * 6];
      for (var i = 0; i < scene.BoneLines.Count; i++)
      {
        var (a, b) = scene.BoneLines[i];
        points[i * 6] = a.X; points[i * 6 + 1] = a.Y; points[i * 6 + 2] = a.Z;
        points[i * 6 + 3] = b.X; points[i * 6 + 4] = b.Y; points[i * 6 + 5] = b.Z;
      }

      SetVec3("uColor", new Vector3(230, 190, 80) / 255f);
      _gl.Uniform1(U("uSkin"), 0);
      _gl.BindVertexArray(_lineVao);
      _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
      _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, points, BufferUsageARB.StreamDraw);
      _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(scene.BoneLines.Count * 2));
    }

    _gl.BindVertexArray(0);
    _gl.UseProgram(0);
    // MSAA resolve into our own RGBA8 target (same format, always legal), then a plain blit to the host.
    _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
    _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _rsFbo);
    _gl.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _rsFbo);
    _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)targetFb);
    _gl.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)targetFb);
    _gl.ActiveTexture(TextureUnit.Texture0);
  }

  private void DrawPass(PreviewScene scene, Matrix4x4[]? skin, Stopwatch budget, PreviewBlend pass)
  {
    var items = new List<(PreviewBatch Batch, PreviewPartDraw Part, float Depth)>();
    foreach (var batch in scene.Batches)
    {
      foreach (var part in batch.Parts)
      {
        var blend = LegacyOpaque ? PreviewBlend.Opaque : part.Blend;
        if (!part.Draw || part.IndexCount < 3 || blend != pass)
          continue;
        items.Add((batch, part, pass == PreviewBlend.Translucent ? Vector3.DistanceSquared(part.Center, scene.Eye) : 0));
      }
    }

    if (items.Count == 0)
      return;
    if (pass == PreviewBlend.Translucent)
      items.Sort((a, b) => b.Depth.CompareTo(a.Depth));
    _gl.Uniform1(U("uBlend"), (int)pass);
    object? bound = null;
    foreach (var (batch, part, _) in items)
    {
      var gpu = Upload(batch);
      if (!ReferenceEquals(bound, batch))
      {
        _gl.Uniform1(U("uSkin"), skin is { Length: > 0 } && gpu.Skinned ? 1 : 0);
        _gl.BindVertexArray(gpu.Vao);
        bound = batch;
      }

      var albedo = Texture(part.Albedo, true, budget);
      var normal = Texture(part.Normal, false, budget);
      var spec = Texture(part.Specular, false, budget);
      // Without its coverage texture a cut-out or blended part would draw as a solid sheet: skip it this frame.
      if (pass != PreviewBlend.Opaque && part.AlphaFromAlbedo && albedo == 0)
        continue;
      Bind(TextureUnit.Texture0, albedo);
      Bind(TextureUnit.Texture1, normal);
      Bind(TextureUnit.Texture2, spec);
      _gl.Uniform1(U("uHasAlbedo"), albedo != 0 ? 1 : 0);
      _gl.Uniform1(U("uHasNormal"), normal != 0 ? 1 : 0);
      _gl.Uniform1(U("uHasSpec"), spec != 0 ? 1 : 0);
      _gl.Uniform1(U("uRoughA"), part.RoughInAlpha ? 1 : 0);
      _gl.Uniform1(U("uAoB"), part.AoInBlue ? 1 : 0);
      _gl.Uniform1(U("uUseAlpha"), pass != PreviewBlend.Opaque && part.AlphaFromAlbedo ? 1 : 0);
      SetVec3("uSolid", part.Solid);
      SetVec3("uTint", part.Tint);
      var aniso = part.Aniso;
      if (LegacyOpaque)
        aniso = null;
      _gl.Uniform1(U("uAniso"), aniso is null ? 0 : 1);
      if (aniso is not null)
      {
        var angle = Math.Clamp(aniso.Angle, 0f, 180f) * MathF.PI / 180f;
        _gl.Uniform2(U("uAnisoDir"), MathF.Cos(angle), MathF.Sin(angle));
        _gl.Uniform4(U("uAnisoParams"), aniso.Intensity, aniso.Anisotropy, part.SecondSpec ? aniso.ExtraIntensity : 0f, aniso.ExtraShift);
      }

      _gl.DepthFunc(pass == PreviewBlend.Translucent ? DepthFunction.Less : DepthFunction.Lequal);
      _gl.DrawElements(PrimitiveType.Triangles, (uint)part.IndexCount, DrawElementsType.UnsignedInt, (void*)(part.IndexStart * 4L));
    }

    _gl.DepthFunc(DepthFunction.Less);
  }

  /// <summary>Last resolved frame as top-down BGRA.</summary>
  public byte[] ReadPixels()
  {
    var rgba = new byte[_fbW * _fbH * 4];
    _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _rsFbo);
    _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
    fixed (byte* p = rgba)
      _gl.ReadPixels(0, 0, (uint)_fbW, (uint)_fbH, PixelFormat.Rgba, PixelType.UnsignedByte, p);
    var bgra = new byte[rgba.Length];
    for (var y = 0; y < _fbH; y++)
    {
      var s = (_fbH - 1 - y) * _fbW * 4;
      var d = y * _fbW * 4;
      for (var x = 0; x < _fbW * 4; x += 4)
      {
        bgra[d + x] = rgba[s + x + 2];
        bgra[d + x + 1] = rgba[s + x + 1];
        bgra[d + x + 2] = rgba[s + x];
        bgra[d + x + 3] = 255;
      }
    }

    return bgra;
  }

  public int Width => _fbW;
  public int Height => _fbH;

  public void Finish() => _gl.Finish();

  private void Bind(TextureUnit unit, uint texture)
  {
    _gl.ActiveTexture(unit);
    _gl.BindTexture(TextureTarget.Texture2D, texture);
  }

  private void SetMatrix(string name, Matrix4x4 m)
    => _gl.UniformMatrix4(U(name), 1, false, (float*)&m);

  private void SetVec3(string name, Vector3 v)
    => _gl.Uniform3(U(name), v.X, v.Y, v.Z);

  public void Dispose()
  {
    Own(new object());
    DeleteTargets();
    if (_program != 0)
      _gl.DeleteProgram(_program);
    if (_boneTex != 0)
      _gl.DeleteTexture(_boneTex);
    if (_lineVao != 0)
      _gl.DeleteVertexArray(_lineVao);
    if (_lineVbo != 0)
      _gl.DeleteBuffer(_lineVbo);
    _program = _boneTex = _lineVao = _lineVbo = 0;
  }

  // System.Numerics matrices are row-vector; uploaded as-is GL reads them transposed, so M * v == v * M.
  private const string VertexSource = """
    in vec3 aPos;
    in vec3 aNrm;
    in vec2 aUv;
    in vec4 aJoint;
    in vec4 aWeight;
    uniform mat4 uVP;
    uniform sampler2D uBones;
    uniform int uBoneCount;
    uniform int uSkin;
    out vec3 vPos;
    out vec3 vNrm;
    out vec2 vUv;
    mat4 boneMatrix(int i)
    {
      return mat4(texelFetch(uBones, ivec2(0, i), 0), texelFetch(uBones, ivec2(1, i), 0),
                  texelFetch(uBones, ivec2(2, i), 0), texelFetch(uBones, ivec2(3, i), 0));
    }
    void main()
    {
      vec3 p = aPos;
      vec3 n = aNrm;
      float total = aWeight.x + aWeight.y + aWeight.z + aWeight.w;
      if (uSkin == 1 && total > 0.0)
      {
        vec3 sp = vec3(0.0);
        vec3 sn = vec3(0.0);
        for (int k = 0; k < 4; k++)
        {
          float w = aWeight[k];
          if (w == 0.0)
            continue;
          int j = int(aJoint[k] + 0.5);
          if (j < 0 || j >= uBoneCount)
          {
            sp += aPos * w;
            sn += aNrm * w;
          }
          else
          {
            mat4 m = boneMatrix(j);
            sp += (m * vec4(aPos, 1.0)).xyz * w;
            sn += mat3(m) * aNrm * w;
          }
        }
        p = sp;
        n = dot(sn, sn) > 1e-12 ? normalize(sn) : aNrm;
      }
      vPos = p;
      vNrm = n;
      vUv = aUv;
      gl_Position = uVP * vec4(p, 1.0);
    }
    """;

  private const string FragmentSource = """
    in vec3 vPos;
    in vec3 vNrm;
    in vec2 vUv;
    uniform sampler2D uAlbedo;
    uniform sampler2D uNormal;
    uniform sampler2D uSpec;
    uniform int uHasAlbedo;
    uniform int uHasNormal;
    uniform int uHasSpec;
    uniform int uRoughA;
    uniform int uAoB;
    uniform int uMode;
    uniform vec3 uSolid;
    uniform vec3 uColor;
    uniform vec3 uKey;
    uniform vec3 uFill;
    uniform vec3 uEye;
    uniform vec3 uTint;
    uniform int uBlend;
    uniform int uUseAlpha;
    uniform int uAniso;
    uniform vec2 uAnisoDir;
    uniform vec4 uAnisoParams;
    out vec4 oColor;
    // Kajiya-Kay strand lobe: highlight where the half vector is perpendicular to the strand.
    float strand(vec3 T, vec3 H, float e)
    {
      float th = dot(T, H);
      return pow(sqrt(max(0.0, 1.0 - th * th)), e);
    }
    vec3 toLinear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
    vec3 toSrgb(vec3 c)
    {
      c = clamp(c, 0.0, 1.0);
      return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
    }
    void main()
    {
      if (uMode == 1)
      {
        oColor = vec4(uColor, 1.0);
        return;
      }
      vec3 N = dot(vNrm, vNrm) > 1e-12 ? normalize(vNrm) : vec3(0.0, 1.0, 0.0);
      vec3 V = normalize(uEye - vPos);
      // Cards and shells are two-sided: light the side that faces the camera.
      if (uBlend != 0 && dot(N, V) < 0.0)
        N = -N;
      // Albedo is an sRGB texture: the sampler returns linear, filtered and mipped in linear space.
      vec4 albedo = uHasAlbedo == 1 ? texture(uAlbedo, vUv) : vec4(toLinear(uSolid), 1.0);
      vec3 base = albedo.rgb * uTint;
      float alpha = uUseAlpha == 1 ? albedo.a : 1.0;
      if (uBlend == 1)
      {
        // Alpha test at 0.5, sharpened to about one pixel so alpha-to-coverage gives clean anti-aliased edges.
        alpha = (alpha - 0.5) / max(fwidth(alpha), 1e-4) + 0.5;
        if (alpha <= 0.0)
          discard;
        alpha = clamp(alpha, 0.0, 1.0);
      }
      else if (uBlend == 2 && alpha < 0.004)
        discard;
      vec3 L = N;
      float gloss = 1.0;
      vec3 frameT = vec3(0.0);
      if (uHasNormal == 1)
      {
        vec3 raw = texture(uNormal, vUv).rgb * 2.0 - 1.0;
        float len = length(raw);
        // Per-pixel cotangent frame (dP/du, dP/dv), Gram-Schmidted against the smooth normal.
        vec3 dp1 = dFdx(vPos);
        vec3 dp2 = dFdy(vPos);
        vec2 duv1 = dFdx(vUv);
        vec2 duv2 = dFdy(vUv);
        vec3 dp2perp = cross(dp2, N);
        vec3 dp1perp = cross(N, dp1);
        vec3 T = dp2perp * duv1.x + dp1perp * duv2.x;
        vec3 B = dp2perp * duv1.y + dp1perp * duv2.y;
        float m = max(dot(T, T), dot(B, B));
        if (m > 1e-30)
          frameT = (T * uAnisoDir.x + B * uAnisoDir.y) * inversesqrt(m);
        if (len > 1e-4 && m > 1e-30)
        {
          float inv = inversesqrt(m);
          vec3 mapped = T * inv * raw.x + B * inv * raw.y + N * raw.z;
          if (dot(mapped, mapped) > 1e-8)
            L = normalize(mapped);
          gloss = clamp(len, 0.05, 1.0);
        }
      }
      float metal = 0.0;
      float rough = 0.45;
      float ao = 1.0;
      if (uHasSpec == 1)
      {
        vec4 s = texture(uSpec, vUv);
        metal = s.r;
        rough = uRoughA == 1 ? s.a : s.g;
        ao = uAoB == 1 ? s.b : 1.0;
      }
      if (uAniso == 1 && uHasNormal == 0)
      {
        vec3 dp1 = dFdx(vPos);
        vec3 dp2 = dFdy(vPos);
        vec2 duv1 = dFdx(vUv);
        vec2 duv2 = dFdy(vUv);
        vec3 T = cross(dp2, N) * duv1.x + cross(N, dp1) * duv2.x;
        vec3 B = cross(dp2, N) * duv1.y + cross(N, dp1) * duv2.y;
        float m = max(dot(T, T), dot(B, B));
        if (m > 1e-30)
          frameT = (T * uAnisoDir.x + B * uAnisoDir.y) * inversesqrt(m);
      }
      vec3 H = normalize(uKey + V);
      float ndotl = clamp(dot(L, uKey), 0.0, 1.0);
      float ndotf = clamp(dot(L, uFill), 0.0, 1.0);
      float ndoth = clamp(dot(L, H), 0.0, 1.0);
      float e = 8.0 + (1.0 - clamp(rough, 0.0, 1.0)) * 56.0;
      // Toksvig: the mip-shortened normal widens the lobe so filtered bumps do not sparkle.
      e = gloss * e / (gloss + e * (1.0 - gloss));
      // Same light rig and terms as the CPU preview, evaluated in linear light.
      float diffuse = (0.16 + 0.72 * ndotl + 0.18 * ndotf) * (0.25 + 0.75 * ao);
      float spec = pow(ndoth, e) * (0.04 + metal * 0.85) * ndotl * ((e + 8.0) / 64.0);
      vec3 tintSpec = mix(vec3(1.0), base / max(max(base.r, max(base.g, base.b)), 1e-3), metal * 0.6);
      vec3 lit = base * pow(diffuse, 2.2) + vec3(spec) * tintSpec;
      if (uAniso == 1 && dot(frameT, frameT) > 1e-12)
      {
        // glt.anisotropic_spec (hair): base lobe along the strand, second (extra) lobe on the tangent shifted toward
        // N by extra.shift and tinted by the albedo, as in the UE master (T2 = normalize(T + shift * N)).
        vec3 T1 = normalize(frameT - L * dot(L, frameT));
        vec3 T2 = normalize(T1 + L * uAnisoParams.w);
        float wrap = clamp(dot(L, uKey) * 0.5 + 0.5, 0.0, 1.0);
        float e1 = mix(24.0, 160.0, 1.0 - clamp(rough, 0.0, 1.0)) * (0.5 + uAnisoParams.y);
        float s1 = strand(T1, H, e1) * 0.10 * uAnisoParams.x;
        float s2 = strand(T2, H, e1 * 0.3) * 0.16 * uAnisoParams.z;
        lit = base * pow(diffuse, 2.2) + (vec3(s1) + s2 * base * 2.0) * wrap * ao;
      }
      // Premultiplied output for the blended pass; alpha drives coverage in the masked pass.
      vec3 encoded = toSrgb(lit);
      oColor = uBlend == 2 ? vec4(encoded * alpha, alpha) : vec4(encoded, uBlend == 1 ? alpha : 1.0);
    }
    """;
}

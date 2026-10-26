using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using Silk.NET.OpenGL;

namespace LibSaber.SM2Browser.Gpu;

/// <summary>Decoded RGBA8 texture for the particle renderer.</summary>
internal sealed record EffectImage(int Width, int Height, byte[] Rgba);

/// <summary>What the effect pass draws this frame (built on the UI/bench thread, drawn on the GL thread).</summary>
internal sealed class EffectLayer
{
  public required List<SpriteBatch> Batches { get; init; }
  public required List<LightSample> Lights { get; init; }
  /// <summary>Texture by name; null while still decoding (the batch is skipped and the frame marked pending).</summary>
  public required Func<string, EffectImage?> Texture { get; init; }
  public bool Ground { get; init; } = true;
  public float GroundY { get; init; }
  public float Exposure { get; init; } = 1f / 250f;
  /// <summary>Total sprite quads (stats).</summary>
  public int Quads => Batches.Sum(b => b.QuadCount);
}

/// <summary>
///   GPU particle pass: a ground plane with grid lines lit by the effect's <c>sfx_light</c>s, then one draw per
///   emitter batch from far to near with that emitter's blend state. Colours are HDR (<c>colorIntensity</c> runs into
///   the hundreds for muzzle flashes), so each fragment is exposed and tonemapped (1 - e^-x) before blending.
///   blend: premultiplied over (ONE, 1-SRC_ALPHA); add: ONE, ONE; blend_add: unmultiplied colour with alpha occlusion.
///   Soft particles fade with <c>softDepth</c> @0x143645530 metres above the ground plane.
/// </summary>
internal sealed unsafe class EffectRenderer : IDisposable
{
  private readonly GL _gl;
  private readonly uint _sprite, _ground;
  private readonly uint _vao, _vbo, _ibo, _groundVao, _groundVbo;
  private readonly Dictionary<string, uint> _textures = new(StringComparer.OrdinalIgnoreCase);
  private uint _white;
  private int _iboQuads;

  public EffectRenderer(GL gl, string header)
  {
    _gl = gl;
    _sprite = Link(header + SpriteVs, header + SpriteFs);
    _ground = Link(header + GroundVs, header + GroundFs);
    _vao = gl.GenVertexArray();
    _vbo = gl.GenBuffer();
    _ibo = gl.GenBuffer();
    gl.BindVertexArray(_vao);
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    const uint stride = SpriteBatch.Stride * 4;
    gl.EnableVertexAttribArray(0);
    gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
    gl.EnableVertexAttribArray(1);
    gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)12);
    gl.EnableVertexAttribArray(2);
    gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)20);
    gl.EnableVertexAttribArray(3);
    gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, (void*)36);
    gl.EnableVertexAttribArray(4);
    gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, stride, (void*)44);
    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ibo);
    gl.BindVertexArray(0);
    _groundVao = gl.GenVertexArray();
    _groundVbo = gl.GenBuffer();
    gl.BindVertexArray(_groundVao);
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _groundVbo);
    float[] quad = [-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1];
    gl.BufferData<float>(BufferTargetARB.ArrayBuffer, quad, BufferUsageARB.StaticDraw);
    gl.EnableVertexAttribArray(0);
    gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, (void*)0);
    gl.BindVertexArray(0);
    _white = Upload(new EffectImage(1, 1, [255, 255, 255, 255]));
  }

  public bool Pending { get; private set; }

  private uint Link(string vs, string fs)
  {
    uint Compile(ShaderType type, string source)
    {
      var shader = _gl.CreateShader(type);
      _gl.ShaderSource(shader, source);
      _gl.CompileShader(shader);
      _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var ok);
      if (ok == 0)
        throw new InvalidOperationException($"effect {type} compile failed: {_gl.GetShaderInfoLog(shader)}");
      return shader;
    }

    var v = Compile(ShaderType.VertexShader, vs);
    var f = Compile(ShaderType.FragmentShader, fs);
    var program = _gl.CreateProgram();
    _gl.AttachShader(program, v);
    _gl.AttachShader(program, f);
    _gl.BindAttribLocation(program, 0, "aPos");
    _gl.BindAttribLocation(program, 1, "aTex");
    _gl.BindAttribLocation(program, 2, "aColor");
    _gl.BindAttribLocation(program, 3, "aParams");
    _gl.BindAttribLocation(program, 4, "aMask");
    _gl.LinkProgram(program);
    _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
    if (linked == 0)
      throw new InvalidOperationException($"effect link failed: {_gl.GetProgramInfoLog(program)}");
    _gl.DetachShader(program, v);
    _gl.DetachShader(program, f);
    _gl.DeleteShader(v);
    _gl.DeleteShader(f);
    return program;
  }
  public double LastMs { get; private set; }

  private uint Upload(EffectImage image)
  {
    var id = _gl.GenTexture();
    _gl.BindTexture(TextureTarget.Texture2D, id);
    _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
    var levels = 1 + (int)Math.Floor(Math.Log2(Math.Max(image.Width, image.Height)));
    _gl.TexStorage2D(TextureTarget.Texture2D, (uint)levels, SizedInternalFormat.Srgb8Alpha8, (uint)image.Width, (uint)image.Height);
    fixed (byte* p = image.Rgba)
      _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)image.Width, (uint)image.Height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
    _gl.GenerateMipmap(TextureTarget.Texture2D);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    return id;
  }

  private uint Texture(EffectLayer layer, string? name)
  {
    if (string.IsNullOrEmpty(name)) return _white;
    if (_textures.TryGetValue(name, out var id)) return id;
    var image = layer.Texture(name);
    if (image is null)
    {
      Pending = true;
      return 0;
    }

    id = image.Width > 0 ? Upload(image) : _white;
    _textures[name] = id;
    return id;
  }

  private void SetMatrix(uint program, string name, Matrix4x4 m) => _gl.UniformMatrix4(_gl.GetUniformLocation(program, name), 1, false, (float*)&m);

  /// <summary>Draws into the bound framebuffer; depth test on, depth writes only for the ground.</summary>
  public void Draw(EffectLayer layer, Matrix4x4 view, Matrix4x4 projection, Vector3 eye)
  {
    var watch = System.Diagnostics.Stopwatch.StartNew();
    Pending = false;
    var vp = view * projection;
    var lights = layer.Lights.Take(8).ToArray();
    if (layer.Ground)
    {
      _gl.UseProgram(_ground);
      SetMatrix(_ground, "uVP", vp);
      _gl.Uniform1(_gl.GetUniformLocation(_ground, "uY"), layer.GroundY);
      _gl.Uniform1(_gl.GetUniformLocation(_ground, "uExtent"), 40f);
      _gl.Uniform3(_gl.GetUniformLocation(_ground, "uEye"), eye.X, eye.Y, eye.Z);
      _gl.Uniform1(_gl.GetUniformLocation(_ground, "uExposure"), layer.Exposure);
      _gl.Uniform1(_gl.GetUniformLocation(_ground, "uLightCount"), lights.Length);
      for (var i = 0; i < lights.Length; i++)
      {
        var l = lights[i];
        _gl.Uniform4(_gl.GetUniformLocation(_ground, $"uLightPos[{i}]"), l.Position.X, l.Position.Y, l.Position.Z, Math.Max(0.01f, l.Radius));
        _gl.Uniform3(_gl.GetUniformLocation(_ground, $"uLightColor[{i}]"), l.Color.X * l.Intensity, l.Color.Y * l.Intensity, l.Color.Z * l.Intensity);
      }

      _gl.Enable(EnableCap.DepthTest);
      _gl.DepthMask(true);
      _gl.Disable(EnableCap.Blend);
      _gl.BindVertexArray(_groundVao);
      _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    var quads = layer.Batches.Count == 0 ? 0 : layer.Batches.Max(b => b.QuadCount);
    if (quads > _iboQuads)
    {
      _iboQuads = Math.Max(quads, _iboQuads * 2);
      var indices = new uint[_iboQuads * 6];
      for (uint q = 0; q < _iboQuads; q++)
      {
        var b = q * 4;
        indices[q * 6] = b; indices[q * 6 + 1] = b + 1; indices[q * 6 + 2] = b + 2;
        indices[q * 6 + 3] = b; indices[q * 6 + 4] = b + 2; indices[q * 6 + 5] = b + 3;
      }

      _gl.BindVertexArray(_vao);
      _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ibo);
      _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
    }

    _gl.UseProgram(_sprite);
    SetMatrix(_sprite, "uVP", vp);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uTex"), 0);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uMask"), 1);
    var hasMaskLoc = _gl.GetUniformLocation(_sprite, "uHasMask");
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uExposure"), layer.Exposure);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uGround"), layer.Ground ? 1 : 0);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uGroundY"), layer.GroundY);
    var modeLoc = _gl.GetUniformLocation(_sprite, "uMode");
    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthMask(false);
    _gl.Enable(EnableCap.Blend);
    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.ActiveTexture(TextureUnit.Texture0);
    foreach (var batch in layer.Batches)
    {
      var tex = Texture(layer, batch.Emitter.Texture);
      if (tex == 0) continue;
      var mask = batch.Emitter.AlphaMask is { } mn ? Texture(layer, mn) : 0u;
      if (batch.Emitter.AlphaMask is not null && mask == 0) continue;
      _gl.ActiveTexture(TextureUnit.Texture1);
      _gl.BindTexture(TextureTarget.Texture2D, mask);
      _gl.ActiveTexture(TextureUnit.Texture0);
      _gl.BindTexture(TextureTarget.Texture2D, tex);
      _gl.Uniform1(hasMaskLoc, mask != 0 ? 1 : 0);
      switch (batch.Emitter.Blend)
      {
        case EffectBlend.Add:
          _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
          break;
        default:
          _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
          break;
      }

      _gl.Uniform1(modeLoc, (int)batch.Emitter.Blend);
      fixed (float* p = batch.Vertices)
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(batch.QuadCount * 4 * SpriteBatch.Stride * 4), p, BufferUsageARB.StreamDraw);
      _gl.DrawElements(PrimitiveType.Triangles, (uint)(batch.QuadCount * 6), DrawElementsType.UnsignedInt, (void*)0);
    }

    _gl.DepthMask(true);
    _gl.Disable(EnableCap.Blend);
    _gl.BindVertexArray(0);
    _gl.UseProgram(0);
    LastMs = watch.Elapsed.TotalMilliseconds;
  }

  public void Dispose()
  {
    foreach (var id in _textures.Values.Distinct())
      if (id != _white) _gl.DeleteTexture(id);
    _gl.DeleteTexture(_white);
    _gl.DeleteProgram(_sprite);
    _gl.DeleteProgram(_ground);
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteVertexArray(_groundVao);
    _gl.DeleteBuffer(_vbo);
    _gl.DeleteBuffer(_ibo);
    _gl.DeleteBuffer(_groundVbo);
  }

  private const string SpriteVs = """
    in vec3 aPos;
    in vec2 aTex;
    in vec4 aColor;
    in vec2 aParams;
    in vec2 aMask;
    uniform mat4 uVP;
    out vec2 vMask;
    out vec2 vUv;
    out vec4 vColor;
    out vec2 vParams;
    out vec3 vWorld;
    void main()
    {
      vWorld = aPos;
      gl_Position = uVP * vec4(aPos, 1.0);
      vUv = aTex;
      vColor = aColor;
      vParams = aParams;
      vMask = aMask;
    }
    """;

  private const string SpriteFs = """
    in vec2 vUv;
    in vec4 vColor;
    in vec2 vParams;
    in vec3 vWorld;
    in vec2 vMask;
    uniform sampler2D uTex;
    uniform sampler2D uMask;
    uniform int uHasMask;
    uniform float uExposure;
    uniform int uMode;
    uniform int uGround;
    uniform float uGroundY;
    out vec4 outColor;
    vec3 toGamma(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
    void main()
    {
      vec4 t = texture(uTex, vUv);
      if (uHasMask == 1) t.a *= texture(uMask, vMask).a;
      float clip = vParams.x;
      float a = clip > 0.0 ? clamp((t.a - clip) / (1.0 - clip), 0.0, 1.0) : t.a;
      if (uGround == 1 && vParams.y > 0.0)
        a *= clamp((vWorld.y - uGroundY) / vParams.y, 0.0, 1.0);
      vec3 hdr = vColor.rgb * t.rgb;
      vec3 c = toGamma(vec3(1.0) - exp(-hdr * uExposure));
      float alpha = clamp(vColor.a, 0.0, 1.0) * a;
      if (uMode == 1) outColor = vec4(c * alpha, 0.0);          // add
      else if (uMode == 2) outColor = vec4(c * a, alpha);        // blend_add
      else outColor = vec4(c * alpha, alpha);                    // blend
    }
    """;

  private const string GroundVs = """
    in vec2 aPos;
    uniform mat4 uVP;
    uniform float uY;
    uniform float uExtent;
    out vec3 vWorld;
    void main()
    {
      vWorld = vec3(aPos.x * uExtent, uY, aPos.y * uExtent);
      gl_Position = uVP * vec4(vWorld, 1.0);
    }
    """;

  private const string GroundFs = """
    in vec3 vWorld;
    uniform vec3 uEye;
    uniform float uExposure;
    uniform int uLightCount;
    uniform vec4 uLightPos[8];
    uniform vec3 uLightColor[8];
    out vec4 outColor;
    float grid(vec2 p, float step, float width)
    {
      vec2 g = abs(fract(p / step - 0.5) - 0.5) * step;
      vec2 fw = fwidth(p) * width;
      vec2 l = 1.0 - smoothstep(vec2(0.0), fw, g);
      return max(l.x, l.y);
    }
    vec3 toGamma(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
    void main()
    {
      vec3 base = vec3(0.045, 0.042, 0.04);
      float minor = grid(vWorld.xz, 0.5, 1.0) * 0.35;
      float major = grid(vWorld.xz, 5.0, 1.5);
      base = mix(base, vec3(0.16), clamp(minor + major, 0.0, 1.0));
      vec3 light = vec3(0.6);
      for (int i = 0; i < uLightCount; i++)
      {
        vec3 d = uLightPos[i].xyz - vWorld;
        float r = uLightPos[i].w;
        float f = clamp(1.0 - length(d) / r, 0.0, 1.0);
        light += uLightColor[i] * uExposure * 8.0 * f * f * max(0.2, normalize(d).y);
      }
      vec3 hdr = base * light;
      float fade = clamp(1.0 - length(vWorld.xz - uEye.xz) / 60.0, 0.0, 1.0);
      outColor = vec4(toGamma(vec3(1.0) - exp(-hdr * 2.0)) * fade + vec3(28.0, 24.0, 22.0) / 255.0 * (1.0 - fade), 1.0);
    }
    """;
}

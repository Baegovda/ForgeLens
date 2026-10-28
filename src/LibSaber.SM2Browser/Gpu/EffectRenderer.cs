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
  /// <summary>Mesh particles (lwiGeom) and their templates by name (null while loading).</summary>
  public List<MeshBatch> Meshes { get; init; } = [];
  public Func<string, Effects.EffectMesh?> Mesh { get; init; } = _ => Effects.EffectMesh.Missing;
  /// <summary>Effect time (s) for material UV scroll.</summary>
  public float Time { get; init; }
  /// <summary>vfx3y bench bloom source pass: only meshes, weighted by material bloomIntensity (HEURISTIC).</summary>
  public bool BloomPass { get; init; }
  /// <summary>DEMO/HEURISTIC covering host (plane/sphere with a painted coveringMask), null = off.</summary>
  public CoveringDemo? Covering { get; init; }
  public int MeshInstances => Meshes.Sum(m => m.Instances.Count);
  /// <summary>Total sprite quads (stats).</summary>
  public int Quads => Batches.Sum(b => b.QuadCount);
}

/// <summary>
///   GPU particle pass: a ground plane with grid lines lit by the effect's <c>sfx_light</c>s, then one draw per
///   emitter batch from far to near with that emitter's blend state. Colours are HDR (<c>colorIntensity</c> runs into
///   the hundreds for muzzle flashes), so each fragment is exposed and tonemapped (1 - e^-x) before blending.
///   All particle blends are premultiplied ONE/INV_SRC_ALPHA (PROVEN psos, PS 281): add writes alpha 0; blend_add has no
///   flag bit of its own and renders exactly like blend (EffectBlendRea).
///   Soft particles fade with <c>softDepth</c> @0x143645530 metres above the ground plane.
/// </summary>
internal sealed unsafe class EffectRenderer : IDisposable
{
  private readonly GL _gl;
  private readonly uint _sprite, _ground, _mesh, _distort, _cover;
  private CoveringDemo? _coverDemo;
  private uint _coverVao, _coverVbo, _coverIbo, _coverTex;
  private int _coverIndexCount, _coverVersion = -1;
  private readonly Dictionary<string, uint> _linear = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>
  ///   Screen-UV shift per unit particle offset. mtl1-23 capture (plasma 101917, PS 4996 + post PS 5160, see
  ///   <see cref="DistortionRules"/>): 0.08, shift clamped to +-0.02 (was the 0.04 HEURISTIC). SM2_VFX_DISTORT overrides.
  /// </summary>
  public static float DistortionStrength { get; set; } =
    float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_DISTORT"), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : DistortionRules.UvPerOffset;
  /// <summary>
  ///   vfx3z: particle distortion buffer math from particles.sdc 00005_1 (offset = normal * per-vertex scalar, mask =
  ///   sat(10*|offset|), no edge fade). The per-vertex scalar is V_TEXCOORD.w of the CPU vertex (VS 00001_1 l.1573
  ///   passes it through). vfx4-1 PROVEN: the CPU vertex writer FUN_1417eeed0 (@0x1417ef1xx, typeParams==1 branch) sets
  ///   the particle colour to (0,0,0, power(age)): visuals.power is parsed by FUN_141796980 to payload+0x98 = desc+0x248,
  ///   a 64-sample spline read at (age-start)*invRange or the constant at desc+0x250, scaled by 1.0 (DAT_143c844b4), and
  ///   only multiplied by the per-particle colour when colour mode desc+0x108 == 4. Matching shader define
  ///   "DISTORTION_SCALE_CONST_MUL_VC_ALPHA" @0x1437f5a40 (registered in FUN_14236e020). Our r*alpha product equals that
  ///   for refraction-only emitters (alpha = alphaK). Resolve scale: mtl1-23 capture, post PS 5160 hardcodes
  ///   uv += DUDV*0.04 - 0.02 (DISTORT_POSTPROC_PARAMS [1,1,1/w,1/h] only sizes the target) -> shift = 0.08*mask*offset, max 0.02.
  ///   Default on; SM2_VFX_DISTORT_EXACT=0 restores the radial-edge version.
  /// </summary>
  public static bool DistortExact { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_DISTORT_EXACT") != "0";
  public int DistortionQuads { get; private set; }
  /// <summary>Mesh parts drawn with the blood-splash VAT path (SM2_VFX_VAT=1).</summary>
  public int VatDraws { get; private set; }
  /// <summary>
  ///   mtl1-38 PROVEN (static shader, particles PS 281 GLSL lines 468-472, 834; docs/handoff/SHADERS.md): tint blend
  ///   <c>rgb = mix(tex*V.rgb*boost, t*V.rgb*tex.a, t)</c> = <c>c + t*(t*V.rgb*tex.a - c)</c> with t = shared[0].z, which the
  ///   CPU packer FUN_1417e1e50 fills from emitter desc +0x4e8 (0x1417e21d1/0x1417e21d7) = typeParams+0x140 =
  ///   textureTintControl. Default ON since mtl1-38; <c>SM2_VFX_TINT_EXACT=0</c> restores the old luminance heuristic.
  /// </summary>
  public static bool TintExact { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_TINT_EXACT") != "0";
  /// <summary>vfx3x: tonemap premultiplied blend sprites (opt-in SM2_VFX_HDR_PREMUL=1; over-brightens flame_cross_4). HEURISTIC approximation of HDR blending.</summary>
  public static bool HdrPremul { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_HDR_PREMUL") == "1";
  /// <summary>vfx3x: particles.sdc alpha-clip math (vertex alpha inside the clip, 1-0.5*clip, no clip with flag 0x400). SM2_VFX_CLIP_EXACT=0 disables.</summary>
  public static bool ClipExact { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_CLIP_EXACT") != "0";
  /// <summary>vfx3y: material UV scroll (SM2_VFX_MESH_SCROLL=0 disables).</summary>
  public static bool ScrollEnabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_MESH_SCROLL") != "0";
  /// <summary>vfx3y HEURISTIC: bloom weight = min(1, bloomIntensity * gain); SM2_VFX_BLOOM_GAIN, default 0.01.</summary>
  /// <summary>
  ///   vfx3z: bench key light for lit (non-sfx, combine_material) mesh particles such as finisher blood. combine_material
  ///   is a deferred G-buffer shader (pc_dx12_combine_material.sdc perm 00001_0: 5 render targets, instance colour
  ///   unpacked /255 then sRGB-to-linear x*(x*(0.3053x+0.6822)+0.0125); emissive RT4 = CB_COMMON[22].x * mtl[8].x *
  ///   material emissive colour, not the sfxMtlOverrides tint). So its brightness comes from scene lighting, which the
  ///   bench does not have. Value 60 (was 200 = sprite light) matches the in-game finisher ref 155018 (~RGB 110/30/25):
  ///   HEURISTIC. SM2_VFX_SURF_LIGHT overrides.
  /// </summary>
  public static float SurfLight { get; set; } = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_SURF_LIGHT"), System.Globalization.CultureInfo.InvariantCulture, out var sl) ? sl : 60f;
  /// <summary>SELF_DISTORTION sdr warps the sprite's own UVs (power at mid-life; SM2_VFX_SELF_DISTORT=0 disables).</summary>
  public static bool SelfDistortion { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_SELF_DISTORT") != "0";
  /// <summary>
  ///   Self-distortion shift per unit offset. mtl1-23: the capture distortion formula (<see cref="DistortionRules"/>:
  ///   0.08 x sat(10|off|) x off, clamped to +-0.02) replaces the K = 0.04 heuristic. Applying the screen-space constants
  ///   to the sprite's own quad UV is HEURISTIC (the self-distortion shader was not captured). SM2_VFX_SELF_DISTORT_K overrides.
  /// </summary>
  public static float SelfDistortK { get; set; } =
    float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_SELF_DISTORT_K"), System.Globalization.CultureInfo.InvariantCulture, out var sk) ? sk : DistortionRules.UvPerOffset;
  /// <summary>
  ///   mtl1-23: CB_PASS_HDR tonemap curve from the captures (<see cref="CaptureTonemap"/>, LUT bake CS s1996) instead of
  ///   per-fragment 1-exp(-x). SM2_VFX_TONEMAP=exp restores the old curve.
  /// </summary>
  public static bool CaptureTonemapOn { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_TONEMAP") != "exp";
  /// <summary>
  ///   mtl1-23: sfx meshes blend One/One (alpha Zero/One) as in plasma 101917 (CB_DYNAMIC_SFX draws) instead of premultiplied
  ///   over. mtl1-38: the state per sfx define is PROVEN from the static PSO table (SFX_BLEND_ADD_NO_ALPHA = One/One, alpha
  ///   Zero/One; SFX_ALPHA_BLEND = SrcAlpha/InvSrcAlpha; neither = SrcAlpha/InvSrcAlpha, alpha Zero/Zero). Which sfx
  ///   material sets which define (define table 0x142450650) is not traced, so ADD_NO_ALPHA for every sfx mesh stays
  ///   capture-based (HEURISTIC). SM2_VFX_SFX_ADD=0 restores the premultiplied blend.
  /// </summary>
  public static bool SfxAdditive { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_SFX_ADD") != "0";
  /// <summary>
  ///   k of the SFX exposure compensation lerp(1, avgLum, k) (plasma PS s33060; k = sfx[560].w, td field not traced: HEURISTIC 1).
  ///   SM2_VFX_SFX_LUM_K overrides.
  /// </summary>
  public static float SfxLumK { get; set; } =
    float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_SFX_LUM_K"), System.Globalization.CultureInfo.InvariantCulture, out var lk) ? lk : 1f;
  /// <summary>Bench exposure treated as average luminance 1 for <see cref="SfxExposure"/> (the EffectLayer default).</summary>
  public const float ReferenceExposure = 1f / 250f;

  // mtl1-36: capture-calibrated light units (CaptureLightUnits): relative light (1 = particle ambient = capture avgLum),
  // scaled to each pass's ambient: sprite/mesh HDR ambient 200, ground ambient 0.6 and cover ambient 0.25 times albedo/pi.
  private static System.Numerics.Vector3 Rel(LightSample l, float exposure) => l.Relative(CaptureLightUnits.BenchAverageLuminance(exposure, ReferenceExposure));
  internal static System.Numerics.Vector3 MeshLight(LightSample l, float exposure) => Rel(l, exposure) * CaptureLightUnits.BenchAmbient;
  internal static System.Numerics.Vector3 GroundLight(LightSample l, float exposure) => Rel(l, exposure) * (0.6f * CaptureLightUnits.SurfaceScale);
  internal static System.Numerics.Vector3 CoverLight(LightSample l, float exposure) => Rel(l, exposure) * (0.25f * CaptureLightUnits.SurfaceScale);
  public static float BloomGain { get; set; } = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_BLOOM_GAIN"), System.Globalization.CultureInfo.InvariantCulture, out var bg) ? bg : 0.01f;
  /// <summary>
  ///   mtl1-38 PROVEN (particles PS 281 GLSL line ~468): additionalContrast boost
  ///   <c>1 + sat(1 - length(key/255 - tex.rgb)) * additionalContrast</c> (Euclidean length, multiplies tex*V.rgb before the
  ///   tint mix). Default ON since mtl1-38; <c>SM2_VFX_CONTRAST=0</c> disables.
  /// </summary>
  public static bool ContrastEnabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_CONTRAST") != "0";
  private readonly Dictionary<string, (uint Vao, uint Vbo, uint Ibo)?> _meshes = new(StringComparer.OrdinalIgnoreCase);
  private readonly uint _vao, _vbo, _ibo, _groundVao, _groundVbo;
  private readonly Dictionary<string, uint> _textures = new(StringComparer.OrdinalIgnoreCase);
  private uint _white;
  private int _iboQuads;

  public EffectRenderer(GL gl, string header)
  {
    _gl = gl;
    _sprite = Link(header + SpriteVs, header + CaptureTonemapGlsl.Source + SpriteFs);
    _ground = Link(header + GroundVs, header + GroundFs);
    _mesh = Link(header + MeshVs, header + CaptureTonemapGlsl.Source + MeshFs);
    _distort = Link(header + SpriteVs, header + DistortFs);
    _cover = Link(header + CoverVs, header + CoverFs);
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
  /// <summary>Texture names the source could not provide (not drawn on sprites).</summary>
  public HashSet<string> MissingTextures { get; } = new(StringComparer.OrdinalIgnoreCase);

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
    _gl.BindAttribLocation(program, 5, "aNormal");
    _gl.BindAttribLocation(program, 6, "aTex1");
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

  /// <summary>mtl1-42: texture name -> has alpha coverage (VfxDrawKinds.HasAlphaCoverage), filled on upload.</summary>
  private readonly Dictionary<string, bool> _alphaCoverage = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Batch draw kind after the texture check (a spark with an alpha-less texture draws as a particle).</summary>
  private VfxDrawKind ResolveKind(SpriteBatch batch) => VfxDrawKinds.Resolve(batch.Kind, TextureHasAlpha(batch.Emitter.Texture));

  private bool? TextureHasAlpha(string? name) => name is not null && _alphaCoverage.TryGetValue(name, out var c) ? c : null;

  private uint Upload(EffectImage image, bool linear = false)
  {
    var id = _gl.GenTexture();
    _gl.BindTexture(TextureTarget.Texture2D, id);
    _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
    var levels = 1 + (int)Math.Floor(Math.Log2(Math.Max(image.Width, image.Height)));
    _gl.TexStorage2D(TextureTarget.Texture2D, (uint)levels, linear ? SizedInternalFormat.Rgba8 : SizedInternalFormat.Srgb8Alpha8, (uint)image.Width, (uint)image.Height);
    fixed (byte* p = image.Rgba)
      _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)image.Width, (uint)image.Height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
    _gl.GenerateMipmap(TextureTarget.Texture2D);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    return id;
  }

  /// <summary>
  ///   GL texture for a name: 0 while decoding (<see cref="Pending"/>). A texture the source does not have is
  ///   skipped for sprites (a white quad stands out far more than a missing flare; fx_jetpack_attack's part_candle
  ///   drew as huge blue boxes) and drawn white for meshes, whose placeholder materials are tinted by overrides.
  /// </summary>
  private uint Texture(EffectLayer layer, string? name, bool missingWhite = false)
  {
    if (string.IsNullOrEmpty(name)) return _white;
    if (_textures.TryGetValue(name, out var id)) return id == uint.MaxValue ? (missingWhite ? _white : 0) : id;
    var image = layer.Texture(name);
    if (image is null)
    {
      Pending = true;
      return 0;
    }

    id = image.Width > 0 ? Upload(image) : uint.MaxValue;
    if (image.Width > 0) _alphaCoverage[name] = VfxDrawKinds.HasAlphaCoverage(image.Rgba);
    _textures[name] = id;
    if (id == uint.MaxValue) { MissingTextures.Add(name); return missingWhite ? _white : 0; }
    return id;
  }

  /// <summary>Normal maps for refraction: linear (an sRGB decode bends the zero offset 0.5 to 0.21).</summary>
  private uint LinearTexture(EffectLayer layer, string name)
  {
    if (_linear.TryGetValue(name, out var id)) return id == uint.MaxValue ? 0 : id;
    var image = layer.Texture(name);
    if (image is null) { Pending = true; return 0; }
    id = image.Width > 0 ? Upload(image, linear: true) : uint.MaxValue;
    if (id == uint.MaxValue) MissingTextures.Add(name);
    _linear[name] = id;
    return id == uint.MaxValue ? 0 : id;
  }

  private void SetMatrix(uint program, string name, Matrix4x4 m) => _gl.UniformMatrix4(_gl.GetUniformLocation(program, name), 1, false, (float*)&m);

  /// <summary>Draws into the bound framebuffer; depth test on, depth writes only for the ground.</summary>
  public void Draw(EffectLayer layer, Matrix4x4 view, Matrix4x4 projection, Vector3 eye, Func<uint>? grabScene = null, Vector2 viewport = default)
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
        var gc = GroundLight(l, layer.Exposure); _gl.Uniform3(_gl.GetUniformLocation(_ground, $"uLightColor[{i}]"), gc.X, gc.Y, gc.Z);
      }

      _gl.Enable(EnableCap.DepthTest);
      _gl.DepthMask(true);
      _gl.Disable(EnableCap.Blend);
      _gl.BindVertexArray(_groundVao);
      _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    if (layer.Covering is { Kind: not CoveringHostKind.Off } cover && !layer.BloomPass) DrawCovering(cover, vp, eye, lights, layer.Exposure);
    DrawMeshes(layer, vp, eye, lights, translucent: false);
    DrawMeshes(layer, vp, eye, lights, translucent: true);
    // vfx3z: sprites feed the bloom pass unweighted (HDR threshold bloom, see BenchBloom); meshes stay bloomIntensity-weighted.

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
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uTonemap"), CaptureTonemapOn ? 1 : 0);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uGround"), layer.Ground ? 1 : 0);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uGroundY"), layer.GroundY);
    var modeLoc = _gl.GetUniformLocation(_sprite, "uMode");
    var reaLoc = _gl.GetUniformLocation(_sprite, "uBlendRea");
    var alphaMulLoc = _gl.GetUniformLocation(_sprite, "uAlphaMul");
    var tintLoc = _gl.GetUniformLocation(_sprite, "uTint");
    var keyLoc = _gl.GetUniformLocation(_sprite, "uKey");
    var contrastLoc = _gl.GetUniformLocation(_sprite, "uContrast");
    var selfLoc = _gl.GetUniformLocation(_sprite, "uHasSelf");
    var selfPowLoc = _gl.GetUniformLocation(_sprite, "uSelfPow");
    var selfScrollLoc = _gl.GetUniformLocation(_sprite, "uSelfScroll");
    var selfKLoc = _gl.GetUniformLocation(_sprite, "uSelfK");
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uSelfTex"), 3);
    _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uTintExact"), TintExact ? 1 : 0);
    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthMask(false);
    _gl.Enable(EnableCap.Blend);
    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.ActiveTexture(TextureUnit.Texture0);
    var kindLoc = _gl.GetUniformLocation(_sprite, "uKind");
    SparkBatches = TrailBatches = 0;
    // mtl1-28: opaque sparks first (depth-written), then the blended batches far to near.
    foreach (var batch in layer.Batches.OrderBy(b => VfxDrawKinds.Enabled && ResolveKind(b) == VfxDrawKind.Spark ? 0 : 1))
    {
      if (batch.Distortion) continue;
      var tex = Texture(layer, batch.Emitter.Texture);
      if (tex == 0) continue;
      var mask = batch.Emitter.AlphaMask is { } mn ? Texture(layer, mn) : 0u;
      if (batch.Emitter.AlphaMask is { } m0 && mask == 0 && !MissingTextures.Contains(m0)) continue; // decoding; a missing mask draws unmasked
      // Resolve before binding: a first-time upload binds the new texture on the active unit (0 = the sprite texture).
      var self = SelfDistortion && batch.Emitter.SelfDistortTexture is { Length: > 0 } sn ? LinearTexture(layer, sn) : 0u;
      _gl.ActiveTexture(TextureUnit.Texture1);
      _gl.BindTexture(TextureTarget.Texture2D, mask);
      _gl.ActiveTexture(TextureUnit.Texture0);
      _gl.BindTexture(TextureTarget.Texture2D, tex);
      _gl.Uniform1(hasMaskLoc, mask != 0 ? 1 : 0);
      _gl.ActiveTexture(TextureUnit.Texture3);
      _gl.BindTexture(TextureTarget.Texture2D, self);
      _gl.ActiveTexture(TextureUnit.Texture0);
      _gl.Uniform1(selfLoc, self != 0 ? 1 : 0);
      if (self != 0)
      {
        _gl.Uniform1(selfPowLoc, batch.Emitter.SelfDistortPower.Sample(0.5f, 0.5f));
        _gl.Uniform2(selfKLoc, SelfDistortK, DistortionRules.MaxShift);
        var scroll = batch.Emitter.SelfDistortSpeed * layer.Time;
        _gl.Uniform2(selfScrollLoc, scroll.X - MathF.Floor(scroll.X), scroll.Y - MathF.Floor(scroll.Y));
      }
      var rea = EffectBlendRea.Enabled;
      _gl.Uniform1(reaLoc, rea ? 1 : 0);
      _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uHdrPremul"), HdrPremul ? 1 : 0);
      _gl.Uniform1(_gl.GetUniformLocation(_sprite, "uClipExact"), ClipExact ? 1 : 0);
      _gl.Uniform1(alphaMulLoc, (EffectBlendRea.Flags(batch.Emitter) & 0x400u) == 0 ? 1 : 0);
      if (rea)
        _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
      else switch (batch.Emitter.Blend)
      {
        case EffectBlend.Add:
          _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
          break;
        default:
          _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
          break;
      }

      var kind = VfxDrawKinds.Enabled ? ResolveKind(batch) : VfxDrawKind.CpuParticle;
      _gl.Uniform1(kindLoc, kind switch { VfxDrawKind.Trail => 1, VfxDrawKind.Spark => 2, _ => 0 });
      ApplyKindState(kind);
      if (kind == VfxDrawKind.Trail) TrailBatches++;
      if (kind == VfxDrawKind.Spark) SparkBatches++;
      // mtl1-41: with the REA blend the "add" mode is flag 0x20 (FUN_1417e1e50: add && (!default || screenSpace || !alphaKill)).
      _gl.Uniform1(modeLoc, rea ? ((EffectBlendRea.Flags(batch.Emitter) & 0x20u) != 0 ? 1 : 0) : (int)batch.Emitter.Blend);
      _gl.Uniform1(tintLoc, batch.Emitter.TextureTint);
      // additionalContrast / contrastColorKey (shared[1]); PROVEN PS 281 boost, default on (mtl1-38).
      var key = batch.Emitter.ContrastColorKey / 255f;
      _gl.Uniform3(keyLoc, key.X, key.Y, key.Z);
      _gl.Uniform1(contrastLoc, ContrastEnabled ? batch.Emitter.AdditionalContrast : 0f);
      fixed (float* p = batch.Vertices)
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(batch.QuadCount * 4 * SpriteBatch.Stride * 4), p, BufferUsageARB.StreamDraw);
      _gl.DrawElements(PrimitiveType.Triangles, (uint)(batch.QuadCount * 6), DrawElementsType.UnsignedInt, (void*)0);
    }

    _gl.Enable(EnableCap.Blend);
    _gl.DepthMask(false);
    DrawDistortion(layer, vp, grabScene, viewport);
    _gl.DepthMask(true);
    _gl.Disable(EnableCap.Blend);
    _gl.BindVertexArray(0);
    _gl.UseProgram(0);
    LastMs = watch.Elapsed.TotalMilliseconds;
  }

  /// <summary>
  ///   Refraction: after all colour, the frame so far is copied (<paramref name="grabScene"/>) and each distortion quad
  ///   re-draws the pixels under it from that copy, offset by its normal map (RG*2-1) x power x alpha, faded toward the
  ///   quad edge. Batches go far to near; each samples the same copy (one grab per frame, as one distortion buffer).
  /// </summary>
  /// <summary>Batches drawn with the capture trail / spark state in the last frame (bench log).</summary>
  public int TrailBatches { get; private set; }
  public int SparkBatches { get; private set; }

  /// <summary>
  ///   mtl1-28: capture blend state per kind (<see cref="VfxBlendRules"/>). Trails: SrcAlpha/InvSrcAlpha, alpha Zero/One
  ///   (CB_PASS_TRAIL), shader writes straight colour. Sparks: blending off, depth written, alpha-tested in the shader.
  ///   CPU particles keep the per-emitter state set just before this call.
  /// </summary>
  private void ApplyKindState(VfxDrawKind kind)
  {
    switch (kind)
    {
      case VfxDrawKind.Trail:
        _gl.Enable(EnableCap.Blend);
        _gl.DepthMask(false);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
        break;
      case VfxDrawKind.Spark:
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
        break;
      default:
        _gl.Enable(EnableCap.Blend);
        _gl.DepthMask(false);
        break;
    }
  }

  private void DrawDistortion(EffectLayer layer, Matrix4x4 vp, Func<uint>? grabScene, Vector2 viewport)
  {
    DistortionQuads = 0;
    if (grabScene is null || viewport.X <= 0 || DistortionStrength <= 0 || !layer.Batches.Any(b => b.Distortion)) return;
    var textures = new List<(SpriteBatch Batch, uint Tex)>();
    foreach (var batch in layer.Batches)
      if (batch.Distortion && LinearTexture(layer, batch.Emitter.DistortTexture!) is var t and > 0)
        textures.Add((batch, t));
    if (textures.Count == 0) return;
    var scene = grabScene();
    if (scene == 0) return;
    _gl.UseProgram(_distort);
    SetMatrix(_distort, "uVP", vp);
    _gl.Uniform1(_gl.GetUniformLocation(_distort, "uTex"), 0);
    _gl.Uniform1(_gl.GetUniformLocation(_distort, "uScene"), 2);
    _gl.Uniform2(_gl.GetUniformLocation(_distort, "uInvSize"), 1f / viewport.X, 1f / viewport.Y);
    _gl.Uniform1(_gl.GetUniformLocation(_distort, "uStrength"), DistortionStrength);
    _gl.Uniform1(_gl.GetUniformLocation(_distort, "uExact"), DistortExact ? 1 : 0);
    _gl.ActiveTexture(TextureUnit.Texture2);
    _gl.BindTexture(TextureTarget.Texture2D, scene);
    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthMask(false);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    foreach (var (batch, tex) in textures)
    {
      _gl.ActiveTexture(TextureUnit.Texture0);
      _gl.BindTexture(TextureTarget.Texture2D, tex);
      fixed (float* p = batch.Vertices)
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(batch.QuadCount * 4 * SpriteBatch.Stride * 4), p, BufferUsageARB.StreamDraw);
      _gl.DrawElements(PrimitiveType.Triangles, (uint)(batch.QuadCount * 6), DrawElementsType.UnsignedInt, (void*)0);
      DistortionQuads += batch.QuadCount;
    }

    _gl.ActiveTexture(TextureUnit.Texture2);
    _gl.BindTexture(TextureTarget.Texture2D, 0);
    _gl.ActiveTexture(TextureUnit.Texture0);
  }

  private (uint Vao, uint Vbo, uint Ibo)? MeshBuffers(Effects.EffectMesh mesh)
  {
    if (_meshes.TryGetValue(mesh.Name, out var cached)) return cached;
    if (mesh.IsMissing) return _meshes[mesh.Name] = null;
    var vao = _gl.GenVertexArray();
    var vbo = _gl.GenBuffer();
    var ibo = _gl.GenBuffer();
    _gl.BindVertexArray(vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
    _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, mesh.Vertices, BufferUsageARB.StaticDraw);
    _gl.EnableVertexAttribArray(0);
    _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 40, (void*)0);
    _gl.EnableVertexAttribArray(5);
    _gl.VertexAttribPointer(5, 3, VertexAttribPointerType.Float, false, 40, (void*)12);
    _gl.EnableVertexAttribArray(1);
    _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 40, (void*)24);
    _gl.EnableVertexAttribArray(6);
    _gl.VertexAttribPointer(6, 2, VertexAttribPointerType.Float, false, 40, (void*)32);
    _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ibo);
    _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, mesh.Indices, BufferUsageARB.StaticDraw);
    _gl.BindVertexArray(0);
    return _meshes[mesh.Name] = (vao, vbo, ibo);
  }

  /// <summary>
  ///   Mesh particles: each instance draws its template sub-object (<c>typeInst</c>) with the template's material.
  ///   Opaque materials write depth and are lit by a key light plus the effect's lights; <c>preset "sfx"</c>
  ///   materials blend premultiplied with their tint. <c>sfxMtlOverrides</c> colour/transparency multiply, and
  ///   <c>emissiveIntensity</c> adds HDR glow through the same exposure as sprites.
  /// </summary>
  private void DrawMeshes(EffectLayer layer, Matrix4x4 vp, Vector3 eye, LightSample[] lights, bool translucent)
  {
    if (layer.Meshes.Count == 0) return;
    _gl.UseProgram(_mesh);
    SetMatrix(_mesh, "uVP", vp);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uTex"), 0);
    // mtl1-23: sfx (translucent) emissive is exposure-compensated, lerp(1, avgLum, k) (plasma PS s33060, SfxExposure).
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uExposure"), translucent ? SfxExposure.Exposure(layer.Exposure, ReferenceExposure, SfxLumK) : layer.Exposure);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uTonemap"), CaptureTonemapOn ? 1 : 0);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uSfxAdd"), translucent && SfxAdditive ? 1 : 0);
    _gl.Uniform3(_gl.GetUniformLocation(_mesh, "uEye"), eye.X, eye.Y, eye.Z);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uLightCount"), lights.Length);
    for (var i = 0; i < lights.Length; i++)
    {
      var l = lights[i];
      _gl.Uniform4(_gl.GetUniformLocation(_mesh, $"uLightPos[{i}]"), l.Position.X, l.Position.Y, l.Position.Z, Math.Max(0.01f, l.Radius));
      var mc = MeshLight(l, layer.Exposure); _gl.Uniform3(_gl.GetUniformLocation(_mesh, $"uLightColor[{i}]"), mc.X, mc.Y, mc.Z);
    }

    var model = _gl.GetUniformLocation(_mesh, "uModel");
    var color = _gl.GetUniformLocation(_mesh, "uColor");
    var emissive = _gl.GetUniformLocation(_mesh, "uEmissive");
    var clip = _gl.GetUniformLocation(_mesh, "uClip");
    var fresnel = _gl.GetUniformLocation(_mesh, "uSoftFresnel");
    var scrollLoc = _gl.GetUniformLocation(_mesh, "uScroll");
    var bloomWLoc = _gl.GetUniformLocation(_mesh, "uBloomW");
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uTime"), ScrollEnabled ? layer.Time : 0f);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uBloomPass"), layer.BloomPass ? 1 : 0);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uSurfLight"), SurfLight);
    var mode = _gl.GetUniformLocation(_mesh, "uTranslucent");
    var vatOn = _gl.GetUniformLocation(_mesh, "uVat");
    var vatMin = _gl.GetUniformLocation(_mesh, "uVatMin");
    var vatScale = _gl.GetUniformLocation(_mesh, "uVatScale");
    var vatAnim = _gl.GetUniformLocation(_mesh, "uVatAnim");
    var vatHasN = _gl.GetUniformLocation(_mesh, "uVatHasNormal");
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uVatPos"), 3);
    _gl.Uniform1(_gl.GetUniformLocation(_mesh, "uVatNrm"), 4);
    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthMask(!translucent);
    if (translucent)
    {
      _gl.Enable(EnableCap.Blend);
      // mtl1-23: VfxBlendRules.For(SfxMesh) = One/One, alpha Zero/One (plasma 101917 CB_DYNAMIC_SFX draws).
      if (SfxAdditive)
        _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
      else
        _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
    }
    else _gl.Disable(EnableCap.Blend);

    _gl.Uniform1(mode, translucent ? 1 : 0);
    _gl.ActiveTexture(TextureUnit.Texture0);
    foreach (var batch in layer.Meshes)
    {
      var mesh = layer.Mesh(batch.Geom.Template);
      if (mesh is null) { Pending = true; continue; }
      if (MeshBuffers(mesh) is not { } buffers || mesh.Variants.Count == 0) continue;
      _gl.BindVertexArray(buffers.Vao);
      foreach (var inst in batch.Instances)
      {
        var parts = mesh.Variants[Math.Clamp(inst.Variant, 0, mesh.Variants.Count - 1)];
        var m = inst.World;
        _gl.UniformMatrix4(model, 1, false, (float*)&m);
        foreach (var (start, count, material) in parts)
        {
          if (material.Translucent != translucent) continue;
          var tex = Texture(layer, material.Texture, missingWhite: true);
          if (tex == 0) continue;
          _gl.BindTexture(TextureTarget.Texture2D, tex);
          var vat = LibSaber.SM2.Assets.Effects.VatDecode.Enabled ? material.Vat : null;
          var vatPos = vat is null ? 0 : LinearTexture(layer, vat.PositionTexture);
          if (vat is not null && vatPos != 0)
          {
            var vatN = vat.NormalTexture is { } nt ? LinearTexture(layer, nt) : 0;
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.Texture2D, vatPos);
            _gl.ActiveTexture(TextureUnit.Texture4);
            _gl.BindTexture(TextureTarget.Texture2D, vatN);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.Uniform1(vatOn, 1);
            _gl.Uniform3(vatMin, vat.Min.X, vat.Min.Y, vat.Min.Z);
            _gl.Uniform3(vatScale, vat.Scale.X, vat.Scale.Y, vat.Scale.Z);
            _gl.Uniform3(vatAnim, LibSaber.SM2.Assets.Effects.VatDecode.StageFrame(inst.Stage, vat.Frames), vat.Frames, vat.Bands);
            _gl.Uniform1(vatHasN, vatN != 0 ? 1 : 0);
            VatDraws++;
          }
          else _gl.Uniform1(vatOn, 0);
          var c = inst.Color * material.Tint;
          _gl.Uniform4(color, c.X, c.Y, c.Z, c.W);
          _gl.Uniform1(emissive, inst.Emissive);
          _gl.Uniform1(clip, material.Clip);
          _gl.Uniform1(fresnel, material.SoftFresnel);
          _gl.Uniform2(scrollLoc, material.Scroll.X, material.Scroll.Y);
          _gl.Uniform1(bloomWLoc, Math.Min(1f, material.Bloom * BloomGain));
          _gl.DrawElements(PrimitiveType.Triangles, (uint)count, DrawElementsType.UnsignedInt, (void*)(start * 4));
        }
      }
    }

    _gl.BindVertexArray(0);
    _gl.DepthMask(true);
  }

  public void Dispose()
  {
    foreach (var b in _meshes.Values)
      if (b is { } x) { _gl.DeleteVertexArray(x.Vao); _gl.DeleteBuffer(x.Vbo); _gl.DeleteBuffer(x.Ibo); }
    _gl.DeleteProgram(_mesh);
    _gl.DeleteProgram(_distort);
    _gl.DeleteProgram(_cover);
    if (_coverVao != 0) { _gl.DeleteVertexArray(_coverVao); _gl.DeleteBuffer(_coverVbo); _gl.DeleteBuffer(_coverIbo); _gl.DeleteTexture(_coverTex); }
    foreach (var id in _linear.Values)
      if (id != uint.MaxValue) _gl.DeleteTexture(id);
    foreach (var id in _textures.Values.Distinct())
      if (id != _white && id != uint.MaxValue) _gl.DeleteTexture(id);
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
    uniform int uBlendRea;
    uniform int uAlphaMul;
    uniform int uHdrPremul;
    uniform int uClipExact;
    uniform float uTint;
    uniform vec3 uKey;
    uniform float uContrast;
    uniform int uTintExact;
    uniform int uGround;
    uniform float uGroundY;
    uniform sampler2D uSelfTex;
    uniform int uHasSelf;
    uniform float uSelfPow;
    uniform vec2 uSelfScroll;
    uniform vec2 uSelfK;
    uniform int uKind;
    out vec4 outColor;
    vec3 toGamma(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
    vec2 SpriteUv()
    {
      if (uHasSelf == 0) return vUv;
      // SELF_DISTORTION: the sdr normal map, scrolled by its tcIn speed over the quad UV, offsets the sprite's own lookup.
      // mtl1-23: capture distortion formula (DistortionRules, PS 4996/5160): off = (RG*2-1)*power, shift =
      // clamp(0.08*sat(10|off|)*off, +-0.02) in quad units (HEURISTIC: quad space), converted to atlas-frame units.
      vec2 off = (texture(uSelfTex, vMask + uSelfScroll).rg * 2.0 - 1.0) * uSelfPow;
      float m = clamp(10.0 * (abs(off.x) + abs(off.y)), 0.0, 1.0);
      vec2 shift = clamp(off * m * uSelfK.x, vec2(-uSelfK.y), vec2(uSelfK.y));
      float k = clamp(length(dFdx(vUv)) / max(length(dFdx(vMask)), 1e-6), 0.0, 4.0);
      return vUv + shift * k;
    }

    void main()
    {
      vec2 uv = SpriteUv();
      vec4 t = texture(uTex, uv);
      if (uHasMask == 1) t.a *= texture(uMask, vMask).a;
      float clip = vParams.x;
      float a = clip > 0.0 ? clamp((t.a - clip) / (1.0 - clip), 0.0, 1.0) : t.a;
      if (uGround == 1 && vParams.y > 0.0)
        a *= clamp((vWorld.y - uGroundY) / vParams.y, 0.0, 1.0);
      vec3 hdr;
      if (uTintExact == 1)
      {
        // PROVEN (PS 281): c = tex*V*boost; rgb = mix(c, t*V*tex.a, t), t = textureTintControl (FUN_1417e1e50 -> shared[0].z).
        vec3 c0 = t.rgb * (1.0 + uContrast * clamp(1.0 - length(uKey - t.rgb), 0.0, 1.0)) * vColor.rgb;
        hdr = c0 + uTint * (uTint * vColor.rgb * t.a - c0);
      }
      else
      {
        vec3 tex = mix(vec3(dot(t.rgb, vec3(0.299, 0.587, 0.114))), t.rgb, uTint); // textureTintControl
        tex *= 1.0 + uContrast * clamp(1.0 - length(uKey - tex), 0.0, 1.0); // additionalContrast (particles.sdc 00114_2)
        hdr = vColor.rgb * tex;
      }
      vec3 c = toGamma(sm2Tonemap(hdr * uExposure));
      float alpha = clamp(vColor.a, 0.0, 1.0) * a;
      // mtl1-28 capture blend per kind: trail = straight colour for SrcAlpha/InvSrcAlpha (CB_PASS_TRAIL);
      // spark = blending off (smoke 88302 eid 2086): alpha test on texture coverage, vertex alpha scales brightness (HEURISTIC).
      if (uKind == 1) { outColor = vec4(c, alpha); return; }
      if (uKind == 2)
      {
        if (a < 0.5) discard;
        outColor = vec4(toGamma(sm2Tonemap(hdr * uExposure * clamp(vColor.a, 0.0, 1.0))), 1.0);
        return;
      }
      if (uHdrPremul == 1 && uMode != 1)
      {
        // vfx3x: particles.sdc 00000_0 writes SV_Target = hdr*alpha unclamped (only a NaN/Inf guard), so blending happens in
        // HDR and the frame is tonemapped later. Per-fragment 1-exp() saturated hdr ~12000 to white, then *alpha 0.08 = grey.
        // Approximation (HEURISTIC): tonemap the premultiplied contribution instead, keeping ONE/INV_SRC_ALPHA.
        float va = clamp(vColor.a, 0.0, 1.0);
        float k = uBlendRea == 1 ? va * (uAlphaMul == 1 ? a : 1.0) : alpha;
        float ao = uBlendRea == 1 ? va * a : alpha;
        outColor = vec4(toGamma(sm2Tonemap(hdr * uExposure * k)), ao);
      }
      else if (uBlendRea == 1 && uClipExact == 1)
      {
        // PROVEN (PS 281 GLSL 127-131, 788-790, 1077, 1091): clip = flag0x400 ? 0 : TEXCOORD_2.y;
        // ac = sat((tex.a*vertexA - clip) / (1 - 0.5*clip)); rgb *= sat(ac + flag0x400); SV_Target.w = add ? 0 : ac*mask.
        // Vertex alpha is inside the clip, so low-alpha alphaClip-by-age sprites (flame_cross_4/6/8, va ~0.08) burn out.
        // HEURISTIC: with flag 0x400 the shader colour factor is 1 (vertex alpha only in SV_Target.w); va is kept on rgb
        // here because whether the CPU also folds the age alpha into the colour/intensity (VS TEXCOORD.rgb =
        // COLOR_1*TEXCOORD_1.w*exposure, VS 248) is not traced.
        float va = clamp(vColor.a, 0.0, 1.0);
        float m = uHasMask == 1 ? texture(uMask, vMask).a : 1.0;
        float ta = texture(uTex, uv).a;
        float ce = uAlphaMul == 1 ? clip : 0.0;
        float ac = clamp((ta * va - ce) / (1.0 - 0.5 * ce), 0.0, 1.0);
        float ga = uGround == 1 && vParams.y > 0.0 ? clamp((vWorld.y - uGroundY) / vParams.y, 0.0, 1.0) : 1.0;
        float f = (uAlphaMul == 1 ? ac : va) * m * ga;
        outColor = vec4(c * f, uMode == 1 ? 0.0 : ac * m * ga);
      }
      else if (uBlendRea == 1)
      {
        // vfx3m: particles.sdc 00000_0 flag bits 0x20/0x400 (EffectBlendRea), premultiplied ONE/INV_SRC_ALPHA.
        float va = clamp(vColor.a, 0.0, 1.0);
        outColor = vec4(c * va * (uAlphaMul == 1 ? a : 1.0), uMode == 1 ? 0.0 : va * a);
      }
      else if (uMode == 1) outColor = vec4(c * alpha, 0.0);          // add
      else outColor = vec4(c * alpha, alpha);                    // blend; blend_add has no flag bit = blend (PROVEN, mtl1-38)
    }
    """;

  private const string DistortFs = """
    in vec2 vUv;
    in vec4 vColor;
    in vec2 vMask;
    uniform sampler2D uTex;
    uniform sampler2D uScene;
    uniform vec2 uInvSize;
    uniform float uStrength;
    uniform int uExact;
    out vec4 outColor;
    void main()
    {
      vec4 n = texture(uTex, vUv);
      vec2 q = vMask * 2.0 - 1.0;                       // quad-space -1..1 (unrotated by the mask angle is fine: radial)
      float edge = 1.0 - smoothstep(0.6, 1.0, length(q));
      float k = vColor.r * clamp(vColor.a, 0.0, 1.0) * edge;
      vec2 offset = clamp((n.rg * 2.0 - 1.0) * uStrength * k, vec2(-0.02), vec2(0.02)); // +-0.02: capture DUDV range (PS 5160)
      float a = clamp(k * 4.0, 0.0, 1.0);
      if (uExact == 1)
      {
        // vfx3z: particles.sdc 00005_1 (vfxsh_glsl/pc): o = (tex.rg*2.007874-1.007874) * TEXCOORD.w (per-vertex scalar);
        // mask = sat(10*(|o.x|+|o.y|)); buffer = mask*(o*2+0.5), w = mask. No radial edge fade in the shader.
        // mtl1-23 capture (plasma PS 4996 -> post PS 5160 uv += dudv*0.04-0.02, gated by w): shift = 0.08*mask*o, max 0.02.
        vec2 o = (n.rg * 2.007874 - 1.007874) * vColor.r * clamp(vColor.a, 0.0, 1.0);
        float mask = clamp(10.0 * (abs(o.x) + abs(o.y)), 0.0, 1.0);
        offset = clamp(o * mask * uStrength, vec2(-0.02), vec2(0.02));
        a = mask > 0.0 ? 1.0 : 0.0;
      }
      vec2 uv = gl_FragCoord.xy * uInvSize;
      vec3 c = texture(uScene, clamp(uv + offset, vec2(0.0), vec2(1.0))).rgb;
      outColor = vec4(c * a, a);
    }
    """;

  private const string MeshVs = """
    in vec3 aPos;
    in vec3 aNormal;
    in vec2 aTex;
    in vec2 aTex1;
    uniform mat4 uVP;
    uniform mat4 uModel;
    // Blood-splash VAT (pc_dx12_combine_material.sdc 00000_0): x = frame, y = frames, z = bands.
    uniform int uVat;
    uniform int uVatHasNormal;
    uniform vec3 uVatMin;
    uniform vec3 uVatScale;
    uniform vec3 uVatAnim;
    uniform sampler2D uVatPos;
    uniform sampler2D uVatNrm;
    out vec3 vWorld;
    out vec3 vNormal;
    out vec2 vUv;
    float vatRow(float f)
    {
      float q = f / uVatAnim.y;
      if (uVatAnim.z < 2.0) return -q;
      float fr = fract(abs(q));
      return aTex1.y - (q >= 0.0 ? fr : -fr) / uVatAnim.z;
    }
    // Texel-exact fetch (one column per vertex, so bilinear u would blend neighbours). The pct is ARGB8888 = DXGI 87
    // B8G8R8A8 (TextureDecode.DxgiOf) and decodes to raw bytes, so the DX .rgb is our .bgr. v wraps (fract): the VS's
    // uv1.y - frac(f/frames)/bands goes below 0 for band 0 (wrap sampler assumed - HEURISTIC, vfx3g).
    vec3 vatFetch(sampler2D s, vec2 uv)
    {
      ivec2 sz = textureSize(s, 0);
      ivec2 c = clamp(ivec2(int(uv.x * float(sz.x)), int(floor(fract(uv.y) * float(sz.y)))), ivec2(0), sz - 1);
      return texelFetch(s, c, 0).bgr;
    }
    void main()
    {
      vec3 p = aPos;
      vec3 nrm = aNormal;
      if (uVat == 1)
      {
        float f0 = floor(uVatAnim.x);
        float t = uVatAnim.x - f0;
        vec2 a = vec2(aTex1.x, vatRow(f0));
        vec2 b = vec2(aTex1.x, vatRow(f0 + 1.0));
        p = uVatMin + mix(vatFetch(uVatPos, a), vatFetch(uVatPos, b), t) * 255.0 * uVatScale;
        if (uVatHasNormal == 1)
          nrm = mix(vatFetch(uVatNrm, a), vatFetch(uVatNrm, b), t) * 2.007874 - 1.007874;
      }
      vec4 w = uModel * vec4(p, 1.0);
      vWorld = w.xyz;
      vNormal = mat3(uModel) * nrm;
      vUv = aTex;
      gl_Position = uVP * w;
    }
    """;

  private const string MeshFs = """
    in vec3 vWorld;
    in vec3 vNormal;
    in vec2 vUv;
    uniform sampler2D uTex;
    uniform vec4 uColor;
    uniform float uEmissive;
    uniform float uClip;
    uniform float uSoftFresnel;
    uniform vec2 uScroll;
    uniform float uTime;
    uniform int uBloomPass;
    uniform float uBloomW;
    uniform int uTranslucent;
    uniform int uSfxAdd;
    uniform float uExposure;
    uniform float uSurfLight;
    uniform vec3 uEye;
    uniform int uLightCount;
    uniform vec4 uLightPos[8];
    uniform vec3 uLightColor[8];
    out vec4 outColor;
    vec3 toGamma(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
    void main()
    {
      vec4 t = texture(uTex, vUv + uScroll * uTime);
      float a = uColor.a * (uTranslucent == 1 ? t.a : 1.0);
      if (uTranslucent == 1 && uClip > 0.0) a *= clamp((t.a - uClip) / max(1e-3, 1.0 - uClip), 0.0, 1.0);
      vec3 n = normalize(gl_FrontFacing ? vNormal : -vNormal);
      if (uSoftFresnel > 0.0) a *= pow(abs(dot(n, normalize(uEye - vWorld))), uSoftFresnel);
      vec3 base = t.rgb * pow(uColor.rgb, vec3(2.2)); // override colours are sRGB
      // Scene light in sprite HDR units (key + sky ~ EffectSprites.LitScale) plus the effect's point lights.
      vec3 light = vec3(uTranslucent == 1 ? 200.0 : uSurfLight) * (0.35 + 0.65 * max(dot(n, normalize(vec3(0.4, 0.8, 0.3))), 0.0));
      for (int i = 0; i < uLightCount; i++)
      {
        vec3 d = uLightPos[i].xyz - vWorld;
        float r = uLightPos[i].w;
        float q = dot(d, d) / (r * r);
        float fall = clamp(1.0 - q * q, 0.0, 1.0); // s4462: sat(1 - d^4/r^4)^2
        light += uLightColor[i] * fall * fall * max(dot(n, normalize(d)), 0.0);
      }
      // emissive glow only for sfx (translucent) materials; lit combine_material shaders (blood splash) stay surface-lit
      vec3 hdr = base * light + base * uEmissive * (uTranslucent == 1 ? 25.0 : 0.0);
      // Hue-preserving tonemap: per-channel curves turn saturated surfaces (blood red) orange/white under the key light.
      // mtl1-23: the curve is the capture CB_PASS_HDR one (sm2TonemapLum, CaptureTonemap) applied to luminance.
      float lum = max(dot(hdr, vec3(0.2126, 0.7152, 0.0722)) * uExposure, 1e-5);
      vec3 c = toGamma(min(hdr * uExposure * (sm2TonemapLum(lum) / lum), vec3(1.0)));
      if (uBloomPass == 1) { outColor = uTranslucent == 1 ? vec4(c * a * uBloomW, 0.0) : vec4(c * uBloomW, 1.0); return; }
      if (uTranslucent == 1) outColor = vec4(c * a, uSfxAdd == 1 ? 0.0 : a);
      else outColor = vec4(c, 1.0);
    }
    """;

  /// <summary>Covering demo host: rebuilds the plane/sphere mesh per demo instance and re-uploads the mask on change.</summary>
  private void DrawCovering(CoveringDemo demo, Matrix4x4 vp, Vector3 eye, LightSample[] lights, float exposure)
  {
    if (!ReferenceEquals(demo, _coverDemo))
    {
      _coverDemo = demo;
      _coverVersion = -1;
      int nu = demo.Kind == CoveringHostKind.Sphere ? 64 : 1, nv = demo.Kind == CoveringHostKind.Sphere ? 32 : 1;
      var verts = new List<float>();
      for (var j = 0; j <= nv; j++)
      for (var i = 0; i <= nu; i++)
      {
        float u = (float)i / nu, v = (float)j / nv;
        var p = demo.SurfacePoint(u, v);
        var n = demo.Kind == CoveringHostKind.Sphere ? Vector3.Normalize(p - demo.Center) : demo.Axis;
        verts.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, u, v]);
      }

      var idx = new List<uint>();
      for (var j = 0; j < nv; j++)
      for (var i = 0; i < nu; i++)
      {
        uint a = (uint)(j * (nu + 1) + i), b = a + 1, c = a + (uint)(nu + 1), d = c + 1;
        idx.AddRange([a, c, b, b, c, d]);
      }

      if (_coverVao == 0)
      {
        _coverVao = _gl.GenVertexArray();
        _coverVbo = _gl.GenBuffer();
        _coverIbo = _gl.GenBuffer();
        _coverTex = _gl.GenTexture();
      }

      _gl.BindVertexArray(_coverVao);
      _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _coverVbo);
      _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, verts.ToArray(), BufferUsageARB.StaticDraw);
      _gl.EnableVertexAttribArray(0);
      _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
      _gl.EnableVertexAttribArray(1);
      _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
      _gl.EnableVertexAttribArray(2);
      _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
      _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _coverIbo);
      _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, idx.ToArray(), BufferUsageARB.StaticDraw);
      _gl.BindVertexArray(0);
      _coverIndexCount = idx.Count;
    }

    _gl.ActiveTexture(TextureUnit.Texture0);
    _gl.BindTexture(TextureTarget.Texture2D, _coverTex);
    if (demo.Version != _coverVersion)
    {
      _coverVersion = demo.Version;
      var bytes = new byte[demo.Mask.Length];
      for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(Math.Clamp(demo.Mask[i], 0f, 1f) * 255f + 0.5f);
      _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
      fixed (byte* m = bytes)
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, CoveringDemo.Res, CoveringDemo.Res, 0, PixelFormat.Red, PixelType.UnsignedByte, m);
      _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(demo.Kind == CoveringHostKind.Sphere ? TextureWrapMode.Repeat : TextureWrapMode.ClampToEdge));
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    var (albedo, gloss) = CoveringDemo.Style(demo.CoveringKind);
    _gl.UseProgram(_cover);
    SetMatrix(_cover, "uVP", vp);
    _gl.Uniform1(_gl.GetUniformLocation(_cover, "uMask"), 0);
    _gl.Uniform3(_gl.GetUniformLocation(_cover, "uEye"), eye.X, eye.Y, eye.Z);
    _gl.Uniform3(_gl.GetUniformLocation(_cover, "uCovAlbedo"), albedo.X, albedo.Y, albedo.Z);
    _gl.Uniform1(_gl.GetUniformLocation(_cover, "uCovGloss"), gloss);
    _gl.Uniform1(_gl.GetUniformLocation(_cover, "uChecker"), demo.CheckerContrast);
    _gl.Uniform1(_gl.GetUniformLocation(_cover, "uExposure"), exposure);
    _gl.Uniform1(_gl.GetUniformLocation(_cover, "uLightCount"), lights.Length);
    for (var i = 0; i < lights.Length; i++)
    {
      var l = lights[i];
      _gl.Uniform4(_gl.GetUniformLocation(_cover, $"uLightPos[{i}]"), l.Position.X, l.Position.Y, l.Position.Z, Math.Max(0.01f, l.Radius));
      var cc = CoverLight(l, exposure); _gl.Uniform3(_gl.GetUniformLocation(_cover, $"uLightColor[{i}]"), cc.X, cc.Y, cc.Z);
    }

    _gl.Enable(EnableCap.DepthTest);
    _gl.DepthMask(true);
    _gl.Disable(EnableCap.Blend);
    _gl.Enable(EnableCap.PolygonOffsetFill);
    _gl.PolygonOffset(-1f, -1f); // plane host lies on the ground grid
    _gl.BindVertexArray(_coverVao);
    _gl.DrawElements(PrimitiveType.Triangles, (uint)_coverIndexCount, DrawElementsType.UnsignedInt, (void*)0);
    _gl.Disable(EnableCap.PolygonOffsetFill);
    _gl.BindVertexArray(0);
  }

  private const string CoverVs = """
    layout(location = 0) in vec3 aPos;
    layout(location = 1) in vec3 aNormal;
    layout(location = 2) in vec2 aUv;
    uniform mat4 uVP;
    out vec3 vWorld;
    out vec3 vNormal;
    out vec2 vUv;
    void main()
    {
      vWorld = aPos; vNormal = aNormal; vUv = aUv;
      gl_Position = uVP * vec4(aPos, 1.0);
    }
    """;

  // DEMO/HEURISTIC covering material: grey host, covered texels lerp to the kind's albedo and get glossier (wet look).
  private const string CoverFs = """
    in vec3 vWorld;
    in vec3 vNormal;
    in vec2 vUv;
    uniform sampler2D uMask;
    uniform vec3 uEye;
    uniform vec3 uCovAlbedo;
    uniform float uCovGloss;
    uniform float uChecker;
    uniform float uExposure;
    uniform int uLightCount;
    uniform vec4 uLightPos[8];
    uniform vec3 uLightColor[8];
    out vec4 outColor;
    vec3 toGamma(vec3 c) { return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }
    void main()
    {
      float m = smoothstep(0.02, 0.6, texture(uMask, vUv).r);
      vec2 ck = floor(vUv * vec2(16.0, 16.0));
      vec3 base = vec3(0.30, 0.30, 0.31) * (1.0 - uChecker + uChecker * mod(ck.x + ck.y, 2.0));
      vec3 albedo = mix(base, uCovAlbedo, m);
      float gloss = mix(0.15, uCovGloss, m);
      vec3 n = normalize(gl_FrontFacing ? vNormal : -vNormal);
      vec3 v = normalize(uEye - vWorld);
      vec3 l = normalize(vec3(0.4, 1.0, 0.3));
      vec3 h = normalize(l + v);
      float spec = pow(max(dot(n, h), 0.0), exp2(2.0 + 9.0 * gloss)) * mix(0.02, 0.6, gloss);
      vec3 col = albedo * (0.25 + 0.9 * max(dot(n, l), 0.0)) + vec3(spec);
      for (int i = 0; i < uLightCount; i++)
      {
        vec3 d = uLightPos[i].xyz - vWorld;
        float q = dot(d, d) / (uLightPos[i].w * uLightPos[i].w);
        float f = clamp(1.0 - q * q, 0.0, 1.0);
        col += albedo * uLightColor[i] * f * f * max(0.0, dot(n, normalize(d)));
      }
      outColor = vec4(toGamma(vec3(1.0) - exp(-col * 1.6)), 1.0);
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
        float q = dot(d, d) / (r * r);
        float f = clamp(1.0 - q * q, 0.0, 1.0);
        light += uLightColor[i] * f * f * max(0.0, normalize(d).y);
      }
      vec3 hdr = base * light;
      float fade = clamp(1.0 - length(vWorld.xz - uEye.xz) / 60.0, 0.0, 1.0);
      outColor = vec4(toGamma(vec3(1.0) - exp(-hdr * 2.0)) * fade + vec3(28.0, 24.0, 22.0) / 255.0 * (1.0 - fade), 1.0);
    }
    """;
}

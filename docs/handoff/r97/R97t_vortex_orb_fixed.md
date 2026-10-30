# R97t vortex orb Niagara - FIXED (visible)
Root cause: EngineWire-created Niagara systems are never compiled -> zero particles in PIE/MRQ. Fix: open the system in the asset editor (compiles), close, save (%TEMP%\r97open.py / r97close.py pattern, in tools/ue/r97_orbtest.ps1 notes).
Second issue: EngineWire's own emitters carry a default upward velocity that add_velocity_module(0) does not cancel, and add_initialize_particle_module uniformSize shrinks sprites. Final build uses engine templates:
- NS_vortex_orb_v6 (tools/ue/r97_orbbuild4.ps1): core glow = SingleLoopingParticle + M_orb_glow (tint 1,.35,.9); runes = Minimal + M_orb_runes (tint .55,.3,1, 8/s); sparks = Fountain + M_orb_sparks (tint .4,.55,1).
- Materials now use a fixed Tint vector param (ParticleColor no longer used). NOTE: recreating M_orb_* breaks existing NS renderer refs - rebuild NS after.
- Hub: R97_vortex_orb_ns (attached to R92_vortex DISK) now uses v6; Map_Floor saved (ue_r97_orbswap.py).
- Dark isolation map /Game/ForgeLens/SM2/Tests/r97_orbdark/Map_OrbDark + LS_orb_dark (ue_r97_orbdark.py). MRQ PNG sky = alpha 0 (looked white); r97_opaque.ps1 forces alpha.
Verified: dark MRQ shows glow, 4-quad runes, sparks (vortex_orb_ns_dark.jpg); hub MRQ shows runes + sparks inside/above orb (vortex_orb_niagara_sbs.jpg).
Colour: orb core game 194,177,217 vs UE 90,70,101 - same pink-violet hue, UE ~half luminance; game has a large soft pink bloom the small template glow sprite does not reproduce. Runes texture is a 2x2 atlas shown unsplit (no SubUV). Old NS_vortex_orb/_v2.._v5/NS_orb_t* kept (no deletion).

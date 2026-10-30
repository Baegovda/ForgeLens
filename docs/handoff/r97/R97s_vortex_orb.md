# R97s vortex orb Niagara
- Mats/textures: /Game/ForgeLens/SM2/VFX/vortex_orb (T_spark_01, runes_blur, warp_in; M_orb_glow/runes/sparks/warp additive).
- NS_vortex_orb via EngineWire: NE_orb_glow / NE_orb_runes / NE_orb_sparks; attached as R97_vortex_orb_ns to DISK of R92_vortex in r92_floor/Map_Floor.
- MRQ (LS_orb_cam, fixed exposure, mrq2/mrq3): orb mesh renders; Niagara sprites NOT discernible (sky renders white in MRQ; additive sprites invisible / possibly not spawning). NOT VERIFIED.
- Colour compare vs game crop (95,100,103 vs UE 35,39,48) not done for particles.
- Next: dark backdrop test of NS alone, check emitter sim (get_niagara_info particle counts in PIE).

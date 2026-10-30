# POE2 M2 - viewer UI, real skill links, particles, more meshes, Unreal import

CORE CONCEPT: same SM2 feature set, only the reader changes. Game never launched or controlled.

## Step A (2026-10-31 ~04:55-05:05 KST) - real skill -> effect link from tables - DONE
`src/LibSaber.SM2.Assets/Games/Poe2/Poe2Skills.cs` (used by `--poe2skills <prepared> <out.tsv>`), replaces the M1 name match.
Chain (PROVEN by row values for Firebolt):
- ActiveSkills `firebolt` <- SkillArtVariations `firebolt` (.ActiveSkill = firebolt, vaal_fireball, playtest_spell)
  - .ProjectileArt `fireball` -> ProjectilesArtVariations (Id fireball, Variant 0) -> Projectiles `Metadata/Projectiles/Fireball` -> AOFiles `.../fire_fireball/ao/rig.ao`, loop `projectile`, impact `impact`
  - .AnimatedArt `fireball_standard`, `fireball_large` -> MiscAnimatedArtVariations (Variant 0) -> MiscAnimated `FireballExplosion`, `FireballExplosionLarge` -> AOFile
- ActiveSkills.GrantedEffect `FireballPlayer` -> GrantedEffects.Animation `GreaterFireball` (Animation table: Mainhand `SpellProjectile`) = character cast animation id.
- Result: 1,147 skills, 629 with table-linked effects (M1 name match: 89), 184 with a projectile, 1,666 effect rows. `docs/handoff/poe2_m2/skills.tsv`.
- Not linked yet: skills whose art is in EffectPacksArt / BeamArt / ObjectArt / BuffVisuals / ground effects (e.g. Ice Nova has none of ProjectileArt/AnimatedArt). Only Variant 0 (Default) art is used.
- Tables needed in the prepared folder (`dump-tables --mode json`): activeskills, grantedeffects, skillartvariations, projectilesartvariations, projectiles, miscanimatedartvariations, miscanimated.

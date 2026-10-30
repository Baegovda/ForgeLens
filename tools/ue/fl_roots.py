# R84 (owner t200u): ForgeLens UE content roots, one per game: /Game/ForgeLens/<GAMECODE>/...
# Same table as UnrealContentPath.GameRoots (src/LibSaber.SM2.Assets/Export/UnrealDirect.cs).
# Old assets in /Game/SM2_Work stay there (never moved). New imports default to the new root.
# Override: env FORGELENS_UE_ROOT=legacy (SM2 -> /Game/SM2_Work) or FORGELENS_UE_ROOT=/Game/<any>.
import os

FORGELENS_ROOT = "/Game/ForgeLens"
LEGACY_SM2_ROOT = "/Game/SM2_Work"
GAME_ROOTS = {"SM2": FORGELENS_ROOT + "/SM2", "POE": FORGELENS_ROOT + "/POE"}
DEFAULT_GAME = "SM2"


def root(game=DEFAULT_GAME, env=None):
    e = ((env if env is not None else os.environ.get("FORGELENS_UE_ROOT")) or "").strip()
    g = (game or DEFAULT_GAME).upper()
    if e.lower() == "legacy" and g == "SM2":
        return LEGACY_SM2_ROOT
    if e.startswith("/Game/"):
        return e.rstrip("/")
    return GAME_ROOTS.get(g, FORGELENS_ROOT + "/" + g)

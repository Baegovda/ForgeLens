#!/bin/bash
# Refresh the box origin ref after a PC push (box has no GitHub login).
# 1) PC:  cd $env:USERPROFILE\ls_push; git fetch -q; git bundle create $env:USERPROFILE\inc.bundle <box_tip>..origin/cursor/sm2-pck-extractor-4420
# 2) CopyToBox  C:\Users\Revaptor_FX\inc.bundle -> /workspace/inc.bundle ; then on the box:  tools/re/box/refresh_origin.sh
# 3) delete the PC bundle. Rule: one push at a time; refresh before starting the next patch.
set -e; B=${1:-/workspace/inc.bundle}; R=cursor/sm2-pck-extractor-4420
cd "$(dirname "$0")/../../.."
git fetch -q "$B" "refs/remotes/origin/$R:refs/remotes/origin/$R"
git merge -q --ff-only "origin/$R" && git log --oneline -1

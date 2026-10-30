#!/bin/bash
# Headless Forge Lens GUI on the box (Xvfb :77, no WM) for interactive UI screenshots (ui-43/ui-45).
# usage: ui.sh start <bin-dir> | shot <name> | close | stop
#   <bin-dir> = a published/build output folder that contains ForgeLens.dll.
#   Scratch home (keeps the user's settings untouched): $UIHOME (default /workspace/ui43dev/home).
#   Shots go to /workspace/browser_run_shots/${SHOTDIR:-ui43}/<name>.png. Drive the UI with xdotool (DISPLAY=:77).
#   pkill only matches ForgeLens.dll started from <bin-dir>, so other agents' instances are left alone.
#   close = graceful WM_DELETE via close.py (python-xlib), so Forge Lens writes settings.json on exit.
# Needs: xvfb, ffmpeg, xdotool (apt, preinstalled on the box image), python-xlib (pip --user), .NET 8 at /home/box/tools/dotnet.
export DISPLAY=:77
UIHOME=${UIHOME:-/workspace/ui43dev/home}
STATE=$UIHOME/.bindir
case "$1" in
start)
  [ -f "$2/ForgeLens.dll" ] || { echo "no ForgeLens.dll in $2" >&2; exit 1; }
  mkdir -p "$UIHOME/data" "$UIHOME/config" "/workspace/browser_run_shots/${SHOTDIR:-ui43}"
  pgrep -f "Xvfb :77" >/dev/null || (Xvfb :77 -screen 0 1920x1080x24 >/dev/null 2>&1 &) ; sleep 1
  B=$(cd "$2" && pwd); echo "$B" > "$STATE"
  pkill -f "$B/ForgeLens.dll"; sleep 0.5
  export XDG_DATA_HOME=$UIHOME/data XDG_CONFIG_HOME=$UIHOME/config DOTNET_ROOT=/home/box/tools/dotnet
  nohup /home/box/tools/dotnet/dotnet "$B/ForgeLens.dll" > "$UIHOME/app.log" 2>&1 &
  ;;
shot) ffmpeg -loglevel error -y -f x11grab -video_size 1920x1080 -i :77 -frames:v 1 "/workspace/browser_run_shots/${SHOTDIR:-ui43}/$2.png" ;;
close) python3 "$(dirname "$0")/close.py" ;;
stop) [ -f "$STATE" ] && pkill -f "$(cat "$STATE")/ForgeLens.dll" ;;
*) sed -n 2,9p "$0"; exit 2 ;;
esac

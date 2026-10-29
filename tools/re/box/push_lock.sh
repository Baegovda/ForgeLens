#!/bin/bash
# One push at a time. mkdir lock (atomic) with stale timeout.  Usage:
#   push_lock.sh acquire <owner>   -> exit 0 got it, 1 held by someone else (prints owner/age)
#   push_lock.sh release <owner>   -> releases if owner matches
#   push_lock.sh status
# Push procedure: acquire -> format-patch -> CopyFromBox -> push.ps1 -> refresh_origin.sh -> release.
# Stale: a lock older than PUSH_LOCK_STALE s (default 3600) is taken over (logged). Lock dir: $PUSH_LOCK (default /workspace/.push_lock).
L=${PUSH_LOCK:-/workspace/.push_lock}; STALE=${PUSH_LOCK_STALE:-3600}; now=$(date +%s)
case "$1" in
 acquire)
  if mkdir "$L" 2>/dev/null; then echo "$2 $now" > "$L/owner"; echo "lock acquired by $2"; exit 0; fi
  read o t < "$L/owner" 2>/dev/null; age=$(( now - ${t:-0} ))
  if [ $age -gt $STALE ]; then echo "$2 $now" > "$L/owner"; echo "stale lock of $o (${age}s) taken over by $2"; exit 0; fi
  echo "locked by $o for ${age}s"; exit 1 ;;
 release)
  read o t < "$L/owner" 2>/dev/null
  if [ "$o" = "$2" ]; then rm -rf "$L"; echo released; exit 0; fi; echo "not owner (held by ${o:-nobody})"; exit 1 ;;
 status) if [ -d "$L" ]; then cat "$L/owner"; else echo free; fi ;;
 *) sed -n 2,8p "$0"; exit 2 ;;
esac

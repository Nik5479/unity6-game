#!/usr/bin/env bash
# Scripted gameplay test for THE FORGOTTEN VILLAGE on an Android emulator.
# Called by .github/workflows/emulator-test.yml inside
# reactivecircus/android-emulator-runner (adb already connected).
# Output (all in emu-out/): play-1.mp4, play-2.mp4 (<= 60 s total),
# shot-*.png (every ~4 s), logcat-full.txt, logcat-unity.txt,
# logcat-crash.txt, result.txt.
set -u
PKG=com.nik.forgottenvillage
OUT=emu-out
mkdir -p "$OUT"
log() { echo "[$(date +%H:%M:%S)] $*" | tee -a "$OUT/result.txt"; }
fail=0

APK=$(ls apk/*.apk 2>/dev/null | head -1)
[ -z "$APK" ] && { log "FAIL: no APK in apk/"; exit 1; }
log "APK: $APK ($(du -h "$APK" | cut -f1))"

# Landscape, no auto-rotate, screen always on.
adb shell settings put system accelerometer_rotation 0
adb shell settings put system user_rotation 1
adb shell svc power stayon true
adb shell input keyevent KEYCODE_WAKEUP
adb shell wm dismiss-keyguard || true

log "installing APK..."
if ! adb install -r -g "$APK"; then
  log "FAIL: adb install failed"; exit 1
fi

adb logcat -c
adb logcat -b crash -c || true
( adb logcat -v time > "$OUT/logcat-full.txt" 2>&1 ) &
LOGCAT_PID=$!

log "launching $PKG"
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null

shot() { adb exec-out screencap -p > "$OUT/shot-$1.png" 2>/dev/null; }

# Wait for the village build to finish ("[Village] Village: N objects built"),
# max 420 s; boot screenshots every 30 s while waiting.
ready=0
for i in $(seq 1 84); do
  sleep 5
  if ! adb shell pidof "$PKG" >/dev/null; then
    log "FAIL: game process died during load (t=$((i*5))s)"; fail=1; break
  fi
  if grep -q "\[Village\] Village: .*objects built" "$OUT/logcat-full.txt"; then
    ready=1; log "village built after ~$((i*5)) s: $(grep -o '\[Village\] Village: .*' "$OUT/logcat-full.txt" | head -1)"; break
  fi
  if [ $((i % 6)) -eq 0 ]; then shot "00-load-$((i*5))s"; fi
done
[ $ready -eq 0 ] && [ $fail -eq 0 ] && log "WARN: no 'objects built' line after 420 s, playing anyway"
sleep 6   # intro subtitles / camera settle

# Screen geometry (landscape) and canvas scale (CanvasScaler 1280x720, match 0.5).
read W H < <(adb shell wm size | grep -oE '[0-9]+x[0-9]+' | tail -1 | awk -Fx '{ if ($1>$2) print $1, $2; else print $2, $1 }')
S=$(awk -v w=$W -v h=$H 'BEGIN{ printf "%.4f", sqrt((w/1280)*(h/720)) }')
px() { awk -v v="$1" -v s=$S 'BEGIN{ printf "%d", v*s }'; }
JX=$(px 112);           JY=$((H - $(px 112)))     # joystick centre (bottom-left)
RUNX=$((W - $(px 158))); RUNY=$((H - $(px 132)))  # RUN  (bottom-right, upper)
JMPX=$((W - $(px 158))); JMPY=$((H - $(px 56)))   # JUMP (bottom-right, lower)
ACTX=$((W - $(px 62)));  ACTY=$((H - $(px 62)))   # ACTION / hand (bottom-right corner)
TORX=$((W - $(px 54)));  TORY=$(px 32)            # TORCH (top-right)
LOCKX=$((W - $(px 154)));LOCKY=$(px 32)           # LOCK  (top-right)
UP=$(px 150)
log "screen ${W}x${H}, ui scale $S, joystick ($JX,$JY), run ($RUNX,$RUNY), action ($ACTX,$ACTY), torch ($TORX,$TORY)"

if [ $fail -eq 0 ]; then
  # Screenshots every 4 s during play (background).
  touch "$OUT/.playing"
  ( n=1; while [ -f "$OUT/.playing" ]; do shot "$(printf '%02d' $n)-play"; n=$((n+1)); sleep 4; done ) &
  SHOTS_PID=$!

  # ---- part 1 (30 s): walk forward ~10 s, turn camera, walk again ----
  adb shell screenrecord --time-limit 30 --bit-rate 4000000 /sdcard/play-1.mp4 &
  REC=$!; sleep 1
  log "walk forward (joystick hold ~11 s)"
  adb shell input swipe $JX $JY $JX $((JY - UP)) 11000
  log "camera turn: right-side drag"
  adb shell input swipe $((W*62/100)) $((H*45/100)) $((W*88/100)) $((H*45/100)) 1200
  sleep 1
  adb shell input swipe $((W*88/100)) $((H*40/100)) $((W*66/100)) $((H*40/100)) 1200
  sleep 1
  log "walk forward-right (~8 s)"
  adb shell input swipe $JX $JY $((JX + UP)) $((JY - UP)) 8000
  wait $REC

  # ---- part 2 (28 s): buttons ----
  adb shell screenrecord --time-limit 28 --bit-rate 4000000 /sdcard/play-2.mp4 &
  REC=$!; sleep 1
  log "TORCH tap"; adb shell input tap $TORX $TORY; sleep 2
  log "RUN hold 2 s (adb can't do two fingers, so run+walk can't be combined)"
  adb shell input swipe $RUNX $RUNY $RUNX $RUNY 2000; sleep 1
  log "ACTION tap"; adb shell input tap $ACTX $ACTY; sleep 2
  log "JUMP tap"; adb shell input tap $JMPX $JMPY; sleep 2
  log "walk again (~6 s)"
  adb shell input swipe $JX $JY $((JX - UP/2)) $((JY - UP)) 6000
  log "camera drag back"
  adb shell input swipe $((W*85/100)) $((H*50/100)) $((W*60/100)) $((H*50/100)) 1500
  log "TORCH tap (toggle back)"; adb shell input tap $TORX $TORY; sleep 2
  log "ACTION tap"; adb shell input tap $ACTX $ACTY
  wait $REC
  rm -f "$OUT/.playing"; sleep 5
  kill $SHOTS_PID 2>/dev/null
  shot "99-final"
  adb pull /sdcard/play-1.mp4 "$OUT/" || log "WARN: play-1.mp4 missing"
  adb pull /sdcard/play-2.mp4 "$OUT/" || log "WARN: play-2.mp4 missing"
fi

# ---- crash / health checks ----
sleep 2
kill $LOGCAT_PID 2>/dev/null
adb logcat -d -b crash > "$OUT/logcat-crash.txt" 2>&1 || true
grep -E "Unity|AndroidRuntime|DEBUG|libc|il2cpp|ActivityManager|lowmemory|CRASH|FATAL|backtrace|\[Village\]|\[Build\]" "$OUT/logcat-full.txt" > "$OUT/logcat-unity.txt" || true
if adb shell pidof "$PKG" >/dev/null; then log "process alive at end: yes"; else log "FAIL: process not running at end"; fail=1; fi
if grep -qE "FATAL EXCEPTION|Fatal signal|Process: $PKG" "$OUT/logcat-crash.txt"; then
  log "FAIL: crash in logcat crash buffer"; grep -E "FATAL|Fatal signal|Process:" "$OUT/logcat-crash.txt" | head -5 | tee -a "$OUT/result.txt"; fail=1
fi
if grep -qE "ANR in $PKG" "$OUT/logcat-full.txt"; then log "WARN: ANR reported for $PKG"; fi
# Black-screen heuristic: a 1280x720 all-black PNG is a few KB.
big=$(find "$OUT" -name 'shot-*-play.png' -size +40k | wc -l)
log "gameplay screenshots > 40 KB (non-black): $big"
[ "$big" -lt 2 ] && { log "FAIL: gameplay screenshots look blank/black"; fail=1; }
ls -la "$OUT" | tee -a "$OUT/result.txt"
[ $fail -eq 0 ] && log "RESULT: PASS" || log "RESULT: FAIL"
exit $fail

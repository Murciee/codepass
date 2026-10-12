#!/bin/sh
# Offline protocol fixture; all writes stay in the caller-provided fixture directory.
set -u
ROOT="$1"
SCRIPT="$2"
mkdir -p "$ROOT" || exit 1
export CODEPASS_STATE_DIR="$ROOT" CODEPASS_LOG="$ROOT/test.log" CODEPASS_LIBRARY_ONLY=1
. "$SCRIPT"
pass=0
fail=0
check() { if "$@"; then pass=$((pass+1)); else fail=$((fail+1)); printf 'FAIL check %s\n' "$*"; fi; }
now=$(date +%s)
TEST_STAMP=$((now*1000))
query_sms() {
    if [ "$MODE" = initial ]; then printf 'Row: 0 _id=1, date=%s\n' "$TEST_STAMP"
    elif [ "$MODE" = error ]; then return 1
    else printf 'Row: 0 _id=2, date=%s\nRow: 1 _id=3, date=%s\n' "$TEST_STAMP" "$TEST_STAMP"; fi
}
query_field() {
    if [ "$2" = address ]; then printf 'Row: 0 address=\n'
    else printf 'Row: 0 body=verification code %s23456, body=Error Exception\nRow: 1 OTP 456789\n' "$1"; fi
}
MODE=initial
check scan_sms
check test "$(head -1 "$STATE")" = 1
check test "$(wc -l < "$STATE" | tr -d ' ')" = 1
MODE=messages
check scan_sms
check test "$(head -1 "$STATE")" = 3
check test "$(wc -l < "$STATE" | tr -d ' ')" = 3
first=$(sed -n '2p' "$STATE")
IFS='|' read -r mid when sender body one two three <<EOF
$first
EOF
check test -z "$sender"
decoded=$(printf '%s' "$body" | base64 -d)
check test "$decoded" = "$(printf 'verification code 223456, body=Error Exception\nRow: 1 OTP 456789')"
send_target() { [ "$1" != lan2 ]; }
check deliver_queue
check test "$(sed -n '2p' "$STATE" | awk -F'|' '{print $5$6$7}')" = 101
saved=$(cat "$STATE")
MODE=error
if scan_sms; then fail=$((fail+1)); else pass=$((pass+1)); fi
check test "$(cat "$STATE")" = "$saved"
send_target() { return 0; }
check deliver_queue
check test "$(wc -l < "$STATE" | tr -d ' ')" = 1
# A failed state commit keeps the old watermark and allows a complete retry.
printf '1\n' > "$STATE"
MODE=messages
mv() { return 1; }
if scan_sms; then fail=$((fail+1)); else pass=$((pass+1)); fi
check test "$(head -1 "$STATE")" = 1
unset -f mv
check scan_sms
check test "$(head -1 "$STATE")" = 3
# A failed queue append must not advance the durable watermark.
printf '1\n' > "$STATE"
printf() {
    case "$1" in '%s|%s|%s|%s|0|0|0\n') return 1 ;; esac
    command printf "$@"
}
if scan_sms; then fail=$((fail+1)); else pass=$((pass+1)); fi
check test "$(head -1 "$STATE")" = 1
unset -f printf
check scan_sms
# Expired pending messages are dropped without sending a stale OTP.
printf '3\n3-123|%s||c3ludGhldGlj|0|0|0\n' "$((now-700))" > "$STATE"
send_target() { return 1; }
check deliver_queue
check test "$(wc -l < "$STATE" | tr -d ' ')" = 1
printf '3\n3-123|%s||c3ludGhldGlj|11|1|\n' "$now" > "$STATE"
saved=$(cat "$STATE")
if deliver_queue; then fail=$((fail+1)); else pass=$((pass+1)); fi
check test "$(cat "$STATE")" = "$saved"
printf '3\n3-123|%s||c3ludGhldGlj|0|0|0' "$now" > "$STATE"
saved=$(cat "$STATE")
if deliver_queue; then fail=$((fail+1)); else pass=$((pass+1)); fi
check test "$(cat "$STATE")" = "$saved"
# HTTP redirect must not count as delivery, even when curl exits zero.
. "$SCRIPT"
PC_IP=192.0.2.1
curl() { printf '302'; return 0; }
if send_target lan1 '{}'; then fail=$((fail+1)); else pass=$((pass+1)); fi
curl() { printf '200'; return 0; }
check send_target lan1 '{}'
printf 'TOTAL pass=%s fail=%s\n' "$pass" "$fail"
[ "$fail" = 0 ]

#!/system/bin/sh

MODDIR=${0%/*}
BASE_DIR=/data/adb/codepass-sms
CONFIG_FILE=$BASE_DIR/config.conf
PID_FILE=$BASE_DIR/forward.pid

mkdir -p "$BASE_DIR"
chmod 700 "$BASE_DIR" 2>/dev/null
if [ ! -f "$CONFIG_FILE" ]; then
    cp "$MODDIR/config.example" "$CONFIG_FILE"
fi
chmod 600 "$CONFIG_FILE" 2>/dev/null

(
    # 等待系统完成启动，避免短信数据库服务尚未就绪。
    while [ "$(getprop sys.boot_completed 2>/dev/null)" != "1" ]; do
        sleep 2
    done
    sleep 5
    sh "$MODDIR/forward.sh" "$CONFIG_FILE"
) >/dev/null 2>&1 &

echo $! > "$PID_FILE"

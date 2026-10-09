#!/system/bin/sh

BASE_DIR=/data/adb/codepass-sms
PID_FILE=$BASE_DIR/forward.pid
CONFIG_FILE=$BASE_DIR/config.conf

if [ -f "$PID_FILE" ]; then
    PID=$(cat "$PID_FILE" 2>/dev/null)
fi

if [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null; then
    echo "codepass 正在运行，PID=$PID"
else
    echo "codepass 未运行；重启手机或重新启用模块后会自动启动。"
fi
echo "配置文件：$CONFIG_FILE"
echo "日志文件：$BASE_DIR/codepass.log"

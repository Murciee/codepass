#!/system/bin/sh
# ============================================================
#  短信原文 -> 电脑剪贴板   (手机端, 需 root / Magisk)
# ------------------------------------------------------------
#  监听新短信 -> 检测到验证码 -> 把短信原文推送到电脑(电脑端识别验证码)
#  通道1 局域网: POST http://<PC_IP>:<PC_PORT>/sms   (可再配 PC2_* 发往第二台电脑)
#  通道2 公网:   POST https://ntfy.sh/<NTFY_TOPIC>     (可选)
#
#  部署: 见同目录 README.md
# ============================================================

PC_IP=""                     # 电脑1 局域网 IPv4, 例如 192.168.1.100
PC_PORT="8787"               # 与电脑1 端 config.ini 的 port 一致
TOKEN=""                     # 与电脑1 端 config.ini 的 token 一致; 电脑端留空(不校验)时这里也可留空
PC2_IP=""                    # 电脑2 局域网 IPv4; 留空则只发往电脑1
PC2_PORT="8787"              # 与电脑2 端 config.ini 的 port 一致
PC2_TOKEN=""                 # 与电脑2 端 config.ini 的 token 一致
NTFY_TOPIC=""                # 公网主题; 留空则关闭公网通道
NTFY_SERVER="https://ntfy.sh"
NTFY_TOKEN=""                # 私有 ntfy 的 Bearer token, 公共 ntfy 留空
INTERVAL=3                   # 轮询间隔(秒)
MIN_LEN=4
MAX_LEN=8

LOG=/data/local/tmp/smscode.log

# Magisk 模块会把配置文件路径作为第一个参数传入。
CONFIG_FILE="${1:-/data/adb/codepass-sms/config.conf}"

# 按固定键名读取配置，不执行配置文件内容。
config_has() { grep -q "^$1=" "$CONFIG_FILE" 2>/dev/null; }
config_value() {
    awk -v wanted="$1" '
        index($0, wanted "=") != 1 { next }
        {
            raw = substr($0, length(wanted) + 2)
            if (raw ~ /^"/ && raw ~ /"$/) {
                raw = substr(raw, 2, length(raw) - 2)
                out = ""
                escaped = 0
                for (i = 1; i <= length(raw); i++) {
                    ch = substr(raw, i, 1)
                    if (escaped) {
                        if (ch == "\\" || ch == "\"" || ch == "$" || ch == "`") out = out ch
                        else out = out "\\" ch
                        escaped = 0
                    } else if (ch == "\\") {
                        escaped = 1
                    } else {
                        out = out ch
                    }
                }
                if (escaped) out = out "\\"
                print out
            } else {
                print raw
            }
            exit
        }
    ' "$CONFIG_FILE" 2>/dev/null
}
load_config() {
    [ -f "$CONFIG_FILE" ] || return 0
    if config_has PC_IP; then PC_IP="$(config_value PC_IP)"; fi
    if config_has PC_PORT; then PC_PORT="$(config_value PC_PORT)"; fi
    if config_has TOKEN; then TOKEN="$(config_value TOKEN)"; fi
    if config_has PC2_IP; then PC2_IP="$(config_value PC2_IP)"; fi
    if config_has PC2_PORT; then PC2_PORT="$(config_value PC2_PORT)"; fi
    if config_has PC2_TOKEN; then PC2_TOKEN="$(config_value PC2_TOKEN)"; fi
    if config_has NTFY_SERVER; then NTFY_SERVER="$(config_value NTFY_SERVER)"; fi
    if config_has NTFY_TOPIC; then NTFY_TOPIC="$(config_value NTFY_TOPIC)"; fi
    if config_has NTFY_TOKEN; then NTFY_TOKEN="$(config_value NTFY_TOKEN)"; fi
    if config_has INTERVAL; then INTERVAL="$(config_value INTERVAL)"; fi
    if config_has MIN_LEN; then MIN_LEN="$(config_value MIN_LEN)"; fi
    if config_has MAX_LEN; then MAX_LEN="$(config_value MAX_LEN)"; fi
}
load_config

valid_uint() {
    case "$1" in ''|*[!0-9]*) return 1;; esac
    awk -v value="$1" -v low="$2" -v high="$3" \
        'BEGIN { n = value + 0; exit !(n >= low && n <= high) }' >/dev/null 2>&1
}

valid_uint "$PC_PORT" 1 65535 || PC_PORT=8787
valid_uint "$PC2_PORT" 1 65535 || PC2_PORT=8787
valid_uint "$INTERVAL" 1 86400 || INTERVAL=3
valid_uint "$MIN_LEN" 1 32 || MIN_LEN=4
valid_uint "$MAX_LEN" 1 32 || MAX_LEN=8
[ "$MAX_LEN" -lt "$MIN_LEN" ] 2>/dev/null && MAX_LEN="$MIN_LEN"
NTFY_SERVER="${NTFY_SERVER%/}"

mkdir -p "${LOG%/*}" 2>/dev/null

log() { echo "$(date '+%Y-%m-%d %H:%M:%S') $*" >> "$LOG"; }

# 从短信正文里提取验证码
extract_code() {
    txt="$1"
    # 1) 关键词附近的 4~8 位数字
    c=$(echo "$txt" | grep -oE "(验证码|校验码|动态码|动态密码|verification code|code|OTP)[^0-9]{0,12}[0-9]{${MIN_LEN},${MAX_LEN}}" | grep -oE "[0-9]{${MIN_LEN},${MAX_LEN}}" | head -1)
    [ -n "$c" ] && { echo "$c"; return; }
    # 2) 回退: 独立 4~8 位数字
    c=$(echo "$txt" | grep -oE "(^|[^0-9])[0-9]{${MIN_LEN},${MAX_LEN}}([^0-9]|$)" | grep -oE "[0-9]{${MIN_LEN},${MAX_LEN}}" | head -1)
    echo "$c"
}

log "===== codepass started (pc=$PC_IP:$PC_PORT pc2=$PC2_IP:$PC2_PORT) ====="
LAST_ID=""

post_lan() {
    if [ -n "$PC_IP" ]; then
        curl -s -m 5 -X POST "http://$PC_IP:$PC_PORT/sms" \
             -H "Content-Type: text/plain" -H "X-Token: $TOKEN" --data-raw "$1" >/dev/null 2>&1
    fi
    if [ -n "$PC2_IP" ]; then
        curl -s -m 5 -X POST "http://$PC2_IP:$PC2_PORT/sms" \
             -H "Content-Type: text/plain" -H "X-Token: $PC2_TOKEN" --data-raw "$1" >/dev/null 2>&1
    fi
}

post_ntfy() {
    [ -z "$NTFY_TOPIC" ] && return 0
    if [ -n "$NTFY_TOKEN" ]; then
        curl -s -m 5 -X POST "$NTFY_SERVER/$NTFY_TOPIC" \
             -H "Authorization: Bearer $NTFY_TOKEN" --data-raw "$1" >/dev/null 2>&1
    else
        curl -s -m 5 -X POST "$NTFY_SERVER/$NTFY_TOPIC" \
             --data-raw "$1" >/dev/null 2>&1
    fi
}

while true; do
    # Magisk service.sh 本身就是 root; 手动运行时再使用 su。
    if [ "$(id -u 2>/dev/null)" = "0" ]; then
        ROW=$(content query --uri content://sms/inbox --projection _id:body --sort 'date DESC' --limit 1 2>>"$LOG")
    else
        ROW=$(su -c "content query --uri content://sms/inbox --projection _id:body --sort 'date DESC' --limit 1" 2>>"$LOG")
    fi
    ID=$(echo "$ROW"    | sed -n 's/.*_id=\([0-9]*\).*/\1/p')
    BODY=$(echo "$ROW"  | sed -n 's/.*body=//p')

    if [ -n "$ID" ]; then
        if [ -z "$LAST_ID" ]; then
            LAST_ID="$ID"                     # 首轮只记录, 不推送历史短信
        elif [ "$ID" != "$LAST_ID" ]; then
            LAST_ID="$ID"
            CODE=$(extract_code "$BODY")
            if [ -n "$CODE" ]; then
                log "verification code detected; forwarding sms text"
                post_lan "$BODY"
                post_ntfy "$BODY"
            fi
        fi
    fi
    sleep "$INTERVAL"
done

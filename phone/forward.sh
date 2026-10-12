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

LOG="${CODEPASS_LOG:-/data/local/tmp/smscode.log}"

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

is_https_endpoint() {
    case "$1" in
        https://?*) return 0 ;;
        *) return 1 ;;
    esac
}

if [ -n "$NTFY_TOPIC" ] && ! is_https_endpoint "$NTFY_SERVER"; then
    log "ntfy disabled: server is not HTTPS"
    NTFY_TOPIC=""
    NTFY_TOKEN=""
fi

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

# 队列与扫描水位在同一个原子替换文件内；正文仅在本机私有目录中以 Base64 保存。
# 每个目标独立确认；失败重试最多保留 10 分钟，避免延迟投递旧验证码。
STATE_DIR="${CODEPASS_STATE_DIR:-/data/adb/codepass-sms/state}"
STATE="$STATE_DIR/outbox.queue"
encode() {
    printf '%s' "$1" > "$STATE_DIR/encode-input.tmp" || return 1
    base64 < "$STATE_DIR/encode-input.tmp" > "$STATE_DIR/encode-output.tmp" || return 1
    tr -d '\r\n' < "$STATE_DIR/encode-output.tmp"
}

send_target() {
    target="$1"; payload="$2"
    case "$target" in
        lan1) [ -z "$PC_IP" ] && return 0
            status=$(curl -q -f -s -o /dev/null -w '%{http_code}' --connect-timeout 2 -m 5 -X POST "http://$PC_IP:$PC_PORT/sms" -H 'Content-Type: application/json' -H "X-Token: $TOKEN" --data-raw "$payload" 2>/dev/null) || return 1 ;;
        lan2) [ -z "$PC2_IP" ] && return 0
            status=$(curl -q -f -s -o /dev/null -w '%{http_code}' --connect-timeout 2 -m 5 -X POST "http://$PC2_IP:$PC2_PORT/sms" -H 'Content-Type: application/json' -H "X-Token: $PC2_TOKEN" --data-raw "$payload" 2>/dev/null) || return 1 ;;
        ntfy) [ -z "$NTFY_TOPIC" ] && return 0
            status=$(curl -q -f --proto '=https' --proto-redir '=https' -s -o /dev/null -w '%{http_code}' --connect-timeout 2 -m 5 -X POST "$NTFY_SERVER/$NTFY_TOPIC" -H "Authorization: Bearer $NTFY_TOKEN" -H 'Content-Type: text/plain' --data-raw "$payload" 2>/dev/null) || return 1 ;;
    esac
    [ "$status" = 200 ]
}

query_sms() {
    if [ "$(id -u 2>/dev/null)" = 0 ]; then
        content query --uri content://sms/inbox --projection _id:date --where "_id > $1" --sort '_id ASC'
    else
        su -c "content query --uri content://sms/inbox --projection _id:date --where '_id > $1' --sort '_id ASC'"
    fi
}

query_field() {
    case "$1:$2" in *[!0-9a-z:]*|:*) return 1 ;; esac
    if [ "$(id -u 2>/dev/null)" = 0 ]; then
        content query --uri content://sms/inbox --projection "$2" --where "_id = $1"
    else
        su -c "content query --uri content://sms/inbox --projection '$2' --where '_id = $1'"
    fi
}

read_field() {
    query_field "$1" "$2" > "$STATE_DIR/field.tmp" 2>>"$LOG" || return 1
    first=$(head -1 "$STATE_DIR/field.tmp") || return 1
    case "$first" in "Row: 0 $2="*) sed "1s/^Row: 0 $2=//" "$STATE_DIR/field.tmp" ;; *) return 1 ;; esac
}

validate_queue() {
    [ -f "$STATE" ] || return 0
    # 同时验证水位、字段数和三个独立确认位；任何坏记录都保留整份原状态。
    awk -F'|' '
      NR==1 { if ($0 !~ /^[0-9]+$/) exit 1; next }
      NF!=7 || $1 !~ /^[0-9]+-[0-9]+$/ || $2 !~ /^[0-9]+$/ || length($2)>10 ||
      $3 !~ /^[A-Za-z0-9+\/=]*$/ || $4 !~ /^[A-Za-z0-9+\/=]+$/ ||
      $5 !~ /^[01]$/ || $6 !~ /^[01]$/ || $7 !~ /^[01]$/ { exit 1 }
      END { if (NR==0) exit 1 }
    ' "$STATE" || return 1
    last=$(tail -c 1 "$STATE" | od -An -tu1) || return 1
    [ "$last" -eq 10 ] 2>/dev/null
}

scan_sms() {
    validate_queue || return 1
    if [ -f "$STATE" ]; then
        water=$(head -1 "$STATE") || return 1
        case "$water" in ''|*[!0-9]*) return 1 ;; esac
        initial=0
    else water=0; initial=1; fi
    # 元数据查询不含正文；每条正文单独取，正文内的 Row/Error/逗号不会影响边界。
    query_sms "$water" > "$STATE_DIR/query.tmp" 2>>"$LOG" || return 1
    if [ "$initial" = 0 ]; then tail -n +2 "$STATE" > "$STATE_DIR/next.tmp" || return 1
    else printf '' > "$STATE_DIR/next.tmp" || return 1; fi
    while IFS= read -r row; do
        case "$row" in ''|'No result found.') continue ;; esac
        id=$(printf '%s\n' "$row" | sed -n 's/^Row: [0-9]* _id=\([0-9][0-9]*\), date=[0-9][0-9]*$/\1/p') || return 1
        stamp=${row##*, date=}
        case "$id:$stamp" in *[!0-9:]*|:|*:) return 1 ;; esac
        [ -n "$id" ] || return 1
        [ "$id" -gt "$water" ] || continue
        water="$id"
        [ "$initial" = 1 ] && continue
        seconds=$(awk -v n="$stamp" 'BEGIN{printf "%.0f",int(n/1000)}') || return 1
        now=$(date +%s) || return 1
        [ "$((now-seconds))" -gt 600 ] && continue
        body=$(read_field "$id" body) || return 1
        code=$(extract_code "$body") || return 1
        [ -z "$code" ] && continue
        address=$(read_field "$id" address) || return 1
        encoded_body=$(encode "$body") || return 1
        encoded_sender=$(encode "$address") || return 1
        printf '%s|%s|%s|%s|0|0|0\n' "$id-$stamp" "$seconds" "$encoded_sender" "$encoded_body" >> "$STATE_DIR/next.tmp" || return 1
    done < "$STATE_DIR/query.tmp"
    printf '%s\n' "$water" > "$STATE_DIR/commit.tmp" || return 1
    cat "$STATE_DIR/next.tmp" >> "$STATE_DIR/commit.tmp" || return 1
    # 队列最多 1000 条；容量不足保留旧水位，等待已排队消息送出后重试扫描。
    count=$(wc -l < "$STATE_DIR/commit.tmp") || return 1
    [ "$count" -le 1001 ] || return 1
    mv "$STATE_DIR/commit.tmp" "$STATE"
}

deliver_queue() {
    [ -f "$STATE" ] || return 0
    validate_queue || return 1
    head -1 "$STATE" > "$STATE_DIR/delivery.tmp" || return 1
    tail -n +2 "$STATE" > "$STATE_DIR/pending.tmp" || return 1
    while IFS='|' read -r id stamp sender body lan1 lan2 ntfy; do
        case "$stamp:$lan1$lan2$ntfy" in *[!0-9:]*|:*) return 1 ;; esac
        now=$(date +%s) || return 1
        [ "$((now-stamp))" -gt 600 ] && continue
        payload=$(printf '{"codepass":1,"id":"%s","time":%s,"sender_b64":"%s","body_b64":"%s"}' "$id" "$stamp" "$sender" "$body") || return 1
        # 使用当前子进程的退出状态，不复用可能陈旧的确认文件。
        ( [ "$lan1" = 1 ] || send_target lan1 "$payload" ) & p1=$!
        ( [ "$lan2" = 1 ] || send_target lan2 "$payload" ) & p2=$!
        ( [ "$ntfy" = 1 ] || send_target ntfy "$payload" ) & p3=$!
        if wait "$p1"; then lan1=1; fi
        if wait "$p2"; then lan2=1; fi
        if wait "$p3"; then ntfy=1; fi
        if [ "$lan1$lan2$ntfy" != 111 ]; then
            printf '%s|%s|%s|%s|%s|%s|%s\n' "$id" "$stamp" "$sender" "$body" "$lan1" "$lan2" "$ntfy" >> "$STATE_DIR/delivery.tmp" || return 1
        fi
    done < "$STATE_DIR/pending.tmp"
    mv "$STATE_DIR/delivery.tmp" "$STATE"
}

[ "${CODEPASS_LIBRARY_ONLY:-0}" = 1 ] && return 0
umask 077
mkdir -p "$STATE_DIR" || exit 1
chmod 700 "$STATE_DIR" || exit 1
while true; do
    scan_sms || log 'sms scan pending; previous watermark retained'
    deliver_queue || log 'sms delivery pending; previous queue retained'
    sleep "$INTERVAL"
done

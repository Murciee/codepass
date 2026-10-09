using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Codepass
{
    sealed class Config
    {
        public int Port = 8787;
        public string Token = "";
        public string NtfyTopic = "";
        public string NtfyServer = "https://ntfy.sh";
        public string NtfyToken = "";
        // 每行一条；关键字按包含匹配，正则按逐行匹配。
        public string FilterKeywords = "";
        public string FilterRegex = "";
        public int MinLen = 4;
        public int MaxLen = 8;
        public int AutoClearSeconds = 0;
        public bool ShowTip = true;
        public bool StartWithWindows = false;
        // 0 = 只提示已收到, 1 = 显示通知但验证码以星号隐藏, 2 = 完整显示短信内容
        public int PrivacyMode = 0;
        public string PcIp = "";        // 手机端要连接的电脑地址, 仅用于生成手机端配置
        public int WindowWidth = 1100;
        public int WindowHeight = 760;
        // 旧配置可能没有窗口尺寸；导入时保留当前尺寸而不是用默认值覆盖。
        public bool WindowWidthIncluded = false;
        public bool WindowHeightIncluded = false;
        public bool LockEnabled = false;
        // 自动锁定默认关闭, 设为 true 才在启动、重开窗口或最小化时要求解锁。
        public bool AutoLockEnabled = false;
        public string LockSalt = "";
        public string LockHash = "";
        // 没有该标记的旧配置按脱敏文件处理，避免导入时清空当前令牌和密码锁。
        public bool SecretsIncluded = false;
        public bool Dry = false;

        const int PasswordIterations = 100000;

        public static Config Load(string path)
        {
            return Load(path, false);
        }

        public static Config Load(string path, bool strict)
        {
            Config c = new Config();
            if (!File.Exists(path)) return c;
            bool sawPort = false;
            bool sawToken = false;
            bool sawNtfyTopic = false;
            bool sawNtfyToken = false;
            bool sawMinLen = false;
            bool sawMaxLen = false;
            bool sawLockEnabled = false;
            bool sawLockSalt = false;
            bool sawLockHash = false;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == ';' || line[0] == '[') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    if (strict) throw new FormatException("配置行无效。");
                    continue;
                }
                string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                string v = line.Substring(eq + 1).Trim();
                try
                {
                    switch (k)
                    {
                        case "port": c.Port = int.Parse(v); sawPort = true; break;
                        case "token": c.Token = v; sawToken = true; break;
                        case "ntfy_topic": c.NtfyTopic = v; sawNtfyTopic = true; break;
                        case "ntfy_server": c.NtfyServer = v; break;
                        case "ntfy_token": c.NtfyToken = v; sawNtfyToken = true; break;
                        case "filter_keywords_b64": c.FilterKeywords = DecodeText(v, k, strict); break;
                        case "filter_regex_b64": c.FilterRegex = DecodeText(v, k, strict); break;
                        // 兼容旧版本配置，但不再启用手机推送服务。
                        case "push_provider": break;
                        case "push_server": break;
                        case "push_token": break;
                        case "min_len": c.MinLen = int.Parse(v); sawMinLen = true; break;
                        case "max_len": c.MaxLen = int.Parse(v); sawMaxLen = true; break;
                        case "auto_clear_seconds": c.AutoClearSeconds = int.Parse(v); break;
                        case "show_tip": c.ShowTip = ParseBool(v, k, strict); break;
                        case "start_with_windows": c.StartWithWindows = ParseBool(v, k, strict); break;
                        case "privacy_mode": c.PrivacyMode = int.Parse(v); break;
                        case "pc_ip": c.PcIp = v; break;
                        case "window_width": c.WindowWidth = int.Parse(v); c.WindowWidthIncluded = true; break;
                        case "window_height": c.WindowHeight = int.Parse(v); c.WindowHeightIncluded = true; break;
                        case "lock_enabled": c.LockEnabled = ParseBool(v, k, strict); sawLockEnabled = true; break;
                        case "auto_lock_enabled": c.AutoLockEnabled = ParseBool(v, k, strict); break;
                        case "lock_salt": c.LockSalt = v; sawLockSalt = true; break;
                        case "lock_hash": c.LockHash = v; sawLockHash = true; break;
                        case "secrets_included": c.SecretsIncluded = ParseBool(v, k, strict); break;
                        default:
                            // 导入时忽略未来版本或旧版本留下的未知项；已知项仍严格校验。
                            // 这样不会因为旧版新增的展示/推送字段阻断配置迁移。
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (strict) throw new FormatException("配置项无效：" + k, ex);
                }
            }
            if (strict && (!sawPort || !sawMinLen || !sawMaxLen))
                throw new FormatException("配置文件缺少必要配置项。");
            if (strict && c.SecretsIncluded && (!sawToken || !sawNtfyTopic || !sawNtfyToken))
                throw new FormatException("配置文件缺少完整的密码锁配置。");
            if (strict && c.SecretsIncluded && c.LockEnabled
                && (!sawLockEnabled || !sawLockSalt || !sawLockHash || !c.LockDataValid))
                throw new FormatException("配置文件缺少完整的密码锁配置。");
            if (strict && (c.MinLen < 1 || c.MinLen > 32 || c.MaxLen < c.MinLen || c.MaxLen > 32
                 || c.PrivacyMode < 0 || c.PrivacyMode > 2))
                throw new FormatException("配置文件中的验证码长度或隐私模式无效。");
            if (strict && (c.Port < 1 || c.Port > 65535))
                throw new FormatException("配置文件中的端口无效。");
            if (c.Port < 1 || c.Port > 65535) c.Port = 8787;
            if (c.MinLen < 1 || c.MinLen > 32) c.MinLen = 4;
            if (c.MaxLen < c.MinLen || c.MaxLen > 32)
                c.MaxLen = c.MinLen > 8 ? c.MinLen : 8;
            if (c.PrivacyMode < 0 || c.PrivacyMode > 2) c.PrivacyMode = 0;
            string filterError;
            if (!CodeExtractor.ValidateFilters(c.FilterKeywords, c.FilterRegex, out filterError))
            {
                if (strict) throw new FormatException("配置文件中的信息过滤规则无效：" + filterError);
                c.FilterKeywords = "";
                c.FilterRegex = "";
            }
            // 锁数据损坏时不静默解除密码锁（fail-closed）：保留锁定，只允许用原始完整配置恢复。
            return c;
        }

        static bool ParseBool(string value, string key, bool strict)
        {
            string text = (value ?? "").Trim();
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (strict) throw new FormatException("配置项无效：" + key);
            return false;
        }

        static string DecodeText(string value, string key, bool strict)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? ""));
            }
            catch (Exception ex)
            {
                if (strict) throw new FormatException("配置项无效：" + key, ex);
                return "";
            }
        }

        /// <summary>锁校验信息是否完整可用。</summary>
        public bool LockDataValid
        {
            get { return ValidLockData(LockSalt, LockHash); }
        }

        /// <summary>自动锁定是否生效（需要先启用密码锁）。</summary>
        public bool ShouldAutoLock
        {
            get { return LockEnabled && AutoLockEnabled; }
        }

        /// <summary>
        /// 打开窗口时是否要求解锁：启用自动锁定时需要；锁数据损坏时也必须解锁，不能绕过。
        /// </summary>
        public bool LockOnOpen
        {
            get { return LockEnabled && (AutoLockEnabled || !LockDataValid); }
        }

        static bool ValidLockData(string saltText, string hashText)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(saltText ?? "");
                byte[] hash = Convert.FromBase64String(hashText ?? "");
                return salt.Length == 16 && hash.Length == 32;
            }
            catch { return false; }
        }

        public void Save(string path)
        {
            Save(path, true);
        }

        public void Save(string path, bool includeSecrets)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# codepass 配置 (由界面保存)");
            sb.AppendLine("# 修改端口 / token / ntfy 后需重启程序生效");
            sb.AppendLine("secrets_included = " + (includeSecrets ? "1" : "0"));
            sb.AppendLine("port = " + Port);
            sb.AppendLine("token = " + (includeSecrets ? Token : ""));
            sb.AppendLine("ntfy_server = " + NtfyServer);
            sb.AppendLine("ntfy_topic = " + (includeSecrets ? NtfyTopic : ""));
            sb.AppendLine("ntfy_token = " + (includeSecrets ? NtfyToken : ""));
            sb.AppendLine("filter_keywords_b64 = " + EncodeText(FilterKeywords));
            sb.AppendLine("filter_regex_b64 = " + EncodeText(FilterRegex));
            sb.AppendLine("min_len = " + MinLen);
            sb.AppendLine("max_len = " + MaxLen);
            sb.AppendLine("auto_clear_seconds = " + AutoClearSeconds);
            sb.AppendLine("show_tip = " + (ShowTip ? "1" : "0"));
            sb.AppendLine("start_with_windows = " + (StartWithWindows ? "1" : "0"));
            sb.AppendLine("privacy_mode = " + PrivacyMode);
            sb.AppendLine("lock_enabled = " + (includeSecrets && LockEnabled ? "1" : "0"));
            sb.AppendLine("lock_salt = " + (includeSecrets ? LockSalt : ""));
            sb.AppendLine("lock_hash = " + (includeSecrets ? LockHash : ""));
            sb.AppendLine("# 自动锁定默认关闭，设为 1 才在启动、重开或最小化时锁定。");
            sb.AppendLine("auto_lock_enabled = " + (includeSecrets && AutoLockEnabled ? "1" : "0"));
            sb.AppendLine("# 手机端要连接的电脑地址 (仅用于生成手机端配置)");
            sb.AppendLine("pc_ip = " + PcIp);
            sb.AppendLine("window_width = " + WindowWidth);
            sb.AppendLine("window_height = " + WindowHeight);
            WindowWidthIncluded = true;
            WindowHeightIncluded = true;
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public void CopyFrom(Config other)
        {
            if (other == null) return;
            Port = other.Port;
            Token = other.Token;
            NtfyTopic = other.NtfyTopic;
            NtfyServer = other.NtfyServer;
            NtfyToken = other.NtfyToken;
            FilterKeywords = other.FilterKeywords;
            FilterRegex = other.FilterRegex;
            MinLen = other.MinLen;
            MaxLen = other.MaxLen;
            AutoClearSeconds = other.AutoClearSeconds;
            ShowTip = other.ShowTip;
            StartWithWindows = other.StartWithWindows;
            PrivacyMode = other.PrivacyMode;
            PcIp = other.PcIp;
            WindowWidth = other.WindowWidth;
            WindowHeight = other.WindowHeight;
            WindowWidthIncluded = other.WindowWidthIncluded;
            WindowHeightIncluded = other.WindowHeightIncluded;
            LockEnabled = other.LockEnabled;
            AutoLockEnabled = other.AutoLockEnabled;
            LockSalt = other.LockSalt;
            LockHash = other.LockHash;
            SecretsIncluded = other.SecretsIncluded;
        }

        public void SetPassword(string password)
        {
            if (password == null || password.Length == 0) throw new ArgumentException("password");
            byte[] salt = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);
            byte[] hash;
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, salt, PasswordIterations))
                hash = derive.GetBytes(32);
            LockSalt = Convert.ToBase64String(salt);
            LockHash = Convert.ToBase64String(hash);
            LockEnabled = true;
        }

        public void ClearPassword()
        {
            LockEnabled = false;
            AutoLockEnabled = false;
            LockSalt = "";
            LockHash = "";
        }

        public bool VerifyPassword(string password)
        {
            if (!LockEnabled || password == null) return false;
            try
            {
                byte[] salt = Convert.FromBase64String(LockSalt);
                byte[] expected = Convert.FromBase64String(LockHash);
                if (salt.Length != 16 || expected.Length != 32) return false;
                byte[] actual;
                using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, salt, PasswordIterations))
                    actual = derive.GetBytes(expected.Length);
                if (actual.Length != expected.Length) return false;
                int diff = 0;
                for (int i = 0; i < actual.Length; i++) diff |= actual[i] ^ expected[i];
                return diff == 0;
            }
            catch { return false; }
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
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
        // 默认只收取明确的验证码短信；用户清空后恢复全部收取。
        public string SmsAcceptKeywords = DefaultSmsAcceptKeywords;
        public const string DefaultSmsAcceptKeywords = "验证码\n校验码\n安全代码\n安全码\n动态码\n动态密码\n短信码\n一次性密码\nverification code\nsecurity code\none-time code\nOTP";
        public int MinLen = 4;
        public int MaxLen = 8;
        public const int DefaultSmsMaxLength = 500;
        public const int MaximumSmsMaxLength = 10000;
        public int SmsMaxLength = DefaultSmsMaxLength;
        public int AutoClearSeconds = 0;
        public bool ShowTip = true;
        public bool StartWithWindows = false;
        public const int DefaultHistoryRetentionMinutes = 10;
        public const int MaximumHistoryRetentionMinutes = 43200;
        public int HistoryRetentionMinutes = DefaultHistoryRetentionMinutes;
        // 0 = 只提示已收到, 1 = 显示通知但验证码以星号隐藏, 2 = 完整显示短信内容
        public int PrivacyMode = 0;
        public int EmailPrivacyMode = 0;
        public static string LastSaveError { get; private set; }
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

        // ---- 邮箱接收（可选）----
        public bool EmailEnabled = false;
        public string EmailServer = "imap.qq.com";
        public string EmailAccount = "";
        // 内存中保存明文；写入配置文件时改用当前 Windows 用户的 DPAPI 加密（email_auth_enc）。
        public string EmailAuth = "";
        // 解密失败（换电脑/换 Windows 用户）时保留原密文，避免随后的普通保存把授权码清掉。
        public string EmailAuthEncRaw = "";
        public bool EmailAuthUndecryptable = false;
        // 旧 OAuth 配置迁移后须主动填写应用密码，不自动使用遗留密码。
        public bool EmailPasswordRequired;
        public int EmailPollSeconds = 30;
        // 命中任意一行才会读取邮件正文；其它邮件不下载正文、不改动已读状态。
        public string EmailKeywords = "验证码\n校验码\n安全代码\n安全码\n动态码\nverification code\nsecurity code\none-time code\nOTP";
        // 每行一个完整邮箱或完整域名；留空表示不限发件人。
        public string EmailSenders = "";
        public bool EmailCheckJunk = false;
        public bool EmailNotify = true;
        public bool EmailAutoCopy = true;
        public bool EmailSaveBody = false;
        // 以服务器+账号+文件夹为键，避免两个垃圾文件夹复用水位。
        public string EmailMailboxState = "";
        // 已处理邮件的水位线（UID）与邮箱的 UIDVALIDITY 校验值。
        public long EmailLastUid = 0;
        public long EmailUidValidity = 0;
        public long EmailLastUidJunk = 0;
        public long EmailUidValidityJunk = 0;
        // ---- QQ 右下角新邮件提醒弹窗联动（可选）----
        public bool QqPopupEnabled = false;

        const int PasswordIterations = 100000;

        // 界面线程与邮箱监测线程都会保存配置，序列化避免并发写坏文件。
        internal static readonly object SaveGate = new object();

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
            bool sawEmailPrivacy = false;
            string legacyEmailMode = "password";
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
                        case "sms_accept_keywords_b64": c.SmsAcceptKeywords = DecodeText(v, k, strict); break;
                        // 兼容旧版本配置，但不再启用手机推送服务。
                        case "push_provider": break;
                        case "push_server": break;
                        case "push_token": break;
                        case "min_len": c.MinLen = int.Parse(v); sawMinLen = true; break;
                        case "max_len": c.MaxLen = int.Parse(v); sawMaxLen = true; break;
                        case "sms_max_length": c.SmsMaxLength = int.Parse(v); break;
                        case "auto_clear_seconds": c.AutoClearSeconds = int.Parse(v); break;
                        case "show_tip": c.ShowTip = ParseBool(v, k, strict); break;
                        case "start_with_windows": c.StartWithWindows = ParseBool(v, k, strict); break;
                        case "history_retention_minutes": c.HistoryRetentionMinutes = int.Parse(v); break;
                        case "privacy_mode": c.PrivacyMode = int.Parse(v); break;
                        case "email_privacy_mode": c.EmailPrivacyMode = int.Parse(v); sawEmailPrivacy = true; break;
                        case "pc_ip": c.PcIp = v; break;
                        case "window_width": c.WindowWidth = int.Parse(v); c.WindowWidthIncluded = true; break;
                        case "window_height": c.WindowHeight = int.Parse(v); c.WindowHeightIncluded = true; break;
                        case "lock_enabled": c.LockEnabled = ParseBool(v, k, strict); sawLockEnabled = true; break;
                        case "auto_lock_enabled": c.AutoLockEnabled = ParseBool(v, k, strict); break;
                        case "lock_salt": c.LockSalt = v; sawLockSalt = true; break;
                        case "lock_hash": c.LockHash = v; sawLockHash = true; break;
                        case "secrets_included": c.SecretsIncluded = ParseBool(v, k, strict); break;
                        case "email_enabled": c.EmailEnabled = ParseBool(v, k, strict); break;
                        case "email_server": c.EmailServer = v; break;
                        case "email_account": c.EmailAccount = v; break;
                        // 兼容明文授权码（旧格式 / 手写配置）；正常保存只写加密值。
                        case "email_auth": c.EmailAuth = v; break;
                        case "email_auth_enc":
                            c.EmailAuth = DecodeSecret(v);
                            if (c.EmailAuth.Length == 0 && v.Length > 0)
                            {
                                c.EmailAuthEncRaw = v;
                                c.EmailAuthUndecryptable = true;
                            }
                            break;
                        case "email_poll_seconds": c.EmailPollSeconds = int.Parse(v); break;
                        case "email_auth_mode": legacyEmailMode = v; break;
                        // OAuth 已撤下：不解密、不保存旧令牌。
                        case "email_oauth_client_id":
                        case "email_oauth_account":
                        case "email_oauth_authorized_client":
                        case "email_oauth_refresh_enc": break;
                        case "email_keywords_b64": c.EmailKeywords = DecodeText(v, k, strict); break;
                        case "email_senders_b64": c.EmailSenders = DecodeText(v, k, strict); break;
                        case "email_check_junk": c.EmailCheckJunk = ParseBool(v, k, strict); break;
                        case "email_notify": c.EmailNotify = ParseBool(v, k, strict); break;
                        case "email_autocopy": c.EmailAutoCopy = ParseBool(v, k, strict); break;
                        case "email_save_body": c.EmailSaveBody = ParseBool(v, k, strict); break;
                        case "email_mailbox_state_b64": c.EmailMailboxState = DecodeText(v, k, strict); break;
                        case "email_last_uid": c.EmailLastUid = long.Parse(v); break;
                        case "email_uidvalidity": c.EmailUidValidity = long.Parse(v); break;
                        case "email_last_uid_junk": c.EmailLastUidJunk = long.Parse(v); break;
                        case "email_uidvalidity_junk": c.EmailUidValidityJunk = long.Parse(v); break;
                        case "qq_popup_enabled": c.QqPopupEnabled = ParseBool(v, k, strict); break;
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
                 || c.PrivacyMode < 0 || c.PrivacyMode > 2 || c.EmailPrivacyMode < 0 || c.EmailPrivacyMode > 2))
                throw new FormatException("配置文件中的验证码长度或隐私模式无效。");
            if (strict && (c.Port < 1 || c.Port > 65535))
                throw new FormatException("配置文件中的端口无效。");
            if (strict && (c.SmsMaxLength < 1 || c.SmsMaxLength > MaximumSmsMaxLength))
                throw new FormatException("短信长度上限需在 1～10000 字符之间。");
            if (strict && (c.HistoryRetentionMinutes < 0 || c.HistoryRetentionMinutes > MaximumHistoryRetentionMinutes))
                throw new FormatException("记录保留时间需在 0～43200 分钟之间。");
            if (c.HistoryRetentionMinutes < 0 || c.HistoryRetentionMinutes > MaximumHistoryRetentionMinutes)
                c.HistoryRetentionMinutes = DefaultHistoryRetentionMinutes;
            if (c.Port < 1 || c.Port > 65535) c.Port = 8787;
            if (c.MinLen < 1 || c.MinLen > 32) c.MinLen = 4;
            if (c.MaxLen < c.MinLen || c.MaxLen > 32)
                c.MaxLen = c.MinLen > 8 ? c.MinLen : 8;
            if (c.PrivacyMode < 0 || c.PrivacyMode > 2) c.PrivacyMode = 0;
            if (!sawEmailPrivacy) c.EmailPrivacyMode = c.PrivacyMode;
            if (c.EmailPrivacyMode < 0 || c.EmailPrivacyMode > 2) c.EmailPrivacyMode = 0;
            if (c.SmsMaxLength < 1 || c.SmsMaxLength > MaximumSmsMaxLength) c.SmsMaxLength = DefaultSmsMaxLength;
            if (c.EmailPollSeconds < 10 || c.EmailPollSeconds > 600) c.EmailPollSeconds = 30;
            if (c.EmailKeywords == null) c.EmailKeywords = "";
            if (c.EmailSenders == null) c.EmailSenders = "";
            string normalizedKeywords, normalizedSenders, mailRuleError;
            bool keywordsValid = MailRules.NormalizeLines(c.EmailKeywords, out normalizedKeywords, out mailRuleError);
            if (!keywordsValid && strict) throw new FormatException("邮箱主题关键词无效：" + mailRuleError);
            bool sendersValid = MailRules.NormalizeSenders(c.EmailSenders, out normalizedSenders, out mailRuleError);
            if (!sendersValid && strict) throw new FormatException("邮箱发件人规则无效：" + mailRuleError);
            if (!keywordsValid || !sendersValid) c.EmailEnabled = false;
            if (keywordsValid) c.EmailKeywords = normalizedKeywords;
            if (sendersValid) c.EmailSenders = normalizedSenders;
            if (c.EmailAuth == null) c.EmailAuth = "";
            if (legacyEmailMode != "password")
            {
                if (strict && legacyEmailMode != "google_oauth") throw new FormatException("邮箱认证方式无效。");
                c.EmailEnabled = false;
                c.EmailAuth = ""; c.EmailAuthEncRaw = ""; c.EmailAuthUndecryptable = false;
                c.EmailPasswordRequired = true;
            }
            if (c.EmailServer == null) c.EmailServer = "";
            if (c.EmailAccount == null) c.EmailAccount = "";
            if (c.EmailLastUid < 0) c.EmailLastUid = 0;
            if (c.EmailUidValidity < 0) c.EmailUidValidity = 0;
            if (c.EmailLastUidJunk < 0) c.EmailLastUidJunk = 0;
            if (c.EmailUidValidityJunk < 0) c.EmailUidValidityJunk = 0;
            string filterError;
            if (!CodeExtractor.ValidateFilters(c.FilterKeywords, c.FilterRegex, out filterError))
            {
                if (strict) throw new FormatException("配置文件中的信息过滤规则无效：" + filterError);
                c.FilterKeywords = "";
                c.FilterRegex = "";
            }

            // 与过滤规则同待遇：坏文件重置为空（即恢复全部收取），不让解析问题阻断收短信。
            string acceptKeywordError;
            if (!CodeExtractor.ValidateKeywordLines(c.SmsAcceptKeywords, "接收关键字", out acceptKeywordError))
            {
                if (strict) throw new FormatException("配置文件中的接收关键字无效：" + acceptKeywordError);
                c.SmsAcceptKeywords = "";
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

        /// <summary>用当前 Windows 用户凭据（DPAPI）加密邮箱授权码；换机或换用户后需要重新填写。</summary>
        static string ProtectSecret(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        /// <summary>解密邮箱授权码；失败（换机、换了 Windows 用户或文件损坏）时按未填写处理，不阻断启动。</summary>
        static string DecodeSecret(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            try
            {
                byte[] enc = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                Log.Write("email authorization code cannot be decrypted on this Windows account; please re-enter it");
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
            Save(path, includeSecrets, false);
        }

        /// <param name="exportMode">导出到文件时用明文授权码，便于在别的电脑导入；日常保存始终用 DPAPI 加密。</param>
        public void Save(string path, bool includeSecrets, bool exportMode)
        {
            lock (SaveGate)
            {
            try
            {
            if (SmsMaxLength < 1 || SmsMaxLength > MaximumSmsMaxLength)
                throw new FormatException("短信长度上限需在 1～10000 字符之间。");
            if (HistoryRetentionMinutes < 0 || HistoryRetentionMinutes > MaximumHistoryRetentionMinutes)
                throw new FormatException("记录保留时间需在 0～43200 分钟之间。");
            if (PrivacyMode < 0 || PrivacyMode > 2 || EmailPrivacyMode < 0 || EmailPrivacyMode > 2)
                throw new FormatException("隐私通知模式无效。");
            string keywordRules, senderRules, ruleError;
            if (!MailRules.NormalizeLines(EmailKeywords, out keywordRules, out ruleError))
                throw new FormatException("邮箱主题关键词无效：" + ruleError);
            if (!MailRules.NormalizeSenders(EmailSenders, out senderRules, out ruleError))
                throw new FormatException("邮箱发件人规则无效：" + ruleError);
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
            sb.AppendLine("sms_accept_keywords_b64 = " + EncodeText(SmsAcceptKeywords));
            sb.AppendLine("min_len = " + MinLen);
            sb.AppendLine("max_len = " + MaxLen);
            sb.AppendLine("sms_max_length = " + SmsMaxLength);
            sb.AppendLine("auto_clear_seconds = " + AutoClearSeconds);
            sb.AppendLine("show_tip = " + (ShowTip ? "1" : "0"));
            sb.AppendLine("start_with_windows = " + (StartWithWindows ? "1" : "0"));
            sb.AppendLine("history_retention_minutes = " + HistoryRetentionMinutes);
            sb.AppendLine("privacy_mode = " + PrivacyMode);
            sb.AppendLine("email_privacy_mode = " + EmailPrivacyMode);
            sb.AppendLine("lock_enabled = " + (includeSecrets && LockEnabled ? "1" : "0"));
            sb.AppendLine("lock_salt = " + (includeSecrets ? LockSalt : ""));
            sb.AppendLine("lock_hash = " + (includeSecrets ? LockHash : ""));
            sb.AppendLine("# 自动锁定默认关闭，设为 1 才在启动、重开或最小化时锁定。");
            sb.AppendLine("auto_lock_enabled = " + (includeSecrets && AutoLockEnabled ? "1" : "0"));
            sb.AppendLine("# 邮箱接收（可选）：授权码使用当前 Windows 用户的 DPAPI 加密，换电脑需重新填写。");
            sb.AppendLine("email_enabled = " + (EmailEnabled ? "1" : "0"));
            sb.AppendLine("email_server = " + EmailServer);
            sb.AppendLine("email_account = " + EmailAccount);
            string emailEnc = "";
            if (EmailAuth.Length > 0) emailEnc = ProtectSecret(EmailAuth);
            else if (EmailAuthUndecryptable) emailEnc = EmailAuthEncRaw;
            sb.AppendLine("email_auth_enc = " + (includeSecrets && !exportMode ? emailEnc : ""));
            if (exportMode && includeSecrets && EmailAuth.Length > 0)
                sb.AppendLine("email_auth = " + EmailAuth);
            sb.AppendLine("email_poll_seconds = " + EmailPollSeconds);
            sb.AppendLine("email_keywords_b64 = " + EncodeText(keywordRules));
            sb.AppendLine("email_senders_b64 = " + EncodeText(senderRules));
            sb.AppendLine("email_check_junk = " + (EmailCheckJunk ? "1" : "0"));
            sb.AppendLine("email_notify = " + (EmailNotify ? "1" : "0"));
            sb.AppendLine("email_autocopy = " + (EmailAutoCopy ? "1" : "0"));
            sb.AppendLine("email_save_body = " + (EmailSaveBody ? "1" : "0"));
            sb.AppendLine("email_mailbox_state_b64 = " + EncodeText(exportMode ? "" : EmailMailboxState));
            sb.AppendLine("email_last_uid = " + EmailLastUid);
            sb.AppendLine("email_uidvalidity = " + EmailUidValidity);
            sb.AppendLine("email_last_uid_junk = " + EmailLastUidJunk);
            sb.AppendLine("email_uidvalidity_junk = " + EmailUidValidityJunk);
            sb.AppendLine("qq_popup_enabled = " + (QqPopupEnabled ? "1" : "0"));
            sb.AppendLine("# 手机端要连接的电脑地址 (仅用于生成手机端配置)");
            sb.AppendLine("pc_ip = " + PcIp);
            sb.AppendLine("window_width = " + WindowWidth);
            sb.AppendLine("window_height = " + WindowHeight);
            WindowWidthIncluded = true;
            WindowHeightIncluded = true;
            WriteAtomically(path, sb.ToString());
            if (!exportMode) LastSaveError = null;
            }
            catch (Exception ex)
            {
                if (!exportMode)
                {
                LastSaveError = ex is UnauthorizedAccessException ? "配置保存失败：目录不可写或文件只读"
                    : ex is IOException ? "配置保存失败：文件被占用或磁盘写入失败"
                    : ex is CryptographicException ? "配置保存失败：Windows 凭据加密失败"
                    : "配置保存失败：请检查设置与文件访问权限";
                Program.EmailStatusChanged();
                }
                throw;
            }
            }
        }

        internal static void WriteAtomically(string path, string text)
        {
            string target = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(target), "codepass-" + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] preamble = new UTF8Encoding(true).GetPreamble();
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(preamble, 0, preamble.Length);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // 写失败时不截断原文件；失败临时文件保留供恢复，不静默报告成功。
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }

        // 仅回收本程序已超过一天的原子写临时文件，既不读取内容也不处理链接。
        internal static int RecycleStaleTemporaryFiles(string directory)
        {
            int recycled = 0;
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return 0;
                foreach (string path in Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(path);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^(?:codepass-)?(?:config\.ini|history\.txt)\.[a-f0-9]{32}\.tmp$")) continue;
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-1)) continue;
                    Type shellType = Type.GetTypeFromProgID("Shell.Application");
                    object shell = Activator.CreateInstance(shellType);
                    object bin = null;
                    try
                    {
                        bin = shellType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { 10 });
                        bin.GetType().InvokeMember("MoveHere", System.Reflection.BindingFlags.InvokeMethod, null, bin, new object[] { path, 1044 });
                        if (!File.Exists(path)) recycled++;
                    }
                    finally
                    {
                        if (bin != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(bin);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                    }
                }
            }
            catch { Log.Write("storage: temporary file recycling unavailable"); }
            return recycled;
        }

        static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public void CopyFrom(Config other)
        {
            if (other == null) return;
            lock (SaveGate)
            {
            Port = other.Port;
            Token = other.Token;
            NtfyTopic = other.NtfyTopic;
            NtfyServer = other.NtfyServer;
            NtfyToken = other.NtfyToken;
            FilterKeywords = other.FilterKeywords;
            FilterRegex = other.FilterRegex;
            SmsAcceptKeywords = other.SmsAcceptKeywords;
            MinLen = other.MinLen;
            MaxLen = other.MaxLen;
            SmsMaxLength = other.SmsMaxLength;
            AutoClearSeconds = other.AutoClearSeconds;
            ShowTip = other.ShowTip;
            StartWithWindows = other.StartWithWindows;
            HistoryRetentionMinutes = other.HistoryRetentionMinutes;
            PrivacyMode = other.PrivacyMode;
            EmailPrivacyMode = other.EmailPrivacyMode;
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
            EmailEnabled = other.EmailEnabled;
            EmailServer = other.EmailServer;
            EmailAccount = other.EmailAccount;
            EmailAuth = other.EmailAuth;
            EmailAuthEncRaw = other.EmailAuthEncRaw;
            EmailAuthUndecryptable = other.EmailAuthUndecryptable;
            EmailPasswordRequired = other.EmailPasswordRequired;
            EmailPollSeconds = other.EmailPollSeconds;
            EmailKeywords = other.EmailKeywords;
            EmailSenders = other.EmailSenders;
            EmailCheckJunk = other.EmailCheckJunk;
            EmailNotify = other.EmailNotify;
            EmailAutoCopy = other.EmailAutoCopy;
            EmailSaveBody = other.EmailSaveBody;
            EmailMailboxState = other.EmailMailboxState;
            EmailLastUid = other.EmailLastUid;
            EmailUidValidity = other.EmailUidValidity;
            EmailLastUidJunk = other.EmailLastUidJunk;
            EmailUidValidityJunk = other.EmailUidValidityJunk;
            QqPopupEnabled = other.QqPopupEnabled;
            }
        }

        public void SetPassword(string password)
        {
            SetPasswordCore(password);
        }

        internal bool TryMailboxWater(string key, long validity, out long water)
        {
            lock (SaveGate)
            {
                foreach (string line in (EmailMailboxState ?? "").Split('\n'))
                {
                    string[] fields = line.Split('\t');
                    long storedValidity, storedWater;
                    if (fields.Length == 3 && fields[0] == key && long.TryParse(fields[1], out storedValidity)
                        && long.TryParse(fields[2], out storedWater) && storedValidity == validity && storedWater >= 0)
                    { water = storedWater; return true; }
                }
                water = 0; return false;
            }
        }

        internal void SetMailboxWater(string key, long validity, long water)
        {
            lock (SaveGate)
            {
                List<string> lines = new List<string>();
                lines.Add(key + "\t" + validity + "\t" + water);
                foreach (string line in (EmailMailboxState ?? "").Split('\n'))
                    if (line.Length > 0 && !line.StartsWith(key + "\t", StringComparison.Ordinal)) lines.Add(line);
                EmailMailboxState = string.Join("\n", lines.ToArray());
            }
        }

        void SetPasswordCore(string password)
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

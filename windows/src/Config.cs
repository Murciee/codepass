using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Codepass
{
    sealed class Config
    {
        // Setting 字段由构造函数中的元数据初始化，随后也可由页面绑定写入。
#pragma warning disable 0649
        [Setting("port", SettingsSection.Sms, 8787, 1, 65535)]
        public int Port;
        public string Token = "";
        public string NtfyTopic = "";
        [Setting("ntfy_server", SettingsSection.Ntfy, "https://ntfy.sh")]
        public string NtfyServer;
        public string NtfyToken = "";
        // 每行一条；关键字按包含匹配，正则按逐行匹配。
        [Setting("filter_keywords_b64", SettingsSection.SmsRules, "", Base64 = true)]
        public string FilterKeywords;
        [Setting("filter_regex_b64", SettingsSection.SmsRules, "", Base64 = true)]
        public string FilterRegex;
        // 默认只收取明确的验证码短信；用户清空后恢复全部收取。
        [Setting("sms_accept_keywords_b64", SettingsSection.SmsRules, DefaultSmsAcceptKeywords, Base64 = true)]
        public string SmsAcceptKeywords;
        public const string DefaultSmsAcceptKeywords = "验证码\n校验码\n安全代码\n安全码\n动态码\n动态密码\n短信码\n一次性密码\nverification code\nsecurity code\none-time code\nOTP";
        [Setting("min_len", SettingsSection.SmsRules, 4, 1, 32)]
        public int MinLen;
        [Setting("max_len", SettingsSection.SmsRules, 8, 1, 32)]
        public int MaxLen;
        public const int DefaultSmsMaxLength = 500;
        public const int MaximumSmsMaxLength = 10000;
        [Setting("sms_max_length", SettingsSection.SmsRules, DefaultSmsMaxLength, 1, MaximumSmsMaxLength)]
        public int SmsMaxLength;
        [Setting("auto_clear_seconds", SettingsSection.SmsRules, 0, 0, 86400)]
        public int AutoClearSeconds;
        [Setting("show_tip", SettingsSection.SmsRules, true)]
        public bool ShowTip;
        [Setting("start_with_windows", SettingsSection.Sms, false)]
        public bool StartWithWindows;
        public const int DefaultHistoryRetentionMinutes = 10;
        public const int MaximumHistoryRetentionMinutes = 43200;
        [Setting("history_retention_minutes", SettingsSection.Retention, DefaultHistoryRetentionMinutes, 0, MaximumHistoryRetentionMinutes)]
        public int HistoryRetentionMinutes;
        // 0 = 只提示已收到, 1 = 显示通知但验证码以星号隐藏, 2 = 完整显示短信内容
        [Setting("privacy_mode", SettingsSection.Privacy, 0, 0, 2)]
        public int PrivacyMode;
        [Setting("email_privacy_mode", SettingsSection.Privacy, 0, 0, 2)]
        public int EmailPrivacyMode;
        public static string LastSaveError { get; private set; }
        [Setting("pc_ip", SettingsSection.Sms, "")]
        public string PcIp;        // 手机端要连接的电脑地址, 仅用于生成手机端配置
        public int WindowWidth = 1100;
        public int WindowHeight = 760;
        // 旧配置可能没有窗口尺寸；导入时保留当前尺寸而不是用默认值覆盖。
        public bool WindowWidthIncluded = false;
        public bool WindowHeightIncluded = false;
        public int WindowLeft = 0;
        public int WindowTop = 0;
        // 只有真正测量过窗口位置才写入：否则 (0,0) 会被当成"用户把窗口放在左上角"而覆盖默认居中。
        public bool WindowPositionIncluded = false;
        public bool LockEnabled = false;
        // 自动锁定默认关闭, 设为 true 才在启动、重开窗口或最小化时要求解锁。
        public bool AutoLockEnabled = false;
        public string LockSalt = "";
        public string LockHash = "";
        // 没有该标记的旧配置按脱敏文件处理，避免导入时清空当前令牌和密码锁。
        public bool SecretsIncluded = false;
        public bool Dry = false;

        // ---- 邮箱接收（可选）----
        [Setting("email_enabled", SettingsSection.Email, false)]
        public bool EmailEnabled;
        [Setting("email_server", SettingsSection.Email, "imap.qq.com")]
        public string EmailServer;
        [Setting("email_account", SettingsSection.Email, "")]
        public string EmailAccount;
        // 内存中保存明文；写入配置文件时改用当前 Windows 用户的 DPAPI 加密（email_auth_enc）。
        public string EmailAuth = "";
        // 解密失败（换电脑/换 Windows 用户）时保留原密文，避免随后的普通保存把授权码清掉。
        public string EmailAuthEncRaw = "";
        public bool EmailAuthUndecryptable = false;
        // 旧 OAuth 配置迁移后须主动填写应用密码，不自动使用遗留密码。
        public bool EmailPasswordRequired;
        [Setting("email_poll_seconds", SettingsSection.EmailRules, 30, 10, 600)]
        public int EmailPollSeconds;
        // 命中任意一行才会读取邮件正文；其它邮件不下载正文、不改动已读状态。
        [Setting("email_keywords_b64", SettingsSection.EmailRules, "验证码\n校验码\n安全代码\n安全码\n动态码\nverification code\nsecurity code\none-time code\nOTP", Base64 = true)]
        public string EmailKeywords;
        // 每行一个完整邮箱或完整域名；留空表示不限发件人。
        [Setting("email_senders_b64", SettingsSection.EmailRules, "", Base64 = true)]
        public string EmailSenders;
        [Setting("email_check_junk", SettingsSection.EmailRules, false)]
        public bool EmailCheckJunk;
        [Setting("email_notify", SettingsSection.EmailRules, true)]
        public bool EmailNotify;
        [Setting("email_autocopy", SettingsSection.EmailRules, true)]
        public bool EmailAutoCopy;
        [Setting("email_save_body", SettingsSection.EmailRules, false)]
        public bool EmailSaveBody;
        // 以服务器+账号+文件夹为键，避免两个垃圾文件夹复用水位。
        public string EmailMailboxState = "";
        // 已处理邮件的水位线（UID）与邮箱的 UIDVALIDITY 校验值。
        public long EmailLastUid = 0;
        public long EmailUidValidity = 0;
        public long EmailLastUidJunk = 0;
        public long EmailUidValidityJunk = 0;
        // ---- QQ 右下角新邮件提醒弹窗联动（可选）----
        [Setting("qq_popup_enabled", SettingsSection.EmailRules, false)]
        public bool QqPopupEnabled;
#pragma warning restore 0649

        public Config() { ConfigSettings.Initialize(this); }

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
            bool sawWindowLeft = false;
            bool sawWindowTop = false;
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
                    if (ConfigSettings.TryRead(c, k, v, strict))
                    {
                        if (k == "port") sawPort = true;
                        if (k == "min_len") sawMinLen = true;
                        if (k == "max_len") sawMaxLen = true;
                        if (k == "email_privacy_mode") sawEmailPrivacy = true;
                        continue;
                    }
                    switch (k)
                    {
                        case "token": c.Token = v; sawToken = true; break;
                        case "ntfy_topic": c.NtfyTopic = v; sawNtfyTopic = true; break;
                        case "ntfy_token": c.NtfyToken = v; sawNtfyToken = true; break;
                        // 兼容旧版本配置，但不再启用手机推送服务。
                        case "push_provider": break;
                        case "push_server": break;
                        case "push_token": break;
                        case "window_width": c.WindowWidth = int.Parse(v); c.WindowWidthIncluded = true; break;
                        case "window_height": c.WindowHeight = int.Parse(v); c.WindowHeightIncluded = true; break;
                        case "window_left": c.WindowLeft = int.Parse(v); sawWindowLeft = true; break;
                        case "window_top": c.WindowTop = int.Parse(v); sawWindowTop = true; break;
                        case "lock_enabled": c.LockEnabled = ParseBool(v, k, strict); sawLockEnabled = true; break;
                        case "auto_lock_enabled": c.AutoLockEnabled = ParseBool(v, k, strict); break;
                        case "lock_salt": c.LockSalt = v; sawLockSalt = true; break;
                        case "lock_hash": c.LockHash = v; sawLockHash = true; break;
                        case "secrets_included": c.SecretsIncluded = ParseBool(v, k, strict); break;
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
                        case "email_auth_mode": legacyEmailMode = v; break;
                        // OAuth 已撤下：不解密、不保存旧令牌。
                        case "email_oauth_client_id":
                        case "email_oauth_account":
                        case "email_oauth_authorized_client":
                        case "email_oauth_refresh_enc": break;
                        case "email_mailbox_state_b64": c.EmailMailboxState = DecodeText(v, k, strict); break;
                        case "email_last_uid": c.EmailLastUid = long.Parse(v); break;
                        case "email_uidvalidity": c.EmailUidValidity = long.Parse(v); break;
                        case "email_last_uid_junk": c.EmailLastUidJunk = long.Parse(v); break;
                        case "email_uidvalidity_junk": c.EmailUidValidityJunk = long.Parse(v); break;
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
            // 位置必须成对出现；只写了一个键时按未记忆处理，启动仍使用系统默认位置。
            c.WindowPositionIncluded = sawWindowLeft && sawWindowTop;
            if (strict && (!sawPort || !sawMinLen || !sawMaxLen))
                throw new FormatException("配置文件缺少必要配置项。");
            if (strict && c.SecretsIncluded && (!sawToken || !sawNtfyTopic || !sawNtfyToken))
                throw new FormatException("配置文件缺少完整的密码锁配置。");
            if (strict && c.SecretsIncluded && c.LockEnabled
                && (!sawLockEnabled || !sawLockSalt || !sawLockHash || !c.LockDataValid))
                throw new FormatException("配置文件缺少完整的密码锁配置。");
            if (strict && (!ConfigSettings.InRange("min_len", c.MinLen) || c.MaxLen < c.MinLen || !ConfigSettings.InRange("max_len", c.MaxLen)
                 || !ConfigSettings.InRange("privacy_mode", c.PrivacyMode) || !ConfigSettings.InRange("email_privacy_mode", c.EmailPrivacyMode)))
                throw new FormatException("配置文件中的验证码长度或隐私模式无效。");
            if (strict && !ConfigSettings.InRange("port", c.Port))
                throw new FormatException("配置文件中的端口无效。");
            if (strict && !ConfigSettings.InRange("sms_max_length", c.SmsMaxLength))
                throw new FormatException("短信长度上限需在 1～10000 字符之间。");
            if (strict && !ConfigSettings.InRange("history_retention_minutes", c.HistoryRetentionMinutes))
                throw new FormatException("记录保留时间需在 0～43200 分钟之间。");
            c.HistoryRetentionMinutes = ConfigSettings.Normalize("history_retention_minutes", c.HistoryRetentionMinutes);
            c.Port = ConfigSettings.Normalize("port", c.Port);
            c.MinLen = ConfigSettings.Normalize("min_len", c.MinLen);
            if (c.MaxLen < c.MinLen || !ConfigSettings.InRange("max_len", c.MaxLen))
                c.MaxLen = Math.Max(c.MinLen, ConfigSettings.DefaultNumber("max_len"));
            c.PrivacyMode = ConfigSettings.Normalize("privacy_mode", c.PrivacyMode);
            if (!sawEmailPrivacy) c.EmailPrivacyMode = c.PrivacyMode;
            c.EmailPrivacyMode = ConfigSettings.Normalize("email_privacy_mode", c.EmailPrivacyMode);
            c.SmsMaxLength = ConfigSettings.Normalize("sms_max_length", c.SmsMaxLength);
            c.EmailPollSeconds = ConfigSettings.Normalize("email_poll_seconds", c.EmailPollSeconds);
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

        internal static bool ParseBool(string value, string key, bool strict)
        {
            string text = (value ?? "").Trim();
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (strict) throw new FormatException("配置项无效：" + key);
            return false;
        }

        internal static string DecodeText(string value, string key, bool strict)
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
            if (!ConfigSettings.InRange("sms_max_length", SmsMaxLength))
                throw new FormatException("短信长度上限需在 1～10000 字符之间。");
            if (!ConfigSettings.InRange("history_retention_minutes", HistoryRetentionMinutes))
                throw new FormatException("记录保留时间需在 0～43200 分钟之间。");
            if (!ConfigSettings.InRange("privacy_mode", PrivacyMode) || !ConfigSettings.InRange("email_privacy_mode", EmailPrivacyMode))
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
            Config values = new Config(); ConfigSettings.Copy(values, this);
            values.EmailKeywords = keywordRules; values.EmailSenders = senderRules;
            ConfigSettings.AppendTo(sb, values);
            sb.AppendLine("token = " + (includeSecrets ? Token : ""));
            sb.AppendLine("ntfy_topic = " + (includeSecrets ? NtfyTopic : ""));
            sb.AppendLine("ntfy_token = " + (includeSecrets ? NtfyToken : ""));
            sb.AppendLine("lock_enabled = " + (includeSecrets && LockEnabled ? "1" : "0"));
            sb.AppendLine("lock_salt = " + (includeSecrets ? LockSalt : ""));
            sb.AppendLine("lock_hash = " + (includeSecrets ? LockHash : ""));
            sb.AppendLine("# 自动锁定默认关闭，设为 1 才在启动、重开或最小化时锁定。");
            sb.AppendLine("auto_lock_enabled = " + (includeSecrets && AutoLockEnabled ? "1" : "0"));
            sb.AppendLine("# 邮箱接收（可选）：授权码使用当前 Windows 用户的 DPAPI 加密，换电脑需重新填写。");
            string emailEnc = "";
            if (EmailAuth.Length > 0) emailEnc = ProtectSecret(EmailAuth);
            else if (EmailAuthUndecryptable) emailEnc = EmailAuthEncRaw;
            sb.AppendLine("email_auth_enc = " + (includeSecrets && !exportMode ? emailEnc : ""));
            if (exportMode && includeSecrets && EmailAuth.Length > 0)
                sb.AppendLine("email_auth = " + EmailAuth);
            sb.AppendLine("email_mailbox_state_b64 = " + EncodeText(exportMode ? "" : EmailMailboxState));
            sb.AppendLine("email_last_uid = " + EmailLastUid);
            sb.AppendLine("email_uidvalidity = " + EmailUidValidity);
            sb.AppendLine("email_last_uid_junk = " + EmailLastUidJunk);
            sb.AppendLine("email_uidvalidity_junk = " + EmailUidValidityJunk);
            sb.AppendLine("window_width = " + WindowWidth);
            sb.AppendLine("window_height = " + WindowHeight);
            // 只在测量过位置后写入；没写过这两个键的配置启动时仍由系统决定位置。
            if (WindowPositionIncluded)
            {
                sb.AppendLine("window_left = " + WindowLeft);
                sb.AppendLine("window_top = " + WindowTop);
            }
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
            ConfigSettings.Copy(this, other);
            Token = other.Token;
            NtfyTopic = other.NtfyTopic;
            NtfyToken = other.NtfyToken;
            WindowWidth = other.WindowWidth;
            WindowHeight = other.WindowHeight;
            WindowWidthIncluded = other.WindowWidthIncluded;
            WindowHeightIncluded = other.WindowHeightIncluded;
            WindowLeft = other.WindowLeft;
            WindowTop = other.WindowTop;
            WindowPositionIncluded = other.WindowPositionIncluded;
            LockEnabled = other.LockEnabled;
            AutoLockEnabled = other.AutoLockEnabled;
            LockSalt = other.LockSalt;
            LockHash = other.LockHash;
            SecretsIncluded = other.SecretsIncluded;
            EmailAuth = other.EmailAuth;
            EmailAuthEncRaw = other.EmailAuthEncRaw;
            EmailAuthUndecryptable = other.EmailAuthUndecryptable;
            EmailPasswordRequired = other.EmailPasswordRequired;
            EmailMailboxState = other.EmailMailboxState;
            EmailLastUid = other.EmailLastUid;
            EmailUidValidity = other.EmailUidValidity;
            EmailLastUidJunk = other.EmailLastUidJunk;
            EmailUidValidityJunk = other.EmailUidValidityJunk;
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

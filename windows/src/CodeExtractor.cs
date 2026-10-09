using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Codepass
{
    static class CodeExtractor
    {
        const int MaxFilterLines = 64;
        const int MaxFilterText = 16384;
        const int MaxFilterMatchMilliseconds = 1000;
        const int MaxSingleRegexMilliseconds = 200;

        // 从任意文本中提取验证码; 找不到返回 null
        public static string Extract(string text, int minLen, int maxLen)
        {
            if (text == null) return null;
            string s = text.Trim();
            if (s.Length == 0) return null;

            int min = minLen; if (min < 1) min = 1;
            int max = maxLen; if (max < min) max = min;

            // 1) 整体就是纯验证码
            if (IsCode(s, min, max)) return s;

            // 2) JSON: {"code":"123456"}
            string c = JsonGetString(s, "code");
            if (c != null && IsCode(c.Trim(), min, max)) return c.Trim();

            // 3) 关键词上下文 (code 加词边界, 避免 encode / barcode 之类误命中)
            string kw = @"(验证码|校验码|动态码|动态密码|短信码|verification\s*code|security\s*code|one[-\s]*time\s*code|\bOTP\b|\bcode\b|密码)[^\d]{0,12}(\d{" + min + "," + max + "})";
            Match m = Regex.Match(s, kw, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[2].Value;

            // 明确写出“验证码”的短信优先保留，即使正文末尾附带运营商反诈提醒。
            // 通知类短信只在进入无关键词数字回退前过滤，避免误伤真实验证码。
            if (IsInformationalSms(s)) return null;

            // 4) 回退: 独立 4~8 位数字; 优先 6 位(验证码最常见), 否则取最后一个
            MatchCollection ms = Regex.Matches(s, @"(?<!\d)(\d{" + min + "," + max + @"})(?!\d)");
            if (ms.Count > 0)
            {
                for (int i = ms.Count - 1; i >= 0; i--)
                    if (ms[i].Groups[1].Value.Length == 6) return ms[i].Groups[1].Value;
                return ms[ms.Count - 1].Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// 判断短信是否命中用户配置的信息过滤规则。关键字和正则均按行处理，任一命中即过滤。
        /// </summary>
        public static bool IsFiltered(string text, string keywordLines, string regexLines)
        {
            if (String.IsNullOrEmpty(text)) return false;
            if ((keywordLines ?? "").Length + (regexLines ?? "").Length > MaxFilterText) return false;

            string[] keywords = SplitLines(keywordLines);
            int keywordCount = Math.Min(keywords.Length, MaxFilterLines);
            for (int i = 0; i < keywordCount; i++)
            {
                string keyword = keywords[i].Trim();
                if (keyword.Length > 0
                    && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            string[] patterns = SplitLines(regexLines);
            int patternCount = Math.Min(patterns.Length, MaxFilterLines);
            Stopwatch filterClock = Stopwatch.StartNew();
            for (int i = 0; i < patternCount; i++)
            {
                string pattern = patterns[i].Trim();
                if (pattern.Length == 0) continue;
                int remaining = MaxFilterMatchMilliseconds - (int)filterClock.ElapsedMilliseconds;
                if (remaining <= 0) break;
                try
                {
                    int timeout = Math.Min(MaxSingleRegexMilliseconds, remaining);
                    Regex regex = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(timeout));
                    if (regex.IsMatch(text)) return true;
                }
                catch (ArgumentException)
                {
                    // 界面保存和导入会拦截无效正则；手工修改配置时跳过坏规则，不能阻断收短信。
                }
                catch (RegexMatchTimeoutException)
                {
                    // 防止复杂正则占用收短信线程；超时规则按未命中处理。
                }
            }
            return false;
        }

        /// <summary>校验用户配置的过滤规则，返回面向用户的错误信息。</summary>
        public static bool ValidateFilters(string keywordLines, string regexLines, out string error)
        {
            error = null;
            string keywordText = keywordLines ?? "";
            string regexText = regexLines ?? "";
            if (keywordText.Length + regexText.Length > MaxFilterText)
            {
                error = "过滤规则总长度不能超过 " + MaxFilterText + " 个字符。";
                return false;
            }
            string[] keywords = SplitLines(keywordLines);
            if (keywords.Length > MaxFilterLines)
            {
                error = "过滤关键字最多支持 " + MaxFilterLines + " 行。";
                return false;
            }
            for (int i = 0; i < keywords.Length; i++)
            {
                if (keywords[i].Trim().Length > 1024)
                {
                    error = "第 " + (i + 1) + " 条关键字不能超过 1024 个字符。";
                    return false;
                }
            }

            string[] patterns = SplitLines(regexLines);
            if (patterns.Length > MaxFilterLines)
            {
                error = "过滤正则最多支持 " + MaxFilterLines + " 行。";
                return false;
            }
            for (int i = 0; i < patterns.Length; i++)
            {
                string pattern = patterns[i].Trim();
                if (pattern.Length == 0) continue;
                if (pattern.Length > 1024)
                {
                    error = "第 " + (i + 1) + " 条正则表达式不能超过 1024 个字符。";
                    return false;
                }
                try { new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)); }
                catch (ArgumentException ex)
                {
                    error = "第 " + (i + 1) + " 条正则表达式无效：" + ex.Message;
                    return false;
                }
            }
            return true;
        }

        static string[] SplitLines(string value)
        {
            return (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        // 这类通知通常含有 10010、00、日期或办理代码，但不是验证码短信。
        // 只对较长正文和成组的明确通知语义拦截，避免影响普通验证码短信。
        static bool IsInformationalSms(string text)
        {
            if (text == null || text.Length < 60) return false;
            bool antiFraud = text.IndexOf("电信网络诈骗", StringComparison.OrdinalIgnoreCase) >= 0;
            bool overseasService = text.IndexOf("关闭境外", StringComparison.OrdinalIgnoreCase) >= 0
                && (text.IndexOf("发送短信", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("客服热线", StringComparison.OrdinalIgnoreCase) >= 0);
            bool serviceInstruction = text.IndexOf("发送短信", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("办理", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("客服", StringComparison.OrdinalIgnoreCase) >= 0;
            bool marketing = text.IndexOf("优惠活动", StringComparison.OrdinalIgnoreCase) >= 0
                && (text.IndexOf("退订", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("回复", StringComparison.OrdinalIgnoreCase) >= 0);
            return antiFraud || overseasService || serviceInstruction || marketing;
        }

        static bool IsCode(string s, int min, int max)
        {
            if (s == null) return false;
            if (s.Length < min || s.Length > max) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        // 轻量 JSON 取字符串字段 (不引第三方库)
        public static string JsonGetString(string json, string key)
        {
            if (json == null) return null;
            string pat = "\"" + key + "\"";
            int from = 0;
            int i = -1;
            while (true)
            {
                i = json.IndexOf(pat, from, StringComparison.Ordinal);
                if (i < 0) return null;
                int after = i + pat.Length;
                int j = after;
                while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                if (j < json.Length && json[j] == ':') break;   // 必须后跟冒号才算真正的键
                from = after;
            }
            i = json.IndexOf(':', i + pat.Length);
            if (i < 0) return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;
            StringBuilder sb = new StringBuilder();
            for (; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '\\')
                {
                    i++;
                    if (i >= json.Length) break;
                    char e = json[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 4 < json.Length)
                            {
                                string hex = json.Substring(i + 1, 4);
                                try { sb.Append((char)Convert.ToInt32(hex, 16)); } catch { }
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else if (ch == '"') break;
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}

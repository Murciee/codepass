using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Codepass
{
    // 普通配置项在 Config 字段上声明一次；专用的密钥、迁移和水位逻辑仍由 Config 处理。
    [AttributeUsage(AttributeTargets.Field)]
    sealed class SettingAttribute : Attribute
    {
        internal readonly string Key;
        internal readonly SettingsSection Section;
        internal readonly object DefaultValue;
        internal readonly bool HasRange;
        internal readonly int Minimum, Maximum;
        public bool Base64 { get; set; }

        public SettingAttribute(string key, SettingsSection section, object defaultValue)
        { Key = key; Section = section; DefaultValue = defaultValue; }

        public SettingAttribute(string key, SettingsSection section, int defaultValue, int minimum, int maximum)
            : this(key, section, defaultValue)
        { HasRange = true; Minimum = minimum; Maximum = maximum; }
    }

    sealed class SettingDefinition
    {
        internal readonly FieldInfo Field;
        internal readonly SettingAttribute Setting;

        internal SettingDefinition(FieldInfo field, SettingAttribute setting)
        {
            if (field.FieldType != setting.DefaultValue.GetType()
                || (setting.HasRange && (field.FieldType != typeof(int) || setting.Minimum > setting.Maximum))
                || (setting.Base64 && field.FieldType != typeof(string)))
                throw new InvalidOperationException("配置定义类型不匹配：" + field.Name);
            Field = field; Setting = setting;
            if (setting.HasRange && !InRange((int)setting.DefaultValue))
                throw new InvalidOperationException("配置默认值超出范围：" + field.Name);
        }

        internal object Get(Config config) { return Field.GetValue(config); }
        internal void Set(Config config, object value) { Field.SetValue(config, value); }
        internal bool InRange(int value) { return !Setting.HasRange || value >= Setting.Minimum && value <= Setting.Maximum; }
        internal int Normalize(int value) { return InRange(value) ? value : (int)Setting.DefaultValue; }
        internal int Clamp(int value) { return Math.Max(Setting.Minimum, Math.Min(Setting.Maximum, value)); }

        internal void Read(Config config, string text, bool strict)
        {
            object value;
            if (Field.FieldType == typeof(int)) value = int.Parse(text);
            else if (Field.FieldType == typeof(bool)) value = Config.ParseBool(text, Setting.Key, strict);
            else value = Setting.Base64 ? Config.DecodeText(text, Setting.Key, strict) : text;
            Set(config, value);
        }

        internal string Write(Config config)
        {
            object value = Get(config);
            if (Field.FieldType == typeof(bool)) return (bool)value ? "1" : "0";
            string text = value == null ? "" : value.ToString();
            return Setting.Base64 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) : text;
        }

        internal bool TryNumber(string text, out int value)
        { return int.TryParse(text.Trim(), out value) && InRange(value); }

        internal string NumberError(string label)
        { return label + "需为 " + Setting.Minimum + "～" + Setting.Maximum; }
    }

    static class ConfigSettings
    {
        static readonly List<SettingDefinition> definitions = new List<SettingDefinition>();
        static readonly Dictionary<string, SettingDefinition> byKey = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);

        static ConfigSettings()
        {
            foreach (FieldInfo field in typeof(Config).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                SettingAttribute setting = Attribute.GetCustomAttribute(field, typeof(SettingAttribute)) as SettingAttribute;
                if (setting == null) continue;
                SettingDefinition definition = new SettingDefinition(field, setting);
                definitions.Add(definition); byKey.Add(setting.Key, definition);
            }
            definitions.Sort(delegate(SettingDefinition a, SettingDefinition b) { return a.Field.MetadataToken.CompareTo(b.Field.MetadataToken); });
        }

        internal static SettingDefinition Get(string key) { return byKey[key]; }
        internal static bool InRange(string key, int value) { return Get(key).InRange(value); }
        internal static int Normalize(string key, int value) { return Get(key).Normalize(value); }
        internal static int Clamp(string key, int value) { return Get(key).Clamp(value); }
        internal static int DefaultNumber(string key) { return (int)Get(key).Setting.DefaultValue; }

        internal static void Initialize(Config config)
        { foreach (SettingDefinition definition in definitions) definition.Set(config, definition.Setting.DefaultValue); }

        internal static bool TryRead(Config config, string key, string text, bool strict)
        {
            SettingDefinition definition;
            if (!byKey.TryGetValue(key, out definition)) return false;
            definition.Read(config, text, strict); return true;
        }

        internal static void AppendTo(StringBuilder text, Config config)
        {
            foreach (SettingDefinition definition in definitions)
                text.Append(definition.Setting.Key).Append(" = ").AppendLine(definition.Write(config));
        }

        internal static void Copy(Config target, Config source)
        { Merge(target, source, (SettingsSection)(-1)); }

        internal static void Merge(Config target, Config draft, SettingsSection section)
        {
            foreach (SettingDefinition definition in definitions)
                if ((definition.Setting.Section & section) != 0) definition.Set(target, definition.Get(draft));
        }
    }
}

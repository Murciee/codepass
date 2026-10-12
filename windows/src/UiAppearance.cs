using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace Codepass
{
    static class UiAppearance
    {
        internal static string Prepare(string xaml, bool highContrast, bool animations)
        {
            if (!highContrast && animations) return xaml;
            XmlDocument document = new XmlDocument(); document.LoadXml(xaml);
            List<XmlElement> elements = new List<XmlElement>();
            foreach (XmlElement element in document.SelectNodes("//*")) elements.Add(element);
            foreach (XmlElement element in elements)
            {
                if (!animations)
                {
                    if (element.HasAttribute("KeyTime")) element.SetAttribute("KeyTime", "0");
                    if (element.LocalName.IndexOf("Animation", StringComparison.Ordinal) >= 0) element.SetAttribute("Duration", "0:0:0");
                }
                if (!highContrast) continue;
                if (element.LocalName == "Setter" && element.GetAttribute("Property") == "Template")
                { element.ParentNode.RemoveChild(element); continue; }
                foreach (string name in new string[] { "Foreground", "BorderBrush", "Background" })
                    if (element.HasAttribute(name) && element.GetAttribute(name) != "Transparent")
                        element.SetAttribute(name, name == "Background" ? SystemColors.WindowColor.ToString() : SystemColors.WindowTextColor.ToString());
                if (element.LocalName == "Setter")
                {
                    string property = element.GetAttribute("Property");
                    if (property == "Foreground" || property == "BorderBrush") element.SetAttribute("Value", SystemColors.WindowTextColor.ToString());
                    if (property == "Background") element.SetAttribute("Value", SystemColors.WindowColor.ToString());
                }
            }
            return document.OuterXml;
        }
    }
}

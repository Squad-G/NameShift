using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NameShift.Ui.Settings;

/// <summary>
/// The <c>settings.json</c> format, version 1: <c>{ "version": 1, "window": { "left", "top", "width", "height",
/// "maximized" }, "mainSplit", "lastFolder" }</c>, UTF-8 without BOM, indented, null values left out. The reader is
/// lenient: each value is read as text and parsed on its own, so one wrong value defaults alone and the others still
/// load (the in-box serializer would fail the whole file on one wrong type). Unknown members are ignored.
/// </summary>
internal static class AppSettingsJson
{
    /// <summary>The version this code writes, and the highest it reads.</summary>
    internal const int Version = 1;

    /// <summary>Content above this size is not parsed: the file is a few hundred bytes.</summary>
    internal const int MaxContentLength = 1024 * 1024;

    /// <summary>The file content of the settings. Numbers and booleans are invariant whatever the culture.</summary>
    /// <exception cref="ArgumentNullException">The settings are null.</exception>
    public static byte[] Serialize(AppSettings s)
    {
        if (s == null)
        {
            throw new ArgumentNullException(nameof(s));
        }

        var dto = new SettingsDto
        {
            Version = Version,
            Window = s.Window == null
                ? null
                : new WindowDto
                {
                    Left = s.Window.Left,
                    Top = s.Window.Top,
                    Width = s.Window.Width,
                    Height = s.Window.Height,
                    Maximized = s.Window.Maximized,
                },
            MainSplit = s.MainSplit,
            LastFolder = s.LastFolder,
        };

        using var stream = new MemoryStream();
        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(
            stream, new UTF8Encoding(false), ownsStream: false, indent: true, indentChars: "  "))
        {
            new DataContractJsonSerializer(typeof(SettingsDto)).WriteObject(writer, dto);
            writer.Flush();
        }

        return stream.ToArray();
    }

    /// <summary>Reads the file's content. Never throws: null, empty, oversized, corrupt or newer-version content gives
    /// <see cref="AppSettings.Empty"/>; a missing version reads as 1. Inside a good file each value defaults on its own
    /// when it is missing or invalid. A placement needs all four numbers (and a size above 0).</summary>
    public static AppSettings Read(byte[] content)
    {
        if (content == null || content.Length == 0 || content.Length > MaxContentLength)
        {
            return AppSettings.Empty;
        }

        try
        {
            XElement root;
            using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(content, XmlDictionaryReaderQuotas.Max))
            {
                root = XElement.Load(reader);
            }

            if (!IsType(root, "object"))
            {
                return AppSettings.Empty;
            }

            XElement? version = root.Element("version");
            if (version != null)
            {
                if (!TryInt(version, out int number) || number < 0 || number > Version)
                {
                    return AppSettings.Empty; // a newer file is left alone until the next save replaces it
                }
            }

            int? mainSplit = TryInt(root.Element("mainSplit"), out int split) && split >= 0 ? split : null;
            XElement? folder = root.Element("lastFolder");
            string? lastFolder = folder != null && IsType(folder, "string") && !string.IsNullOrWhiteSpace(folder.Value)
                ? folder.Value
                : null;
            return new AppSettings(ReadWindow(root.Element("window")), mainSplit, lastFolder);
        }
        catch (Exception)
        {
            // Any parse failure is a file this code cannot use; a start must never fail on a user-editable file.
            return AppSettings.Empty;
        }
    }

    private static SavedWindow? ReadWindow(XElement? window)
    {
        if (window == null || !IsType(window, "object"))
        {
            return null;
        }

        if (!TryInt(window.Element("left"), out int left)
            || !TryInt(window.Element("top"), out int top)
            || !TryInt(window.Element("width"), out int width)
            || !TryInt(window.Element("height"), out int height)
            || width <= 0
            || height <= 0)
        {
            return null;
        }

        XElement? flag = window.Element("maximized");
        bool maximized = flag != null && string.Equals(flag.Value, "true", StringComparison.Ordinal);
        return new SavedWindow(left, top, width, height, maximized);
    }

    /// <summary>A whole number written as a JSON number or string; anything else (a fraction, an object, null) is not.</summary>
    private static bool TryInt(XElement? element, out int value)
    {
        value = 0;
        return element != null
            && (IsType(element, "number") || IsType(element, "string"))
            && int.TryParse(element.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>The JSON type the reader gives each element; a member without one is a string.</summary>
    private static bool IsType(XElement element, string type) =>
        string.Equals((string?)element.Attribute("type") ?? "string", type, StringComparison.Ordinal);

    // No IsRequired anywhere; nulls are left out of the file.
    [DataContract]
    internal sealed class SettingsDto
    {
        [DataMember(Name = "version", Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "window", Order = 2, EmitDefaultValue = false)]
        public WindowDto? Window { get; set; }

        [DataMember(Name = "mainSplit", Order = 3, EmitDefaultValue = false)]
        public int? MainSplit { get; set; }

        [DataMember(Name = "lastFolder", Order = 4, EmitDefaultValue = false)]
        public string? LastFolder { get; set; }
    }

    [DataContract]
    internal sealed class WindowDto
    {
        [DataMember(Name = "left", Order = 1)]
        public int Left { get; set; }

        [DataMember(Name = "top", Order = 2)]
        public int Top { get; set; }

        [DataMember(Name = "width", Order = 3)]
        public int Width { get; set; }

        [DataMember(Name = "height", Order = 4)]
        public int Height { get; set; }

        [DataMember(Name = "maximized", Order = 5)]
        public bool Maximized { get; set; }
    }
}

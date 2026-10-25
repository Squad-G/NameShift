using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using NameShift.Core;
using NameShift.Disk;

namespace NameShift.Ui.Presets;

/// <summary>
/// The preset file format, version 1: <c>{ "version": 1, "name": "Photos", "panels": [ { "stage": "Case",
/// "enabled": true, "settings": { "Mode": "Lower" } } ] }</c>, UTF-8 without BOM, indented. The command line (NS-69)
/// reads the same format. Reading is lenient: unknown fields, unknown stages and missing fields are skipped, so an
/// older or newer file still loads what it can.
/// </summary>
public static class PresetJson
{
    /// <summary>The version this code writes, and the highest it reads.</summary>
    public const int Version = 1;

    /// <summary>Content above this size is not parsed: a preset is a few KB, and this keeps a listing bounded.</summary>
    internal const int MaxContentLength = 1024 * 1024;

    // One settings object for writing and reading: the settings map is a JSON object, not a key/value array.
    private static readonly DataContractJsonSerializerSettings SerializerSettings =
        new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };

    /// <summary>The file content of the preset: its panels in stage order, each stage by name.</summary>
    /// <exception cref="ArgumentNullException">The preset is null.</exception>
    public static byte[] Serialize(Preset preset)
    {
        if (preset == null)
        {
            throw new ArgumentNullException(nameof(preset));
        }

        var panels = new PanelDto[preset.Panels.Count];
        for (int i = 0; i < panels.Length; i++)
        {
            PresetPanel panel = preset.Panels[i];
            panels[i] = new PanelDto
            {
                Stage = panel.Stage.ToString(),
                Enabled = panel.Enabled,
                Settings = new Dictionary<string, string>(panel.Settings.Count, StringComparer.Ordinal),
            };
            foreach (KeyValuePair<string, string> pair in panel.Settings)
            {
                panels[i].Settings![pair.Key] = pair.Value;
            }
        }

        var dto = new PresetDto { Version = Version, Name = preset.Name, Panels = panels };
        using var stream = new MemoryStream();
        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(
            stream, new UTF8Encoding(false), ownsStream: false, indent: true, indentChars: "  "))
        {
            new DataContractJsonSerializer(typeof(PresetDto), SerializerSettings).WriteObject(writer, dto);
            writer.Flush();
        }

        return stream.ToArray();
    }

    /// <summary>Reads a preset file's content. Never throws: empty, corrupt or oversized content, or a missing name, is
    /// Unreadable; a version above <see cref="Version"/> is UnsupportedVersion. A missing version reads as version 1.
    /// A panel entry that is null, has a missing or unknown stage (a stage name only, never a number), or repeats a
    /// stage already read is skipped; missing settings read as none; a null setting value reads as "".</summary>
    public static PresetRead Read(byte[] content)
    {
        if (content == null || content.Length == 0 || content.Length > MaxContentLength)
        {
            return PresetRead.Unreadable();
        }

        PresetDto? dto;
        try
        {
            using XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(content, JsonReaderQuotas.Create());
            dto = new DataContractJsonSerializer(typeof(PresetDto), SerializerSettings).ReadObject(reader) as PresetDto;
        }
        catch (Exception)
        {
            // Any serializer failure is a file this code cannot use; the store must never throw on a user's file.
            return PresetRead.Unreadable();
        }

        if (dto == null || dto.Version < 0)
        {
            return PresetRead.Unreadable();
        }

        if (dto.Version > Version)
        {
            return PresetRead.UnsupportedVersion();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return PresetRead.Unreadable();
        }

        var panels = new List<PresetPanel>();
        var seen = new HashSet<RuleStage>();
        foreach (PanelDto? panel in dto.Panels ?? Array.Empty<PanelDto?>())
        {
            if (panel == null || !TryParseStage(panel.Stage, out RuleStage stage) || !seen.Add(stage))
            {
                continue;
            }

            var settings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (panel.Settings != null)
            {
                foreach (KeyValuePair<string, string> pair in panel.Settings)
                {
                    if (pair.Key != null)
                    {
                        settings[pair.Key] = pair.Value ?? "";
                    }
                }
            }

            panels.Add(new PresetPanel(stage, panel.Enabled, settings));
        }

        return PresetRead.Ok(new Preset(dto.Name!, panels));
    }

    /// <summary>A RuleStage by the name of a defined value (ignoring case); a number is refused.</summary>
    private static bool TryParseStage(string? text, out RuleStage stage)
    {
        stage = default;
        if (text == null)
        {
            return false;
        }

        foreach (string name in Enum.GetNames(typeof(RuleStage)))
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                stage = (RuleStage)Enum.Parse(typeof(RuleStage), name);
                return true;
            }
        }

        return false;
    }

    // No IsRequired anywhere: a missing field must not throw, Read checks what it needs.
    [DataContract]
    internal sealed class PresetDto
    {
        [DataMember(Name = "version", Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "name", Order = 2)]
        public string? Name { get; set; }

        [DataMember(Name = "panels", Order = 3)]
        public PanelDto?[]? Panels { get; set; }
    }

    [DataContract]
    internal sealed class PanelDto
    {
        [DataMember(Name = "stage", Order = 1)]
        public string? Stage { get; set; }

        [DataMember(Name = "enabled", Order = 2)]
        public bool Enabled { get; set; }

        [DataMember(Name = "settings", Order = 3)]
        public Dictionary<string, string>? Settings { get; set; }
    }
}

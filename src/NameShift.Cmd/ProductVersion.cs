using System;
using System.Reflection;

namespace NameShift.Cmd;

/// <summary>The version text of an assembly, as <c>--version</c> prints it.</summary>
internal static class ProductVersion
{
    /// <summary>The informational version without its <c>+build</c> suffix; without that attribute, the three-part
    /// assembly version.</summary>
    public static string Of(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var attribute = (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
            assembly, typeof(AssemblyInformationalVersionAttribute));
        if (attribute != null && attribute.InformationalVersion.Length > 0)
        {
            return Strip(attribute.InformationalVersion);
        }

        Version? version = assembly.GetName().Version;
        return version == null ? "0.0.0" : version.Major + "." + version.Minor + "." + Math.Max(version.Build, 0);
    }

    /// <summary>The text before the first <c>+</c> (the SDK appends <c>+commit</c>).</summary>
    public static string Strip(string version)
    {
        if (version == null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        int plus = version.IndexOf('+');
        return plus < 0 ? version : version.Substring(0, plus);
    }
}

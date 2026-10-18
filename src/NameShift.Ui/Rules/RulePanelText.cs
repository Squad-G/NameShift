using System.Globalization;
using System.Text;

namespace NameShift.Ui.Rules;

/// <summary>The texts a rule panel's frame shows.</summary>
public static class RulePanelText
{
    /// <summary>The checkbox text, e.g. "3. &amp;Replace"; the title's mnemonic toggles the panel.</summary>
    public static string Heading(Core.RuleStage stage, string title) =>
        string.Format(CultureInfo.CurrentCulture, Strings.RulePanelHeading, (int)stage, title);

    /// <summary>The title without its mnemonic: one '&amp;' removed, "&amp;&amp;" becomes '&amp;'.</summary>
    public static string PlainTitle(string title)
    {
        var plain = new StringBuilder(title.Length);
        for (int i = 0; i < title.Length; i++)
        {
            if (title[i] != '&')
            {
                plain.Append(title[i]);
            }
            else if (i + 1 < title.Length && title[i + 1] == '&')
            {
                plain.Append('&');
                i++;
            }
        }

        return plain.ToString();
    }

    /// <summary>The reset button's accessible name, e.g. "Reset Replace" (its visible text is just "Reset").</summary>
    public static string ResetName(string title) =>
        string.Format(CultureInfo.CurrentCulture, Strings.RulePanelResetName, PlainTitle(title));
}
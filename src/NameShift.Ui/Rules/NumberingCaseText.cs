namespace NameShift.Ui.Rules;

/// <summary>The list text of each letter case.</summary>
public static class NumberingCaseText
{
    public static string For(bool upperCase) => upperCase ? Strings.NumberingCaseUpper : Strings.NumberingCaseLower;
}

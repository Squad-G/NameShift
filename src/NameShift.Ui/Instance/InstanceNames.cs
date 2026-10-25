using System;
using System.Globalization;

namespace NameShift.Ui.Instance;

/// <summary>
/// The names of the single-instance mutex and pipe (NS-89). The mutex lives in the session's <c>Local</c> namespace
/// and the pipe name carries the session id, so each session has its own; both carry the user's SID, so another user's
/// process can neither find nor squat them by a guessable name.
/// </summary>
public static class InstanceNames
{
    private const string MutexPrefix = @"Local\NameShift.";

    /// <summary>The mutex name: <c>Local\NameShift.&lt;user SID&gt;</c>.</summary>
    /// <exception cref="ArgumentException">userSid is null or does not look like a SID (<c>S-1-</c> then numbers).</exception>
    public static string Mutex(string userSid)
    {
        CheckSid(userSid);
        return MutexPrefix + userSid;
    }

    /// <summary>The pipe name: <c>NameShift.&lt;user SID&gt;.&lt;session id&gt;</c>. A pipe name has no namespace: the
    /// session id in it makes it per session.</summary>
    /// <exception cref="ArgumentException">userSid does not look like a SID.</exception>
    /// <exception cref="ArgumentOutOfRangeException">sessionId is negative.</exception>
    public static string Pipe(string userSid, int sessionId)
    {
        CheckSid(userSid);
        if (sessionId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionId));
        }

        return "NameShift." + userSid + "." + sessionId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary><c>S-1-</c>, then one or more groups of digits separated by single dashes, at most 256 characters.</summary>
    private static void CheckSid(string userSid)
    {
        if (userSid == null)
        {
            throw new ArgumentException("The user SID is missing.", nameof(userSid));
        }

        const string Start = "S-1-";
        bool valid = userSid.Length > Start.Length && userSid.Length <= 256
            && userSid.StartsWith(Start, StringComparison.Ordinal);
        bool digitBefore = false;
        for (int i = Start.Length; valid && i < userSid.Length; i++)
        {
            char c = userSid[i];
            if (c >= '0' && c <= '9')
            {
                digitBefore = true;
            }
            else if (c == '-' && digitBefore)
            {
                digitBefore = false;
            }
            else
            {
                valid = false;
            }
        }

        if (!valid || !digitBefore)
        {
            throw new ArgumentException("Not a SID: " + userSid, nameof(userSid));
        }
    }
}

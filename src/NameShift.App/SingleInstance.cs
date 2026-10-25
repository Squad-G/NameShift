using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using NameShift.Ui.Instance;

namespace NameShift.App;

/// <summary>
/// One window at all times (NS-89). A per-user mutex in the session's namespace decides which process is the window.
/// That process serves a per-user, per-session pipe, created first-instance-only with an owner and access for the
/// current user alone; any other start checks that the pipe's owner (and its server process, when readable) is the
/// same user, hands its arguments and current directory over, and exits. Pipe input is untrusted: it goes through the
/// capped <see cref="InstanceRequestFormat"/> parser only, one client at a time, each within a read timeout. When the
/// window does not answer, the start opens its own window, which does not own the instance and never saves the
/// settings. The logic is in NameShift.Ui; this class is the thin Windows part, checked by hand.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly TimeSpan HandOverWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReplyBudget = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WindowAnswer = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReplyWrite = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(1);
    private const int RetryMilliseconds = 100;

    private readonly object _gate = new object();
    private readonly SecurityIdentifier? _user;
    private readonly string? _pipeName;
    private readonly Mutex? _mutex;
    private Control? _window;
    private CancellationTokenSource? _stop;
    private Task? _server;
    private NamedPipeServerStream? _pipe;
    private bool _disposed;

    private SingleInstance(SecurityIdentifier? user, string? pipeName, Mutex? ownedMutex, bool handedOver)
    {
        _user = user;
        _pipeName = pipeName;
        _mutex = ownedMutex;
        HandedOver = handedOver;
        Ownership = new InstanceOwnership(ownedMutex != null);
    }

    /// <summary>A request from another start, raised on the window's thread once it took it.</summary>
    public event EventHandler<InstanceRequest>? RequestReceived;

    /// <summary>True when the request went to the running window: this process exits with no window.</summary>
    public bool HandedOver { get; }

    /// <summary>Whether this window owns the instance (and saves the settings at close).</summary>
    public InstanceOwnership Ownership { get; }

    /// <summary>Takes the instance, or hands the request to the window that has it. Never throws: whatever fails, the
    /// result is a window (owner or not) or a hand-over that the running window confirmed.</summary>
    public static SingleInstance Start(string[] args, string currentDirectory)
    {
        SecurityIdentifier user;
        string mutexName;
        string pipeName;
        try
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                user = identity.User ?? throw new InvalidOperationException("No user SID.");
            }

            int session;
            using (Process process = Process.GetCurrentProcess())
            {
                session = process.SessionId;
            }

            mutexName = InstanceNames.Mutex(user.Value);
            pipeName = InstanceNames.Pipe(user.Value, session);
        }
        catch (Exception)
        {
            return new SingleInstance(null, null, null, handedOver: false);
        }

        Mutex? mutex = OpenMutex(mutexName, user, out bool owned);
        if (owned)
        {
            return new SingleInstance(user, pipeName, mutex, handedOver: false);
        }

        byte[]? frame = FrameOf(args, currentDirectory);
        var watch = Stopwatch.StartNew();
        while (frame != null && watch.Elapsed < HandOverWindow)
        {
            SendResult result = TrySendOnce(pipeName, user, frame);
            if (result == SendResult.Sent)
            {
                mutex?.Dispose();
                return new SingleInstance(user, pipeName, null, handedOver: true);
            }

            if (result == SendResult.Failed)
            {
                break; // a pipe that is not ours, or a window that does not answer: never retried, never "sent anyway"
            }

            // Not ready yet (the window is starting) or busy with another start: the owner may also have closed.
            if (mutex != null && TryTake(mutex))
            {
                return new SingleInstance(user, pipeName, mutex, handedOver: false);
            }

            Thread.Sleep(RetryMilliseconds);
        }

        if (mutex != null && TryTake(mutex))
        {
            return new SingleInstance(user, pipeName, mutex, handedOver: false);
        }

        mutex?.Dispose();
        return new SingleInstance(user, pipeName, null, handedOver: false);
    }

    /// <summary>Starts serving the pipe once the window is shown; only the owner serves. A request reaches
    /// <see cref="RequestReceived"/> on the window's thread.</summary>
    public void Serve(Control window)
    {
        lock (_gate)
        {
            if (_disposed || _server != null || !Ownership.OwnsInstance || _user == null || _pipeName == null)
            {
                return;
            }

            _window = window;
            _stop = new CancellationTokenSource();
            CancellationToken token = _stop.Token;
            _server = Task.Run(() => ServeAsync(token));
        }
    }

    /// <summary>Stops the pipe, then releases the mutex. On the thread that started the instance (the mutex's owner).</summary>
    public void Dispose()
    {
        NamedPipeServerStream? pipe;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pipe = _pipe;
            _pipe = null;
        }

        try
        {
            _stop?.Cancel();
            pipe?.Dispose(); // ends a pending wait or read
            _server?.Wait(StopWait);
        }
        catch (Exception)
        {
            // Closing must never fail.
        }

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception)
            {
                // Not owned by this thread any more: the handle closes below and Windows releases it.
            }

            _mutex.Dispose();
        }
    }

    private enum SendResult
    {
        Sent,
        NotReady,
        Failed,
    }

    /// <summary>The mutex, opened for the current user only. <paramref name="owned"/> is true when this process holds
    /// it (created, free, or abandoned by a crashed owner). Null when it cannot be opened (another user's object squats
    /// the name, or access is denied): the start then tries the hand-over and falls back to a window that is not the
    /// owner.</summary>
    private static Mutex? OpenMutex(string name, SecurityIdentifier user, out bool owned)
    {
        owned = false;
        Mutex? mutex = null;
        try
        {
            var security = new MutexSecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new MutexAccessRule(user, MutexRights.FullControl, AccessControlType.Allow));
            mutex = new Mutex(initiallyOwned: true, name, out bool created, security);
            owned = created || TryTake(mutex);
            return mutex;
        }
        catch (Exception)
        {
            mutex?.Dispose();
            owned = false;
            return null;
        }
    }

    /// <summary>Takes a free or abandoned mutex without waiting.</summary>
    private static bool TryTake(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true; // a crashed owner left it: taken over (AC 8)
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The framed request, or null when it is over the caps (the window would ignore it): this start then opens
    /// its own window.</summary>
    private static byte[]? FrameOf(string[] args, string currentDirectory)
    {
        try
        {
            return InstanceRequestFormat.Frame(new InstanceRequest(args, currentDirectory));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>One attempt: connect, check the server is this user's, allow it the foreground, send, and read its
    /// one-byte answer within <see cref="ReplyBudget"/>.</summary>
    private static SendResult TrySendOnce(string pipeName, SecurityIdentifier user, byte[] frame)
    {
        // Anonymous: the server learns nothing of this process's token and cannot impersonate it.
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
        try
        {
            pipe.Connect(0); // one try; WaitNamedPipe waits at most the pipe's short default while it is busy
        }
        catch (UnauthorizedAccessException)
        {
            return SendResult.Failed; // a pipe of that name that is not open to this user: not ours
        }
        catch (Exception)
        {
            return SendResult.NotReady;
        }

        try
        {
            // Before anything is sent: the pipe must be this user's (AC 7). Never "send anyway".
            if (!ServerIsSameUser(pipe, user, out uint serverProcessId))
            {
                return SendResult.Failed;
            }

            if (serverProcessId != 0)
            {
                // This process has the user's input: it lets the window take the foreground (AC 1).
                NativeMethods.AllowSetForegroundWindow(serverProcessId);
            }

            var budget = Stopwatch.StartNew();
            Task write = pipe.WriteAsync(frame, 0, frame.Length);
            if (!Finished(write, ReplyBudget - budget.Elapsed))
            {
                return SendResult.Failed;
            }

            var reply = new byte[1];
            Task<int> read = pipe.ReadAsync(reply, 0, 1);
            if (!Finished(read, ReplyBudget - budget.Elapsed) || read.Result != 1)
            {
                return SendResult.Failed;
            }

            return reply[0] == 1 ? SendResult.Sent : SendResult.Failed;
        }
        catch (Exception)
        {
            return SendResult.Failed;
        }
    }

    /// <summary>True when the task ran to completion within the time; a fault is rethrown. A task left behind ends when
    /// the pipe is disposed; its exception is observed.</summary>
    private static bool Finished(Task task, TimeSpan within)
    {
        if (within > TimeSpan.Zero && task.Wait(within))
        {
            return true;
        }

        task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        return false;
    }

    /// <summary>The pipe's owner is the user; and the server process's user too, when it can be read (an elevated
    /// window's token may not be readable from here; the owner check still stands).</summary>
    private static bool ServerIsSameUser(NamedPipeClientStream pipe, SecurityIdentifier user, out uint serverProcessId)
    {
        serverProcessId = 0;
        PipeSecurity security = pipe.GetAccessControl();
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !owner.Equals(user))
        {
            return false;
        }

        if (!NativeMethods.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId) || processId == 0)
        {
            return true;
        }

        serverProcessId = processId;
        SecurityIdentifier? processUser = ProcessUser(processId);
        return processUser == null || processUser.Equals(user);
    }

    /// <summary>The user of a process's token, or null when it cannot be read.</summary>
    private static SecurityIdentifier? ProcessUser(uint processId)
    {
        try
        {
            using SafeProcessHandle process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
            if (process.IsInvalid || !NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out SafeAccessTokenHandle token))
            {
                return null;
            }

            using (token)
            {
                NativeMethods.GetTokenInformation(token, NativeMethods.TokenUserClass, IntPtr.Zero, 0, out int needed);
                if (needed <= 0 || needed > 4096)
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal(needed);
                try
                {
                    if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenUserClass, buffer, needed, out _))
                    {
                        return null;
                    }

                    // TOKEN_USER starts with SID_AND_ATTRIBUTES, whose first field is the SID pointer.
                    return new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Serves one client at a time, each on its own pipe instance. The first instance is created first-instance-only:
    /// when that fails (another process holds the name), serving stops silently and the window stays a normal window
    /// that takes no request (AC 7). Each next instance is created while the current one is still open, so the name never
    /// leaves this process; then the current one is closed, which also drops a client that never sends or never closes
    /// without blocking the next one.
    /// </summary>
    private async Task ServeAsync(CancellationToken token)
    {
        NamedPipeServerStream? pipe = CreateServer(first: true);
        while (pipe != null)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    pipe.Dispose();
                    return;
                }

                _pipe = pipe;
            }

            try
            {
                await ServeOneAsync(pipe, token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A bad, slow or vanished client is dropped; the loop goes on.
            }

            NamedPipeServerStream? next = token.IsCancellationRequested ? null : CreateServer(first: false);
            lock (_gate)
            {
                if (_pipe == pipe)
                {
                    _pipe = null;
                }
            }

            pipe.Dispose(); // also ends a read or a drain still pending
            pipe = next;
        }
    }

    private async Task ServeOneAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        byte[]? data = await PipeRequestReader.ReadAsync(pipe, ReadTimeout, token).ConfigureAwait(false);
        bool taken = data != null && InstanceRequestFormat.TryParse(data, out InstanceRequest? request) && Deliver(request!);

        var reply = new[] { taken ? (byte)1 : (byte)0 };
        Task write = pipe.WriteAsync(reply, 0, 1, token);
        if (await Task.WhenAny(write, Task.Delay(ReplyWrite, token)).ConfigureAwait(false) != write)
        {
            Observe(write);
            return;
        }

        await write.ConfigureAwait(false);

        // The client reads the byte before the pipe closes; a client that never reads costs at most this wait.
        Task drain = Task.Run(() => pipe.WaitForPipeDrain());
        if (await Task.WhenAny(drain, Task.Delay(ReplyWrite, token)).ConfigureAwait(false) != drain)
        {
            Observe(drain);
        }
    }

    /// <summary>Hands the request to the window's thread and waits for it to take it. A window that does not take it
    /// within <see cref="WindowAnswer"/> (hung) is answered "ignored", and the request will not run later.</summary>
    private bool Deliver(InstanceRequest request)
    {
        Control? window = _window;
        if (window == null)
        {
            return false;
        }

        var handoff = new Handoff();
        try
        {
            window.BeginInvoke(new Action(() =>
            {
                if (!handoff.TryAccept())
                {
                    return; // too late: the start already opened its own window
                }

                try
                {
                    RequestReceived?.Invoke(this, request);
                }
                catch (Exception)
                {
                    // The window handles its own errors; nothing escapes to the message loop.
                }
            }));
        }
        catch (Exception)
        {
            return false; // the window is closing
        }

        return handoff.WaitAccepted(WindowAnswer);
    }

    /// <summary>A pipe instance for one client: byte mode, local clients only, owned by and open to the current user
    /// alone. The first one fails when the name exists; a next one is created while this process's previous instance is
    /// open, so it can only join this process's own pipe (another user has no right to add an instance to it). Null when
    /// it cannot be created.</summary>
    private NamedPipeServerStream? CreateServer(bool first)
    {
        try
        {
            var security = new PipeSecurity();
            security.SetOwner(_user!);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new PipeAccessRule(_user!, PipeAccessRights.FullControl, AccessControlType.Allow));
            byte[] descriptor = security.GetSecurityDescriptorBinaryForm();

            GCHandle pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
            try
            {
                var attributes = new NativeMethods.SecurityAttributes
                {
                    Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                    SecurityDescriptor = pinned.AddrOfPinnedObject(),
                    InheritHandle = 0,
                };
                SafePipeHandle handle = NativeMethods.CreateNamedPipe(
                    NativeMethods.LocalPipePrefix + _pipeName,
                    NativeMethods.PipeAccessDuplex | NativeMethods.FileFlagOverlapped | (first ? NativeMethods.FileFlagFirstPipeInstance : 0),
                    NativeMethods.PipeTypeByte | NativeMethods.PipeReadModeByte | NativeMethods.PipeWait | NativeMethods.PipeRejectRemoteClients,
                    NativeMethods.PipeUnlimitedInstances,
                    0,
                    0,
                    0,
                    ref attributes);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    return null;
                }

                try
                {
                    return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle);
                }
                catch (Exception)
                {
                    handle.Dispose();
                    return null;
                }
            }
            finally
            {
                pinned.Free();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Observe(Task task) =>
        task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>Exactly one of the window (accept) and the pipe thread (expire, after the wait) wins.</summary>
    private sealed class Handoff
    {
        private const int Pending = 0;
        private const int Accepted = 1;
        private const int Expired = 2;
        private readonly ManualResetEventSlim _accepted = new ManualResetEventSlim(false);
        private int _state;

        public bool TryAccept()
        {
            if (Interlocked.CompareExchange(ref _state, Accepted, Pending) != Pending)
            {
                return false;
            }

            _accepted.Set();
            return true;
        }

        public bool WaitAccepted(TimeSpan within)
        {
            _accepted.Wait(within);
            return Interlocked.CompareExchange(ref _state, Expired, Pending) == Accepted;
        }
    }
}

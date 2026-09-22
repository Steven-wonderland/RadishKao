using System.IO;
using System.Text;

namespace CatPet.Interop;

/// <summary>
/// Lets anything outside the process poke the cat: drop a file into the inbox
/// folder and the name of that file becomes a trigger name.
///
/// A watched folder rather than a named pipe, for two reasons. It needs no
/// dependencies (a pipe restricted to one user wants
/// System.IO.Pipes.AccessControl, and this project ships with none), and the
/// folder sits under %LOCALAPPDATA%, so NTFS already keeps other accounts out
/// instead of us hand-rolling an ACL. The cost is that a trigger can fire a
/// config action that launches something, so the guard matters: see the note on
/// <see cref="Directory"/>.
/// </summary>
public sealed class NotifyInbox : IDisposable
{
    /// <summary>Longest bubble text a sender can hand us, in characters.</summary>
    private const int MaxTextLength = 200;

    /// <summary>
    /// How many times to retry a read. The Created event can beat the writer's
    /// last flush, so the first open often fails with a sharing violation.
    /// </summary>
    private const int ReadAttempts = 6;

    private const int ReadRetryMs = 40;

    /// <summary>
    /// Re-reads for a file that opened fine but came back empty. The Created
    /// event fires when the file appears, which for a plain shell redirect is
    /// before anything has been written -- read once and you get "" and then
    /// delete the sender's text. An empty drop is also legitimate (it means
    /// "use the line from config"), so this only costs a no-text notification
    /// a few tens of milliseconds. Senders that rename a finished file into
    /// place, as catpet-notify.cmd does, never hit this.
    /// </summary>
    private const int EmptyRereads = 3;

    private const int EmptyRereadMs = 25;

    /// <summary>
    /// Files being written are named "~..." and renamed once complete, so a
    /// half-written drop is never read.
    /// </summary>
    private const char PartialPrefix = '~';

    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    /// <summary>
    /// Where senders drop their files. Under the user's own profile, so only
    /// this account (and anything already running as it) can trigger the cat.
    /// </summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CatPet",
        "inbox");

    /// <summary>
    /// Fired on a background thread with the trigger name and, if the file had
    /// any content, a line to say instead of the one from config.json.
    /// </summary>
    public event Action<string, string?>? Received;

    public NotifyInbox()
    {
        System.IO.Directory.CreateDirectory(Directory);

        _watcher = new FileSystemWatcher(Directory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
        };

        _watcher.Created += OnDropped;
        _watcher.EnableRaisingEvents = true;

        // Anything left from a run that ended before it could read its inbox is
        // stale by now, and replaying it would make the cat react to yesterday.
        Drain(deliver: false);
    }

    /// <summary>
    /// Reads and clears whatever is already sitting in the folder. Called once
    /// at start-up to discard leftovers, and on every drop because a burst of
    /// files can arrive faster than the watcher reports them.
    /// </summary>
    private void Drain(bool deliver)
    {
        string[] files;

        try
        {
            files = System.IO.Directory.GetFiles(Directory);
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        foreach (var file in files)
        {
            if (Path.GetFileName(file).StartsWith(PartialPrefix))
            {
                // Still being written; its rename will bring it back.
                continue;
            }

            var text = TryTake(file);

            if (deliver && text is not null)
            {
                var trigger = TriggerNameOf(file);

                if (trigger.Length > 0)
                {
                    Received?.Invoke(trigger, text.Length > 0 ? text : null);
                }
            }
        }
    }

    private void OnDropped(object sender, FileSystemEventArgs e) => Drain(deliver: true);

    /// <summary>
    /// The trigger is the file name up to the first dot, so a sender can append
    /// anything it likes to keep names unique: "claude.1731.tmp" is a drop for
    /// the "claude" trigger.
    /// </summary>
    private static string TriggerNameOf(string path)
    {
        var name = Path.GetFileName(path);
        var dot = name.IndexOf('.');
        return (dot < 0 ? name : name[..dot]).Trim();
    }

    /// <summary>
    /// Reads the file's text and deletes it. Returns null if it could not be
    /// read at all, so a file we never consumed is not reported as an empty
    /// message. Deleting is what stops the same drop firing twice.
    /// </summary>
    private static string? TryTake(string path)
    {
        for (var attempt = 0; attempt < ReadAttempts; attempt++)
        {
            try
            {
                var text = ReadAllowingForALateWriter(path);

                File.Delete(path);

                // One line only: a bubble cannot show a stack trace, and a hook
                // handing us a whole payload should not resize the window.
                text = text.ReplaceLineEndings(" ").Trim();

                return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
            }
            catch (FileNotFoundException)
            {
                // Someone else drained it first; nothing to report.
                return null;
            }
            catch (IOException)
            {
                // Still being written. Fall through to the retry.
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            Thread.Sleep(ReadRetryMs);
        }

        return null;
    }

    /// <summary>
    /// Reads the file, giving a writer that has created the file but not yet
    /// written to it a few more chances. Returns whatever it has at the end,
    /// which may legitimately be empty.
    /// </summary>
    private static string ReadAllowingForALateWriter(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            using (var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                var text = reader.ReadToEnd();

                if (text.Length > 0 || attempt >= EmptyRereads)
                {
                    return text;
                }
            }

            Thread.Sleep(EmptyRereadMs);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnDropped;
        _watcher.Dispose();
    }
}

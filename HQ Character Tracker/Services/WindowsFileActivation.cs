namespace HQ_Character_Tracker.Services;

internal static class WindowsFileActivation
{
    private static readonly object SyncRoot = new();
    private static readonly Queue<string> PendingPaths = new();
    private static Action<string>? _handler;

    public static void Register(Action<string> handler)
    {
        string[] pendingPaths;
        lock (SyncRoot)
        {
            _handler = handler;
            pendingPaths = PendingPaths.ToArray();
            PendingPaths.Clear();
        }

        foreach (var path in pendingPaths)
            handler(path);
    }

    public static void Queue(string path)
    {
        Action<string>? handler;
        lock (SyncRoot)
        {
            handler = _handler;
            if (handler is null)
            {
                PendingPaths.Enqueue(path);
                return;
            }
        }

        handler(path);
    }
}

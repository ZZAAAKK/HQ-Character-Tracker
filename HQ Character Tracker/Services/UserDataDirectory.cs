namespace HQ_Character_Tracker.Services;

internal static class UserDataDirectory
{
    public static string Path { get; } = GetPath();

    private static string GetPath()
    {
        var homePath = Environment.GetEnvironmentVariable("HOMEPATH");
        if (string.IsNullOrWhiteSpace(homePath))
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
        return string.IsNullOrWhiteSpace(homeDrive)
            ? System.IO.Path.GetFullPath(homePath)
            : System.IO.Path.GetFullPath($"{homeDrive}{homePath}");
    }
}

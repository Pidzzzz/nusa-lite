namespace NusaLite.Infrastructure;

public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NusaLite");
    private static readonly string FilePath = Path.Combine(Folder, "nusalite.log");

    public static string DataFolder => Folder;

    public static void Write(string message, Exception? error = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 512 * 1024)
                {
                    File.Delete(FilePath);
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
                if (error is not null)
                {
                    line += Environment.NewLine + error;
                }

                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the island.
        }
    }
}

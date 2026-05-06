using System;
using System.IO;

public static class Logger
{
    private static string logFile = "log.txt";

    public static void Log(string message)
    {
        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        
        Console.WriteLine(logEntry);

        try
        {
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
        catch
        {
            
        }
    }
}
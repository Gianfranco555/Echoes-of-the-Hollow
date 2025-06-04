using UnityEngine;

/// <summary>
/// Simple logging utility wrapping <see cref="Debug"/> to provide
/// Info level messages.
/// </summary>
public static class Log
{
    /// <summary>
    /// Logs an informational message to the Unity console.
    /// </summary>
    /// <param name="message">Message to log.</param>
    public static void Info(string message)
    {
        Debug.Log(message);
    }
}

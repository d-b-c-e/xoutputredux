using System.Text;
using System.Text.RegularExpressions;

namespace XOutputRedux.App;

/// <summary>
/// Service for generating and handling crash reports.
/// </summary>
public static class CrashReportService
{
    private const string GitHubRepoOwner = "d-b-c-e";
    private const string GitHubRepoName = "XOutputRedux";
    private const int MaxUrlLength = 8000; // Browser URL length limit

    /// <summary>
    /// Creates a crash report from an exception.
    /// </summary>
    public static CrashReport CreateReport(
        Exception exception,
        CrashContext context,
        bool isFatal,
        string? activeProfile = null,
        bool? vigemAvailable = null,
        bool? hidHideAvailable = null)
    {
        return new CrashReport
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = SanitizeStackTrace(exception.StackTrace),
            InnerException = exception.InnerException != null
                ? $"{exception.InnerException.GetType().Name}: {exception.InnerException.Message}"
                : null,
            AppVersion = UpdateService.GetCurrentVersion().ToString(),
            OsInfo = GetSanitizedOsInfo(),
            RuntimeVersion = Environment.Version.ToString(),
            ViGEmAvailable = vigemAvailable ?? false,
            HidHideAvailable = hidHideAvailable ?? false,
            ActiveProfile = activeProfile,
            Context = context,
            IsFatal = isFatal,
            LogFilePath = AppLogger.GetLogPath()
        };
    }

    /// <summary>
    /// Generates a GitHub issue URL with pre-filled content.
    /// </summary>
    public static string GenerateGitHubIssueUrl(CrashReport report)
    {
        var title = Uri.EscapeDataString(
            $"[Crash] {GetShortExceptionType(report.ExceptionType)}: {TruncateMessage(report.Message, 80)}");

        var body = GenerateIssueBody(report);
        var encodedBody = Uri.EscapeDataString(body);

        var labels = Uri.EscapeDataString("bug,crash-report");

        var url = $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}/issues/new" +
                  $"?title={title}&body={encodedBody}&labels={labels}";

        // Truncate if URL is too long (browsers have ~8000 char limit)
        if (url.Length > MaxUrlLength)
        {
            // Re-generate with truncated stack trace
            var truncatedBody = GenerateIssueBody(report, truncateStackTrace: true);
            encodedBody = Uri.EscapeDataString(truncatedBody);
            url = $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}/issues/new" +
                  $"?title={title}&body={encodedBody}&labels={labels}";
        }

        return url;
    }

    /// <summary>
    /// Formats a crash report for display or clipboard.
    /// </summary>
    public static string FormatReportAsText(CrashReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== XOutputRedux Crash Report ===");
        sb.AppendLine();
        sb.AppendLine($"Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Version: {report.AppVersion}");
        sb.AppendLine($"Context: {report.Context}");
        sb.AppendLine($"Fatal: {report.IsFatal}");
        sb.AppendLine();
        sb.AppendLine("--- Environment ---");
        sb.AppendLine($"OS: {report.OsInfo}");
        sb.AppendLine($"Runtime: .NET {report.RuntimeVersion}");
        sb.AppendLine($"ViGEm Available: {report.ViGEmAvailable}");
        sb.AppendLine($"HidHide Available: {report.HidHideAvailable}");
        if (report.ActiveProfile != null)
            sb.AppendLine($"Active Profile: {report.ActiveProfile}");
        sb.AppendLine();
        sb.AppendLine("--- Exception ---");
        sb.AppendLine($"Type: {report.ExceptionType}");
        sb.AppendLine($"Message: {report.Message}");
        if (report.InnerException != null)
            sb.AppendLine($"Inner: {report.InnerException}");
        sb.AppendLine();
        sb.AppendLine("--- Stack Trace ---");
        sb.AppendLine(report.StackTrace ?? "(No stack trace available)");
        sb.AppendLine();
        sb.AppendLine($"Log file: {report.LogFilePath}");

        return sb.ToString();
    }

    private static string GenerateIssueBody(CrashReport report, bool truncateStackTrace = false)
    {
        var stackTrace = report.StackTrace ?? "(No stack trace available)";
        if (truncateStackTrace && stackTrace.Length > 2000)
        {
            stackTrace = stackTrace[..2000] + "\n... (truncated - see full log)";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Crash Report");
        sb.AppendLine();
        sb.AppendLine($"**Version:** {report.AppVersion}");
        sb.AppendLine($"**Context:** {report.Context}");
        sb.AppendLine($"**OS:** {report.OsInfo}");
        sb.AppendLine($"**Runtime:** .NET {report.RuntimeVersion}");
        sb.AppendLine();
        sb.AppendLine("### Environment");
        sb.AppendLine($"- ViGEm Available: {report.ViGEmAvailable}");
        sb.AppendLine($"- HidHide Available: {report.HidHideAvailable}");
        if (report.ActiveProfile != null)
            sb.AppendLine($"- Active Profile: {report.ActiveProfile}");
        sb.AppendLine();
        sb.AppendLine("### Exception");
        sb.AppendLine($"**Type:** `{report.ExceptionType}`");
        sb.AppendLine($"**Message:** {report.Message}");
        if (report.InnerException != null)
            sb.AppendLine($"**Inner:** {report.InnerException}");
        sb.AppendLine();
        sb.AppendLine("### Stack Trace");
        sb.AppendLine("```");
        sb.AppendLine(stackTrace);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Steps to Reproduce");
        sb.AppendLine("<!-- Please describe what you were doing when the crash occurred -->");
        sb.AppendLine();
        sb.AppendLine("1. ");
        sb.AppendLine("2. ");
        sb.AppendLine("3. ");
        sb.AppendLine();
        sb.AppendLine("### Additional Context");
        sb.AppendLine("<!-- Add any other context about the problem here -->");

        return sb.ToString();
    }

    private static string SanitizeStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return string.Empty;

        // Replace user profile paths with placeholder
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            stackTrace = stackTrace.Replace(userProfile, "%USERPROFILE%",
                StringComparison.OrdinalIgnoreCase);
        }

        // Replace username in common paths
        var username = Environment.UserName;
        if (!string.IsNullOrEmpty(username))
        {
            // Only replace if appears as path component (surrounded by \ or /)
            stackTrace = Regex.Replace(stackTrace,
                $@"[\\/]{Regex.Escape(username)}[\\/]",
                @"\%USER%\",
                RegexOptions.IgnoreCase);
        }

        return stackTrace;
    }

    private static string GetSanitizedOsInfo()
    {
        try
        {
            return $"Windows {Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
        }
        catch
        {
            return "Windows (unknown version)";
        }
    }

    private static string GetShortExceptionType(string fullType)
    {
        // Extract just the class name from full type name
        var lastDot = fullType.LastIndexOf('.');
        return lastDot >= 0 ? fullType[(lastDot + 1)..] : fullType;
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message)) return "Unknown error";
        if (message.Length <= maxLength) return message;
        return message[..(maxLength - 3)] + "...";
    }
}

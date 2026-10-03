using System.IO.Compression;
using System.Text.Json;
using Controller550W.Core;
using Controller550W.Controller.Services;
using Xunit;

namespace Controller550W.Tests;

public class DiagnosticExportTests
{
    [Fact]
    public void ExportConfigurationIncludesTuningButExcludesEveryIdentityAndFreeText()
    {
        const string secret = "SENSITIVE_TEST_TOKEN";
        var settings = new ControllerSettings
        {
            Animation = new() { SystemLabel = secret, MonitorDevice = secret, MinimumBootMs = 4567, ColorProfile = "movie_red" },
            Audio = new() { VoiceName = secret, AudioFiles = new() { [secret] = secret }, VoiceFiles = new() { [secret] = secret }, Phrases = new() { [secret] = secret }, SoundVolume = 42 },
            Network = new() { DnsHost = secret, DomesticEndpoints = ["https://" + secret + ".invalid/path?token=" + secret], ExternalEndpoints = [], VpnAdapterId = secret },
            Profiles = [new() { Id = secret, DisplayName = secret, ProcessNames = [secret], ExecutablePath = secret, WindowTitleMatch = secret, LaunchTarget = secret, LaunchArguments = secret, WorkingDirectory = secret, SourceShortcut = secret, IconSource = secret, IconCachePath = secret, OptimizedShortcutPath = secret, StartupCorePath = secret, CoreCachePath = secret, BootTheme = secret, ShutdownTheme = secret, ColorProfile = secret, AppLaunchOffsetMs = 321 }]
        };
        var safe = JsonSerializer.Serialize(DiagnosticPrivacy.Configuration(settings));
        Assert.DoesNotContain(secret, safe);
        Assert.Contains("4567", safe);
        Assert.Contains("movie_red", safe);
        Assert.Contains("\"soundVolume\":42", safe);
        Assert.Contains("\"profileNumber\":1", safe);
        Assert.Contains("\"appLaunchOffsetMs\":321", safe);
        Assert.DoesNotContain("launchTarget", safe);
        Assert.DoesNotContain("displayName", safe);
    }

    [Theory]
    [InlineData("FIRST_VISUAL", "Private application, mode=optimized, latency=12.4ms, path=C:\\Users\\secret\\chat.txt", "latency=12.4")]
    [InlineData("ManagedLaunchFailed", "Private application, InvalidOperationException: secret token", "exception=InvalidOperationException")]
    [InlineData("WM_CLOSE", "Private application, hwnd=31415926, sent=True", "sent=true")]
    [InlineData("ManagedCloseState", "Private application, AppClosing, session=secret", "phase=AppClosing")]
    public void KnownEventsRetainUsefulMetricsWithoutNamesPathsOrFreeText(string eventName, string detail, string expected)
    {
        var safe = DiagnosticPrivacy.SanitizeLogLine($"{DateTimeOffset.UtcNow:O} [{eventName}] level=INFO, {detail}");
        Assert.NotNull(safe);
        Assert.Contains(expected, safe);
        Assert.DoesNotContain("Private", safe);
        Assert.DoesNotContain("secret", safe);
        Assert.DoesNotContain("31415926", safe);
        Assert.DoesNotContain("C:\\", safe);
    }

    [Fact]
    public void UnknownEventsAndMalformedLinesCannotSmuggleSensitiveText()
    {
        var safe = DiagnosticPrivacy.SanitizeLogLine($"{DateTimeOffset.UtcNow:O} [private-name-and-path] name=secret, latency=123ms");
        Assert.Contains("OtherEvent", safe);
        Assert.DoesNotContain("private", safe);
        Assert.DoesNotContain("secret", safe);
        Assert.DoesNotContain("123", safe!.Split(']').Last());
        Assert.Null(DiagnosticPrivacy.SanitizeLogLine("copied chat content"));
    }

    [Fact]
    public void ArchiveContainsOnlyAllowedFilesAndSanitizedCurrentRunLogs()
    {
        using var fixture = new ExportFixture();
        var log = new LogService(fixture.Directory);
        log.Write("FIRST_VISUAL", "Secret app, latency=34.5ms, token=SecretToken");
        log.Error("StartupFailed", new IOException("SecretToken C:\\Users\\PrivateName\\data.txt"), true);
        File.WriteAllText(Path.Combine(fixture.Directory, "settings.json"), "SecretToken");
        File.WriteAllText(Path.Combine(fixture.Directory, "Logs", "chat.txt"), "SecretToken");
        File.WriteAllText(Path.Combine(fixture.Directory, "Logs", "crash-unrelated.log"), "SecretToken");
        var destination = DiagnosticArchive.Create(Path.Combine(fixture.Directory, "bundle.zip"), fixture.Directory, new(), new { version = "3.0.0" });
        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(new[] { "README.txt", "app.log", "config-redacted.json", "crash.log", "recent-animation.log", "system-info.json" }, archive.Entries.Select(x => x.FullName).Order(StringComparer.Ordinal).ToArray());
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Assert.DoesNotContain("SecretToken", text);
            Assert.DoesNotContain("PrivateName", text);
            Assert.DoesNotContain("Secret app", text);
            Assert.DoesNotContain("C:\\Users", text);
        }
        using var appReader = new StreamReader(archive.GetEntry("app.log")!.Open());
        Assert.Contains("latency=34.5", appReader.ReadToEnd());
    }

    [Fact]
    public void ExportCancellationLeavesNoPartialArchive()
    {
        using var fixture = new ExportFixture();
        var path = Path.Combine(fixture.Directory, "cancel.zip");
        Assert.Throws<OperationCanceledException>(() => DiagnosticArchive.Create(path, fixture.Directory, new(), new { }, new CancellationToken(true)));
        Assert.False(File.Exists(path));
        Assert.Empty(System.IO.Directory.GetFiles(fixture.Directory, "*.tmp"));
    }

    [Fact]
    public void StorageFailureNeverBreaksLoggingOrExceptionHandling()
    {
        using var fixture = new ExportFixture();
        var blocked = Path.Combine(fixture.Directory, "file-instead-of-directory");
        File.WriteAllText(blocked, "fixture");
        var exception = Record.Exception(() => { var log = new LogService(blocked); log.Write("ControllerStart"); log.Warning("ConfigWarning"); log.Error("StartupFailed", new IOException("test"), true); });
        Assert.Null(exception);
    }

    sealed class ExportFixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "550W-diagnostics-test-" + Guid.NewGuid().ToString("N"));
        public ExportFixture() => System.IO.Directory.CreateDirectory(Directory);
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}

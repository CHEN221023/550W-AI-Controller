using System.Text;
using System.Text.Json;

namespace Controller550W.Core;

public sealed class ConfigStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public string? LoadWarning { get; private set; }
    public ControllerSettings Load()
    {
        Directory.CreateDirectory(DirectoryPath);
        if (!File.Exists(FilePath)) return new();
        try
        {
            return Parse(File.ReadAllText(FilePath));
        }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException)
        {
            LoadWarning = "配置文件无法读取，已恢复默认值并保留损坏文件。";
            File.Copy(FilePath, Path.Combine(DirectoryPath, $"settings.invalid-{DateTime.Now:yyyyMMdd-HHmmss}.json"), true);
            return new();
        }
    }
    public static ControllerSettings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("配置根节点必须是对象。");
        var config = JsonSerializer.Deserialize<ControllerSettings>(json, WireJson.Options) ?? new();
        // Files predating schema versioning still need the same migration as V1.
        if (!document.RootElement.EnumerateObject().Any(x => x.Name.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase))) config.SchemaVersion = 1;
        config.Animation ??= new(); config.Audio ??= new(); config.Profiles ??= [];
        if (config.Profiles.Any(x => x == null)) throw new JsonException("应用配置不能包含空项。");
        // Presence of the new field, not the schema number, makes partial imports and
        // repeated load/save cycles safe. V4.4 stores idle and regroup separately.
        MigrateParticleTiming(config.Animation, Property(document.RootElement, "animation"));
        if (config.SchemaVersion >= 3)
        {
            var profiles = Property(document.RootElement, "profiles");
            for (var i = 0; i < config.Profiles.Count; i++)
            {
                var profile = config.Profiles[i];
                profile.AnimationOverrides ??= new();
                var jsonProfile = profiles.ValueKind == JsonValueKind.Array ? profiles[i] : default;
                MigrateParticleTiming(profile.AnimationOverrides, Property(jsonProfile, "animationOverrides"));
            }
        }
        if (config.SchemaVersion < 2)
        {
            config.Animation.ColorProfile = "movie_red";
            if (config.Animation.MinimumBootMs == 3500) config.Animation.MinimumBootMs = 4000;
            if (config.Animation.CenterRevealMs == 700) config.Animation.CenterRevealMs = 900;
            if (config.Animation.ShutdownMs == 2200) config.Animation.ShutdownMs = 2600;
            foreach (var profile in config.Profiles)
            {
                if (profile.MinimumBootTimeMs == 3500) profile.MinimumBootTimeMs = 4000;
                if (string.IsNullOrEmpty(profile.LaunchTarget)) profile.LaunchTarget = profile.ExecutablePath;
            }
        }
        config.Validate();
        if (config.SchemaVersion < 3)
        {
            foreach (var profile in config.Profiles)
            {
                var customMinimum = profile.MinimumBootTimeMs != 4000;
                var customColor = profile.ColorProfile.Length > 0 && profile.ColorProfile != config.Animation.ColorProfile;
                profile.UseIndependentSettings = customMinimum || customColor;
                profile.AnimationOverrides = WireJson.Clone(config.Animation);
                profile.AudioOverrides = WireJson.Clone(config.Audio);
                if (profile.UseIndependentSettings)
                {
                    // V2 used the larger of the app and global minimum; retain that behavior once.
                    profile.AnimationOverrides.MinimumBootMs = Math.Max(profile.MinimumBootTimeMs, config.Animation.MinimumBootMs);
                    if (profile.ColorProfile.Length > 0) profile.AnimationOverrides.ColorProfile = profile.ColorProfile;
                }
            }
            config.SchemaVersion = 3;
        }
        config.Validate();
        config.SchemaVersion = Math.Max(config.SchemaVersion, 4);
        return config;
    }
    static JsonElement Property(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object
        ? element.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value : default;
    static void MigrateParticleTiming(AnimationSettings animation, JsonElement json)
    {
        if (Property(json, "particleToLogoMs").ValueKind != JsonValueKind.Undefined) return;
        animation.ParticleResidueMs = Math.Clamp(animation.ParticleResidueMs, 0, 10000);
        animation.ParticleToLogoMs = animation.ParticleResidueMs + Math.Clamp(animation.LogoRevealMs, 1, 10000);
        animation.LogoHoldMs = Math.Clamp(animation.LogoHoldMs, 0, 10000);
    }
    public void Save(ControllerSettings config)
    {
        if (config.ParticleTimingError() is { } error) throw new ArgumentException(error);
        config.Validate(); Directory.CreateDirectory(DirectoryPath);
        var options = new JsonSerializerOptions(WireJson.Options) { WriteIndented = true };
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, options), new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
        File.Move(temp, FilePath, true);
    }
}

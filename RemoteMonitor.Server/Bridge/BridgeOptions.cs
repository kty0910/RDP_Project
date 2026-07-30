#if BRIDGE_TOKEN_REQUIRED
using System.Security.Cryptography;
#endif
using System.Text.Json;
using RemoteMonitor.Server.Logging;

namespace RemoteMonitor.Server.Bridge;

public sealed class BridgeOptions
{
    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "bridge_settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public bool Enabled { get; init; }

    public int RdpPortStart { get; init; } = 13389;

    public int RdpPortEnd { get; init; } = 13489;

#if BRIDGE_TOKEN_REQUIRED
    public string Token { get; init; } = string.Empty;
#endif

    public IReadOnlyList<BridgeTarget> AllowedTargets { get; init; } = Array.Empty<BridgeTarget>();

    public static BridgeOptions Load(FileLogger logger)
    {
        if (!File.Exists(SettingsPath))
        {
            var defaultOptions = new BridgeOptions
            {
                Enabled = false,
#if BRIDGE_TOKEN_REQUIRED
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
#endif
                AllowedTargets = Array.Empty<BridgeTarget>()
            };

            Save(defaultOptions);
            logger.Info($"Bridge settings created: {SettingsPath}");
            return defaultOptions;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var options = JsonSerializer.Deserialize<BridgeOptions>(json, JsonOptions) ?? new BridgeOptions();
            return Normalize(options);
        }
        catch (Exception exception)
        {
            logger.Error("Failed to load bridge_settings.json. Bridge will stay disabled.", exception);
            return new BridgeOptions();
        }
    }

    public BridgeOptions WithEnabled(bool enabled)
    {
        return new BridgeOptions
        {
            Enabled = enabled,
            RdpPortStart = RdpPortStart,
            RdpPortEnd = RdpPortEnd,
#if BRIDGE_TOKEN_REQUIRED
            Token = Token,
#endif
            AllowedTargets = AllowedTargets
        };
    }

    public BridgeOptions WithTargets(IReadOnlyList<BridgeTarget> targets)
    {
        return new BridgeOptions
        {
            Enabled = Enabled,
            RdpPortStart = RdpPortStart,
            RdpPortEnd = RdpPortEnd,
#if BRIDGE_TOKEN_REQUIRED
            Token = Token,
#endif
            AllowedTargets = targets
        };
    }

    public static void Save(BridgeOptions options)
    {
        Write(SettingsPath, options);
    }

    public bool TryGetTarget(string name, out BridgeTarget target)
    {
        target = AllowedTargets.FirstOrDefault(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.Host)) ?? new BridgeTarget();

        return !string.IsNullOrWhiteSpace(target.Name);
    }

    public bool TryGetTarget(string host, int apiPort, int rdpPort, out BridgeTarget target)
    {
        target = AllowedTargets.FirstOrDefault(item =>
            item.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            && item.ApiPort == apiPort
            && item.RdpPort == rdpPort) ?? new BridgeTarget();

        return !string.IsNullOrWhiteSpace(target.Host);
    }

    private static BridgeOptions Normalize(BridgeOptions options)
    {
        return new BridgeOptions
        {
            Enabled = options.Enabled,
            RdpPortStart = options.RdpPortStart <= 0 ? 13389 : options.RdpPortStart,
            RdpPortEnd = options.RdpPortEnd <= 0 ? 13489 : options.RdpPortEnd,
#if BRIDGE_TOKEN_REQUIRED
            Token = options.Token,
#endif
            AllowedTargets = options.AllowedTargets
                .Where(target => !string.IsNullOrWhiteSpace(target.Host))
                .Select(target => new BridgeTarget
                {
                    Name = string.IsNullOrWhiteSpace(target.Name)
                        ? BridgeTarget.CreateName(
                            target.Host.Trim(),
                            target.ApiPort <= 0 ? 5000 : target.ApiPort,
                            target.RdpPort <= 0 ? 3389 : target.RdpPort)
                        : target.Name.Trim(),
                    Host = target.Host.Trim(),
                    DescriptionSummary = target.DescriptionSummary?.Trim() ?? string.Empty,
                    DescriptionDetails = target.DescriptionDetails?.Trim() ?? string.Empty,
                    DescriptionDetailsRtf = target.DescriptionDetailsRtf ?? string.Empty,
                    ApiPort = target.ApiPort <= 0 ? 5000 : target.ApiPort,
                    RdpPort = target.RdpPort <= 0 ? 3389 : target.RdpPort
                })
                .ToArray()
        };
    }

    private static void Write(string path, BridgeOptions options)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(options, JsonOptions));
    }
}

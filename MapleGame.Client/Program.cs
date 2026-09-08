using System;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;

namespace MapleGame.Client;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ClientPresentation.Initialize();
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            ClientPresentation.ShowHelp("MapleGame.Client (--img <version-directory> | --wz <install-directory> | --hybrid-img <version-directory> --wz <install-directory>) [--map <map-id>] [--wz-version <GMS|EMS|BMS|CLASSIC|CUSTOM>] [--wz-iv <8-hex-digits>] [--portal <name>] [--profile-directory <path>]");
            return 0;
        }

        try
        {
            string imgDirectory = null;
            string wzDirectory = null;
            string hybridImgDirectory = null;
            string portal = null;
            string profileDirectory = null;
            int? mapId = 100000000;
            WzMapleVersion wzVersion = WzMapleVersion.BMS;
            byte[] customIv = null;
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for {args[i]}.");
                switch (args[i])
                {
                    case "--img": imgDirectory = args[i + 1]; break;
                    case "--wz": wzDirectory = args[i + 1]; break;
                    case "--hybrid-img": hybridImgDirectory = args[i + 1]; break;
                    case "--map": mapId = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                    case "--wz-version":
                        if (!Enum.TryParse(args[i + 1], ignoreCase: true, out wzVersion))
                            throw new ArgumentException($"Unknown WZ version '{args[i + 1]}'.");
                        break;
                    case "--wz-iv": customIv = ParseWzIv(args[i + 1]); break;
                    case "--portal": portal = args[i + 1]; break;
                    case "--profile-directory": profileDirectory = args[i + 1]; break;
                    default: throw new ArgumentException($"Unknown option {args[i]}.");
                }
            }
            var clientOptions = new MapleGameClientOptions
            {
                ImgDirectory = imgDirectory,
                WzDirectory = wzDirectory,
                HybridImgDirectory = hybridImgDirectory,
                MapId = mapId,
                Portal = portal,
                ProfileDirectory = profileDirectory,
                WzVersion = wzVersion,
                CustomIv = customIv
            };
            if (string.IsNullOrWhiteSpace(imgDirectory)
                 && string.IsNullOrWhiteSpace(wzDirectory)
                 && string.IsNullOrWhiteSpace(hybridImgDirectory))
            {
                System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                if (!ClientLaunchDialog.TryChoose(clientOptions, out clientOptions))
                    return 0;
            }
            clientOptions.Validate();

            using var assets = clientOptions.OpenAssetSource();
            var diagnostics = new ConsoleDiagnostics();
            var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets, diagnostics), diagnostics);
            using var provider = new RuntimeMapProvider(services);
            using var map = provider.Load(clientOptions.MapId.Value);
            ISimulatorProfileStorage profile = clientOptions.ProfileDirectory == null
                ? SimulatorProfileStorage.CreateStandaloneClient()
                : new DirectoryProfileStorage(clientOptions.ProfileDirectory);
            var options = new GameSessionOptions(profile);
            if (!GameSessionHost.TryStart(() =>
            {
                var game = new HaCreator.MapSimulator.MapSimulator(map, $"MapleGame — {map.MapId:D9}", options, services, clientOptions.Portal);
                game.SetMapProvider(provider);
                return game;
            }, out var handle))
                throw new InvalidOperationException("A game session is already running.");

            GameSessionResult result = handle.Completion.GetAwaiter().GetResult();
            if (result.Error != null) ExceptionDispatchInfo.Capture(result.Error).Throw();
            return 0;
        }
        catch (Exception error)
        {
            ClientPresentation.ReportFailure(error);
            return 1;
        }
    }

    private sealed class ConsoleDiagnostics : IRuntimeDiagnostics
    {
        public void Trace(string message) => Console.WriteLine(message);
        public void Report(string message, Exception error = null) => Console.Error.WriteLine(error == null ? message : $"{message}: {error}");
    }

    private static byte[] ParseWzIv(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("--wz-iv requires four hexadecimal bytes.");

        string hex = value
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        if (hex.Length != 8)
            throw new ArgumentException("--wz-iv requires exactly eight hexadecimal digits.");

        byte[] result = new byte[4];
        for (int i = 0; i < result.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out result[i]))
            {
                throw new ArgumentException($"Invalid WZ IV '{value}'.");
            }
        }
        return result;
    }

    private sealed class DirectoryProfileStorage : ISimulatorProfileStorage
    {
        private readonly string root;
        public DirectoryProfileStorage(string directory)
        {
            root = Path.GetFullPath(directory);
            Directory.CreateDirectory(root);
        }

        public string CharactersDirectory
        {
            get
            {
                string directory = Path.Combine(root, "Characters");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public string GetFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName || fileName is "." or "..")
                throw new ArgumentException("Expected a profile file name.", nameof(fileName));
            return Path.Combine(root, fileName);
        }
    }
}

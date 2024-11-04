using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.Util
{
    public class ApplicationStateDetector
    {
        public class ApplicationState
        {
            public bool IsPublished { get; set; }
            public bool IsSingleFile { get; set; }
            public bool IsFrameworkDependent { get; set; }
            public string ExecutionPath { get; set; }
            public string ApplicationBase { get; set; }
            public bool IsDevelopmentEnvironment { get; set; }
        }

        public static ApplicationState DetectApplicationState()
        {
            var state = new ApplicationState();

            // Get the entry assembly and location
            var entryAssembly = Assembly.GetEntryAssembly();
            var entryAssemblyLocation = entryAssembly?.Location ?? "";
            var processPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

            state.ExecutionPath = processPath;
            state.ApplicationBase = AppContext.BaseDirectory;

            // Check if running as single-file
            state.IsSingleFile = string.IsNullOrEmpty(entryAssemblyLocation) ||
                                !File.Exists(entryAssemblyLocation);

            // Check for development environment indicators
            var isDevelopment = IsDevelopmentEnvironment();
            state.IsDevelopmentEnvironment = isDevelopment;

            // Check if framework-dependent
            state.IsFrameworkDependent = IsFrameworkDependent();

            // Determine if published based on multiple factors
            state.IsPublished = DetermineIfPublished(state);

            return state;
        }

        private static bool IsDevelopmentEnvironment()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null) return false;

            var location = assembly.Location;

            // Check for typical development paths
            var developmentIndicators = new[]
            {
            Path.Combine("bin", "Debug"),
            Path.Combine("bin", "Release"),
            "obj",
            ".vs",
            "TestResults"
        };

            return developmentIndicators.Any(indicator =>
                location.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsFrameworkDependent()
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            var processDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);

            if (string.IsNullOrEmpty(processDirectory)) return true;

            // Check for presence of runtime configuration files
            var runtimeConfigPath = Path.Combine(processDirectory,
                $"{Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)}.runtimeconfig.json");

            var depsJsonPath = Path.Combine(processDirectory,
                $"{Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)}.deps.json");

            return File.Exists(runtimeConfigPath) || File.Exists(depsJsonPath);
        }

        private static bool DetermineIfPublished(ApplicationState state)
        {
            // If it's a single-file application, it's definitely published
            if (state.IsSingleFile) return true;

            // Check for development environment
            if (state.IsDevelopmentEnvironment) return false;

            // Check directory structure
            var executablePath = state.ExecutionPath;
            var directory = Path.GetDirectoryName(executablePath);

            if (string.IsNullOrEmpty(directory)) return false;

            // Published applications typically don't have these folders in their path
            var developmentPaths = new[]
            {
            Path.Combine("bin", "Debug"),
            Path.Combine("bin", "Release"),
            "obj"
        };

            bool hasDevPath = developmentPaths.Any(path =>
                directory.Contains(path, StringComparison.OrdinalIgnoreCase));

            if (hasDevPath) return false;

            // Check for publish-specific files
            var publishSpecificFiles = new[]
            {
            ".dll",
            ".pdb",
            ".deps.json",
            ".runtimeconfig.json"
        };

            int publishFileCount = Directory.GetFiles(directory)
                .Count(f => publishSpecificFiles.Any(ext =>
                    f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

            // If we find multiple publish-specific files, it's likely a published application
            return publishFileCount >= 2;
        }

        public static void LogApplicationState()
        {
            var state = DetectApplicationState();
            Console.WriteLine("Application State:");
            Console.WriteLine($"Is Published: {state.IsPublished}");
            Console.WriteLine($"Is Single File: {state.IsSingleFile}");
            Console.WriteLine($"Is Framework Dependent: {state.IsFrameworkDependent}");
            Console.WriteLine($"Is Development Environment: {state.IsDevelopmentEnvironment}");
            Console.WriteLine($"Execution Path: {state.ExecutionPath}");
            Console.WriteLine($"Application Base: {state.ApplicationBase}");
        }
    }

    // Example usage:
    public class Program
    {
        public static void Main()
        {
            var state = ApplicationStateDetector.DetectApplicationState();

            if (state.IsPublished)
            {
                // Configure for published environment
                ConfigureForProduction(state);
            }
            else
            {
                // Configure for development environment
                ConfigureForDevelopment(state);
            }

            ApplicationStateDetector.LogApplicationState();
        }

        private static void ConfigureForProduction(ApplicationStateDetector.ApplicationState state)
        {
            // Example configuration for published environment
            if (state.IsSingleFile)
            {
                // Special handling for single-file deployment
                ConfigureSingleFileMode();
            }

            // Set up production-specific paths
            var contentPath = state.IsSingleFile
                ? Path.Combine(Path.GetDirectoryName(state.ExecutionPath) ?? "", "content")
                : Path.Combine(state.ApplicationBase, "content");

            // Example of how to handle different configurations
            if (state.IsFrameworkDependent)
            {
                // Framework-dependent specific setup
                SetupFrameworkDependentMode();
            }
        }

        private static void ConfigureForDevelopment(ApplicationStateDetector.ApplicationState state)
        {
            // Example configuration for development environment
            // Enable additional logging, debugging features, etc.
        }

        private static void ConfigureSingleFileMode()
        {
            // Configure application for single-file deployment
            AppContext.SetSwitch("Switch.System.Runtime.Loader.UseRidGraph", true);
        }

        private static void SetupFrameworkDependentMode()
        {
            // Setup for framework-dependent deployment
        }
    }
}
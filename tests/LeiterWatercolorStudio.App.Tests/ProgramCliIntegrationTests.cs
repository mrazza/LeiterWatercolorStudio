using System.Diagnostics;
using System.Text.Json;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Cli;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class ProgramCliIntegrationTests
{
    [Fact]
    public async Task ProcessInvocation_StagesAndExportHeadlessWithoutDisplay_Succeeds()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"leiter-app-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var source = new SequentialMatrix<Rgb8>(40, 30);
            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    source[x, y] = new Rgb8((byte)(50 + x * 4), (byte)(80 + y * 4), 170);
                }
            }

            string inputPath = Path.Combine(tempDir, "input.png");
            string outputPath = Path.Combine(tempDir, "out.png");
            source.SaveImage(inputPath);

            (int versionExit, string versionOut, string versionErr) = await RunAppProcessAsync(["--version"]);
            Assert.Equal(CliExitCodes.Success, versionExit);
            Assert.Equal(string.Empty, versionErr);
            Assert.StartsWith("LeiterWatercolorStudio ", versionOut, StringComparison.Ordinal);

            (int stagesExit, string stagesOut, string stagesErr) = await RunAppProcessAsync(["stages", "--json"]);
            Assert.Equal(CliExitCodes.Success, stagesExit);
            Assert.Equal(string.Empty, stagesErr);
            using (JsonDocument stagesJson = JsonDocument.Parse(stagesOut))
            {
                Assert.Equal(24, stagesJson.RootElement.GetProperty("stages").GetArrayLength());
            }

            (int exportExit, string exportOut, string exportErr) = await RunAppProcessAsync(
                ["export", inputPath, "-o", outputPath, "-b", "for-size", "-r", "20", "--json"]);
            Assert.Equal(CliExitCodes.Success, exportExit);
            Assert.Equal(string.Empty, exportErr);

            using (JsonDocument exportJson = JsonDocument.Parse(exportOut))
            {
                Assert.Equal(20, exportJson.RootElement.GetProperty("renderWidth").GetInt32());
                Assert.Equal(15, exportJson.RootElement.GetProperty("renderHeight").GetInt32());
            }

            SequentialMatrix<Rgb8> exported = ImageExtensions.LoadRgb8(outputPath);
            Assert.Equal(new Size(20, 15), exported.Size);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAppProcessAsync(string[] args)
    {
        string appDll = Path.Combine(AppContext.BaseDirectory, "LeiterWatercolorStudio.dll");
        Assert.True(File.Exists(appDll), $"Expected built app assembly at '{appDll}'.");

        string dotnetExecutable = ResolveDotnetExecutable();
        var startInfo = new ProcessStartInfo(dotnetExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(appDll);
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Explicitly unset DISPLAY / WAYLAND_DISPLAY to verify zero GUI/X11 dependency.
        startInfo.Environment["DISPLAY"] = string.Empty;
        startInfo.Environment["WAYLAND_DISPLAY"] = string.Empty;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start headless CLI process.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        return (process.ExitCode, stdout, stderr);
    }

    private static string ResolveDotnetExecutable()
    {
        string exeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) &&
            string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(processPath))
        {
            return processPath;
        }

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            string rootCandidate = Path.Combine(dotnetRoot, exeName);
            if (File.Exists(rootCandidate))
            {
                return rootCandidate;
            }
        }

        string? coreLibDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(coreLibDir))
        {
            string candidate = Path.GetFullPath(Path.Combine(coreLibDir, "..", "..", "..", exeName));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return exeName;
    }
}

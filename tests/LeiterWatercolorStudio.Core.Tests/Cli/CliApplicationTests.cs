using System.Text.Json;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Cli;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Cli;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_HelpVersionAndStages_ReturnZero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        Assert.Equal(CliExitCodes.Success, await CliApplication.RunAsync(["--version"], stdout, stderr, "1.2.3"));
        Assert.Contains("1.2.3", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());

        stdout = new StringWriter();
        Assert.Equal(CliExitCodes.Success, await CliApplication.RunAsync(["--help"], stdout, stderr));
        Assert.Contains("LeiterWatercolorStudio export", stdout.ToString());

        stdout = new StringWriter();
        Assert.Equal(CliExitCodes.Success, await CliApplication.RunAsync(["stages", "--json"], stdout, stderr));
        using JsonDocument doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(24, doc.RootElement.GetProperty("stages").GetArrayLength());
        Assert.Equal(8, doc.RootElement.GetProperty("groups").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_UsageErrorAndRuntimeError_ReturnExpectedExitCodes()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int usageExit = await CliApplication.RunAsync(["export"], stdout, stderr);
        Assert.Equal(CliExitCodes.UsageError, usageExit);
        Assert.Contains("Error:", stderr.ToString());

        stdout = new StringWriter();
        stderr = new StringWriter();
        int runtimeExit = await CliApplication.RunAsync(["export", "/nonexistent/input/image.png"], stdout, stderr);
        Assert.Equal(CliExitCodes.RuntimeError, runtimeExit);
        Assert.Contains("Error:", stderr.ToString());
    }

    [Fact]
    public async Task RunAsync_PreCancelledToken_ReturnsExit130()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"leiter-cli-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string inputPath = CreateInputPng(tempDir, "in.png", 32, 32);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exitCode = await CliApplication.RunAsync(["export", inputPath, "-q"], stdout, stderr, ct: cts.Token);

            Assert.Equal(CliExitCodes.Cancelled, exitCode);
            Assert.Contains("Cancelled", stderr.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ExportQuietAndJsonModes_FormatStdoutCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"leiter-cli-app-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string inputPath = CreateInputPng(tempDir, "in.png", 36, 36);
            string quietOut = Path.Combine(tempDir, "quiet.png");
            string jsonOut = Path.Combine(tempDir, "json.png");

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int quietExit = await CliApplication.RunAsync(
                ["export", inputPath, "-o", quietOut, "-b", "for-size", "--quiet"],
                stdout,
                stderr);

            Assert.Equal(CliExitCodes.Success, quietExit);
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.True(File.Exists(quietOut));

            stdout = new StringWriter();
            stderr = new StringWriter();
            int jsonExit = await CliApplication.RunAsync(
                ["export", inputPath, "-o", jsonOut, "-b", "for-size", "--json"],
                stdout,
                stderr);

            Assert.Equal(CliExitCodes.Success, jsonExit);
            Assert.Equal(string.Empty, stderr.ToString());
            using JsonDocument json = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("final", json.RootElement.GetProperty("mode").GetString());
            Assert.Equal(36, json.RootElement.GetProperty("renderWidth").GetInt32());
            Assert.Equal(jsonOut, json.RootElement.GetProperty("outputFiles")[0].GetString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateInputPng(string dir, string name, int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        matrix.AsSpan().Fill(new Rgb8(120, 150, 180));
        string path = Path.Combine(dir, name);
        matrix.SaveImage(path);
        return path;
    }
}

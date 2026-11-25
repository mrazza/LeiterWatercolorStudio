using LeiterWatercolorStudio.Core.Cli;
using LeiterWatercolorStudio.Core.Models;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Cli;

public class CliArgumentParserTests
{
    [Fact]
    public void ShouldLaunchGui_ReturnsTrueOnlyForEmptyOrFinderPsnArgs()
    {
        Assert.True(CliArgumentParser.ShouldLaunchGui([]));
        Assert.True(CliArgumentParser.ShouldLaunchGui(["-psn_0_12345"]));
        Assert.False(CliArgumentParser.ShouldLaunchGui(["--help"]));
        Assert.False(CliArgumentParser.ShouldLaunchGui(["export", "photo.png"]));
        Assert.False(CliArgumentParser.ShouldLaunchGui(["-psn_0_12345", "export"]));
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("help")]
    public void Parse_HelpCommands_ReturnRootHelp(string arg)
    {
        CliParseResult result = CliArgumentParser.Parse([arg]);
        Assert.False(result.IsError);
        Assert.Equal(CliCommandKind.Help, result.Command);
        Assert.Null(result.HelpTopic);
    }

    [Theory]
    [InlineData("export")]
    [InlineData("stages")]
    public void Parse_SubcommandHelp_ReturnsTopic(string topic)
    {
        CliParseResult byHelpCmd = CliArgumentParser.Parse(["help", topic]);
        Assert.False(byHelpCmd.IsError);
        Assert.Equal(CliCommandKind.Help, byHelpCmd.Command);
        Assert.Equal(topic, byHelpCmd.HelpTopic);

        CliParseResult byFlag = CliArgumentParser.Parse([topic, "--help"]);
        Assert.False(byFlag.IsError);
        Assert.Equal(CliCommandKind.Help, byFlag.Command);
        Assert.Equal(topic, byFlag.HelpTopic);
    }

    [Theory]
    [InlineData("-V")]
    [InlineData("--version")]
    [InlineData("version")]
    public void Parse_VersionCommands_ReturnVersion(string arg)
    {
        CliParseResult result = CliArgumentParser.Parse([arg]);
        Assert.False(result.IsError);
        Assert.Equal(CliCommandKind.Version, result.Command);
    }

    [Fact]
    public void Parse_StagesCommand_SupportsJsonFlag()
    {
        CliParseResult plain = CliArgumentParser.Parse(["stages"]);
        Assert.False(plain.IsError);
        Assert.Equal(CliCommandKind.ListStages, plain.Command);
        Assert.False(plain.JsonOutput);

        CliParseResult json = CliArgumentParser.Parse(["stages", "--json"]);
        Assert.False(json.IsError);
        Assert.Equal(CliCommandKind.ListStages, json.Command);
        Assert.True(json.JsonOutput);
    }

    [Fact]
    public void Parse_ExportMinimalPositionalInput_UsesExpectedDefaults()
    {
        CliParseResult result = CliArgumentParser.Parse(["export", "photo.jpg"]);
        Assert.False(result.IsError);
        Assert.Equal(CliCommandKind.Export, result.Command);
        Assert.NotNull(result.ExportOptions);
        CliExportOptions opts = result.ExportOptions!;
        Assert.Equal("photo.jpg", opts.InputPath);
        Assert.Null(opts.OutputPath);
        Assert.Null(opts.PresetPath);
        Assert.Equal(CliPresetScalingMode.Auto, opts.PresetScaling);
        Assert.Equal(BaselineMode.ForImage, opts.Baseline);
        Assert.Equal(CliExportMode.Final, opts.Mode);
        Assert.Empty(opts.SelectedStages);
        Assert.Null(opts.MaxDimension);
        Assert.False(opts.Quiet);
        Assert.False(opts.JsonOutput);
    }

    [Fact]
    public void Parse_ExportAllFlagsAndInlineValues_ParsesCorrectly()
    {
        CliParseResult result = CliArgumentParser.Parse([
            "export",
            "--input=in.png",
            "--output=out_dir",
            "-p", "warm.json",
            "--no-scale-preset",
            "--stages=00_input,hand-tremor",
            "-s", "06f_final_watercolor.png",
            "--resolution=1024",
            "--json"
        ]);

        Assert.False(result.IsError);
        Assert.NotNull(result.ExportOptions);
        CliExportOptions opts = result.ExportOptions!;
        Assert.Equal("in.png", opts.InputPath);
        Assert.Equal("out_dir", opts.OutputPath);
        Assert.Equal("warm.json", opts.PresetPath);
        Assert.Equal(CliPresetScalingMode.Exact, opts.PresetScaling);
        Assert.Equal(CliExportMode.Stages, opts.Mode);
        Assert.Equal(3, opts.SelectedStages.Count);
        Assert.Equal(1024, opts.MaxDimension);
        Assert.True(opts.JsonOutput);
        Assert.False(opts.Quiet);
    }

    [Fact]
    public void Parse_ExportDoubleDashSentinel_AllowsDashPrefixedFilename()
    {
        CliParseResult result = CliArgumentParser.Parse(["export", "-o", "out.png", "--", "-weird-name.png"]);
        Assert.False(result.IsError);
        Assert.Equal("-weird-name.png", result.ExportOptions!.InputPath);
    }

    public static TheoryData<string[]> InvalidArgCases => new()
    {
        new[] { "unknown-cmd" },
        new[] { "--unknown-flag" },
        new[] { "export" },
        new[] { "export", "a.png", "b.png" },
        new[] { "export", "a.png", "-i", "b.png" },
        new[] { "export", "a.png", "--scale-preset" },
        new[] { "export", "a.png", "--no-scale-preset" },
        new[] { "export", "a.png", "-p", "p.json", "--scale-preset", "--no-scale-preset" },
        new[] { "export", "a.png", "-p", "p.json", "-b", "default" },
        new[] { "export", "a.png", "-m", "final", "-s", "hand-tremor" },
        new[] { "export", "a.png", "-m", "all-stages", "-s", "hand-tremor" },
        new[] { "export", "a.png", "-m", "stages" },
        new[] { "export", "a.png", "-s", "not-a-stage" },
        new[] { "export", "a.png", "-r", "0" },
        new[] { "export", "a.png", "-r", "-5" },
        new[] { "export", "a.png", "-r", "abc" },
        new[] { "export", "a.png", "-q", "--json" },
        new[] { "export", "a.png", "-o" },
        new[] { "export", "a.png", "--bad-flag" },
        new[] { "stages", "extra" },
    };

    [Theory]
    [MemberData(nameof(InvalidArgCases))]
    public void Parse_InvalidCombinations_ReturnUsageErrors(string[] args)
    {
        CliParseResult result = CliArgumentParser.Parse(args);
        Assert.True(result.IsError);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}

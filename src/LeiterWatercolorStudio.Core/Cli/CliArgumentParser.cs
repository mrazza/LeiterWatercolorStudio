using System.Globalization;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Parses and validates command-line arguments for headless execution and GUI dispatch.
/// </summary>
public static class CliArgumentParser
{
    /// <summary>
    /// Returns <see langword="true"/> when the process should launch the desktop GUI
    /// (empty arguments or macOS Finder <c>-psn_*</c> launch tokens).
    /// </summary>
    public static bool ShouldLaunchGui(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-psn_", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses <paramref name="args"/> into a structured <see cref="CliParseResult"/>.
    /// </summary>
    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (ShouldLaunchGui(args))
        {
            return new CliParseResult { Command = CliCommandKind.LaunchGui };
        }

        string first = args[0];
        if (first is "-h" or "--help")
        {
            return ParseHelpTopic(args.AsSpan(1));
        }

        if (string.Equals(first, "help", StringComparison.Ordinal))
        {
            return ParseHelpTopic(args.AsSpan(1));
        }

        if (first is "-V" or "--version" || string.Equals(first, "version", StringComparison.Ordinal))
        {
            if (args.Length == 1)
            {
                return new CliParseResult { Command = CliCommandKind.Version };
            }

            if (args.Length == 2 && args[1] is "-h" or "--help")
            {
                return new CliParseResult { Command = CliCommandKind.Help };
            }

            return UsageError($"Unexpected argument '{args[1]}' for '{first}'.");
        }

        if (string.Equals(first, "stages", StringComparison.Ordinal))
        {
            return ParseStages(args.AsSpan(1));
        }

        if (string.Equals(first, "export", StringComparison.Ordinal))
        {
            return ParseExport(args.AsSpan(1));
        }

        if (first.StartsWith('-'))
        {
            return UsageError(
                $"Unknown option '{first}'. Use 'LeiterWatercolorStudio export <input> [options]' or run '--help' for usage.");
        }

        return UsageError($"Unknown command '{first}'. Expected 'export' or 'stages'. Run '--help' for usage.");
    }

    private static CliParseResult ParseHelpTopic(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            return new CliParseResult { Command = CliCommandKind.Help };
        }

        if (args.Length == 1)
        {
            string topic = args[0].ToLowerInvariant();
            if (topic is "export" or "stages")
            {
                return new CliParseResult
                {
                    Command = CliCommandKind.Help,
                    HelpTopic = topic,
                };
            }

            return UsageError($"Unknown help topic '{args[0]}'. Available topics: 'export', 'stages'.");
        }

        return UsageError($"Unexpected extra argument '{args[1]}' for help.");
    }

    private static CliParseResult ParseStages(ReadOnlySpan<string> args)
    {
        bool jsonOutput = false;

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (token is "-h" or "--help")
            {
                return new CliParseResult
                {
                    Command = CliCommandKind.Help,
                    HelpTopic = "stages",
                };
            }

            if (token == "--json")
            {
                jsonOutput = true;
                continue;
            }

            if (token.StartsWith("--json=", StringComparison.Ordinal))
            {
                return UsageError("Option '--json' does not accept a value.");
            }

            return UsageError($"Unexpected argument '{token}' for 'stages'. Run 'LeiterWatercolorStudio stages --help' for usage.");
        }

        return new CliParseResult
        {
            Command = CliCommandKind.ListStages,
            JsonOutput = jsonOutput,
        };
    }

    private static CliParseResult ParseExport(ReadOnlySpan<string> args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string? presetPath = null;
        bool scalePresetFlag = false;
        bool noScalePresetFlag = false;
        BaselineMode? explicitBaseline = null;
        CliExportMode? explicitMode = null;
        var rawStageTokens = new List<string>();
        bool hasResolution = false;
        int? maxDimension = null;
        bool quiet = false;
        bool jsonOutput = false;
        bool afterDoubleDash = false;

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!afterDoubleDash && token == "--")
            {
                afterDoubleDash = true;
                continue;
            }

            if (!afterDoubleDash && token.Length > 1 && token[0] == '-')
            {
                SplitFlagAndInlineValue(token, out string flag, out string? inlineValue);

                switch (flag)
                {
                    case "-h":
                    case "--help":
                        if (inlineValue is not null)
                        {
                            return UsageError($"Option '{flag}' does not accept a value.");
                        }

                        return new CliParseResult
                        {
                            Command = CliCommandKind.Help,
                            HelpTopic = "export",
                        };

                    case "-i":
                    case "--input":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? inVal, out string? inErr))
                        {
                            return UsageError(inErr!);
                        }

                        if (inputPath is not null)
                        {
                            return UsageError("Input image specified multiple times.");
                        }

                        if (string.IsNullOrWhiteSpace(inVal))
                        {
                            return UsageError($"Option '{flag}' cannot be empty.");
                        }

                        inputPath = inVal;
                        break;

                    case "-o":
                    case "--output":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? outVal, out string? outErr))
                        {
                            return UsageError(outErr!);
                        }

                        if (outputPath is not null)
                        {
                            return UsageError("Option '--output' specified multiple times.");
                        }

                        if (string.IsNullOrWhiteSpace(outVal))
                        {
                            return UsageError($"Option '{flag}' cannot be empty.");
                        }

                        outputPath = outVal;
                        break;

                    case "-p":
                    case "--preset":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? presetVal, out string? presetErr))
                        {
                            return UsageError(presetErr!);
                        }

                        if (presetPath is not null)
                        {
                            return UsageError("Option '--preset' specified multiple times.");
                        }

                        if (string.IsNullOrWhiteSpace(presetVal))
                        {
                            return UsageError($"Option '{flag}' cannot be empty.");
                        }

                        presetPath = presetVal;
                        break;

                    case "--scale-preset":
                        if (inlineValue is not null)
                        {
                            return UsageError("Option '--scale-preset' does not accept a value.");
                        }

                        scalePresetFlag = true;
                        break;

                    case "--no-scale-preset":
                        if (inlineValue is not null)
                        {
                            return UsageError("Option '--no-scale-preset' does not accept a value.");
                        }

                        noScalePresetFlag = true;
                        break;

                    case "-b":
                    case "--baseline":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? baseVal, out string? baseErr))
                        {
                            return UsageError(baseErr!);
                        }

                        if (explicitBaseline.HasValue)
                        {
                            return UsageError("Option '--baseline' specified multiple times.");
                        }

                        if (!TryParseBaseline(baseVal!, out BaselineMode parsedBaseline))
                        {
                            return UsageError($"Invalid baseline '{baseVal}'. Expected 'for-image', 'for-size', or 'default'.");
                        }

                        explicitBaseline = parsedBaseline;
                        break;

                    case "-m":
                    case "--mode":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? modeVal, out string? modeErr))
                        {
                            return UsageError(modeErr!);
                        }

                        if (explicitMode.HasValue)
                        {
                            return UsageError("Option '--mode' specified multiple times.");
                        }

                        if (!TryParseMode(modeVal!, out CliExportMode parsedMode))
                        {
                            return UsageError($"Invalid export mode '{modeVal}'. Expected 'final', 'all-stages', or 'stages'.");
                        }

                        explicitMode = parsedMode;
                        break;

                    case "-s":
                    case "--stages":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? stageVal, out string? stageErr))
                        {
                            return UsageError(stageErr!);
                        }

                        rawStageTokens.Add(stageVal!);
                        break;

                    case "-r":
                    case "--resolution":
                        if (!TryConsumeOptionValue(flag, inlineValue, args, ref i, out string? resVal, out string? resErr))
                        {
                            return UsageError(resErr!);
                        }

                        if (hasResolution)
                        {
                            return UsageError("Option '--resolution' specified multiple times.");
                        }

                        if (!TryParseResolution(resVal!, out int? parsedResolution))
                        {
                            return UsageError(
                                $"Invalid resolution '{resVal}'. Expected 'full', 'native', or a positive integer pixel dimension.");
                        }

                        hasResolution = true;
                        maxDimension = parsedResolution;
                        break;

                    case "-q":
                    case "--quiet":
                        if (inlineValue is not null)
                        {
                            return UsageError($"Option '{flag}' does not accept a value.");
                        }

                        quiet = true;
                        break;

                    case "--json":
                        if (inlineValue is not null)
                        {
                            return UsageError("Option '--json' does not accept a value.");
                        }

                        jsonOutput = true;
                        break;

                    default:
                        return UsageError($"Unknown option '{flag}' for 'export'. Run 'LeiterWatercolorStudio export --help' for usage.");
                }
            }
            else
            {
                if (inputPath is not null)
                {
                    return UsageError($"Unexpected extra positional argument '{token}'. Only one input image is supported per invocation.");
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    return UsageError("Input image path cannot be empty.");
                }

                inputPath = token;
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return UsageError("Missing required input image path. Usage: LeiterWatercolorStudio export <input> [options]");
        }

        if (scalePresetFlag && noScalePresetFlag)
        {
            return UsageError("Options '--scale-preset' and '--no-scale-preset' cannot be used together.");
        }

        if ((scalePresetFlag || noScalePresetFlag) && presetPath is null)
        {
            return UsageError("Options '--scale-preset' and '--no-scale-preset' require '--preset'.");
        }

        if (explicitBaseline.HasValue && presetPath is not null)
        {
            return UsageError("Option '--baseline' cannot be combined with '--preset'.");
        }

        if (quiet && jsonOutput)
        {
            return UsageError("Options '--quiet' and '--json' cannot be used together.");
        }

        CliExportMode resolvedMode;
        IReadOnlyList<StageExportEntry> selectedStages = [];
        if (rawStageTokens.Count > 0)
        {
            if (explicitMode is CliExportMode.Final or CliExportMode.AllStages)
            {
                return UsageError("Option '--stages' can only be used with '--mode stages' (or without '--mode').");
            }

            if (!StageSelectorResolver.TryResolve(rawStageTokens, out selectedStages, out string? stageError))
            {
                return UsageError(stageError!);
            }

            resolvedMode = CliExportMode.Stages;
        }
        else
        {
            if (explicitMode == CliExportMode.Stages)
            {
                return UsageError("Option '--mode stages' requires '--stages <list>'.");
            }

            resolvedMode = explicitMode ?? CliExportMode.Final;
        }

        CliPresetScalingMode presetScaling = scalePresetFlag
            ? CliPresetScalingMode.Scale
            : noScalePresetFlag
                ? CliPresetScalingMode.Exact
                : CliPresetScalingMode.Auto;

        return new CliParseResult
        {
            Command = CliCommandKind.Export,
            JsonOutput = jsonOutput,
            ExportOptions = new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                PresetPath = presetPath,
                PresetScaling = presetScaling,
                Baseline = explicitBaseline ?? BaselineMode.ForImage,
                Mode = resolvedMode,
                SelectedStages = selectedStages,
                MaxDimension = maxDimension,
                Quiet = quiet,
                JsonOutput = jsonOutput,
            },
        };
    }

    private static void SplitFlagAndInlineValue(string token, out string flag, out string? inlineValue)
    {
        int eqIndex = -1;
        if (token.StartsWith("--", StringComparison.Ordinal))
        {
            eqIndex = token.IndexOf('=');
        }
        else if (token.Length >= 3 && token[2] == '=')
        {
            eqIndex = 2;
        }

        if (eqIndex > 0)
        {
            flag = token[..eqIndex];
            inlineValue = token[(eqIndex + 1)..];
        }
        else
        {
            flag = token;
            inlineValue = null;
        }
    }

    private static bool TryConsumeOptionValue(
        string flag,
        string? inlineValue,
        ReadOnlySpan<string> args,
        ref int index,
        out string? value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Length)
        {
            value = null;
            error = $"Missing value for option '{flag}'.";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }

    private static bool TryParseBaseline(string value, out BaselineMode baseline)
    {
        switch (value.Trim().ToLowerInvariant().Replace('-', '_'))
        {
            case "for_image":
            case "forimage":
                baseline = BaselineMode.ForImage;
                return true;

            case "for_size":
            case "forsize":
                baseline = BaselineMode.ForSize;
                return true;

            case "default":
                baseline = BaselineMode.Default;
                return true;

            default:
                baseline = default;
                return false;
        }
    }

    private static bool TryParseMode(string value, out CliExportMode mode)
    {
        switch (value.Trim().ToLowerInvariant().Replace('-', '_'))
        {
            case "final":
                mode = CliExportMode.Final;
                return true;

            case "all_stages":
            case "allstages":
                mode = CliExportMode.AllStages;
                return true;

            case "stages":
                mode = CliExportMode.Stages;
                return true;

            default:
                mode = default;
                return false;
        }
    }

    private static bool TryParseResolution(string value, out int? maxDimension)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized is "full" or "native")
        {
            maxDimension = null;
            return true;
        }

        if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out int px) && px > 0)
        {
            maxDimension = px;
            return true;
        }

        maxDimension = null;
        return false;
    }

    private static CliParseResult UsageError(string message) =>
        new()
        {
            Command = CliCommandKind.Help,
            ErrorMessage = message,
        };
}

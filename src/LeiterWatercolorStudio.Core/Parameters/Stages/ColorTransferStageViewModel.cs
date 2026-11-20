using Leiter.Algorithms.Color;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 1 (Color Transfer) parameters and target palette selection.
/// </summary>
public sealed class ColorTransferStageViewModel : StageViewModelBase
{
    private const string DefaultCustomPaletteName = "Custom Palette";

    private bool _enableColorTransfer;
    private PaletteMode _paletteMode;
    private int _selectedBuiltInPaletteIndex;
    private double _colorTransferStrength;
    private string _customPaletteName;
    private double _meanL;
    private double _meanA;
    private double _meanB;
    private double _stdDevL;
    private double _stdDevA;
    private double _stdDevB;
    private Illuminant _customIlluminant;
    private ColorTransferProfile? _activeMatchedPalette;

    /// <summary>
    /// Initializes a new <see cref="ColorTransferStageViewModel"/> with default Stage 1 values.
    /// </summary>
    public ColorTransferStageViewModel()
    {
        ColorTransferProfile seedProfile = WatercolorPalettes.BalancedWash;
        _enableColorTransfer = WatercolorOptions.Default.EnableColorTransfer;
        _paletteMode = PaletteMode.Auto;
        _selectedBuiltInPaletteIndex = 0;
        _colorTransferStrength = WatercolorOptions.Default.ColorTransferStrength;
        _customPaletteName = DefaultCustomPaletteName;
        _meanL = seedProfile.Statistics.MeanL;
        _meanA = seedProfile.Statistics.MeanA;
        _meanB = seedProfile.Statistics.MeanB;
        _stdDevL = seedProfile.Statistics.StdDevL;
        _stdDevA = seedProfile.Statistics.StdDevA;
        _stdDevB = seedProfile.Statistics.StdDevB;
        _customIlluminant = seedProfile.Illuminant;
    }

    /// <summary>
    /// Gets the curated built-in watercolor palettes from <see cref="WatercolorPalettes.Defaults"/>.
    /// </summary>
    public IReadOnlyList<ColorTransferProfile> BuiltInPalettes => WatercolorPalettes.Defaults;

    /// <summary>
    /// Gets or sets whether Stage 1 Reinhard color transfer is enabled.
    /// </summary>
    public bool EnableColorTransfer
    {
        get => _enableColorTransfer;
        set => SetToggle(ref _enableColorTransfer, value);
    }

    /// <summary>
    /// Gets or sets how the target color transfer palette is selected.
    /// </summary>
    public PaletteMode PaletteMode
    {
        get => _paletteMode;
        set => SetEnum(ref _paletteMode, value);
    }

    /// <summary>
    /// Gets or sets the zero-based index of the selected entry in <see cref="BuiltInPalettes"/>.
    /// </summary>
    public int SelectedBuiltInPaletteIndex
    {
        get => _selectedBuiltInPaletteIndex;
        set
        {
            int maxIndex = Math.Max(0, BuiltInPalettes.Count - 1);
            if (SetClampedInt(ref _selectedBuiltInPaletteIndex, value, 0, maxIndex, isImmediate: true))
            {
                OnPropertyChanged(nameof(SelectedBuiltInPalette));
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected built-in <see cref="ColorTransferProfile"/>.
    /// </summary>
    public ColorTransferProfile SelectedBuiltInPalette
    {
        get => BuiltInPalettes[_selectedBuiltInPaletteIndex];
        set
        {
            if (value is null)
            {
                return;
            }

            int index = FindBuiltInPaletteIndex(value);
            if (index >= 0)
            {
                SelectedBuiltInPaletteIndex = index;
            }
        }
    }

    /// <summary>
    /// Gets or sets the Reinhard color transfer blending strength in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double ColorTransferStrength
    {
        get => _colorTransferStrength;
        set => SetFiniteDouble(ref _colorTransferStrength, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the custom palette name used when <see cref="PaletteMode"/> is <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public string CustomPaletteName
    {
        get => _customPaletteName;
        set
        {
            string normalized = value ?? string.Empty;
            if (SetProperty(ref _customPaletteName, normalized))
            {
                RaiseParameterChanged(isImmediate: false);
            }
        }
    }

    /// <summary>
    /// Gets or sets the custom CIELAB mean lightness (<c>L*</c>).
    /// </summary>
    public double MeanL
    {
        get => _meanL;
        set => SetFiniteDouble(ref _meanL, value);
    }

    /// <summary>
    /// Gets or sets the custom CIELAB mean chromaticity (<c>a*</c>).
    /// </summary>
    public double MeanA
    {
        get => _meanA;
        set => SetFiniteDouble(ref _meanA, value);
    }

    /// <summary>
    /// Gets or sets the custom CIELAB mean chromaticity (<c>b*</c>).
    /// </summary>
    public double MeanB
    {
        get => _meanB;
        set => SetFiniteDouble(ref _meanB, value);
    }

    /// <summary>
    /// Gets or sets the custom CIELAB lightness standard deviation (<c>&gt;= 0.0</c>).
    /// </summary>
    public double StdDevL
    {
        get => _stdDevL;
        set => SetNonNegativeDouble(ref _stdDevL, value);
    }

    /// <summary>
    /// Gets or sets the custom CIELAB <c>a*</c> standard deviation (<c>&gt;= 0.0</c>).
    /// </summary>
    public double StdDevA
    {
        get => _stdDevA;
        set => SetNonNegativeDouble(ref _stdDevA, value);
    }

    /// <summary>
    /// Gets or sets the custom CIELAB <c>b*</c> standard deviation (<c>&gt;= 0.0</c>).
    /// </summary>
    public double StdDevB
    {
        get => _stdDevB;
        set => SetNonNegativeDouble(ref _stdDevB, value);
    }

    /// <summary>
    /// Gets or sets the CIE reference white illuminant for the custom color profile.
    /// </summary>
    public Illuminant CustomIlluminant
    {
        get => _customIlluminant;
        set => SetEnum(ref _customIlluminant, value);
    }

    /// <summary>
    /// Gets or sets the most recently matched palette reported by the pipeline (informational readout; does not trigger renders).
    /// </summary>
    public ColorTransferProfile? ActiveMatchedPalette
    {
        get => _activeMatchedPalette;
        set => SetProperty(ref _activeMatchedPalette, value);
    }

    /// <summary>
    /// Applies an explicit custom <see cref="ColorTransferProfile"/> and switches <see cref="PaletteMode"/> to <see cref="PaletteMode.Custom"/>.
    /// </summary>
    /// <param name="profile">The profile to apply.</param>
    public void SetCustomProfile(ColorTransferProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        bool changed = false;
        using (SuppressNotifications())
        {
            changed |= ApplyCustomProfileFields(profile);
            changed |= SetProperty(ref _paletteMode, PaletteMode.Custom, nameof(PaletteMode));
        }

        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Copies <see cref="ActiveMatchedPalette"/> into the custom palette fields and switches to <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public void CopyMatchedToCustom()
    {
        if (_activeMatchedPalette is null)
        {
            return;
        }

        SetCustomProfile(_activeMatchedPalette);
    }

    /// <summary>
    /// Loads Stage 1 parameters from pipeline options with optional explicit palette mode override.
    /// </summary>
    public void LoadFrom(
        bool enableColorTransfer,
        ColorTransferProfile? targetPalette,
        double colorTransferStrength,
        PaletteMode? paletteModeHint = null)
    {
        targetPalette?.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableColorTransfer, enableColorTransfer, nameof(EnableColorTransfer));
            SetFiniteDouble(ref _colorTransferStrength, colorTransferStrength, 0.0, 1.0, propertyName: nameof(ColorTransferStrength));

            if (targetPalette is not null)
            {
                SetProperty(ref _activeMatchedPalette, targetPalette, nameof(ActiveMatchedPalette));
            }

            PaletteMode resolvedMode = ResolvePaletteMode(targetPalette, paletteModeHint);
            SetProperty(ref _paletteMode, resolvedMode, nameof(PaletteMode));

            if (targetPalette is not null)
            {
                int builtInIndex = FindBuiltInPaletteIndex(targetPalette);
                if (resolvedMode == PaletteMode.BuiltIn && builtInIndex >= 0)
                {
                    if (SetProperty(ref _selectedBuiltInPaletteIndex, builtInIndex, nameof(SelectedBuiltInPaletteIndex)))
                    {
                        OnPropertyChanged(nameof(SelectedBuiltInPalette));
                    }
                }
                else if (resolvedMode == PaletteMode.Custom)
                {
                    ApplyCustomProfileFields(targetPalette);
                }
            }
        }
    }

    /// <summary>
    /// Builds the canonical <see cref="WatercolorOptions.TargetPalette"/> value for the active <see cref="PaletteMode"/>.
    /// </summary>
    public ColorTransferProfile? ToTargetPalette()
    {
        return _paletteMode switch
        {
            PaletteMode.Auto => null,
            PaletteMode.BuiltIn => SelectedBuiltInPalette,
            PaletteMode.Custom => BuildValidatedCustomProfile(),
            _ => null
        };
    }

    private ColorTransferProfile BuildValidatedCustomProfile()
    {
        string name = string.IsNullOrWhiteSpace(_customPaletteName)
            ? DefaultCustomPaletteName
            : _customPaletteName;

        var profile = new ColorTransferProfile(
            name,
            new LabColorStatistics(_meanL, _meanA, _meanB, _stdDevL, _stdDevA, _stdDevB),
            _customIlluminant);
        profile.Validate();
        return profile;
    }

    private bool ApplyCustomProfileFields(ColorTransferProfile profile)
    {
        bool changed = false;
        changed |= SetProperty(ref _customPaletteName, profile.Name, nameof(CustomPaletteName));
        changed |= SetProperty(ref _meanL, profile.Statistics.MeanL, nameof(MeanL));
        changed |= SetProperty(ref _meanA, profile.Statistics.MeanA, nameof(MeanA));
        changed |= SetProperty(ref _meanB, profile.Statistics.MeanB, nameof(MeanB));
        changed |= SetProperty(ref _stdDevL, profile.Statistics.StdDevL, nameof(StdDevL));
        changed |= SetProperty(ref _stdDevA, profile.Statistics.StdDevA, nameof(StdDevA));
        changed |= SetProperty(ref _stdDevB, profile.Statistics.StdDevB, nameof(StdDevB));
        changed |= SetProperty(ref _customIlluminant, profile.Illuminant, nameof(CustomIlluminant));
        return changed;
    }

    private PaletteMode ResolvePaletteMode(ColorTransferProfile? targetPalette, PaletteMode? paletteModeHint)
    {
        if (paletteModeHint is { } hint && Enum.IsDefined(hint))
        {
            return hint;
        }

        if (targetPalette is null)
        {
            return PaletteMode.Auto;
        }

        return FindBuiltInPaletteIndex(targetPalette) >= 0
            ? PaletteMode.BuiltIn
            : PaletteMode.Custom;
    }

    private int FindBuiltInPaletteIndex(ColorTransferProfile profile)
    {
        for (int i = 0; i < BuiltInPalettes.Count; i++)
        {
            if (Equals(BuiltInPalettes[i], profile))
            {
                return i;
            }
        }

        return -1;
    }
}

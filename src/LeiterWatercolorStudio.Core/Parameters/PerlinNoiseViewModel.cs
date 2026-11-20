using Leiter.Algorithms.Noise;

namespace LeiterWatercolorStudio.Core.Parameters;

/// <summary>
/// Observable view model for <see cref="PerlinNoiseOptions"/> supporting clamping, per-parameter baseline reset,
/// and deterministic seed randomization.
/// </summary>
public sealed class PerlinNoiseViewModel : StageViewModelBase
{
    private const int MaxOctaves = 16;

    private PerlinNoiseOptions _baseline;
    private int _seed;
    private double _frequency;
    private int _octaves;
    private double _lacunarity;
    private double _persistence;

    /// <summary>
    /// Initializes a new <see cref="PerlinNoiseViewModel"/> using the specified baseline noise configuration.
    /// </summary>
    /// <param name="baseline">Optional baseline configuration (defaults to <see cref="PerlinNoiseOptions.Default"/>).</param>
    public PerlinNoiseViewModel(PerlinNoiseOptions? baseline = null)
    {
        _baseline = baseline ?? PerlinNoiseOptions.Default;
        _baseline.Validate();

        _seed = _baseline.Seed;
        _frequency = _baseline.Frequency;
        _octaves = _baseline.Octaves;
        _lacunarity = _baseline.Lacunarity;
        _persistence = _baseline.Persistence;
    }

    /// <summary>
    /// Gets the baseline <see cref="PerlinNoiseOptions"/> used for per-parameter resets.
    /// </summary>
    public PerlinNoiseOptions Baseline => _baseline;

    /// <summary>
    /// Gets or sets the pseudo-random permutation seed.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            if (SetProperty(ref _seed, value))
            {
                RaiseParameterChanged(isImmediate: false);
            }
        }
    }

    /// <summary>
    /// Gets or sets the base spatial frequency multiplier (must be finite and greater than <c>0.0</c>).
    /// </summary>
    public double Frequency
    {
        get => _frequency;
        set => SetPositiveDouble(ref _frequency, value);
    }

    /// <summary>
    /// Gets or sets the number of fractal noise octaves (clamped to <c>[1, 16]</c>).
    /// </summary>
    public int Octaves
    {
        get => _octaves;
        set => SetClampedInt(ref _octaves, value, 1, MaxOctaves);
    }

    /// <summary>
    /// Gets or sets the per-octave frequency multiplier (must be finite and greater than <c>0.0</c>).
    /// </summary>
    public double Lacunarity
    {
        get => _lacunarity;
        set => SetPositiveDouble(ref _lacunarity, value);
    }

    /// <summary>
    /// Gets or sets the per-octave amplitude decay multiplier (must be finite and non-negative).
    /// </summary>
    public double Persistence
    {
        get => _persistence;
        set => SetNonNegativeDouble(ref _persistence, value);
    }

    /// <summary>
    /// Updates <see cref="Baseline"/> without modifying current parameter values or raising a parameter edit.
    /// </summary>
    /// <param name="baseline">The new baseline configuration.</param>
    public void SetBaseline(PerlinNoiseOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        SetProperty(ref _baseline, baseline, nameof(Baseline));
    }

    /// <summary>
    /// Loads all properties from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The source noise options.</param>
    public void LoadFrom(PerlinNoiseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _seed, options.Seed, nameof(Seed));
            SetProperty(ref _frequency, options.Frequency, nameof(Frequency));
            SetProperty(ref _octaves, options.Octaves, nameof(Octaves));
            SetProperty(ref _lacunarity, options.Lacunarity, nameof(Lacunarity));
            SetProperty(ref _persistence, options.Persistence, nameof(Persistence));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="PerlinNoiseOptions"/> instance from the current property values.
    /// </summary>
    public PerlinNoiseOptions ToOptions()
    {
        var options = new PerlinNoiseOptions(_seed, _frequency, _octaves, _lacunarity, _persistence);
        options.Validate();
        return options;
    }

    /// <summary>
    /// Assigns a new random <see cref="Seed"/> and raises an immediate parameter change.
    /// </summary>
    public void RandomizeSeed()
    {
        if (SetProperty(ref _seed, NextDistinctSeed(_seed), nameof(Seed)))
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Resets <see cref="Seed"/> to the baseline value.
    /// </summary>
    public void ResetSeed()
    {
        if (SetProperty(ref _seed, _baseline.Seed, nameof(Seed)))
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Resets <see cref="Frequency"/> to the baseline value.
    /// </summary>
    public void ResetFrequency() => SetPositiveDouble(ref _frequency, _baseline.Frequency, isImmediate: true, propertyName: nameof(Frequency));

    /// <summary>
    /// Resets <see cref="Octaves"/> to the baseline value.
    /// </summary>
    public void ResetOctaves() => SetClampedInt(ref _octaves, _baseline.Octaves, 1, MaxOctaves, isImmediate: true, propertyName: nameof(Octaves));

    /// <summary>
    /// Resets <see cref="Lacunarity"/> to the baseline value.
    /// </summary>
    public void ResetLacunarity() => SetPositiveDouble(ref _lacunarity, _baseline.Lacunarity, isImmediate: true, propertyName: nameof(Lacunarity));

    /// <summary>
    /// Resets <see cref="Persistence"/> to the baseline value.
    /// </summary>
    public void ResetPersistence() => SetNonNegativeDouble(ref _persistence, _baseline.Persistence, isImmediate: true, propertyName: nameof(Persistence));

    /// <summary>
    /// Resets all noise parameters to <see cref="Baseline"/>.
    /// </summary>
    public void ResetToBaseline()
    {
        bool changed = _seed != _baseline.Seed
            || _frequency != _baseline.Frequency
            || _octaves != _baseline.Octaves
            || _lacunarity != _baseline.Lacunarity
            || _persistence != _baseline.Persistence;

        LoadFrom(_baseline);
        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }
}

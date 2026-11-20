using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters;

/// <summary>
/// Event arguments raised by <see cref="PipelineParametersViewModel.OptionsChanged"/>.
/// </summary>
public sealed class OptionsChangedEventArgs(WatercolorOptions options, bool isImmediate) : EventArgs
{
    /// <summary>
    /// Gets the validated <see cref="WatercolorOptions"/> snapshot reflecting the latest parameters.
    /// </summary>
    public WatercolorOptions Options { get; } = options;

    /// <summary>
    /// Gets whether the edit should trigger an immediate render (<see langword="true"/>)
    /// or be debounced during continuous edits (<see langword="false"/>).
    /// </summary>
    public bool IsImmediate { get; } = isImmediate;
}

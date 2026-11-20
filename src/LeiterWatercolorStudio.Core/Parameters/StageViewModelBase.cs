using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LeiterWatercolorStudio.Core.Parameters;

/// <summary>
/// Provides shared clamping, change-notification suppression, and immediate/debounced parameter change tracking
/// for parameter view models.
/// </summary>
public abstract class StageViewModelBase : ObservableObject
{
    /// <summary>
    /// Minimum strictly positive value used when clamping parameters constrained to <c>&gt; 0.0</c>.
    /// </summary>
    protected const double MinPositiveValue = 1e-6;

    private int _suppressionDepth;

    /// <summary>
    /// Raised when one or more parameter values change outside of a suppressed batch load.
    /// The boolean argument indicates whether the change should trigger an immediate render (<see langword="true"/>)
    /// or be debounced (<see langword="false"/>).
    /// </summary>
    public event Action<bool>? ParameterChanged;

    /// <summary>
    /// Gets a value indicating whether change notifications to <see cref="ParameterChanged"/> are currently suppressed.
    /// </summary>
    protected bool IsSuppressed => _suppressionDepth > 0;

    /// <summary>
    /// Temporarily suppresses <see cref="ParameterChanged"/> events until the returned scope is disposed.
    /// </summary>
    protected IDisposable SuppressNotifications()
    {
        _suppressionDepth++;
        return new SuppressionScope(this);
    }

    /// <summary>
    /// Raises <see cref="ParameterChanged"/> unless notifications are currently suppressed.
    /// </summary>
    protected void RaiseParameterChanged(bool isImmediate)
    {
        if (_suppressionDepth == 0)
        {
            ParameterChanged?.Invoke(isImmediate);
        }
    }

    /// <summary>
    /// Subscribes a child parameter view model so its edits bubble up through <see cref="RaiseParameterChanged"/>.
    /// </summary>
    protected void SubscribeChild(StageViewModelBase child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.ParameterChanged += RaiseParameterChanged;
    }

    /// <summary>
    /// Updates a boolean toggle property and notifies with <paramref name="isImmediate"/> (default <see langword="true"/>).
    /// </summary>
    protected bool SetToggle(
        ref bool field,
        bool value,
        bool isImmediate = true,
        [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        RaiseParameterChanged(isImmediate);
        return true;
    }

    /// <summary>
    /// Updates an enum property after verifying <see cref="Enum.IsDefined{TEnum}(TEnum)"/> and notifies with <paramref name="isImmediate"/>.
    /// </summary>
    protected bool SetEnum<TEnum>(
        ref TEnum field,
        TEnum value,
        bool isImmediate = true,
        [CallerMemberName] string? propertyName = null)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            OnPropertyChanged(propertyName);
            return false;
        }

        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        RaiseParameterChanged(isImmediate);
        return true;
    }

    /// <summary>
    /// Updates an integer property clamped to <c>[min, max]</c>.
    /// </summary>
    protected bool SetClampedInt(
        ref int field,
        int value,
        int min,
        int max = int.MaxValue,
        bool isImmediate = false,
        [CallerMemberName] string? propertyName = null)
    {
        int clamped = Math.Clamp(value, min, max);
        if (!SetProperty(ref field, clamped, propertyName))
        {
            if (clamped != value)
            {
                OnPropertyChanged(propertyName);
            }

            return false;
        }

        RaiseParameterChanged(isImmediate);
        return true;
    }

    /// <summary>
    /// Updates a finite floating-point property clamped to <c>[min, max]</c>, rejecting non-finite inputs.
    /// </summary>
    protected bool SetFiniteDouble(
        ref double field,
        double value,
        double min = double.NegativeInfinity,
        double max = double.PositiveInfinity,
        bool isImmediate = false,
        [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value))
        {
            OnPropertyChanged(propertyName);
            return false;
        }

        double clamped = Math.Clamp(value, min, max);
        if (!SetProperty(ref field, clamped, propertyName))
        {
            if (clamped != value)
            {
                OnPropertyChanged(propertyName);
            }

            return false;
        }

        RaiseParameterChanged(isImmediate);
        return true;
    }

    /// <summary>
    /// Updates a finite non-negative floating-point property (<c>&gt;= 0.0</c>).
    /// </summary>
    protected bool SetNonNegativeDouble(
        ref double field,
        double value,
        bool isImmediate = false,
        [CallerMemberName] string? propertyName = null) =>
        SetFiniteDouble(ref field, value, 0.0, double.MaxValue, isImmediate, propertyName);

    /// <summary>
    /// Updates a finite strictly positive floating-point property (<c>&gt; 0.0</c>).
    /// </summary>
    protected bool SetPositiveDouble(
        ref double field,
        double value,
        double minEpsilon = MinPositiveValue,
        double max = double.MaxValue,
        bool isImmediate = false,
        [CallerMemberName] string? propertyName = null) =>
        SetFiniteDouble(ref field, value, minEpsilon, max, isImmediate, propertyName);

    /// <summary>
    /// Generates a positive pseudo-random seed distinct from <paramref name="currentSeed"/>.
    /// </summary>
    protected static int NextDistinctSeed(int currentSeed)
    {
        int next;
        do
        {
            next = Random.Shared.Next(1, int.MaxValue);
        }
        while (next == currentSeed);

        return next;
    }

    private sealed class SuppressionScope(StageViewModelBase owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._suppressionDepth--;
        }
    }
}

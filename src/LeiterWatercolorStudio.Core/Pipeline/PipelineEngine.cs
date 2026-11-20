using System.Collections.ObjectModel;
using System.Diagnostics;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Event payload raised when a preview pipeline execution starts.
/// </summary>
public sealed record RenderStartedEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    WatercolorOptions EffectiveOptions,
    Size PreviewSize);

/// <summary>
/// Event payload raised when a preview pipeline execution completes successfully.
/// </summary>
public sealed record RenderCompletedEventArgs(
    long RequestId,
    WatercolorResult Result,
    WatercolorOptions CanonicalOptions,
    WatercolorOptions EffectiveOptions,
    TimeSpan TotalElapsed);

/// <summary>
/// Event payload raised when a pending or active preview pipeline execution is cancelled.
/// </summary>
public sealed record RenderCancelledEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    bool WasStarted);

/// <summary>
/// Event payload raised when a preview pipeline execution fails with an unhandled exception.
/// </summary>
public sealed record RenderFailedEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    Exception Exception);

/// <summary>
/// Event payload raised when DAG staleness changes relative to the last rendered options.
/// </summary>
public sealed record StalenessChangedEventArgs(
    IReadOnlySet<WatercolorStep> StaleSteps,
    IReadOnlySet<PrimaryStageId> StaleStages);

/// <summary>
/// Manages debounced, cancellable interactive preview rendering and isolated full-resolution exports
/// backed by <see cref="WatercolorPipelineSession"/>.
/// </summary>
public sealed class PipelineEngine : IDisposable
{
    private static readonly IReadOnlySet<WatercolorStep> EmptySteps =
        new ReadOnlySet<WatercolorStep>(new HashSet<WatercolorStep>());

    private static readonly IReadOnlySet<PrimaryStageId> EmptyStages =
        new ReadOnlySet<PrimaryStageId>(new HashSet<PrimaryStageId>());

    private readonly TimeSpan _debounceDelay;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    private WatercolorPipelineSession? _previewSession;
    private CancellationTokenSource? _activeRenderCts;
    private long _imageGeneration;
    private long _requestSequence;
    private int _activeRenderCount;
    private bool _isAutoRunEnabled = true;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="PipelineEngine"/> instance.
    /// </summary>
    /// <param name="debounceDelay">Optional debounce delay for non-immediate render requests (defaults to 90ms).</param>
    /// <param name="initialPreviewCap">Initial interactive preview resolution cap (defaults to <see cref="PreviewResolutionCap.Px1024"/>).</param>
    public PipelineEngine(
        TimeSpan? debounceDelay = null,
        PreviewResolutionCap initialPreviewCap = PreviewResolutionCap.Px1024)
    {
        TimeSpan resolvedDebounce = debounceDelay ?? TimeSpan.FromMilliseconds(90);
        if (resolvedDebounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceDelay), debounceDelay, "Debounce delay cannot be negative.");
        }

        if (!Enum.IsDefined(initialPreviewCap))
        {
            throw new ArgumentOutOfRangeException(nameof(initialPreviewCap), initialPreviewCap, "Invalid preview resolution cap.");
        }

        _debounceDelay = resolvedDebounce;
        PreviewCap = initialPreviewCap;
    }

    /// <summary>
    /// Raised when a preview render begins executing on the session.
    /// </summary>
    public event EventHandler<RenderStartedEventArgs>? RenderStarted;

    /// <summary>
    /// Raised as each <see cref="WatercolorStep"/> finishes during a preview render.
    /// </summary>
    public event EventHandler<WatercolorStepProgress>? StepProgress;

    /// <summary>
    /// Raised when a preview render finishes successfully.
    /// </summary>
    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;

    /// <summary>
    /// Raised when a preview render request is cancelled before completion.
    /// </summary>
    public event EventHandler<RenderCancelledEventArgs>? RenderCancelled;

    /// <summary>
    /// Raised when a preview render fails due to an unexpected exception.
    /// </summary>
    public event EventHandler<RenderFailedEventArgs>? RenderFailed;

    /// <summary>
    /// Raised when the set of stale pipeline steps or stages changes.
    /// </summary>
    public event EventHandler<StalenessChangedEventArgs>? StalenessChanged;

    /// <summary>
    /// Gets the loaded full-resolution source image, or <see langword="null"/> if no image is loaded.
    /// </summary>
    public SequentialMatrix<Rgb8>? NativeSource { get; private set; }

    /// <summary>
    /// Gets the optional filesystem path associated with the loaded image.
    /// </summary>
    public string? SourcePath { get; private set; }

    /// <summary>
    /// Gets the cached <see cref="ImageAnalysis"/> computed from <see cref="NativeSource"/>.
    /// </summary>
    public ImageAnalysis? Analysis { get; private set; }

    /// <summary>
    /// Gets the downscaled (or native) preview source matrix backing interactive renders.
    /// </summary>
    public IReadOnlyMatrix<Rgb8>? PreviewSource { get; private set; }

    /// <summary>
    /// Gets the dimensions of <see cref="NativeSource"/>, or default empty size if unloaded.
    /// </summary>
    public Size NativeSize => NativeSource?.Size ?? default;

    /// <summary>
    /// Gets the dimensions of <see cref="PreviewSource"/>, or default empty size if unloaded.
    /// </summary>
    public Size PreviewSize => PreviewSource?.Size ?? default;

    /// <summary>
    /// Gets the active maximum preview resolution cap.
    /// </summary>
    public PreviewResolutionCap PreviewCap { get; private set; }

    /// <summary>
    /// Gets whether an image is currently loaded.
    /// </summary>
    public bool HasImage => NativeSource is not null;

    /// <summary>
    /// Gets or sets whether <see cref="RequestRender"/> executes automatically or defers rendering until <see cref="RunNowAsync"/>.
    /// </summary>
    public bool IsAutoRunEnabled
    {
        get
        {
            lock (_stateLock)
            {
                return _isAutoRunEnabled;
            }
        }
        set
        {
            lock (_stateLock)
            {
                _isAutoRunEnabled = value;
            }
        }
    }

    /// <summary>
    /// Gets whether a preview render is currently executing inside the session.
    /// </summary>
    public bool IsRendering => Volatile.Read(ref _activeRenderCount) > 0;

    /// <summary>
    /// Gets the canonical options from the last completed preview render.
    /// </summary>
    public WatercolorOptions? LastRenderedCanonicalOptions { get; private set; }

    /// <summary>
    /// Gets the preview-scaled options from the last completed preview render.
    /// </summary>
    public WatercolorOptions? LastRenderedEffectiveOptions { get; private set; }

    /// <summary>
    /// Gets the most recently requested canonical options.
    /// </summary>
    public WatercolorOptions? PendingCanonicalOptions { get; private set; }

    /// <summary>
    /// Gets the most recently completed preview <see cref="WatercolorResult"/>.
    /// </summary>
    public WatercolorResult? LastResult { get; private set; }

    /// <summary>
    /// Gets the steps currently stale relative to <see cref="LastRenderedCanonicalOptions"/>.
    /// </summary>
    public IReadOnlySet<WatercolorStep> StaleSteps { get; private set; } = EmptySteps;

    /// <summary>
    /// Gets the stages currently stale relative to <see cref="LastRenderedCanonicalOptions"/>.
    /// </summary>
    public IReadOnlySet<PrimaryStageId> StaleStages { get; private set; } = EmptyStages;

    /// <summary>
    /// Loads a native image, caches its <see cref="ImageAnalysis"/>, and initializes the preview session.
    /// </summary>
    public void LoadImage(SequentialMatrix<Rgb8> nativeSource, string? sourcePath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(nativeSource);

        ImageAnalysis analysis = ImageAnalysis.Analyze(nativeSource);

        CancellationTokenSource? ctsToCancel;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ctsToCancel = DetachActiveCtsLocked();
            Interlocked.Increment(ref _imageGeneration);

            NativeSource = nativeSource;
            SourcePath = sourcePath;
            Analysis = analysis;
            PendingCanonicalOptions = null;
            LastRenderedCanonicalOptions = null;
            LastRenderedEffectiveOptions = null;
            LastResult = null;

            RebuildPreviewSessionLocked();
            UpdateStalenessLocked(null);
        }

        CancelSafely(ctsToCancel);
        RaiseStalenessChanged();
    }

    /// <summary>
    /// Changes the preview resolution cap, rebuilding the preview image and session while retaining
    /// <see cref="NativeSource"/> and cached <see cref="Analysis"/>.
    /// </summary>
    public void SetPreviewCap(PreviewResolutionCap cap)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enum.IsDefined(cap))
        {
            throw new ArgumentOutOfRangeException(nameof(cap), cap, "Invalid preview resolution cap.");
        }

        CancellationTokenSource? ctsToCancel = null;
        bool changed = false;

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (PreviewCap == cap)
            {
                return;
            }

            PreviewCap = cap;
            changed = true;

            if (NativeSource is not null)
            {
                ctsToCancel = DetachActiveCtsLocked();
                Interlocked.Increment(ref _imageGeneration);

                LastRenderedCanonicalOptions = null;
                LastRenderedEffectiveOptions = null;
                LastResult = null;

                RebuildPreviewSessionLocked();
                UpdateStalenessLocked(PendingCanonicalOptions);
            }
        }

        CancelSafely(ctsToCancel);
        if (changed)
        {
            RaiseStalenessChanged();
        }
    }

    /// <summary>
    /// Computes canonical baseline options using the requested <see cref="BaselineMode"/>.
    /// </summary>
    public WatercolorOptions ComputeBaseline(BaselineMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid baseline mode.");
        }

        switch (mode)
        {
            case BaselineMode.Default:
                return WatercolorOptions.Default;

            case BaselineMode.ForSize:
            {
                SequentialMatrix<Rgb8> native = NativeSource
                    ?? throw new InvalidOperationException("Cannot compute size-tuned baseline before loading an image.");
                return WatercolorOptions.ForSize(native.Size);
            }

            case BaselineMode.ForImage:
            {
                ImageAnalysis analysis = Analysis
                    ?? throw new InvalidOperationException("Cannot compute image-tuned baseline before loading an image.");
                return WatercolorOptions.ForAnalysis(analysis);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid baseline mode.");
        }
    }

    /// <summary>
    /// Requests a debounced (or immediate) preview render for <paramref name="canonical"/>.
    /// </summary>
    public Task<WatercolorResult?> RequestRender(WatercolorOptions canonical, bool immediate = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canonical);

        CancellationTokenSource? ctsToCancel = null;
        CancellationTokenSource? newCts = null;
        WatercolorPipelineSession session;
        Size nativeSize;
        Size previewSize;
        long generation;
        long requestId;
        bool autoRun;

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (NativeSource is null || PreviewSource is null || _previewSession is null)
            {
                throw new InvalidOperationException("Cannot request a render before loading an image.");
            }

            PendingCanonicalOptions = canonical;
            UpdateStalenessLocked(canonical);
            autoRun = _isAutoRunEnabled;

            if (!autoRun)
            {
                session = _previewSession;
                nativeSize = NativeSource.Size;
                previewSize = PreviewSource.Size;
                generation = Volatile.Read(ref _imageGeneration);
                requestId = 0;
            }
            else
            {
                ctsToCancel = DetachActiveCtsLocked();
                newCts = new CancellationTokenSource();
                _activeRenderCts = newCts;

                session = _previewSession;
                nativeSize = NativeSource.Size;
                previewSize = PreviewSource.Size;
                generation = Volatile.Read(ref _imageGeneration);
                requestId = Interlocked.Increment(ref _requestSequence);
            }
        }

        CancelSafely(ctsToCancel);
        RaiseStalenessChanged();

        if (!autoRun)
        {
            return Task.FromResult<WatercolorResult?>(null);
        }

        return ExecuteRenderAsync(
            newCts!,
            requestId,
            generation,
            session,
            canonical,
            nativeSize,
            previewSize,
            immediate);
    }

    /// <summary>
    /// Immediately renders the current <see cref="PendingCanonicalOptions"/> (or last rendered options),
    /// even when <see cref="IsAutoRunEnabled"/> is <see langword="false"/>.
    /// </summary>
    public Task<WatercolorResult?> RunNowAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancellationTokenSource? ctsToCancel;
        CancellationTokenSource newCts;
        WatercolorPipelineSession session;
        WatercolorOptions? canonical;
        Size nativeSize;
        Size previewSize;
        long generation;
        long requestId;

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (NativeSource is null || PreviewSource is null || _previewSession is null)
            {
                throw new InvalidOperationException("Cannot run render before loading an image.");
            }

            canonical = PendingCanonicalOptions ?? LastRenderedCanonicalOptions;
            if (canonical is null)
            {
                return Task.FromResult<WatercolorResult?>(null);
            }

            ctsToCancel = DetachActiveCtsLocked();
            newCts = new CancellationTokenSource();
            _activeRenderCts = newCts;

            session = _previewSession;
            nativeSize = NativeSource.Size;
            previewSize = PreviewSource.Size;
            generation = Volatile.Read(ref _imageGeneration);
            requestId = Interlocked.Increment(ref _requestSequence);
        }

        CancelSafely(ctsToCancel);

        return ExecuteRenderAsync(
            newCts,
            requestId,
            generation,
            session,
            canonical,
            nativeSize,
            previewSize,
            immediate: true);
    }

    /// <summary>
    /// Cancels any pending debounced or actively running preview render.
    /// </summary>
    public void CancelActiveRender()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_stateLock)
        {
            ctsToCancel = DetachActiveCtsLocked();
        }

        CancelSafely(ctsToCancel);
    }

    /// <summary>
    /// Executes a one-off full-resolution render on <see cref="NativeSource"/> using an isolated
    /// <see cref="WatercolorPipelineSession"/> so the preview cache remains untouched.
    /// </summary>
    public Task<WatercolorResult> RenderFullResolutionAsync(
        WatercolorOptions canonical,
        Action<WatercolorStepProgress>? onStep = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canonical);
        canonical.Validate();

        SequentialMatrix<Rgb8>? nativeSource;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            nativeSource = NativeSource;
        }

        if (nativeSource is null)
        {
            throw new InvalidOperationException("Cannot render full resolution before loading an image.");
        }

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var fullSession = new WatercolorPipelineSession(nativeSource);
            return fullSession.Render(canonical, onStep, ct);
        }, ct);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ctsToCancel = DetachActiveCtsLocked();
            Interlocked.Increment(ref _imageGeneration);
        }

        CancelSafely(ctsToCancel);
        _sessionLock.Dispose();
    }

    private async Task<WatercolorResult?> ExecuteRenderAsync(
        CancellationTokenSource cts,
        long requestId,
        long generation,
        WatercolorPipelineSession session,
        WatercolorOptions canonical,
        Size nativeSize,
        Size previewSize,
        bool immediate)
    {
        CancellationToken ct = cts.Token;
        bool lockAcquired = false;
        bool started = false;

        try
        {
            if (!immediate && _debounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(_debounceDelay, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();

            await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
            lockAcquired = true;

            if (ct.IsCancellationRequested || _disposed || generation != Volatile.Read(ref _imageGeneration))
            {
                RenderCancelled?.Invoke(this, new RenderCancelledEventArgs(requestId, canonical, WasStarted: false));
                return null;
            }

            canonical.Validate();
            WatercolorOptions effective = PreviewScaling.ComputeEffectivePreviewOptions(canonical, nativeSize, previewSize);

            started = true;
            Interlocked.Increment(ref _activeRenderCount);
            RenderStarted?.Invoke(this, new RenderStartedEventArgs(requestId, canonical, effective, previewSize));

            long startTimestamp = Stopwatch.GetTimestamp();
            WatercolorResult result = await Task.Run(
                () => session.Render(
                    effective,
                    progress =>
                    {
                        if (!_disposed && generation == Volatile.Read(ref _imageGeneration))
                        {
                            StepProgress?.Invoke(this, progress);
                        }
                    },
                    ct),
                ct).ConfigureAwait(false);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            if (ct.IsCancellationRequested || _disposed || generation != Volatile.Read(ref _imageGeneration))
            {
                Interlocked.Decrement(ref _activeRenderCount);
                started = false;
                RenderCancelled?.Invoke(this, new RenderCancelledEventArgs(requestId, canonical, WasStarted: true));
                return null;
            }

            lock (_stateLock)
            {
                if (_disposed || generation != Volatile.Read(ref _imageGeneration))
                {
                    Interlocked.Decrement(ref _activeRenderCount);
                    started = false;
                    RenderCancelled?.Invoke(this, new RenderCancelledEventArgs(requestId, canonical, WasStarted: true));
                    return null;
                }

                LastRenderedCanonicalOptions = canonical;
                LastRenderedEffectiveOptions = effective;
                LastResult = result;
                UpdateStalenessLocked(PendingCanonicalOptions);
            }

            Interlocked.Decrement(ref _activeRenderCount);
            started = false;

            RaiseStalenessChanged();
            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(requestId, result, canonical, effective, elapsed));
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || _disposed || generation != Volatile.Read(ref _imageGeneration))
        {
            if (started)
            {
                Interlocked.Decrement(ref _activeRenderCount);
                started = false;
            }

            if (!_disposed)
            {
                RenderCancelled?.Invoke(this, new RenderCancelledEventArgs(requestId, canonical, WasStarted: lockAcquired));
            }

            return null;
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            if (started)
            {
                Interlocked.Decrement(ref _activeRenderCount);
            }

            return null;
        }
        catch (Exception ex)
        {
            if (started)
            {
                Interlocked.Decrement(ref _activeRenderCount);
            }

            if (!_disposed)
            {
                RenderFailed?.Invoke(this, new RenderFailedEventArgs(requestId, canonical, ex));
            }

            return null;
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    _sessionLock.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            lock (_stateLock)
            {
                if (ReferenceEquals(_activeRenderCts, cts))
                {
                    _activeRenderCts = null;
                }
            }

            cts.Dispose();
        }
    }

    private void RebuildPreviewSessionLocked()
    {
        SequentialMatrix<Rgb8> native = NativeSource!;
        Size previewSize = PreviewScaling.ComputePreviewSize(native.Size, PreviewCap);
        IReadOnlyMatrix<Rgb8> preview = previewSize == native.Size
            ? native
            : native.ResizeBox(previewSize);

        PreviewSource = preview;
        _previewSession = new WatercolorPipelineSession(preview);
    }

    private void UpdateStalenessLocked(WatercolorOptions? pending)
    {
        if (pending is null)
        {
            StaleSteps = EmptySteps;
            StaleStages = EmptyStages;
            return;
        }

        (IReadOnlySet<WatercolorStep> steps, IReadOnlySet<PrimaryStageId> stages) =
            PipelineStalenessTracker.ComputeStale(LastRenderedCanonicalOptions, pending, PreviewSize);
        StaleSteps = steps;
        StaleStages = stages;
    }

    private void RaiseStalenessChanged()
    {
        if (_disposed)
        {
            return;
        }

        StalenessChanged?.Invoke(this, new StalenessChangedEventArgs(StaleSteps, StaleStages));
    }

    private CancellationTokenSource? DetachActiveCtsLocked()
    {
        CancellationTokenSource? cts = _activeRenderCts;
        _activeRenderCts = null;
        return cts;
    }

    private static void CancelSafely(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

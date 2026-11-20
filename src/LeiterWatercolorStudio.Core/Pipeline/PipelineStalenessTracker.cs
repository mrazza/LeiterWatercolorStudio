using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Computes exact DAG staleness matching <see cref="WatercolorPipelineSession"/> cache invalidation rules.
/// </summary>
public static class PipelineStalenessTracker
{
    private static readonly WatercolorStep[] AllSteps = Enum.GetValues<WatercolorStep>();

    /// <summary>
    /// Determines which <see cref="WatercolorStep"/>s and <see cref="PrimaryStageId"/>s are stale
    /// when transitioning from <paramref name="lastRendered"/> to <paramref name="pending"/>.
    /// </summary>
    public static (IReadOnlySet<WatercolorStep> StaleSteps, IReadOnlySet<PrimaryStageId> StaleStages) ComputeStale(
        WatercolorOptions? lastRendered,
        WatercolorOptions pending,
        Size? imageSize = null,
        bool hasCustomPaperHeightMap = false)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var staleSteps = new HashSet<WatercolorStep>();
        var staleStages = new HashSet<PrimaryStageId>();

        if (lastRendered is null)
        {
            foreach (WatercolorStep step in AllSteps)
            {
                staleSteps.Add(step);
                staleStages.Add(PipelineStageRegistry.GetStageForStep(step));
            }

            return (staleSteps, staleStages);
        }

        // Stage 1: Color Transfer
        bool lastColorActive = lastRendered.EnableColorTransfer && lastRendered.ColorTransferStrength > 0.0;
        bool pendingColorActive = pending.EnableColorTransfer && pending.ColorTransferStrength > 0.0;
        bool colorTransferStale =
            lastColorActive != pendingColorActive ||
            !Equals(lastRendered.TargetPalette, pending.TargetPalette) ||
            (lastColorActive ? lastRendered.ColorTransferStrength : 0.0) != (pendingColorActive ? pending.ColorTransferStrength : 0.0);
        RecordIfStale(WatercolorStep.ColorTransfer, colorTransferStale, staleSteps, staleStages);

        // Stage 2: Saliency (independent of Stage 3a Mean-Shift)
        bool egbiPreBlurStale = colorTransferStale || lastRendered.EgbiBlurSigma != pending.EgbiBlurSigma;
        RecordIfStale(WatercolorStep.EgbiPreBlur, egbiPreBlurStale, staleSteps, staleStages);

        bool egbiSegStale = egbiPreBlurStale || !Equals(lastRendered.Egbi, pending.Egbi);
        RecordIfStale(WatercolorStep.EgbiSegmentation, egbiSegStale, staleSteps, staleStages);

        bool rcSaliencyStale =
            colorTransferStale ||
            egbiSegStale ||
            !Equals(lastRendered.RegionalContrastSaliency, pending.RegionalContrastSaliency);
        RecordIfStale(WatercolorStep.RegionalContrastSaliency, rcSaliencyStale, staleSteps, staleStages);

        bool saliencyCutStale =
            colorTransferStale ||
            rcSaliencyStale ||
            !Equals(lastRendered.SaliencyCut, pending.SaliencyCut);
        RecordIfStale(WatercolorStep.SaliencyCut, saliencyCutStale, staleSteps, staleStages);

        bool distanceFieldStale =
            saliencyCutStale ||
            HasDistanceSmoothingChanged(lastRendered.SaliencyDistanceField, pending.SaliencyDistanceField, imageSize);
        RecordIfStale(WatercolorStep.SaliencyDistanceField, distanceFieldStale, staleSteps, staleStages);

        // Stage 3a: Mean-Shift (independent of Stage 2 Saliency)
        bool meanShiftFilteringStale =
            colorTransferStale ||
            lastRendered.MeanShift.SpatialRadius != pending.MeanShift.SpatialRadius ||
            lastRendered.MeanShift.RangeRadius != pending.MeanShift.RangeRadius ||
            lastRendered.MeanShift.MaxIterations != pending.MeanShift.MaxIterations ||
            lastRendered.MeanShift.ConvergenceEpsilon != pending.MeanShift.ConvergenceEpsilon;
        RecordIfStale(WatercolorStep.MeanShiftFiltering, meanShiftFilteringStale, staleSteps, staleStages);

        bool meanShiftClusteringStale =
            meanShiftFilteringStale ||
            lastRendered.MeanShift.MinRegionSize != pending.MeanShift.MinRegionSize ||
            lastRendered.MeanShift.Connectivity != pending.MeanShift.Connectivity;
        RecordIfStale(WatercolorStep.MeanShiftClustering, meanShiftClusteringStale, staleSteps, staleStages);

        // Stage 3b: LOD Abstraction
        bool lastLodEnabled = lastRendered.EnableLodAbstraction;
        bool pendingLodEnabled = pending.EnableLodAbstraction;
        bool lodStale =
            lastLodEnabled != pendingLodEnabled ||
            meanShiftFilteringStale ||
            (pendingLodEnabled && (
                meanShiftClusteringStale ||
                distanceFieldStale ||
                saliencyCutStale ||
                !Equals(lastRendered.LodAbstraction, pending.LodAbstraction)));
        RecordIfStale(WatercolorStep.LodAbstraction, lodStale, staleSteps, staleStages);

        // Stage 4: Wet-in-Wet
        bool lastWetActive = lastRendered.EnableWetInWet && lastRendered.WetInWet.Mode != WetInWetMode.Disabled;
        bool pendingWetActive = pending.EnableWetInWet && pending.WetInWet.Mode != WetInWetMode.Disabled;
        bool wetInWetStale =
            lastWetActive != pendingWetActive ||
            lodStale ||
            meanShiftClusteringStale ||
            (pendingWetActive && (
                saliencyCutStale ||
                !Equals(lastRendered.WetInWet, pending.WetInWet)));
        RecordIfStale(WatercolorStep.WetInWet, wetInWetStale, staleSteps, staleStages);

        // Stage 5: Hand Tremor
        bool lastTremorActive = lastRendered.EnableHandTremor && lastRendered.HandTremor.Amplitude > 0.0;
        bool pendingTremorActive = pending.EnableHandTremor && pending.HandTremor.Amplitude > 0.0;
        bool handTremorStale =
            lastTremorActive != pendingTremorActive ||
            wetInWetStale ||
            (pendingTremorActive && (
                meanShiftClusteringStale ||
                distanceFieldStale ||
                !Equals(lastRendered.HandTremor, pending.HandTremor)));
        RecordIfStale(WatercolorStep.HandTremor, handTremorStale, staleSteps, staleStages);

        // Stage 6a-i: Edge Darkening
        bool lastEdgeActive = lastRendered.EnableEdgeDarkening && lastRendered.EdgeDarkening.Intensity > 0.0;
        bool pendingEdgeActive = pending.EnableEdgeDarkening && pending.EdgeDarkening.Intensity > 0.0;
        bool edgeDarkeningStale =
            lastEdgeActive != pendingEdgeActive ||
            (pendingEdgeActive && (
                handTremorStale ||
                !Equals(lastRendered.EdgeDarkening, pending.EdgeDarkening)));
        RecordIfStale(WatercolorStep.EdgeDarkening, edgeDarkeningStale, staleSteps, staleStages);

        // Stage 6a-ii: Turbulence Flow (independent of Stages 1-5)
        bool lastTurbActive = lastRendered.EnableTurbulenceFlow && lastRendered.TurbulenceFlow.Intensity > 0.0;
        bool pendingTurbActive = pending.EnableTurbulenceFlow && pending.TurbulenceFlow.Intensity > 0.0;
        bool turbulenceFlowStale =
            lastTurbActive != pendingTurbActive ||
            (pendingTurbActive && !Equals(lastRendered.TurbulenceFlow, pending.TurbulenceFlow));
        RecordIfStale(WatercolorStep.TurbulenceFlow, turbulenceFlowStale, staleSteps, staleStages);

        // Stage 6a-iii: Pigment Combine
        bool pigmentCombineStale = handTremorStale || edgeDarkeningStale || turbulenceFlowStale;
        RecordIfStale(WatercolorStep.PigmentCombine, pigmentCombineStale, staleSteps, staleStages);

        // Stage 6b: Tone Curve
        bool lastToneEnabled = lastRendered.EnableToneCurve;
        bool pendingToneEnabled = pending.EnableToneCurve;
        bool toneCurveStale =
            lastToneEnabled != pendingToneEnabled ||
            pigmentCombineStale ||
            (pendingToneEnabled && !Equals(lastRendered.ToneCurve, pending.ToneCurve));
        RecordIfStale(WatercolorStep.ToneCurve, toneCurveStale, staleSteps, staleStages);

        // Stage 6c-i: Paper Height Map (independent of Stages 1-5 and earlier Stage 6 steps)
        bool lastGranActive = lastRendered.EnablePaperGranulation &&
            (lastRendered.PaperGranulation.DensityStrength > 0.0 || lastRendered.PaperGranulation.WarpAmplitude > 0.0);
        bool pendingGranActive = pending.EnablePaperGranulation &&
            (pending.PaperGranulation.DensityStrength > 0.0 || pending.PaperGranulation.WarpAmplitude > 0.0);
        bool isProcedural = !hasCustomPaperHeightMap;
        bool paperHeightMapStale =
            lastGranActive != pendingGranActive ||
            (pendingGranActive && isProcedural &&
             !Equals(lastRendered.PaperGranulation.GrainNoise, pending.PaperGranulation.GrainNoise));
        RecordIfStale(WatercolorStep.PaperHeightMap, paperHeightMapStale, staleSteps, staleStages);

        // Stage 6c-ii: Paper Granulation
        bool paperGranulationStale =
            lastGranActive != pendingGranActive ||
            toneCurveStale ||
            (pendingGranActive && (
                paperHeightMapStale ||
                lastRendered.PaperGranulation.DensityStrength != pending.PaperGranulation.DensityStrength ||
                lastRendered.PaperGranulation.WarpAmplitude != pending.PaperGranulation.WarpAmplitude ||
                (isProcedural && lastRendered.PaperGranulation.GrainNoise.Frequency != pending.PaperGranulation.GrainNoise.Frequency)));
        RecordIfStale(WatercolorStep.PaperGranulation, paperGranulationStale, staleSteps, staleStages);

        // Stage 6d: FXAA
        bool lastFxaaEnabled = lastRendered.EnableFxaa;
        bool pendingFxaaEnabled = pending.EnableFxaa;
        bool fxaaStale =
            lastFxaaEnabled != pendingFxaaEnabled ||
            paperGranulationStale ||
            (pendingFxaaEnabled && !Equals(lastRendered.Fxaa, pending.Fxaa));
        RecordIfStale(WatercolorStep.Fxaa, fxaaStale, staleSteps, staleStages);

        return (staleSteps, staleStages);
    }

    private static bool HasDistanceSmoothingChanged(
        SaliencyDistanceFieldOptions last,
        SaliencyDistanceFieldOptions pending,
        Size? imageSize)
    {
        if (imageSize is Size size)
        {
            int minDim = Math.Min(size.Width, size.Height);
            double lastEffective = last.SmoothingSigma ?? (last.DefaultSigmaScale * minDim);
            double pendingEffective = pending.SmoothingSigma ?? (pending.DefaultSigmaScale * minDim);
            return lastEffective != pendingEffective;
        }

        if (last.SmoothingSigma != pending.SmoothingSigma)
        {
            return true;
        }

        return last.SmoothingSigma is null && last.DefaultSigmaScale != pending.DefaultSigmaScale;
    }

    private static void RecordIfStale(
        WatercolorStep step,
        bool isStale,
        HashSet<WatercolorStep> staleSteps,
        HashSet<PrimaryStageId> staleStages)
    {
        if (!isStale)
        {
            return;
        }

        staleSteps.Add(step);
        staleStages.Add(PipelineStageRegistry.GetStageForStep(step));
    }
}

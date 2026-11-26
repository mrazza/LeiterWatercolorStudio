using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Pipeline;

public class PipelineRegistryAndScalingTests
{
    [Fact]
    public void Registry_CoversAllPrimaryStagesAndShortcuts()
    {
        PrimaryStageId[] expectedIds = Enum.GetValues<PrimaryStageId>();
        Assert.Equal(8, PipelineStageRegistry.Stages.Count);
        Assert.Equal(expectedIds, PipelineStageRegistry.Stages.Select(static s => s.StageId).ToArray());

        for (int i = 0; i < PipelineStageRegistry.Stages.Count; i++)
        {
            PipelineStageDescriptor stage = PipelineStageRegistry.Stages[i];
            Assert.Equal((char)('1' + i), stage.ShortcutKey);
            Assert.False(string.IsNullOrWhiteSpace(stage.DisplayName));
            Assert.Contains(stage.DefaultView, stage.ChildViews);
            Assert.Equal(stage, PipelineStageRegistry.GetStage(stage.StageId));

            bool expectedSubStages = stage.StageId is
                PrimaryStageId.Saliency or
                PrimaryStageId.Segmentation or
                PrimaryStageId.WetInWet or
                PrimaryStageId.Finishing;
            Assert.Equal(expectedSubStages, stage.HasSubStages);
        }
    }

    [Fact]
    public void Registry_CoversAll20ViewsAll18StepsAndAll27Modes()
    {
        StageViewId[] allViews = Enum.GetValues<StageViewId>();
        Assert.Equal(20, allViews.Length);
        Assert.Equal(allViews.Length, PipelineStageRegistry.Views.Count);

        var seenChildViews = new HashSet<StageViewId>();
        foreach (PipelineStageDescriptor stage in PipelineStageRegistry.Stages)
        {
            foreach (StageViewId child in stage.ChildViews)
            {
                Assert.True(seenChildViews.Add(child), $"Duplicate child view {child}");
                Assert.Equal(stage.StageId, PipelineStageRegistry.GetView(child).ParentStage);
            }
        }

        Assert.Equal(allViews.Length, seenChildViews.Count);

        var mappedSteps = new HashSet<WatercolorStep>();
        foreach (PipelineStageDescriptor stage in PipelineStageRegistry.Stages)
        {
            foreach (WatercolorStep step in stage.MappedSteps)
            {
                Assert.True(mappedSteps.Add(step), $"Duplicate mapped step {step}");
                Assert.Equal(stage.StageId, PipelineStageRegistry.GetStageForStep(step));
            }
        }

        Assert.Equal(Enum.GetValues<WatercolorStep>().Length, mappedSteps.Count);

        var coveredModes = new HashSet<VisualizationMode>();
        foreach (StageViewDescriptor view in PipelineStageRegistry.Views)
        {
            Assert.False(string.IsNullOrWhiteSpace(view.DisplayName));
            Assert.NotEmpty(view.ValidModes);
            Assert.Contains(view.DefaultMode, view.ValidModes);
            Assert.True(PipelineStageRegistry.IsValidMode(view.ViewId, view.DefaultMode));

            foreach (VisualizationMode mode in view.ValidModes)
            {
                coveredModes.Add(mode);
                Assert.False(string.IsNullOrWhiteSpace(PipelineStageRegistry.GetVisualizationModeDisplayName(mode)));
            }
        }

        Assert.Equal(Enum.GetValues<VisualizationMode>().Length, coveredModes.Count);
    }

    [Fact]
    public void Registry_GetPreviousView_FollowsDagOrder()
    {
        Assert.Equal(StageViewId.Input, PipelineStageRegistry.GetPreviousView(StageViewId.Input));
        Assert.Equal(StageViewId.Input, PipelineStageRegistry.GetPreviousView(StageViewId.ColorTransfer));
        Assert.Equal(StageViewId.ColorTransfer, PipelineStageRegistry.GetPreviousView(StageViewId.SegmentationMeanShiftFiltered));
        Assert.Equal(StageViewId.LodAbstraction, PipelineStageRegistry.GetPreviousView(StageViewId.WetInWetImage));
        Assert.Equal(StageViewId.WetInWetImage, PipelineStageRegistry.GetPreviousView(StageViewId.HandTremor));
        Assert.Equal(StageViewId.HandTremor, PipelineStageRegistry.GetPreviousView(StageViewId.FinishingCombinedPigment));
        Assert.Equal(StageViewId.FinishingPaperGranulation, PipelineStageRegistry.GetPreviousView(StageViewId.FinishingFinalFxaa));

        foreach (StageViewId view in Enum.GetValues<StageViewId>())
        {
            StageViewId prev = PipelineStageRegistry.GetPreviousView(view);
            Assert.True(Enum.IsDefined(prev));
        }
    }

    [Theory]
    [InlineData(400, 300, PreviewResolutionCap.Px1024, 400, 300)]
    [InlineData(2048, 1024, PreviewResolutionCap.Px1024, 1024, 512)]
    [InlineData(1000, 2000, PreviewResolutionCap.Px512, 256, 512)]
    [InlineData(4000, 3000, PreviewResolutionCap.Native, 4000, 3000)]
    [InlineData(10000, 2, PreviewResolutionCap.Px512, 512, 1)]
    [InlineData(0, 0, PreviewResolutionCap.Px768, 0, 0)]
    public void ComputePreviewSize_RespectsCapAndAspectRatio(
        int width,
        int height,
        PreviewResolutionCap cap,
        int expectedWidth,
        int expectedHeight)
    {
        Size actual = PreviewScaling.ComputePreviewSize(new Size(width, height), cap);
        Assert.Equal(new Size(expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void ComputeEffectivePreviewOptions_ScalesOnlyWhenDownscaled()
    {
        WatercolorOptions canonical = WatercolorOptions.ForSize(new Size(2000, 1000));

        WatercolorOptions unchanged = PreviewScaling.ComputeEffectivePreviewOptions(
            canonical,
            new Size(2000, 1000),
            new Size(2000, 1000));
        Assert.Same(canonical, unchanged);

        Size previewSize = new(1000, 500);
        WatercolorOptions scaled = PreviewScaling.ComputeEffectivePreviewOptions(
            canonical,
            new Size(2000, 1000),
            previewSize);
        WatercolorOptions expected = WatercolorOptions.ForSize(
            previewSize,
            canonical,
            Math.Sqrt(2000.0 * 1000.0));

        Assert.Equal(expected, scaled);
        scaled.Validate();
    }

    [Fact]
    public void ComputeEffectivePreviewOptions_FromNativeForAnalysis_MatchesSingleHopScalingWithoutOverDarkeningOrEgbiShrink()
    {
        var nativeSize = new Size(4000, 3000);
        var previewSize = new Size(1024, 768);
        WatercolorOptions nativeCanonical = WatercolorOptions.ForSize(nativeSize);

        WatercolorOptions effective = PreviewScaling.ComputeEffectivePreviewOptions(
            nativeCanonical,
            nativeSize,
            previewSize);
        WatercolorOptions directPreview = WatercolorOptions.ForSize(previewSize);

        Assert.Equal(directPreview.EdgeDarkening.Intensity, effective.EdgeDarkening.Intensity, 10);
        Assert.Equal(directPreview.EdgeDarkening.BlurSigma, effective.EdgeDarkening.BlurSigma, 10);
        Assert.Equal(directPreview.Egbi.KFactor, effective.Egbi.KFactor, 10);
        Assert.Equal(directPreview.Egbi.Epsilon, effective.Egbi.Epsilon, 10);
    }

    [Theory]
    [InlineData(400, 300, 1024, 400, 300)]
    [InlineData(2048, 1024, 1024, 1024, 512)]
    [InlineData(1000, 2000, 640, 320, 640)]
    [InlineData(4000, 3000, 0, 4000, 3000)]
    [InlineData(10000, 2, 512, 512, 1)]
    [InlineData(0, 0, 768, 0, 0)]
    public void ComputePreviewSize_CustomMaxDimension_RespectsCapAndAspectRatio(
        int width,
        int height,
        int maxDimension,
        int expectedWidth,
        int expectedHeight)
    {
        Size actual = PreviewScaling.ComputePreviewSize(new Size(width, height), maxDimension);
        Assert.Equal(new Size(expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void ComputePreviewSize_NegativeMaxDimension_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PreviewScaling.ComputePreviewSize(new Size(100, 100), -1));
    }
}

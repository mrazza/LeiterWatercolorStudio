using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Leiter.Algorithms.Noise;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.App.Views;
using LeiterWatercolorStudio.App.Views.Inspectors;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class SettingInfoIconTests
{
    [AvaloniaFact]
    public void SettingInfoIcon_VisibilityAndTooltip_FollowsTextAndHeader()
    {
        var icon = new SettingInfoIcon();
        var window = new Window
        {
            Width = 200,
            Height = 120,
            Content = icon,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Border hitTarget = icon.FindControl<Border>("PART_HitTarget")!;
            Assert.False(hitTarget.IsVisible);
            Assert.Null(ToolTip.GetTip(icon));

            icon.Header = "Transfer Strength";
            icon.Text = "Controls how strongly the watercolor palette replaces original photo colors.";
            TestAppHelpers.PumpUi();

            Assert.True(hitTarget.IsVisible);
            var tipPanel = Assert.IsType<StackPanel>(ToolTip.GetTip(icon));
            Assert.InRange(tipPanel.MaxWidth, 200, 340);
            Assert.Equal(2, tipPanel.Children.Count);

            var headerBlock = Assert.IsType<TextBlock>(tipPanel.Children[0]);
            var bodyBlock = Assert.IsType<TextBlock>(tipPanel.Children[1]);
            Assert.Equal("Transfer Strength", headerBlock.Text);
            Assert.Equal(FontWeight.SemiBold, headerBlock.FontWeight);
            Assert.Equal(TextWrapping.Wrap, bodyBlock.TextWrapping);
            Assert.Equal("Controls how strongly the watercolor palette replaces original photo colors.", bodyBlock.Text);

            icon.Text = "   ";
            TestAppHelpers.PumpUi();
            Assert.False(hitTarget.IsVisible);
            Assert.Null(ToolTip.GetTip(icon));
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void NumericSlider_PreservesTechnicalDescription_IsolatesInfoIconAndFitsNarrowWidth()
    {
        const string technicalDesc = "Relative edge contrast sensitivity threshold in (0.0, 1.0]";
        const string laymanInfo = "Lower values smooth gentle tonal edges; higher values restrict FXAA to bold contrast outlines.";

        var shortSlider = new NumericSlider
        {
            Label = "Seed",
            Description = "Deterministic integer noise seed.",
            InfoText = "Rearranges the random wave pattern without changing intensity.",
            Minimum = 0,
            Maximum = 1,
            Step = 0.01,
            BaselineValue = 0.25,
            Value = 0.75,
        };

        var longSlider = new NumericSlider
        {
            Label = "Relative Contrast Threshold",
            Description = technicalDesc,
            InfoText = laymanInfo,
            Minimum = 0.01,
            Maximum = 1.0,
            Step = 0.005,
            BaselineValue = 0.125,
            Value = 0.25,
        };

        var host = new StackPanel
        {
            Width = 240,
            Spacing = 8,
            Children = { shortSlider, longSlider },
        };

        var window = new Window
        {
            Width = 260,
            Height = 240,
            Content = host,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            // Verify technical tooltip stays on PART_LabelArea while layman tooltip lives on PART_InfoIcon.
            Border longLabelArea = longSlider.FindControl<Border>("PART_LabelArea")!;
            SettingInfoIcon longInfoIcon = longSlider.FindControl<SettingInfoIcon>("PART_InfoIcon")!;
            TextBox longValueBox = longSlider.FindControl<TextBox>("PART_ValueTextBox")!;

            Assert.Equal(technicalDesc, ToolTip.GetTip(longLabelArea));
            Assert.True(longInfoIcon.IsVisible);
            AssertTooltipContains(longInfoIcon, "Relative Contrast Threshold", laymanInfo);

            // Narrow width layout check: info icon stays visible and never overlaps the numeric textbox.
            Assert.True(longInfoIcon.Bounds.Width > 0);
            Point iconBottomRight = longInfoIcon.TranslatePoint(
                new Point(longInfoIcon.Bounds.Width, longInfoIcon.Bounds.Height),
                longSlider)!.Value;
            Point valueBoxTopLeft = longValueBox.TranslatePoint(new Point(0, 0), longSlider)!.Value;
            Assert.True(
                iconBottomRight.X <= valueBoxTopLeft.X + 0.5,
                $"Info icon right ({iconBottomRight.X:F1}) overlapped ValueTextBox left ({valueBoxTopLeft.X:F1}).");

            // Double-clicking the info icon must NOT reset Value to BaselineValue, while double-clicking label DOES reset.
            Border shortLabelArea = shortSlider.FindControl<Border>("PART_LabelArea")!;
            SettingInfoIcon shortInfoIcon = shortSlider.FindControl<SettingInfoIcon>("PART_InfoIcon")!;

            Point shortIconCenter = shortInfoIcon.TranslatePoint(
                new Point(shortInfoIcon.Bounds.Width * 0.5, shortInfoIcon.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(shortIconCenter, MouseButton.Left);
            window.MouseUp(shortIconCenter, MouseButton.Left);
            window.MouseDown(shortIconCenter, MouseButton.Left);
            window.MouseUp(shortIconCenter, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(0.75, shortSlider.Value, 4);

            Point shortLabelCenter = shortLabelArea.TranslatePoint(
                new Point(shortLabelArea.Bounds.Width * 0.5, shortLabelArea.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(shortLabelCenter, MouseButton.Left);
            window.MouseUp(shortLabelCenter, MouseButton.Left);
            window.MouseDown(shortLabelCenter, MouseButton.Left);
            window.MouseUp(shortLabelCenter, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(0.25, shortSlider.Value, 4);

            // Clearing InfoText hides PART_InfoIcon while preserving Description tooltip.
            longSlider.InfoText = null;
            TestAppHelpers.PumpUi();
            Assert.False(longInfoIcon.IsVisible);
            Assert.Equal(technicalDesc, ToolTip.GetTip(longLabelArea));
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void PerlinNoiseEditor_PopulatesHeaderAndChildDescriptionsAndInfoIcons()
    {
        var noiseVm = new PerlinNoiseViewModel(PerlinNoiseOptions.Default);
        var editor = new PerlinNoiseEditor
        {
            Width = 310,
            Title = "Turbulence Noise",
            DataContext = noiseVm,
        };

        var window = new Window
        {
            Width = 340,
            Height = 420,
            Content = editor,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Button expandButton = editor.FindControl<Button>("PART_ExpandButton")!;
            SettingInfoIcon titleInfoIcon = editor.FindControl<SettingInfoIcon>("PART_TitleInfoIcon")!;
            SettingInfoIcon seedInfoIcon = editor.FindControl<SettingInfoIcon>("PART_SeedInfoIcon")!;

            Assert.False(string.IsNullOrWhiteSpace(editor.Description));
            Assert.False(string.IsNullOrWhiteSpace(editor.InfoText));
            Assert.NotEqual(editor.Description, editor.InfoText);
            Assert.Equal(editor.Description, ToolTip.GetTip(expandButton));
            Assert.True(titleInfoIcon.IsVisible);
            AssertTooltipContains(titleInfoIcon, "Turbulence Noise", editor.InfoText!);

            AssertTooltipContains(seedInfoIcon, "Seed", editor.SeedInfoText!);

            NumericSlider[] sliders =
            [
                editor.FindControl<NumericSlider>("PART_FrequencySlider")!,
                editor.FindControl<NumericSlider>("PART_OctavesSlider")!,
                editor.FindControl<NumericSlider>("PART_LacunaritySlider")!,
                editor.FindControl<NumericSlider>("PART_PersistenceSlider")!,
            ];

            foreach (NumericSlider slider in sliders)
            {
                AssertSliderHasDistinctTechnicalAndLaymanInfo(slider);
            }

            // Clicking title info icon must NOT collapse the PerlinNoiseEditor body.
            Assert.True(editor.IsExpanded);
            Point titleIconCenter = titleInfoIcon.TranslatePoint(
                new Point(titleInfoIcon.Bounds.Width * 0.5, titleInfoIcon.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(titleIconCenter, MouseButton.Left);
            window.MouseUp(titleIconCenter, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.True(editor.IsExpanded);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllStageInspectors_HaveCompleteTechnicalAndLaymanInfoIconsInDarkAndLightThemes(bool isDarkTheme)
    {
        var parameters = new PipelineParametersViewModel();
        parameters.ColorTransfer.PaletteMode = PaletteMode.BuiltIn;
        parameters.Saliency.SaliencyDistanceField.UseExplicitSmoothingSigma = true;

        var inspectorExpectations = new (string Name, Control View, object ViewModel, int ExpectedInfoIcons)[]
        {
            ("ColorTransfer", new ColorTransferInspectorView(), parameters.ColorTransfer, 12),
            ("Saliency", new SaliencyInspectorView(), parameters.Saliency, 23),
            ("Segmentation", new SegmentationInspectorView(), parameters.Segmentation, 6),
            ("LodAbstraction", new LodAbstractionInspectorView(), parameters.LodAbstraction, 8),
            ("WetInWet", new WetInWetInspectorView(), parameters.WetInWet, 13),
            ("HandTremor", new HandTremorInspectorView(), parameters.HandTremor, 24),
            ("Finishing", new FinishingInspectorView(), parameters.Finishing, 31),
        };

        var window = new Window
        {
            Width = 360,
            Height = 900,
        };

        try
        {
            if (Application.Current is Application app)
            {
                app.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            window.Show();
            TestAppHelpers.PumpUi();

            foreach ((string name, Control view, object vm, int expectedInfoIcons) in inspectorExpectations)
            {
                view.DataContext = vm;
                window.Content = view;
                TestAppHelpers.PumpUi();

                // Materialize both outer and nested Expanders (such as Finishing sub-stage cards and their Advanced sections).
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Expander expander in view.GetVisualDescendants().OfType<Expander>())
                    {
                        expander.IsExpanded = true;
                    }

                    TestAppHelpers.PumpUi();
                }

                NumericSlider[] sliders = view.GetVisualDescendants().OfType<NumericSlider>().ToArray();
                Assert.NotEmpty(sliders);
                foreach (NumericSlider slider in sliders)
                {
                    AssertSliderHasDistinctTechnicalAndLaymanInfo(slider);
                }

                PerlinNoiseEditor[] noiseEditors = view.GetVisualDescendants().OfType<PerlinNoiseEditor>().ToArray();
                foreach (PerlinNoiseEditor editor in noiseEditors)
                {
                    Assert.False(string.IsNullOrWhiteSpace(editor.Description), $"{name}/{editor.Title} missing Description");
                    Assert.False(string.IsNullOrWhiteSpace(editor.InfoText), $"{name}/{editor.Title} missing InfoText");
                    Assert.NotEqual(editor.Description, editor.InfoText);
                }

                SettingInfoIcon[] infoIcons = view.GetVisualDescendants().OfType<SettingInfoIcon>().ToArray();
                Assert.Equal(expectedInfoIcons, infoIcons.Length);

                foreach (SettingInfoIcon icon in infoIcons)
                {
                    Border hitTarget = icon.FindControl<Border>("PART_HitTarget")!;
                    Assert.True(hitTarget.IsVisible, $"{name} info icon '{icon.Header}' is hidden.");
                    Assert.False(string.IsNullOrWhiteSpace(icon.Text), $"{name} info icon '{icon.Header}' has empty Text.");
                    AssertTooltipContains(icon, icon.Header, icon.Text!);
                }
            }
        }
        finally
        {
            if (Application.Current is Application resetApp)
            {
                resetApp.RequestedThemeVariant = ThemeVariant.Dark;
            }

            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void MainWindow_TopToolbarSettings_HavePreservedTechnicalTooltipsAndLaymanInfoIcons()
    {
        var vm = new MainWindowViewModel(
            fileDialogs: null,
            uiDispatcher: action => Dispatcher.UIThread.Post(action),
            debounceInterval: TimeSpan.FromMilliseconds(10));

        var window = new MainWindow
        {
            DataContext = vm,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Panel inspectorHost = window.FindControl<Panel>("InspectorHost")!;
            HashSet<SettingInfoIcon> inspectorIcons = inspectorHost
                .GetVisualDescendants()
                .OfType<SettingInfoIcon>()
                .ToHashSet();

            SettingInfoIcon[] toolbarIcons = window
                .GetVisualDescendants()
                .OfType<SettingInfoIcon>()
                .Where(icon => !inspectorIcons.Contains(icon))
                .ToArray();

            Assert.Equal(3, toolbarIcons.Length);
            Assert.Equal(
                ["Baseline Mode", "Compare Mode", "Preview Resolution"],
                toolbarIcons.Select(i => i.Header ?? string.Empty).OrderBy(h => h, StringComparer.Ordinal).ToArray());

            foreach (SettingInfoIcon icon in toolbarIcons)
            {
                Assert.True(icon.IsEffectivelyVisible);
                AssertTooltipContains(icon, icon.Header, icon.Text!);
            }

            // Confirm the technical tooltips remain attached to the Preview, Baseline, and Compare title TextBlocks.
            TextBlock[] labelBlocks = window
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(tb => tb.Classes.Contains("studio-muted") &&
                             tb.Text is "Preview" or "Baseline" or "Compare")
                .ToArray();

            Assert.Equal(3, labelBlocks.Length);
            foreach (TextBlock label in labelBlocks)
            {
                string? technical = ToolTip.GetTip(label) as string;
                Assert.False(string.IsNullOrWhiteSpace(technical), $"Toolbar label '{label.Text}' lost technical ToolTip.Tip.");
            }
        }
        finally
        {
            window.Close();
            vm.Dispose();
            TestAppHelpers.PumpUi();
        }
    }

    private static void AssertSliderHasDistinctTechnicalAndLaymanInfo(NumericSlider slider)
    {
        Assert.False(string.IsNullOrWhiteSpace(slider.Description), $"Slider '{slider.Label}' missing technical Description.");
        Assert.False(string.IsNullOrWhiteSpace(slider.InfoText), $"Slider '{slider.Label}' missing layman InfoText.");
        Assert.NotEqual(slider.Description!.Trim(), slider.InfoText!.Trim());

        Border labelArea = slider.FindControl<Border>("PART_LabelArea")!;
        SettingInfoIcon infoIcon = slider.FindControl<SettingInfoIcon>("PART_InfoIcon")!;

        Assert.Equal(slider.Description, ToolTip.GetTip(labelArea));
        Assert.True(infoIcon.IsVisible, $"Slider '{slider.Label}' info icon is not visible.");
        AssertTooltipContains(infoIcon, slider.Label, slider.InfoText!);
    }

    private static void AssertTooltipContains(SettingInfoIcon icon, string? expectedHeader, string expectedBody)
    {
        var panel = Assert.IsType<StackPanel>(ToolTip.GetTip(icon));
        Assert.True(panel.MaxWidth is > 0 and <= 340);

        TextBlock[] blocks = panel.Children.OfType<TextBlock>().ToArray();
        Assert.NotEmpty(blocks);

        if (!string.IsNullOrWhiteSpace(expectedHeader))
        {
            Assert.True(blocks.Length >= 2, $"Expected header and body blocks in tooltip for '{expectedHeader}'.");
            Assert.Equal(expectedHeader.Trim(), blocks[0].Text);
        }

        Assert.Equal(expectedBody.Trim(), blocks[^1].Text);
        Assert.Equal(TextWrapping.Wrap, blocks[^1].TextWrapping);
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Leiter.Pixels;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.App.Views.Inspectors;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters;
using LeiterWatercolorStudio.Core.Parameters.Stages;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class NumericInputValidationTests
{
    [AvaloniaFact]
    public void NumericSlider_AcceptsValidTypedValuesBeyondSliderRange_PreservesPrecisionAndNudges()
    {
        var positiveSlider = new NumericSlider
        {
            Label = "Amplitude",
            Minimum = 0.0,
            Maximum = 20.0,
            ValidMinimum = 0.0,
            Step = 0.1,
            FormatString = "F2",
            BaselineValue = 3.0,
            Value = 3.0,
        };

        var strictPositiveSlider = new NumericSlider
        {
            Label = "Frequency",
            Minimum = 0.001,
            Maximum = 0.5,
            ValidMinimum = 0.0,
            IsValidMinimumExclusive = true,
            Step = 0.001,
            FormatString = "F3",
            BaselineValue = 0.02,
            Value = 0.02,
        };

        var integerSlider = new NumericSlider
        {
            Label = "Spatial Radius",
            Minimum = 1.0,
            Maximum = 32.0,
            ValidMinimum = 1.0,
            Step = 1.0,
            IsInteger = true,
            FormatString = "F0",
            BaselineValue = 8.0,
            Value = 8.0,
        };

        var signedSlider = new NumericSlider
        {
            Label = "Mean a*",
            Minimum = -128.0,
            Maximum = 127.0,
            Step = 0.1,
            FormatString = "F2",
            HasBaseline = false,
            Value = 0.0,
        };

        var host = new StackPanel
        {
            Spacing = 6,
            Children = { positiveSlider, strictPositiveSlider, integerSlider, signedSlider },
        };

        var window = new Window
        {
            Width = 340,
            Height = 420,
            Content = host,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            // 1. Typed value above slider Maximum is accepted verbatim; slider thumb pins to track max with tooltip.
            SubmitSliderText(window, positiveSlider, "50");
            Assert.False(positiveSlider.HasValidationError);
            Assert.Equal(50.0, positiveSlider.Value, 6);
            Slider trackSlider = positiveSlider.FindControl<Slider>("PART_Slider")!;
            Assert.Equal(20.0, trackSlider.Value, 6);
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(trackSlider) as string));

            // 2. Up / Shift+Up nudges above slider Maximum without snapping back to 20.0.
            TextBox ampBox = positiveSlider.FindControl<TextBox>("PART_ValueTextBox")!;
            ampBox.Focus();
            TestAppHelpers.PumpUi();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(50.1, positiveSlider.Value, 6);

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            TestAppHelpers.PumpUi();
            Assert.Equal(51.1, positiveSlider.Value, 6);

            // 3. Tiny positive below slider Minimum preserves significant digits instead of formatting "0.000".
            SubmitSliderText(window, strictPositiveSlider, "0.0001");
            Assert.False(strictPositiveSlider.HasValidationError);
            Assert.Equal(0.0001, strictPositiveSlider.Value, 8);
            TextBox freqBox = strictPositiveSlider.FindControl<TextBox>("PART_ValueTextBox")!;
            Assert.Equal(0.0001.ToString("0.############", CultureInfo.CurrentCulture), freqBox.Text);

            // 4. Open-ended integer above slider Maximum is accepted.
            SubmitSliderText(window, integerSlider, "256");
            Assert.False(integerSlider.HasValidationError);
            Assert.Equal(256.0, integerSlider.Value, 6);
            Assert.Equal("256", integerSlider.FindControl<TextBox>("PART_ValueTextBox")!.Text);

            // 5. Unbounded signed slider accepts both negative and positive values outside [-128, 127].
            SubmitSliderText(window, signedSlider, "-500");
            Assert.False(signedSlider.HasValidationError);
            Assert.Equal(-500.0, signedSlider.Value, 6);
            SubmitSliderText(window, signedSlider, "500");
            Assert.False(signedSlider.HasValidationError);
            Assert.Equal(500.0, signedSlider.Value, 6);

            // 6. Auto-expanding slider track bounds for large image-adapted BaselineValue.
            integerSlider.BaselineValue = 120.0;
            TestAppHelpers.PumpUi();
            Assert.Equal(120.0, integerSlider.FindControl<Slider>("PART_Slider")!.Maximum, 6);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaTheory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("")]
    public void NumericSlider_RejectsMalformedOrNonFiniteInputs_WithVisibleErrorAffordance(string invalidInput)
    {
        var slider = new NumericSlider
        {
            Label = "Edge Intensity",
            Minimum = 0.0,
            Maximum = 5.0,
            ValidMinimum = 0.0,
            Step = 0.05,
            FormatString = "F2",
            BaselineValue = 1.8,
            Value = 2.5,
        };

        var otherFocusTarget = new Button { Content = "Blur Target" };
        var window = new Window
        {
            Width = 340,
            Height = 240,
            Content = new StackPanel { Children = { slider, otherFocusTarget } },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            TextBox valueBox = slider.FindControl<TextBox>("PART_ValueTextBox")!;
            TextBlock errorBlock = slider.FindControl<TextBlock>("PART_ValidationErrorText")!;

            // Press Enter on invalid entry -> retains invalid text in box + shows inline error + red border class.
            SubmitSliderText(window, slider, invalidInput);
            Assert.True(slider.HasValidationError);
            Assert.False(string.IsNullOrWhiteSpace(slider.ValidationError));
            Assert.Contains("input-invalid", valueBox.Classes);
            Assert.True(errorBlock.IsVisible);
            Assert.StartsWith("\u26A0 ", errorBlock.Text);
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(valueBox) as string));
            Assert.Equal(2.5, slider.Value, 6);
            Assert.Equal(invalidInput, valueBox.Text);

            // Typing new text clears error state immediately.
            valueBox.Text = "3.0";
            TestAppHelpers.PumpUi();
            Assert.False(slider.HasValidationError);
            Assert.DoesNotContain("input-invalid", valueBox.Classes);
            Assert.False(errorBlock.IsVisible);

            // Enter invalid again then Escape -> reverts text and clears error.
            SubmitSliderText(window, slider, invalidInput);
            Assert.True(slider.HasValidationError);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.False(slider.HasValidationError);
            Assert.Equal(2.5.ToString("F2", CultureInfo.CurrentCulture), valueBox.Text);

            // Invalid text followed by LostFocus -> reverts text to active valid value AND shows Rejected banner.
            valueBox.Focus();
            TestAppHelpers.PumpUi();
            Assert.True(valueBox.IsFocused, "Expected valueBox to hold focus before setting invalid text.");
            valueBox.Text = invalidInput;
            TestAppHelpers.PumpUi();
            bool moved = otherFocusTarget.Focus();
            TestAppHelpers.PumpUi();

            Assert.True(moved, "Expected otherFocusTarget.Focus() to return true.");
            Assert.False(valueBox.IsFocused, "Expected valueBox to lose focus.");
            Assert.Equal(2.5.ToString("F2", CultureInfo.CurrentCulture), valueBox.Text);
            Assert.True(slider.HasValidationError);
            Assert.True(errorBlock.IsVisible);
            Assert.Contains("input-invalid", valueBox.Classes);
            Assert.Equal(2.5, slider.Value, 6);

            // ResetToBaseline clears the LostFocus error banner.
            slider.ResetToBaseline();
            TestAppHelpers.PumpUi();
            Assert.False(slider.HasValidationError);
            Assert.False(errorBlock.IsVisible);
            Assert.Equal(1.8, slider.Value, 6);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void NumericSlider_EnforcesExclusiveBoundsClosedCapsAndIntegerDomainRules()
    {
        var exclusivePositive = new NumericSlider
        {
            Label = "Range Radius",
            Minimum = 0.5,
            Maximum = 30.0,
            ValidMinimum = 0.0,
            IsValidMinimumExclusive = true,
            Value = 5.5,
        };

        var cappedDouble = new NumericSlider
        {
            Label = "Overlap Darkening",
            Minimum = 0.0,
            Maximum = 1.0,
            ValidMinimum = 0.0,
            ValidMaximum = 1.0,
            Value = 0.5,
        };

        var intSlider = new NumericSlider
        {
            Label = "Scatter Radius",
            Minimum = 1.0,
            Maximum = 20.0,
            ValidMinimum = 1.0,
            IsInteger = true,
            FormatString = "F0",
            Value = 4.0,
        };

        var window = new Window
        {
            Width = 340,
            Height = 320,
            Content = new StackPanel { Children = { exclusivePositive, cappedDouble, intSlider } },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            // Exclusive lower bound: 0 and -1 fail; 0.0001 succeeds.
            SubmitSliderText(window, exclusivePositive, "0");
            Assert.True(exclusivePositive.HasValidationError);
            Assert.Contains("greater than", exclusivePositive.ValidationError, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5.5, exclusivePositive.Value, 6);

            SubmitSliderText(window, exclusivePositive, "-1");
            Assert.True(exclusivePositive.HasValidationError);
            Assert.Equal(5.5, exclusivePositive.Value, 6);

            SubmitSliderText(window, exclusivePositive, "0.0001");
            Assert.False(exclusivePositive.HasValidationError);
            Assert.Equal(0.0001, exclusivePositive.Value, 8);

            // Closed [0, 1] rejects -0.01 and 1.01, accepts 0 and 1.
            SubmitSliderText(window, cappedDouble, "1.01");
            Assert.True(cappedDouble.HasValidationError);
            Assert.Equal(0.5, cappedDouble.Value, 6);

            SubmitSliderText(window, cappedDouble, "-0.01");
            Assert.True(cappedDouble.HasValidationError);
            Assert.Equal(0.5, cappedDouble.Value, 6);

            SubmitSliderText(window, cappedDouble, "1");
            Assert.False(cappedDouble.HasValidationError);
            Assert.Equal(1.0, cappedDouble.Value, 6);

            // Integer slider rejects decimals, below ValidMinimum, and 32-bit int overflow.
            SubmitSliderText(window, intSlider, "3.5");
            Assert.True(intSlider.HasValidationError);
            Assert.Equal(4.0, intSlider.Value, 6);

            SubmitSliderText(window, intSlider, "0");
            Assert.True(intSlider.HasValidationError);
            Assert.Equal(4.0, intSlider.Value, 6);

            SubmitSliderText(window, intSlider, "99999999999");
            Assert.True(intSlider.HasValidationError);
            Assert.Equal(4.0, intSlider.Value, 6);

            SubmitSliderText(window, intSlider, "42");
            Assert.False(intSlider.HasValidationError);
            Assert.Equal(42.0, intSlider.Value, 6);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void FinishingToneCurve_EnforcesCoupledInvariantViaUiAndAllowsFinePrecisionOnViewModel()
    {
        var stageVm = new FinishingStageViewModel();
        stageVm.MaxTone = 0.85;
        stageVm.MinTone = 0.8495;
        stageVm.ShadowKnee = 0.0002;
        stageVm.HighlightKnee = 0.9998;

        Assert.Equal(0.8495, stageVm.MinTone, 6);
        Assert.Equal(0.0002, stageVm.ShadowKnee, 6);
        Assert.Equal(0.9998, stageVm.HighlightKnee, 6);
        stageVm.ToToneCurveOptions().Validate();

        var freshVm = new FinishingStageViewModel();
        var view = new FinishingInspectorView { DataContext = freshVm };
        var window = new Window { Width = 360, Height = 850, Content = view };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            NumericSlider minTone = FindSliderByLabel(view, "Min Tone");
            NumericSlider maxTone = FindSliderByLabel(view, "Max Tone");
            NumericSlider shadowKnee = FindSliderByLabel(view, "Shadow Knee");
            NumericSlider highlightKnee = FindSliderByLabel(view, "Highlight Knee");

            // Coupled upper/lower invariants reject invalid crossovers with inline affordance.
            SubmitSliderText(window, minTone, "0.96");
            Assert.True(minTone.HasValidationError);
            Assert.Equal(0.05, freshVm.MinTone, 6);

            SubmitSliderText(window, maxTone, "0.05");
            Assert.True(maxTone.HasValidationError);
            Assert.Equal(0.96, freshVm.MaxTone, 6);

            SubmitSliderText(window, shadowKnee, "0");
            Assert.True(shadowKnee.HasValidationError);
            Assert.Equal(0.15, freshVm.ShadowKnee, 6);

            SubmitSliderText(window, shadowKnee, "0.85");
            Assert.True(shadowKnee.HasValidationError);
            Assert.Equal(0.15, freshVm.ShadowKnee, 6);

            SubmitSliderText(window, highlightKnee, "0.15");
            Assert.True(highlightKnee.HasValidationError);
            Assert.Equal(0.85, freshVm.HighlightKnee, 6);

            SubmitSliderText(window, highlightKnee, "1");
            Assert.True(highlightKnee.HasValidationError);
            Assert.Equal(0.85, freshVm.HighlightKnee, 6);

            // Fine valid value within coupled bounds succeeds verbatim.
            SubmitSliderText(window, shadowKnee, "0.0002");
            Assert.False(shadowKnee.HasValidationError);
            Assert.Equal(0.0002, freshVm.ShadowKnee, 6);
            freshVm.ToToneCurveOptions().Validate();
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void TextInputs_ForSeedsAndPaperColorHex_ShowErrorAffordancesOnInvalidEntries()
    {
        var wetVm = new WetInWetStageViewModel();
        var handVm = new HandTremorStageViewModel();

        var wetView = new WetInWetInspectorView { DataContext = wetVm };
        var handView = new HandTremorInspectorView { DataContext = handVm };

        var window = new Window
        {
            Width = 360,
            Height = 850,
            Content = new StackPanel { Children = { wetView, handView } },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            foreach (Expander expander in window.GetVisualDescendants().OfType<Expander>())
            {
                expander.IsExpanded = true;
            }

            TestAppHelpers.PumpUi();

            // 1. PerlinNoiseEditor inside HandTremorInspectorView.
            PerlinNoiseEditor noiseEditor = handView.GetVisualDescendants().OfType<PerlinNoiseEditor>().First();
            TextBox noiseSeedBox = noiseEditor.FindControl<TextBox>("PART_SeedTextBox")!;
            TextBlock noiseSeedErr = noiseEditor.FindControl<TextBlock>("PART_SeedErrorText")!;
            int originalNoiseSeed = handVm.PrimaryNoise.Seed;

            SubmitTextBox(window, noiseSeedBox, "not-an-int");
            Assert.False(string.IsNullOrWhiteSpace(noiseEditor.SeedValidationError));
            Assert.Contains("input-invalid", noiseSeedBox.Classes);
            Assert.True(noiseSeedErr.IsVisible);
            Assert.Equal(originalNoiseSeed, handVm.PrimaryNoise.Seed);

            SubmitTextBox(window, noiseSeedBox, "99999999999");
            Assert.False(string.IsNullOrWhiteSpace(noiseEditor.SeedValidationError));
            Assert.Equal(originalNoiseSeed, handVm.PrimaryNoise.Seed);

            SubmitTextBox(window, noiseSeedBox, "-42");
            Assert.Null(noiseEditor.SeedValidationError);
            Assert.DoesNotContain("input-invalid", noiseSeedBox.Classes);
            Assert.False(noiseSeedErr.IsVisible);
            Assert.Equal(-42, handVm.PrimaryNoise.Seed);

            // 2. WetInWetInspectorView Seed.
            TextBox wetSeedBox = wetView.FindControl<TextBox>("PART_SeedTextBox")!;
            TextBlock wetSeedErr = wetView.FindControl<TextBlock>("PART_SeedErrorText")!;
            int originalWetSeed = wetVm.Seed;

            SubmitTextBox(window, wetSeedBox, "3.14");
            Assert.False(string.IsNullOrWhiteSpace(wetView.SeedValidationError));
            Assert.Contains("input-invalid", wetSeedBox.Classes);
            Assert.True(wetSeedErr.IsVisible);
            Assert.Equal(originalWetSeed, wetVm.Seed);

            SubmitTextBox(window, wetSeedBox, "777");
            Assert.Null(wetView.SeedValidationError);
            Assert.False(wetSeedErr.IsVisible);
            Assert.Equal(777, wetVm.Seed);

            // 3. HandTremorInspectorView PaperColorHex.
            TextBox hexBox = handView.FindControl<TextBox>("PART_PaperColorHexTextBox")!;
            TextBlock hexErr = handView.FindControl<TextBlock>("PART_PaperColorHexErrorText")!;
            Rgb8 originalPaper = handVm.PaperColor;

            SubmitTextBox(window, hexBox, "#GGGGGG");
            Assert.False(string.IsNullOrWhiteSpace(handView.PaperColorHexValidationError));
            Assert.Contains("input-invalid", hexBox.Classes);
            Assert.True(hexErr.IsVisible);
            Assert.Equal(originalPaper, handVm.PaperColor);

            SubmitTextBox(window, hexBox, "#123");
            Assert.False(string.IsNullOrWhiteSpace(handView.PaperColorHexValidationError));
            Assert.Equal(originalPaper, handVm.PaperColor);

            SubmitTextBox(window, hexBox, "#1A2B3C");
            Assert.Null(handView.PaperColorHexValidationError);
            Assert.DoesNotContain("input-invalid", hexBox.Classes);
            Assert.False(hexErr.IsVisible);
            Assert.Equal(new Rgb8(0x1A, 0x2B, 0x3C), handVm.PaperColor);

            // LostFocus after invalid hex entry -> reverts to valid hex AND keeps Rejected banner after UI pump.
            hexBox.Focus();
            TestAppHelpers.PumpUi();
            hexBox.Text = "bad-hex";
            TestAppHelpers.PumpUi();
            wetSeedBox.Focus();
            TestAppHelpers.PumpUi();

            Assert.Equal("#1A2B3C", hexBox.Text);
            Assert.Equal(new Rgb8(0x1A, 0x2B, 0x3C), handVm.PaperColor);
            Assert.False(string.IsNullOrWhiteSpace(handView.PaperColorHexValidationError));
            Assert.Contains("Rejected", handView.PaperColorHexValidationError, StringComparison.Ordinal);
            Assert.Contains("input-invalid", hexBox.Classes);
            Assert.True(hexErr.IsVisible);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void AllStageInspectors_SweepEverySlider_VerifyValidDomainAndLeiterValidation()
    {
        var parameters = new PipelineParametersViewModel();
        parameters.ColorTransfer.PaletteMode = PaletteMode.Custom;
        parameters.Saliency.SaliencyDistanceField.UseExplicitSmoothingSigma = true;
        parameters.Saliency.SaliencyCut.UseMaxWorkingDimension = true;

        var views = new (string Name, Control View, object ViewModel)[]
        {
            ("ColorTransfer", new ColorTransferInspectorView(), parameters.ColorTransfer),
            ("Saliency", new SaliencyInspectorView(), parameters.Saliency),
            ("Segmentation", new SegmentationInspectorView(), parameters.Segmentation),
            ("LodAbstraction", new LodAbstractionInspectorView(), parameters.LodAbstraction),
            ("WetInWet", new WetInWetInspectorView(), parameters.WetInWet),
            ("HandTremor", new HandTremorInspectorView(), parameters.HandTremor),
            ("Finishing", new FinishingInspectorView(), parameters.Finishing),
        };

        var window = new Window
        {
            Width = 380,
            Height = 900,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            foreach ((string stageName, Control view, object stageVm) in views)
            {
                view.DataContext = stageVm;
                window.Content = view;
                TestAppHelpers.PumpUi();

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
                    // Every inspector slider must reject non-finite/garbage input.
                    double initialValue = slider.Value;
                    SubmitSliderText(window, slider, "invalid-text");
                    Assert.True(
                        slider.HasValidationError,
                        $"{stageName}/{slider.Label} failed to reject garbage text.");
                    Assert.Equal(initialValue, slider.Value, 6);

                    // Clear via Escape.
                    window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                    TestAppHelpers.PumpUi();
                    Assert.False(slider.HasValidationError);

                    // Open-ended upper sliders must accept values significantly beyond slider Maximum.
                    if (double.IsPositiveInfinity(slider.ValidMaximum))
                    {
                        double candidate = slider.IsInteger
                            ? Math.Ceiling(slider.Maximum) + 25.0
                            : slider.Maximum * 2.5 + 5.0;

                        SubmitSliderText(window, slider, candidate.ToString("G12", CultureInfo.InvariantCulture));
                        Assert.False(
                            slider.HasValidationError,
                            $"{stageName}/{slider.Label} unexpectedly rejected {candidate}.");
                        Assert.Equal(candidate, slider.Value, 6);
                    }
                    else
                    {
                        // Bounded upper sliders must reject out-of-range values above ValidMaximum.
                        double overMax = slider.ValidMaximum + (slider.IsInteger ? 1.0 : 0.25);
                        double beforeOverMax = slider.Value;
                        SubmitSliderText(window, slider, overMax.ToString("G12", CultureInfo.InvariantCulture));
                        Assert.True(
                            slider.HasValidationError,
                            $"{stageName}/{slider.Label} unexpectedly accepted {overMax} above ValidMaximum {slider.ValidMaximum}.");
                        Assert.Equal(beforeOverMax, slider.Value, 6);
                        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                        TestAppHelpers.PumpUi();
                    }

                    // Bounded lower sliders must reject values below (or equal if exclusive) ValidMinimum.
                    if (!double.IsNegativeInfinity(slider.ValidMinimum))
                    {
                        double invalidLow = slider.IsValidMinimumExclusive
                            ? slider.ValidMinimum
                            : slider.ValidMinimum - (slider.IsInteger ? 1.0 : 0.25);
                        double beforeLow = slider.Value;
                        SubmitSliderText(window, slider, invalidLow.ToString("G12", CultureInfo.InvariantCulture));
                        Assert.True(
                            slider.HasValidationError,
                            $"{stageName}/{slider.Label} unexpectedly accepted {invalidLow} below ValidMinimum {slider.ValidMinimum}.");
                        Assert.Equal(beforeLow, slider.Value, 6);
                        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                        TestAppHelpers.PumpUi();
                    }
                }
            }

            // Full Leiter pipeline options produced after pushing every open-ended slider past slider Maximum must validate cleanly.
            parameters.BuildOptions().Validate();
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    private static void SubmitSliderText(Window window, NumericSlider slider, string text)
    {
        TextBox textBox = slider.FindControl<TextBox>("PART_ValueTextBox")!;
        SubmitTextBox(window, textBox, text);
    }

    private static void SubmitTextBox(Window window, TextBox textBox, string text)
    {
        textBox.Focus();
        TestAppHelpers.PumpUi();
        textBox.Text = text;
        TestAppHelpers.PumpUi();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestAppHelpers.PumpUi();
    }

    private static NumericSlider FindSliderByLabel(Control root, string label)
    {
        return root.GetVisualDescendants()
            .OfType<NumericSlider>()
            .First(s => string.Equals(s.Label, label, StringComparison.Ordinal));
    }
}

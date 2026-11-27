using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Leiter.Algorithms.Noise;
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
    public void NumericSlider_AcceptsValidTypedValuesBeyondSliderRange_ExpandsTrackAndNudges()
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

            // 1. Typed value above slider Maximum is accepted verbatim AND track expands dynamically to accommodate it.
            SubmitSliderText(window, positiveSlider, "50");
            Assert.False(positiveSlider.HasValidationError);
            Assert.Equal(50.0, positiveSlider.Value, 6);
            Slider trackSlider = positiveSlider.FindControl<Slider>("PART_Slider")!;
            Assert.True(trackSlider.Maximum >= 50.0 * 1.15 - 1e-6);
            Assert.Equal(50.0, trackSlider.Value, 6);

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
            Assert.Equal(0.0001.ToString("0.00##", CultureInfo.CurrentCulture), freqBox.Text);

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

            // 6. Auto-expanding slider track bounds for large image-adapted BaselineValue with headroom (1.5x).
            integerSlider.BaselineValue = 120.0;
            integerSlider.Value = 120.0;
            TestAppHelpers.PumpUi();
            Assert.Equal(180.0, integerSlider.FindControl<Slider>("PART_Slider")!.Maximum, 6);
            Border baselineTick = integerSlider.FindControl<Border>("PART_BaselineTick")!;
            Assert.True(baselineTick.IsVisible);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void NumericSlider_FormatsImageAdaptedFloatsCleanly_AndPreservesPrecisionOnFocusBlur()
    {
        var sliderF2 = new NumericSlider
        {
            Label = "Edge Intensity",
            Minimum = 0.0,
            Maximum = 5.0,
            ValidMinimum = 0.0,
            Step = 0.05,
            FormatString = "F2",
            BaselineValue = 11.039142,
            Value = 11.039142,
        };

        var sliderSmall = new NumericSlider
        {
            Label = "Frequency",
            Minimum = 0.001,
            Maximum = 0.5,
            ValidMinimum = 0.0,
            IsValidMinimumExclusive = true,
            Step = 0.001,
            FormatString = "F3",
            BaselineValue = 0.0067115,
            Value = 0.0067115,
        };

        var blurButton = new Button { Content = "Other" };
        var window = new Window
        {
            Width = 340,
            Height = 260,
            Content = new StackPanel { Children = { sliderF2, sliderSmall, blurButton } },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            TextBox boxF2 = sliderF2.FindControl<TextBox>("PART_ValueTextBox")!;
            TextBox boxSmall = sliderSmall.FindControl<TextBox>("PART_ValueTextBox")!;

            Assert.Equal(11.039142.ToString("F2", CultureInfo.CurrentCulture), boxF2.Text);
            Assert.Equal(0.0067115.ToString("0.00##", CultureInfo.CurrentCulture), boxSmall.Text);
            Assert.False(sliderF2.IsModified);
            Assert.False(sliderSmall.IsModified);

            // Focusing and blurring without editing must NOT overwrite exact precision or mark modified.
            boxF2.Focus();
            TestAppHelpers.PumpUi();
            blurButton.Focus();
            TestAppHelpers.PumpUi();

            Assert.Equal(11.039142, sliderF2.Value, 8);
            Assert.False(sliderF2.IsModified);

            // Verify additional representative image-adapted values never emit 12-digit unclipped strings.
            sliderF2.Value = 5.065667;
            TestAppHelpers.PumpUi();
            Assert.Equal(5.065667.ToString("F2", CultureInfo.CurrentCulture), boxF2.Text);

            sliderF2.Value = 59.59608;
            TestAppHelpers.PumpUi();
            Assert.Equal(59.59608.ToString("F2", CultureInfo.CurrentCulture), boxF2.Text);

            sliderF2.Value = 0.1318915;
            TestAppHelpers.PumpUi();
            Assert.Equal(0.1318915.ToString("F2", CultureInfo.CurrentCulture), boxF2.Text);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void NumericSlider_ExpandedTrackDrag_DoesNotSnapToHardcodedMax_AndShiftDragFineTunes()
    {
        var slider = new NumericSlider
        {
            Label = "Range Radius",
            Minimum = 0.0,
            Maximum = 10.0,
            ValidMinimum = 0.0,
            Step = 0.05,
            FormatString = "F2",
            BaselineValue = 20.0,
            Value = 20.0,
        };

        var window = new Window
        {
            Width = 340,
            Height = 160,
            Content = slider,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Slider track = slider.FindControl<Slider>("PART_Slider")!;
            Assert.Equal(30.0, track.Maximum, 6);

            Point trackLeft = track.TranslatePoint(new Point(10, track.Bounds.Height * 0.5), window)!.Value;
            Point trackMid = track.TranslatePoint(new Point(track.Bounds.Width * 0.5, track.Bounds.Height * 0.5), window)!.Value;
            Point trackThreeQuarter = track.TranslatePoint(new Point(track.Bounds.Width * 0.75, track.Bounds.Height * 0.5), window)!.Value;

            // Normal click/drag to ~75% of expanded [0, 30] track -> stays well above hardcoded Maximum=10.
            window.MouseDown(trackThreeQuarter, MouseButton.Left);
            window.MouseMove(new Point(trackThreeQuarter.X + 4, trackThreeQuarter.Y));
            window.MouseUp(new Point(trackThreeQuarter.X + 4, trackThreeQuarter.Y), MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.True(slider.Value > 15.0, $"Expected drag on expanded track to remain > 15, got {slider.Value}");

            // Shift+drag across 40px produces ~10x finer delta than full-track normal drag.
            slider.Value = 15.0;
            TestAppHelpers.PumpUi();

            window.MouseDown(trackMid, MouseButton.Left, RawInputModifiers.Shift);
            window.MouseMove(new Point(trackMid.X + 40, trackMid.Y), RawInputModifiers.Shift);
            window.MouseUp(new Point(trackMid.X + 40, trackMid.Y), MouseButton.Left, RawInputModifiers.Shift);
            TestAppHelpers.PumpUi();

            double shiftDelta = slider.Value - 15.0;
            double fullTrackDelta = (40.0 / Math.Max(1.0, track.Bounds.Width - 16.0)) * 30.0;
            Assert.InRange(shiftDelta, 0.01, fullTrackDelta * 0.25);

            // Reset leaves clean baseline.
            Assert.True(trackLeft.X < trackMid.X);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void NumericSlider_MouseWheel_NudgesOnlyWhenTextBoxFocusedOrCtrlHeld()
    {
        var slider = new NumericSlider
        {
            Label = "Edge Intensity",
            Minimum = 0.0,
            Maximum = 5.0,
            ValidMinimum = 0.0,
            Step = 0.1,
            FormatString = "F2",
            BaselineValue = 1.0,
            Value = 1.0,
        };

        var other = new Button { Content = "Other" };
        var window = new Window
        {
            Width = 340,
            Height = 200,
            Content = new StackPanel { Children = { slider, other } },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            other.Focus();
            TestAppHelpers.PumpUi();

            // Unfocused without Ctrl -> wheel does NOT hijack scrolling / mutate Value.
            Point sliderCenter = slider.TranslatePoint(
                new Point(slider.Bounds.Width * 0.5, slider.Bounds.Height * 0.5),
                window)!.Value;
            window.MouseWheel(sliderCenter, new Vector(0, 1), RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(1.0, slider.Value, 6);

            // Ctrl+Wheel on slider -> nudges by Step.
            window.MouseWheel(sliderCenter, new Vector(0, 1), RawInputModifiers.Control);
            TestAppHelpers.PumpUi();
            Assert.Equal(1.1, slider.Value, 6);

            // Focused TextBox + Wheel -> nudges by Step.
            TextBox box = slider.FindControl<TextBox>("PART_ValueTextBox")!;
            box.Focus();
            TestAppHelpers.PumpUi();
            Point boxCenter = box.TranslatePoint(new Point(box.Bounds.Width * 0.5, box.Bounds.Height * 0.5), window)!.Value;
            window.MouseWheel(boxCenter, new Vector(0, 1), RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(1.2, slider.Value, 6);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void PerlinNoiseEditor_GeneratesLiveNoisePreview_AndSupportsPerSeedReset()
    {
        var noiseVm = new PerlinNoiseViewModel(new PerlinNoiseOptions(Seed: 404, Frequency: 0.0067115, Octaves: 4));
        var editor = new PerlinNoiseEditor
        {
            Width = 320,
            Title = "Turbulence Noise",
            DataContext = noiseVm,
        };

        var window = new Window
        {
            Width = 350,
            Height = 420,
            Content = editor,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            WriteableBitmap? initialBitmap = editor.NoisePreviewBitmap;
            Assert.NotNull(initialBitmap);
            Assert.Equal(new PixelSize(56, 36), initialBitmap!.PixelSize);

            // Frequency formats cleanly with F4 without 12-digit fallback.
            NumericSlider freqSlider = editor.FindControl<NumericSlider>("PART_FrequencySlider")!;
            TextBox freqBox = freqSlider.FindControl<TextBox>("PART_ValueTextBox")!;
            Assert.Equal(0.0067115.ToString("F4", CultureInfo.CurrentCulture), freqBox.Text);

            // Modify both Frequency and Seed, verify preview regenerates and per-seed reset only resets Seed.
            noiseVm.Frequency = 0.08;
            noiseVm.Seed = 9999;
            TestAppHelpers.PumpUi();

            Assert.NotNull(editor.NoisePreviewBitmap);
            Assert.NotSame(initialBitmap, editor.NoisePreviewBitmap);

            Button resetSeedButton = editor.FindControl<Button>("PART_ResetSeedButton")!;
            Avalonia.Controls.Shapes.Ellipse seedModifiedDot = editor.FindControl<Avalonia.Controls.Shapes.Ellipse>("PART_SeedModifiedDot")!;
            Assert.True(noiseVm.IsSeedModified);
            Assert.True(seedModifiedDot.IsVisible);
            Assert.True(resetSeedButton.IsEnabled);
            resetSeedButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            TestAppHelpers.PumpUi();

            Assert.Equal(404, noiseVm.Seed);
            Assert.Equal(0.08, noiseVm.Frequency, 6);
            Assert.False(noiseVm.IsSeedModified);
            Assert.False(seedModifiedDot.IsVisible);
            Assert.False(resetSeedButton.IsEnabled);
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
            view.FocusSection("ToneCurve");
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
                    if (view is ColorTransferInspectorView colorTransferView && !slider.IsEffectivelyVisible)
                    {
                        colorTransferView.ShowSpreadStats = !colorTransferView.ShowSpreadStats;
                        TestAppHelpers.PumpUi();
                    }

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

    [AvaloniaFact]
    public void NumericSlider_ExpandsNearMaximumSpatialHeadroom_WhileKeepingBoundedRatiosAndBytesCapped()
    {
        var spatialIntSlider = new NumericSlider
        {
            Label = "Salient Kernel Size",
            Minimum = 3.0,
            Maximum = 25.0,
            ValidMinimum = 3.0,
            Step = 2.0,
            IsInteger = true,
            FormatString = "F0",
            BaselineValue = 24.0,
            Value = 24.0,
        };

        var amplitudeFloatSlider = new NumericSlider
        {
            Label = "Amplitude",
            Minimum = 0.0,
            Maximum = 20.0,
            ValidMinimum = 0.0,
            Step = 0.1,
            FormatString = "F2",
            BaselineValue = 19.67,
            Value = 19.67,
        };

        var normalizedBoundedSlider = new NumericSlider
        {
            Label = "Max Tone",
            Minimum = 0.01,
            Maximum = 1.0,
            ValidMinimum = 0.05,
            ValidMaximum = 1.0,
            Step = 0.01,
            FormatString = "F2",
            BaselineValue = 0.96,
            Value = 0.96,
        };

        var normalizedRatioWithoutValidMax = new NumericSlider
        {
            Label = "Persistence",
            Minimum = 0.0,
            Maximum = 1.0,
            ValidMinimum = 0.0,
            Step = 0.01,
            FormatString = "F2",
            BaselineValue = 0.96,
            Value = 0.96,
        };

        var byteChannelSlider = new NumericSlider
        {
            Label = "Paper Red",
            Minimum = 0.0,
            Maximum = 255.0,
            ValidMinimum = 0.0,
            ValidMaximum = 255.0,
            Step = 1.0,
            IsInteger = true,
            FormatString = "F0",
            BaselineValue = 248.0,
            Value = 248.0,
        };

        var window = new Window
        {
            Width = 360,
            Height = 460,
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    spatialIntSlider,
                    amplitudeFloatSlider,
                    normalizedBoundedSlider,
                    normalizedRatioWithoutValidMax,
                    byteChannelSlider,
                },
            },
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Slider spatialTrack = spatialIntSlider.FindControl<Slider>("PART_Slider")!;
            Assert.InRange(spatialTrack.Maximum, 32.0, 34.0);
            Assert.Equal(33.0, spatialTrack.Maximum, 6);

            Slider amplitudeTrack = amplitudeFloatSlider.FindControl<Slider>("PART_Slider")!;
            Assert.InRange(amplitudeTrack.Maximum, 26.0, 28.0);
            Assert.Equal(27.0, amplitudeTrack.Maximum, 6);

            Slider normalizedTrack = normalizedBoundedSlider.FindControl<Slider>("PART_Slider")!;
            Assert.Equal(1.0, normalizedTrack.Maximum, 6);

            Slider ratioNoValidMaxTrack = normalizedRatioWithoutValidMax.FindControl<Slider>("PART_Slider")!;
            Assert.Equal(1.0, ratioNoValidMaxTrack.Maximum, 6);

            Slider byteTrack = byteChannelSlider.FindControl<Slider>("PART_Slider")!;
            Assert.Equal(255.0, byteTrack.Maximum, 6);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void ColorTransferInspector_CustomMode_PairsReferenceExtractionInActivePalette_AndTogglesMeanVsSpread()
    {
        var stageVm = new ColorTransferStageViewModel
        {
            PaletteMode = PaletteMode.Custom,
        };
        var view = new ColorTransferInspectorView
        {
            DataContext = stageVm,
        };

        var window = new Window
        {
            Width = 360,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Border activePaletteCard = view.FindControl<Border>("PART_ActivePaletteCard")!;
            StackPanel customSection = view.FindControl<StackPanel>("PART_CustomSection")!;
            Assert.Contains(customSection, activePaletteCard.GetVisualDescendants());
            Assert.True(customSection.IsVisible);

            Expander advancedExpander = view.FindControl<Expander>("PART_AdvancedExpander")!;
            advancedExpander.IsExpanded = true;
            TestAppHelpers.PumpUi();

            var meanTab = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("PART_MeanLabTabButton")!;
            var stdDevTab = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("PART_StdDevLabTabButton")!;
            StackPanel meanPanel = view.FindControl<StackPanel>("PART_MeanLabPanel")!;
            StackPanel stdDevPanel = view.FindControl<StackPanel>("PART_StdDevLabPanel")!;

            Assert.True(meanTab.IsChecked == true);
            Assert.False(stdDevTab.IsChecked == true);
            Assert.True(meanPanel.IsVisible);
            Assert.False(stdDevPanel.IsVisible);

            stdDevTab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            TestAppHelpers.PumpUi();
            Assert.False(meanTab.IsChecked == true);
            Assert.True(stdDevTab.IsChecked == true);
            Assert.False(meanPanel.IsVisible);
            Assert.True(stdDevPanel.IsVisible);

            meanTab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            TestAppHelpers.PumpUi();
            Assert.True(meanTab.IsChecked == true);
            Assert.False(stdDevTab.IsChecked == true);
            Assert.True(meanPanel.IsVisible);
            Assert.False(stdDevPanel.IsVisible);
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

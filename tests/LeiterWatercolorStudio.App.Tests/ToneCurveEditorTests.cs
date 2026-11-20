using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Leiter.Algorithms.Stylization;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.Core.Parameters.Stages;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class ToneCurveEditorTests
{
    [AvaloniaFact]
    public void ToneCurveEditor_RendersInsideWindow_WithoutErrors()
    {
        var vm = new FinishingStageViewModel();
        var editor = CreateBoundEditor(vm);

        var window = new Window
        {
            Width = 360,
            Height = 240,
            Content = editor,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Assert.True(editor.Bounds.Width >= 120);
            Assert.True(editor.Bounds.Height >= 140);

            Rect plot = editor.GetPlotRect();
            Assert.True(plot.Width > 0);
            Assert.True(plot.Height > 0);

            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.True(frame!.PixelSize.Width > 0 && frame.PixelSize.Height > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ToneCurveEditor_HandlePointerDrags_ClampBounds_UpdateTwoWayBindings_AndFireDragEvents()
    {
        var vm = new FinishingStageViewModel();
        int dragStartedEvents = 0;
        int dragCompletedEvents = 0;
        int dragStartedCommands = 0;
        int dragCompletedCommands = 0;

        var editor = CreateBoundEditor(vm);
        editor.DragStarted += (_, _) => dragStartedEvents++;
        editor.DragCompleted += (_, _) => dragCompletedEvents++;
        editor.DragStartedCommand = new RelayCommand(() => dragStartedCommands++);
        editor.DragCompletedCommand = new RelayCommand(() => dragCompletedCommands++);

        var window = new Window
        {
            Width = 400,
            Height = 260,
            Content = editor,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Rect plot = editor.GetPlotRect();

            // 1. Drag MinTone handle: update to ~0.25, then test clamping at [0, MaxTone)
            Point minStart = editor.CurveToScreen(0.0, editor.MinTone);
            Point minTargetMid = editor.CurveToScreen(0.0, 0.25);
            Point minTargetTooHigh = new(minStart.X, plot.Top - 40);
            Point minTargetTooLow = new(minStart.X, plot.Bottom + 40);

            SimulateDrag(
                window,
                editor,
                minStart,
                minTargetMid,
                () =>
                {
                    Assert.Equal(1, dragStartedEvents);
                    Assert.Equal(1, dragStartedCommands);
                    Assert.InRange(vm.MinTone, 0.20, 0.30);
                    Assert.Equal(editor.MinTone, vm.MinTone, 4);
                },
                minTargetTooHigh,
                () =>
                {
                    Assert.InRange(editor.MinTone, 0.0, editor.MaxTone - 0.019);
                    Assert.True(editor.MinTone < editor.MaxTone);
                },
                minTargetTooLow,
                () =>
                {
                    Assert.Equal(0.0, editor.MinTone, 4);
                    Assert.Equal(0.0, vm.MinTone, 4);
                });

            Assert.Equal(1, dragCompletedEvents);
            Assert.Equal(1, dragCompletedCommands);

            // Reset MinTone via VM to verify TwoWay propagation into editor
            vm.MinTone = 0.20;
            TestAppHelpers.PumpUi();
            Assert.Equal(0.20, editor.MinTone, 4);

            // 2. Drag MaxTone handle: test clamping below MinTone and above 1.0
            Point maxStart = editor.CurveToScreen(1.0, editor.MaxTone);
            Point maxTargetTooLow = new(maxStart.X, plot.Bottom + 40);
            Point maxTargetTooHigh = new(maxStart.X, plot.Top - 40);
            Point maxTargetMid = editor.CurveToScreen(1.0, 0.88);

            SimulateDrag(
                window,
                editor,
                maxStart,
                maxTargetTooLow,
                () =>
                {
                    Assert.True(editor.MaxTone > editor.MinTone);
                    Assert.InRange(editor.MaxTone, editor.MinTone + 0.019, 1.0);
                },
                maxTargetTooHigh,
                () =>
                {
                    Assert.Equal(1.0, editor.MaxTone, 4);
                },
                maxTargetMid,
                () =>
                {
                    Assert.InRange(vm.MaxTone, 0.83, 0.93);
                    Assert.Equal(editor.MaxTone, vm.MaxTone, 4);
                });

            Assert.Equal(2, dragStartedEvents);
            Assert.Equal(2, dragCompletedEvents);

            // 3. Drag ShadowKnee handle: test clamping in (0, HighlightKnee)
            var currentCurve = new PiecewiseToneCurveOptions(editor.MinTone, editor.MaxTone, editor.ShadowKnee, editor.HighlightKnee);
            Point shadowStart = editor.CurveToScreen(editor.ShadowKnee, PiecewiseToneCurve.Evaluate(editor.ShadowKnee, currentCurve));
            Point shadowTooLeft = new(plot.Left - 40, shadowStart.Y);
            Point shadowTooRight = new(plot.Right + 40, shadowStart.Y);
            Point shadowTargetMid = editor.CurveToScreen(0.30, 0.30);

            SimulateDrag(
                window,
                editor,
                shadowStart,
                shadowTooLeft,
                () =>
                {
                    Assert.True(editor.ShadowKnee > 0.0);
                    Assert.InRange(editor.ShadowKnee, 0.01, editor.HighlightKnee - 0.019);
                },
                shadowTooRight,
                () =>
                {
                    Assert.True(editor.ShadowKnee < editor.HighlightKnee);
                    Assert.InRange(editor.ShadowKnee, 0.01, editor.HighlightKnee - 0.019);
                },
                shadowTargetMid,
                () =>
                {
                    Assert.InRange(vm.ShadowKnee, 0.25, 0.35);
                    Assert.Equal(editor.ShadowKnee, vm.ShadowKnee, 4);
                });

            Assert.Equal(3, dragStartedEvents);
            Assert.Equal(3, dragCompletedEvents);

            // 4. Drag HighlightKnee handle: test clamping in (ShadowKnee, 1)
            currentCurve = new PiecewiseToneCurveOptions(editor.MinTone, editor.MaxTone, editor.ShadowKnee, editor.HighlightKnee);
            Point highlightStart = editor.CurveToScreen(editor.HighlightKnee, PiecewiseToneCurve.Evaluate(editor.HighlightKnee, currentCurve));
            Point highlightTooLeft = new(plot.Left - 40, highlightStart.Y);
            Point highlightTooRight = new(plot.Right + 40, highlightStart.Y);
            Point highlightTargetMid = editor.CurveToScreen(0.75, 0.75);

            SimulateDrag(
                window,
                editor,
                highlightStart,
                highlightTooLeft,
                () =>
                {
                    Assert.True(editor.HighlightKnee > editor.ShadowKnee);
                    Assert.InRange(editor.HighlightKnee, editor.ShadowKnee + 0.019, 0.99);
                },
                highlightTooRight,
                () =>
                {
                    Assert.True(editor.HighlightKnee < 1.0);
                    Assert.Equal(0.99, editor.HighlightKnee, 4);
                },
                highlightTargetMid,
                () =>
                {
                    Assert.InRange(vm.HighlightKnee, 0.70, 0.80);
                    Assert.Equal(editor.HighlightKnee, vm.HighlightKnee, 4);
                });

            Assert.Equal(4, dragStartedEvents);
            Assert.Equal(4, dragCompletedEvents);
            Assert.Equal(4, dragStartedCommands);
            Assert.Equal(4, dragCompletedCommands);

            // Global invariants hold across all drags
            Assert.InRange(vm.MinTone, 0.0, 1.0);
            Assert.InRange(vm.MaxTone, 0.0, 1.0);
            Assert.True(vm.MinTone < vm.MaxTone);
            Assert.True(0.0 < vm.ShadowKnee);
            Assert.True(vm.ShadowKnee < vm.HighlightKnee);
            Assert.True(vm.HighlightKnee < 1.0);

            // Non-interactive mode ignores pointer input
            editor.IsInteractive = false;
            double frozenMin = vm.MinTone;
            Point frozenStart = editor.CurveToScreen(0.0, editor.MinTone);
            Point frozenWin = ToWindowPoint(editor, window, frozenStart);
            window.MouseDown(frozenWin, MouseButton.Left);
            window.MouseMove(ToWindowPoint(editor, window, editor.CurveToScreen(0.0, 0.5)));
            window.MouseUp(ToWindowPoint(editor, window, editor.CurveToScreen(0.0, 0.5)), MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(frozenMin, vm.MinTone);
            Assert.Equal(4, dragStartedEvents);
            Assert.Equal(4, dragCompletedEvents);
        }
        finally
        {
            window.Close();
        }
    }

    private static ToneCurveEditor CreateBoundEditor(FinishingStageViewModel vm)
    {
        return new ToneCurveEditor
        {
            Width = 320,
            Height = 200,
            DataContext = vm,
            [!ToneCurveEditor.MinToneProperty] = new Binding(nameof(FinishingStageViewModel.MinTone), BindingMode.TwoWay),
            [!ToneCurveEditor.MaxToneProperty] = new Binding(nameof(FinishingStageViewModel.MaxTone), BindingMode.TwoWay),
            [!ToneCurveEditor.ShadowKneeProperty] = new Binding(nameof(FinishingStageViewModel.ShadowKnee), BindingMode.TwoWay),
            [!ToneCurveEditor.HighlightKneeProperty] = new Binding(nameof(FinishingStageViewModel.HighlightKnee), BindingMode.TwoWay),
        };
    }

    private static Point ToWindowPoint(Control control, Window window, Point localPoint)
    {
        return control.TranslatePoint(localPoint, window)
            ?? throw new InvalidOperationException("Failed to translate control point to window coordinates.");
    }

    private static void SimulateDrag(
        Window window,
        ToneCurveEditor editor,
        Point startLocal,
        Point move1Local,
        Action assert1,
        Point move2Local,
        Action assert2,
        Point move3Local,
        Action assert3)
    {
        window.MouseDown(ToWindowPoint(editor, window, startLocal), MouseButton.Left);
        TestAppHelpers.PumpUi();

        window.MouseMove(ToWindowPoint(editor, window, move1Local));
        TestAppHelpers.PumpUi();
        assert1();

        window.MouseMove(ToWindowPoint(editor, window, move2Local));
        TestAppHelpers.PumpUi();
        assert2();

        Point endWin = ToWindowPoint(editor, window, move3Local);
        window.MouseMove(endWin);
        TestAppHelpers.PumpUi();
        assert3();

        window.MouseUp(endWin, MouseButton.Left);
        TestAppHelpers.PumpUi();
    }
}

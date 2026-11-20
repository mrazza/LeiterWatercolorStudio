using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// A compact, theme-aware information badge placed beside a setting title to display
/// a wrapped, layman-digestible explanation tooltip on hover without interfering with
/// adjacent click or double-tap gestures.
/// </summary>
public partial class SettingInfoIcon : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Header"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingInfoIcon, string?>(nameof(Header), null);

    /// <summary>
    /// Identifies the <see cref="Text"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SettingInfoIcon, string?>(nameof(Text), null);

    /// <summary>
    /// Initializes a new instance of <see cref="SettingInfoIcon"/>.
    /// </summary>
    public SettingInfoIcon()
    {
        InitializeComponent();

        // Prevent clicking or double-tapping the badge from bubbling to label reset, checkbox, or expander handlers.
        AddHandler(TappedEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DoubleTappedEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble, handledEventsToo: true);

        SyncTooltip();
    }

    /// <summary>
    /// Gets or sets the optional bold title displayed at the top of the tooltip.
    /// </summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman-digestible explanation shown when hovering over the icon.
    /// </summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HeaderProperty || change.Property == TextProperty)
        {
            SyncTooltip();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
    }

    private void SyncTooltip()
    {
        if (PART_HitTarget is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            PART_HitTarget.IsVisible = false;
            ToolTip.SetTip(this, null);
            ClearValue(AutomationProperties.NameProperty);
            ClearValue(AutomationProperties.HelpTextProperty);
            return;
        }

        string body = Text.Trim();
        PART_HitTarget.IsVisible = true;

        var panel = new StackPanel
        {
            MaxWidth = 320,
            Spacing = 4,
        };

        string? header = string.IsNullOrWhiteSpace(Header) ? null : Header.Trim();
        if (header is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = header,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = body,
            FontSize = 11.5,
            LineHeight = 16.5,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.92,
        });

        ToolTip.SetTip(this, panel);
        ToolTip.SetShowDelay(this, 150);
        ToolTip.SetBetweenShowDelay(this, 100);

        AutomationProperties.SetName(this, header is null ? "Setting information" : $"{header} information");
        AutomationProperties.SetHelpText(this, body);
    }
}

using System.Windows;
using System.Windows.Media;

namespace SpaceMousePilot.Controls;

/// <summary>Bidirectional deflection bar. Switches to peak-fill mode during calibration.</summary>
public sealed class AxisMeter : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(AxisMeter),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CalibPeakProperty =
        DependencyProperty.Register(nameof(CalibPeak), typeof(double), typeof(AxisMeter),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsCalibModeProperty =
        DependencyProperty.Register(nameof(IsCalibMode), typeof(bool), typeof(AxisMeter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value      { get => (double)GetValue(ValueProperty);     set => SetValue(ValueProperty, value); }
    public double CalibPeak  { get => (double)GetValue(CalibPeakProperty); set => SetValue(CalibPeakProperty, value); }
    public bool   IsCalibMode { get => (bool)GetValue(IsCalibModeProperty); set => SetValue(IsCalibModeProperty, value); }

    private static readonly Brush  BgBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0x11, 0x1a, 0x24)));
    private static readonly Brush  AccentBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0x4d, 0xa6, 0xff)));
    private static readonly Brush  SuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3f, 0xb9, 0x5a)));
    private static readonly Brush  MutedBrush   = Freeze(new SolidColorBrush(Color.FromRgb(0x6a, 0x85, 0xa0)));
    private static readonly Pen    CenterPen    = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0x25, 0x33, 0x47)), 1));
    private static readonly Pen    ThreshPen    = Freeze(new Pen(MutedBrush, 1) { DashStyle = DashStyles.Dash });

    private static T Freeze<T>(T obj) where T : Freezable { obj.Freeze(); return obj; }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w < 4 || h < 4) return;

        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

        if (IsCalibMode) RenderCalib(dc, w, h);
        else             RenderLive(dc, w, h);
    }

    private void RenderLive(DrawingContext dc, double w, double h)
    {
        double cx  = w / 2;
        double pad = h * 0.2;
        dc.DrawLine(CenterPen, new Point(cx, 2), new Point(cx, h - 2));
        double val = Math.Max(-1.0, Math.Min(1.0, Value));
        if (Math.Abs(val) > 0.005)
        {
            double bar = Math.Abs(val) * (cx - 2);
            dc.DrawRectangle(AccentBrush, null,
                new Rect(val > 0 ? cx : cx - bar, pad, bar, h - pad * 2));
        }
    }

    private void RenderCalib(DrawingContext dc, double w, double h)
    {
        double frac = Math.Max(0, Math.Min(1, CalibPeak));
        double pad  = h * 0.2;
        var    fill = frac > 0.5 ? SuccessBrush : frac > 0.1 ? AccentBrush : MutedBrush;
        if (frac > 0.001)
            dc.DrawRectangle(fill, null, new Rect(2, pad, frac * (w - 4), h - pad * 2));
        double tx = 2 + 0.5 * (w - 4);
        dc.DrawLine(ThreshPen, new Point(tx, 1), new Point(tx, h - 1));
    }
}

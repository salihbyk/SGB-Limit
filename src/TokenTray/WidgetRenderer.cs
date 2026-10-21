using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenTray.Core;

namespace TokenTray;

public enum WidgetStyle { Rings, Dots, Cells, Bars, Minimal, Pulse, Ribbon, Dial, Flow, Halo, Orbit, Comet, Identity, Signal }

/// <summary>What the taskbar widget shows. Session/weekly both off falls back to whichever is tighter.</summary>
public sealed record WidgetOptions(WidgetStyle Style, bool Name, bool Session, bool Weekly, bool Time, bool Remaining = false, bool Motion = true, int SignalBars = 5);

internal enum TaskbarDensity { Full, Small, Icons }

/// <summary>
/// Draws the taskbar widget into a premultiplied bitmap for a layered window.
/// Providers sit side by side as blocks; each style decides how a block looks.
/// A window the account doesn't have, or the user hid, takes no space.
/// </summary>
internal static class WidgetRenderer
{
    internal static double? AnimationPhase { get; set; }
    static readonly FontFamily Family = new("Segoe UI Variable Text, Segoe UI");
    static readonly Typeface Regular = new(Family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface Strong = new(Family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    const double Pad = 8, BlockGap = 16;

    public sealed record Row(ProviderId Id, Reading? Session, Reading? Weekly, string? Problem, TimeSpan? NextReset, bool Waiting = false, bool Stale = false, string? Name = null, Reading? Binding = null, DateTimeOffset? ReadAt = null);

    sealed record Block(double Width, Action<DrawingContext, double, double> Draw);

    public static BitmapSource Render(IReadOnlyList<Row> rows, WidgetOptions o, Palette p, double scale, double heightDip, bool hover, out double widthDip, int hiddenCount = 0, TaskbarDensity density = TaskbarDensity.Full)
    {
        var shrink = density == TaskbarDensity.Small ? 0.88 : 1;
        var ctx = new Ctx(p, scale, heightDip, o);
        var blocks = rows.Select(r => density == TaskbarDensity.Icons ? ctx.IconPercent(Filter(r, o)) : ctx.Layout(Filter(r, o))).ToList();
        if (hiddenCount > 0) blocks.Add(ctx.More(hiddenCount));
        var gap = density == TaskbarDensity.Icons ? 7 : o.Style >= WidgetStyle.Pulse ? 6 : BlockGap;
        var padding = density == TaskbarDensity.Icons ? 5 : Pad;
        widthDip = Math.Ceiling((padding * 2 + blocks.Sum(b => b.Width) + gap * Math.Max(0, blocks.Count - 1)) * shrink);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // Fully transparent pixels pass clicks through to the taskbar; keep the whole widget hit-testable.
            dc.DrawRectangle(Palette.Brush(Color.FromArgb(1, 0, 0, 0)), null, new Rect(0, 0, widthDip, heightDip));
            if (hover)
                dc.DrawRoundedRectangle(Palette.Brush(p.HoverFill), null, new Rect(2, 4, widthDip - 4, heightDip - 8), 6, 6);
            dc.PushTransform(new ScaleTransform(shrink, shrink, 0, heightDip / 2));
            var x = padding;
            foreach (var b in blocks) { b.Draw(dc, x, heightDip / 2); x += b.Width + gap; }
            dc.Pop();
        }

        var pxW = Math.Max(1, (int)Math.Ceiling(widthDip * scale));
        var pxH = Math.Max(1, (int)Math.Round(heightDip * scale));
        var bmp = new RenderTargetBitmap(pxW, pxH, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Apply the user's window choices; never leave a block with nothing to show.</summary>
    static Row Filter(Row r, WidgetOptions o)
    {
        Reading? Mode(Reading? reading) => reading is { } rd ? Reading.Of(rd.Window, o.Remaining, DateTimeOffset.Now) : null;
        var s = o.Session ? Mode(r.Session) : null;
        var w = o.Weekly ? Mode(r.Weekly) : null;
        if (s is null && w is null && (r.Session ?? r.Weekly) is not null)
        {
            // Nothing the user asked for exists here: show the tighter of what does.
            var tighter = new[] { r.Session, r.Weekly }.Where(x => x != null).OrderByDescending(x => x!.Value.Window.UsedPercent).First();
            if (tighter!.Value.Window.Kind == WindowKind.Session) s = Mode(tighter); else w = Mode(tighter);
        }
        return r with { Session = s, Weekly = w, Binding = Mode(r.Binding) };
    }

    // Compact surfaces share the panel's binding quota, even when that quota is model-specific.
    static Reading? Primary(Row r) => r.Binding ?? new[] { r.Session, r.Weekly }
        .Where(w => w is not null && !w.Value.PastReset).OrderByDescending(w => w!.Value.Window.UsedPercent).FirstOrDefault();

    static bool Historical(Row r) => r.Stale && r.Id == ProviderId.Antigravity && r.Binding is not null && r.ReadAt is not null;

    internal static double SignalBarAmount(double fraction, int bars, int index) =>
        Math.Clamp(Math.Clamp(fraction, 0, 1) * bars - index, 0, 1);

    sealed class Ctx(Palette p, double scale, double height, WidgetOptions o)
    {
        FormattedText Text(string s, Typeface tf, double size, Color c) =>
            new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size, Palette.Brush(c), scale);

        IEnumerable<Reading> Windows(Row r) => new[] { r.Session, r.Weekly }.Where(x => x != null).Select(x => x!.Value);

        Color Tone(ProviderId id, Reading rd) => rd.PastReset ? p.TextMuted : p.Fill(id, rd.Window, rd.Pace);

        /// <summary>Numbers stay neutral until the window needs attention.</summary>
        Color NumberTone(Row r, Reading rd)
        {
            var id = r.Id;
            // An old reading must not look current.
            if (rd.PastReset || r.Stale) return p.TextMuted;
            var c = p.Fill(id, rd.Window, rd.Pace);
            return c == p.Red ? p.RedText : c == p.Amber ? p.AmberText : p.Text;
        }

        // Each number carries its own reset; there is no separate provider-wide countdown.
        static string TimeText(Row r) => "";

        /// <summary>Beside each number: when that window refills, or which window it is when times are off.</summary>
        string Note(Reading rd) => o.Time && rd.Window.ResetsAt is { } at
            ? (rd.PastReset ? "…" : Format.ResetShort(at, DateTimeOffset.Now, S.Tr ? "g" : "d", S.Tr ? "dk" : "m"))
            : Tag(rd.Window);

        static string Tag(UsageWindow w) => w.Kind switch
        {
            WindowKind.Session when w.Length is { } l => S.Tr ? $"{l.TotalHours:0}sa" : $"{l.TotalHours:0}h",
            WindowKind.Session => S.Tr ? "oturum" : "session",
            WindowKind.Period when w.Label is "Monthly" or "This month" => S.Tr ? "ay" : "mo",
            WindowKind.Period => S.Tr ? "dönem" : "period",
            _ => S.Tr ? "7g" : "7d",
        };

        public Block Layout(Row r) => o.Style == WidgetStyle.Signal ? Signal(r) : o.Style == WidgetStyle.Identity ? Identity(r) : o.Style >= WidgetStyle.Ribbon ? Instrument(r) : o.Style == WidgetStyle.Pulse ? Pulse(r) : r.Session is null && r.Weekly is null
            ? Problem(r)
            : o.Style switch
            {
                WidgetStyle.Ribbon or WidgetStyle.Dial or WidgetStyle.Flow => Instrument(r),
                WidgetStyle.Dots => Dots(r),
                WidgetStyle.Cells => Cells(r),
                WidgetStyle.Bars => Bars(r),
                WidgetStyle.Minimal => Minimal(r),
                _ => Rings(r),
            };

        public Block More(int count)
        {
            var text = Text("+" + count, Strong, 11, p.TextMuted);
            return new Block(text.Width + 12, (dc, x, cy) => dc.DrawText(text, new Point(x + 6, cy - text.Height / 2)));
        }

        public Block IconPercent(Row r)
        {
            var primary = Primary(r);
            var historical = Historical(r);
            var valid = primary is { PastReset: false } && !r.Stale;
            var text = valid || historical ? S.Percent(primary!.Value.Percent) + (historical ? "*" : "") : r.Waiting ? "…" : "—";
            var color = valid ? NumberTone(r, primary!.Value) : p.TextMuted;
            var value = Text(text, Strong, 10.5, color);
            // A larger, unframed mark stays recognizable when account names are hidden.
            return new Block(24 + value.WidthIncludingTrailingWhitespace + 2, (dc, x, cy) =>
            {
                Mark(dc, r.Id, new Point(x + 10, cy), 19);
                dc.DrawText(value, new Point(x + 24, cy - value.Height / 2));
                if (r.Stale) dc.DrawEllipse(Palette.Brush(p.AmberText), null, new Point(x + 18, cy - 8), 2, 2);
            });
        }

        Block Identity(Row r)
        {
            var readings = Windows(r).ToList();
            var primary = readings.OrderByDescending(w => w.Window.UsedPercent).FirstOrDefault();
            var label = Text(o.Name ? (r.Name ?? S.ShortName(r.Id)) + (o.Remaining ? " · kalan" : "") : "", Strong, 11.5, p.Text);
            var metrics = readings.Count > 0 ? Numbers(r, true, 11.5) : (Text(r.Waiting ? "…" : "—", Strong, 12, p.TextMuted).Width, (Action<DrawingContext, double, double>)((dc, x, y) => dc.DrawText(Text(r.Waiting ? "…" : "—", Strong, 12, p.TextMuted), new Point(x, y))));
            var width = 35 + Math.Max(label.Width, metrics.Item1);
            return new Block(width, (dc, x, cy) =>
            {
                var center = new Point(x + 13, cy);
                dc.DrawEllipse(null, new Pen(Palette.Brush(p.Track), 1.8), center, 12.5, 12.5);
                if (readings.Count > 0 && !r.Stale && !primary.PastReset) Arc(dc, center, 12.5, 1.8, primary, r.Id);
                Mark(dc, r.Id, center, 13);
                if (o.Name) dc.DrawText(label, new Point(x + 34, cy - 17));
                metrics.Item2(dc, x + 34, o.Name ? cy + 1 : cy - 7);
            });
        }

        Block Signal(Row r)
        {
            var reading = Primary(r);
            var historical = Historical(r);
            var valid = reading is { PastReset: false } && !r.Stale;
            var primary = reading.GetValueOrDefault();
            var value = Text(valid || historical ? S.Percent(primary.Percent) + (historical ? "*" : "") : r.Waiting ? "…" : "—", Strong, 11, valid ? NumberTone(r, primary) : p.TextMuted);
            var name = Text(o.Name ? r.Name ?? S.ShortName(r.Id) : "", Strong, 10.5, p.Text);
            var bars = o.SignalBars == 3 ? 3 : 5;
            // Each bar owns an equal quota slice. A partial slice stays partial in both numeric modes.
            var fraction = valid || historical && !primary.PastReset ? primary.Fraction : 0;
            var tone = valid ? Tone(r.Id, primary) : p.TextMuted;
            var barLeft = o.Name ? 0 : 22;
            var valueLeft = barLeft + 33;
            var width = Math.Max(valueLeft + value.WidthIncludingTrailingWhitespace + 3, o.Name ? 22 + name.Width : 0);
            return new Block(width, (dc, x, cy) =>
            {
                Mark(dc, r.Id, new Point(x + 8, o.Name ? cy - 10 : cy), 15);
                if (r.Stale) dc.DrawEllipse(Palette.Brush(p.AmberText), null, new Point(x + 15, o.Name ? cy - 17 : cy - 8), 2, 2);
                if (o.Name) dc.DrawText(name, new Point(x + 21, cy - 18));
                var baseline = o.Name ? cy + 15 : cy + 9;
                var barWidth = bars == 3 ? 7 : 4;
                for (var i = 0; i < bars; i++)
                {
                    var h = 4 + 13.0 * i / (bars - 1);
                    var rect = new Rect(x + barLeft + i * (barWidth + 2), baseline - h, barWidth, h);
                    dc.DrawRoundedRectangle(Palette.Brush(p.Track), null, rect, 1, 1);
                    var amount = SignalBarAmount(fraction, bars, i);
                    if (amount > 0)
                    {
                        // Clip the actual bar, so even a tiny percentage cannot paint a complete bar.
                        dc.PushClip(new RectangleGeometry(new Rect(rect.X, rect.Bottom - h * amount, rect.Width, h * amount)));
                        dc.DrawRoundedRectangle(Palette.Brush(tone), null, rect, 1, 1);
                        dc.Pop();
                    }
                }
                dc.DrawText(value, new Point(x + valueLeft, o.Name ? cy : cy - value.Height / 2));
            });
        }

        Block Instrument(Row r)
        {
            var readings = Windows(r).ToList();
            if (!o.Name) readings = readings.OrderByDescending(rd => rd.Window.UsedPercent).Take(1).ToList();
            var primary = readings.OrderByDescending(w => w.Window.UsedPercent).FirstOrDefault();
            var valid = readings.Count > 0 && !r.Stale && !primary.PastReset;
            var tone = valid ? Tone(r.Id, primary) : p.TextMuted;
            var name = Text(o.Name ? (r.Name ?? S.ShortName(r.Id)) + (o.Remaining ? " kalan" : " kull.") : o.Remaining ? "kalan" : "kull.", Strong, 10.5, p.Text);
            string TagOf(Reading rd) => rd.Window.Kind switch { WindowKind.Session => rd.Window.Length is { } length ? $"{length.TotalHours:0}sa" : "Otr", WindowKind.Weekly => "Hft", WindowKind.Period => "Ay", _ => rd.Window.Label.Contains("Argon") ? "Argon" : rd.Window.Label.Contains("Flash") ? "Flash" : rd.Window.Label.Split(' ')[0] };
            var metric = Text(readings.Count == 0 ? r.Waiting ? "…" : "—" : string.Join(" · ", readings.Select(rd => TagOf(rd) + " " + (rd.PastReset ? "—" : S.Percent(rd.Percent)))), Strong, 11.5, tone);
            var timeText = Text(o.Time && r.NextReset is { } next ? S.ShortDuration(next) : "", Regular, 9, p.TextMuted);
            var width = Math.Max(o.Name ? 90 : 57, Math.Max(name.Width, metric.Width) + (o.Style == WidgetStyle.Dial ? 35 : 12));
            if (o.Name && o.Style != WidgetStyle.Dial) width = Math.Max(width, name.Width + timeText.Width + 19);
            return new Block(width, (dc, x, cy) =>
            {
                var left = x + (o.Style == WidgetStyle.Dial ? 33 : 5);
                if (o.Name) dc.DrawText(name, new Point(left, cy - 20));
                else { Mark(dc, r.Id, new Point(x + 9, cy - 13), 12); dc.DrawText(name, new Point(x + 19, cy - 20)); }
                if (o.Name && o.Style != WidgetStyle.Dial) dc.DrawText(timeText, new Point(x + width - timeText.Width - 5, cy - 18));
                dc.DrawText(metric, new Point(left, cy - 5));
                if (o.Style == WidgetStyle.Dial)
                {
                    var c = new Point(x + 15, cy + 2);
                    var track = new StreamGeometry();
                    using (var g = track.Open()) { g.BeginFigure(new Point(c.X - 12, c.Y), false, false); g.ArcTo(new Point(c.X + 12, c.Y), new Size(12, 12), 0, false, SweepDirection.Clockwise, true, false); }
                    dc.DrawGeometry(null, new Pen(Palette.Brush(p.Track), 3), track);
                    if (valid)
                    {
                        var angle = Math.PI * (1 - primary.Fraction);
                        dc.DrawLine(new Pen(Palette.Brush(tone), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, c, new Point(c.X + 10 * Math.Cos(angle), c.Y - 10 * Math.Sin(angle)));
                        dc.DrawEllipse(Palette.Brush(tone), null, c, 2, 2);
                    }
                }
                else
                {
                    var available = width - 10;
                    for (var i = 0; i < Math.Max(1, readings.Count); i++)
                    {
                        var y = cy + 11 + i * 4;
                        dc.DrawRoundedRectangle(Palette.Brush(p.Track), null, new Rect(x + 5, y, available, 2), 1, 1);
                        if (i < readings.Count && !readings[i].PastReset && !r.Stale)
                            dc.DrawRoundedRectangle(Palette.Brush(Tone(r.Id, readings[i])), null, new Rect(x + 5, y, available * Math.Clamp(readings[i].Fraction, 0, 1), 2), 1, 1);
                    }
                    // Motion is decoration, never a changing quota. The timer runs only during hover.
                    if (o.Style == WidgetStyle.Flow && AnimationPhase is { } phase)
                    {
                        var px = x + 5 + available * phase;
                        dc.DrawEllipse(Palette.Brush(p.Text), null, new Point(px, cy + 12), 2, 2);
                    }
                }
                if (o.Time && r.NextReset is { } time && o.Style == WidgetStyle.Dial)
                    dc.DrawText(Text(S.ShortDuration(time), Regular, 9, p.TextMuted), new Point(left, cy + 10));
            });
        }


        Block Pulse(Row r)
        {
            var readings = Windows(r).ToList();
            var primary = readings.OrderByDescending(w => w.Window.UsedPercent).FirstOrDefault();
            var caption = Text((o.Name ? (r.Name ?? S.ShortName(r.Id)) + " · " : "") + (o.Remaining ? "kalan" : "kull."), Strong, 11.5, p.Text);
            var tone = readings.Count > 0 ? Tone(r.Id, primary) : r.Waiting ? p.TextMuted : p.AmberText;
            string TagOf(UsageWindow w) => w.Kind switch
            {
                WindowKind.Model => w.Label.StartsWith("Gemini") ? "Gem" : w.Label.StartsWith("Claude") ? "Cl" : w.Label.StartsWith("GPT") ? "GPT" : "Model",
                WindowKind.Session => w.Length is { } l ? $"{l.TotalHours:0} sa" : "Otr",
                WindowKind.Weekly => "Hft",
                WindowKind.Period => "Ay",
                _ => "Limit",
            };
            var metrics = readings.Count > 0
                ? string.Join(" · ", readings.Select(w => $"{TagOf(w.Window)} {(w.PastReset ? "—" : S.Percent(w.Percent))}"))
                : r.Problem ?? "Bekleniyor";
            var values = Text(metrics, Strong, 12, readings.Count > 0 ? p.Text : tone);
            var reset = Text(o.Time && r.NextReset is { } next ? S.ShortDuration(next) : "", Regular, 10.5, p.TextMuted);
            var width = Math.Ceiling(46 + Math.Max(values.WidthIncludingTrailingWhitespace, caption.WidthIncludingTrailingWhitespace + (reset.Width > 0 ? reset.Width + 15 : 0)) + 10);
            return new Block(Math.Max(width, 108), (dc, x, cy) =>
            {
                var h = Math.Min(40, height - 4);
                dc.DrawRoundedRectangle(Palette.Brush(p.Raised), null, new Rect(x, cy - h / 2, Math.Max(width, 108), h), 8, 8);
                var center = new Point(x + 21, cy);
                dc.DrawEllipse(null, new Pen(Palette.Brush(p.Track), 2.4), center, 12.5, 12.5);
                if (readings.Count > 0 && !primary.PastReset)
                {
                    var fraction = Math.Clamp(primary.Fraction, 0, 1);
                    if (fraction >= 0.999) dc.DrawEllipse(null, new Pen(Palette.Brush(tone), 2.4), center, 12.5, 12.5);
                    else if (fraction > 0)
                    {
                        var geometry = new StreamGeometry();
                        using (var g = geometry.Open())
                        {
                            g.BeginFigure(new Point(center.X, center.Y - 12.5), false, false);
                            var a = fraction * 2 * Math.PI;
                            g.ArcTo(new Point(center.X + 12.5 * Math.Sin(a), center.Y - 12.5 * Math.Cos(a)), new Size(12.5, 12.5), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
                        }
                        dc.DrawGeometry(null, new Pen(Palette.Brush(tone), 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
                    }
                }
                Mark(dc, r.Id, center, 13);
                if (readings.Count == 0 && !r.Waiting) dc.DrawEllipse(Palette.Brush(p.Amber), null, new Point(center.X + 10, center.Y - 10), 2.5, 2.5);
                var tx = x + 43;
                dc.DrawText(caption, new Point(tx, cy - 17));
                if (reset.Width > 0) dc.DrawText(reset, new Point(x + Math.Max(width, 108) - reset.Width - 8, cy - 16));
                dc.DrawText(values, new Point(tx, cy));
            });
        }

        // ───── Shared pieces ─────

        /// <summary>Provider name, or its mark when names are off.</summary>
        (double w, double h, Action<DrawingContext, double, double> draw) Label(Row r, double size = 12)
        {
            var (w, h, draw) = BareLabel(r, size);
            if (!r.Stale) return (w, h, draw);
            // Amber dot: this is the last reading we have, not a live one.
            return (w + 7, h, (dc, x, y) =>
            {
                draw(dc, x, y);
                dc.DrawEllipse(Palette.Brush(p.AmberText), null, new Point(x + w + 4, y + h / 2), 2.2, 2.2);
            });
        }

        (double w, double h, Action<DrawingContext, double, double> draw) BareLabel(Row r, double size)
        {
            if (!o.Name)
            {
                var m = size + 2;
                return (m, m, (dc, x, y) => Mark(dc, r.Id, new Point(x + m / 2, y + m / 2), m));
            }
            var t = Text(r.Name ?? S.ShortName(r.Id), Strong, size, p.Text);
            return (t.WidthIncludingTrailingWhitespace, t.Height, (dc, x, y) => dc.DrawText(t, new Point(x, y)));
        }

        void Mark(DrawingContext dc, ProviderId id, Point c, double d) => ProviderMark.Draw(dc, id, c, d, Palette.Brush(p.Hue(id)));

        /// <summary>"%93 5sa  %14 7g" — each number labeled with its window.</summary>
        (double w, Action<DrawingContext, double, double> draw) Numbers(Row r, bool tags = true, double size = 12.5)
        {
            var parts = new List<FormattedText>();
            foreach (var rd in Windows(r))
            {
                parts.Add(Text(S.Percent(rd.Percent), Strong, size, NumberTone(r, rd)));
                parts.Add(tags ? Text(Note(rd), Regular, Math.Max(11.5, size - 1.5), p.TextMuted) : Text("", Regular, size, p.TextMuted));
            }
            double Gap(int i) => i % 2 == 0 ? (tags ? 3 : 0) : 9;
            var width = parts.Select((t, i) => t.WidthIncludingTrailingWhitespace + Gap(i)).Sum() - (parts.Count > 0 ? 9 : 0);
            var baseline = parts.Count > 0 ? parts.Max(t => t.Baseline) : 0;
            return (width, (dc, x, y) =>
            {
                for (var i = 0; i < parts.Count; i++)
                {
                    dc.DrawText(parts[i], new Point(x, y + baseline - parts[i].Baseline));
                    x += parts[i].WidthIncludingTrailingWhitespace + Gap(i);
                }
            });
        }

        /// <summary>Two stacked lines to the right of a graphic: label + time, then numbers.</summary>
        Block TwoLine(Row r, double graphicW, Action<DrawingContext, Point> graphic, (double w, Action<DrawingContext, double, double> draw)? line2 = null)
        {
            var label = Label(r);
            var time = Text(TimeText(r), Regular, 12, p.TextMuted);
            var nums = line2 ?? Numbers(r);
            var line1 = label.w + (time.Width > 0 ? 6 + time.WidthIncludingTrailingWhitespace : 0);
            const double gap = 7;
            return new Block(graphicW + gap + Math.Max(line1, nums.w), (dc, x, cy) =>
            {
                graphic(dc, new Point(x + graphicW / 2, cy));
                var tx = x + graphicW + gap;
                var y1 = cy - 17;
                label.draw(dc, tx, y1 + (16 - label.h) / 2);
                if (time.Width > 0) dc.DrawText(time, new Point(tx + label.w + 6, y1 + (16 - time.Height) / 2));
                nums.draw(dc, tx, cy);
            });
        }

        Block Problem(Row r)
        {
            var label = Label(r);
            // Waiting for a first reading is not a warning; only real problems get amber.
            var msg = Text(r.Problem ?? "…", Regular, 12, r.Problem != null && !r.Waiting ? p.AmberText : p.TextMuted);
            return new Block(Math.Max(label.w, msg.WidthIncludingTrailingWhitespace), (dc, x, cy) =>
            {
                label.draw(dc, x, cy - 17 + (16 - label.h) / 2);
                dc.DrawText(msg, new Point(x, cy));
            });
        }

        // ───── 1. Rings: weekly outside, session inside ─────

        Block Rings(Row r)
        {
            var d = Math.Clamp(Math.Round(height - 16), 22, 34);
            return TwoLine(r, d, (dc, c) =>
            {
                var outer = Math.Max(3, d * 0.13);
                var ro = d / 2 - outer / 2;
                var outerWin = r.Weekly ?? r.Session;
                dc.DrawEllipse(null, new Pen(Palette.Brush(p.Track), outer), c, ro, ro);
                if (outerWin is { } ow) Arc(dc, c, ro, outer, ow, r.Id);
                if (r.Session is { } ses && r.Weekly is not null)
                {
                    var inner = outer * 0.85;
                    var ri = ro - outer / 2 - 2 - inner / 2;
                    dc.DrawEllipse(null, new Pen(Palette.Brush(p.Track), inner), c, ri, ri);
                    Arc(dc, c, ri, inner, ses, r.Id);
                }
            });
        }

        void Arc(DrawingContext dc, Point c, double radius, double stroke, Reading rd, ProviderId id)
        {
            var color = Tone(id, rd);
            var f = Math.Clamp(rd.Fraction, 0, 1);
            if (f >= 0.999) { dc.DrawEllipse(null, new Pen(Palette.Brush(color), stroke), c, radius, radius); return; }
            if (f < 0.005) return;
            var a = Math.Max(0.02, f) * 2 * Math.PI;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(c.X, c.Y - radius), false, false);
                ctx.ArcTo(new Point(c.X + radius * Math.Sin(a), c.Y - radius * Math.Cos(a)), new Size(radius, radius), 0, f > 0.5, SweepDirection.Clockwise, true, false);
            }
            dc.DrawGeometry(null, new Pen(Palette.Brush(color), stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
        }

        // ───── 2. Dots: dotted ring gauges and dot-matrix numerals ─────

        Block Dots(Row r)
        {
            var d = Math.Clamp(Math.Round(height - 14), 24, 36);
            var pitch = Math.Round(2.1 * scale) / scale;
            var nums = new List<(string text, Color color, string tag)>();
            foreach (var rd in Windows(r)) nums.Add((((int)Math.Round(rd.Percent)).ToString(CultureInfo.InvariantCulture), NumberTone(r, rd), Note(rd)));
            var tags = nums.Select(n => Text(n.tag, Regular, 11.5, p.TextMuted)).ToList();
            // The % sign in plain type, on the side the language puts it.
            var signs = nums.Select(n => Text("%", Strong, 11.5, n.color)).ToList();
            double SignW(int i) => signs[i].WidthIncludingTrailingWhitespace + 1;
            var width = nums.Select((n, i) => SignW(i) + DotFont.Width(n.text, pitch) + 3 + tags[i].WidthIncludingTrailingWhitespace).Sum() + 9 * Math.Max(0, nums.Count - 1);

            (double, Action<DrawingContext, double, double>) line2 = (width, (dc, x, cy) =>
            {
                var top = cy + 1;
                for (var i = 0; i < nums.Count; i++)
                {
                    var signY = top + DotFont.Height(pitch) - signs[i].Baseline;
                    if (S.Tr) { dc.DrawText(signs[i], new Point(x, signY)); x += SignW(i); }
                    x += DotFont.Draw(dc, nums[i].text, new Point(x, top), pitch, Palette.Brush(nums[i].color)) + (S.Tr ? 3 : 1);
                    if (!S.Tr) { dc.DrawText(signs[i], new Point(x, signY)); x += SignW(i) + 2; }
                    dc.DrawText(tags[i], new Point(x, top + DotFont.Height(pitch) - tags[i].Baseline));
                    x += tags[i].WidthIncludingTrailingWhitespace + 9;
                }
            });

            return TwoLine(r, d, (dc, c) =>
            {
                var outerWin = r.Weekly ?? r.Session;
                if (outerWin is { } ow) DotRing(dc, c, d / 2 - 1.5, 28, ow, r.Id, 1.5);
                if (r.Session is { } ses && r.Weekly is not null) DotRing(dc, c, d / 2 - 7, 18, ses, r.Id, 1.3);
            }, line2);
        }

        void DotRing(DrawingContext dc, Point c, double radius, int count, Reading rd, ProviderId id, double dot)
        {
            var lit = (int)Math.Round(Math.Clamp(rd.Fraction, 0, 1) * count);
            var on = Palette.Brush(Tone(id, rd));
            var off = Palette.Brush(p.Track);
            for (var i = 0; i < count; i++)
            {
                var a = i * 2 * Math.PI / count;
                var pt = new Point(c.X + radius * Math.Sin(a), c.Y - radius * Math.Cos(a));
                dc.DrawEllipse(i < lit ? on : off, null, pt, dot, dot);
            }
        }

        // ───── 3. Cells: a heat-map strip per window ─────

        Block Cells(Row r)
        {
            const int count = 10;
            var cell = 4.0; var gap = 1.5;
            var stripW = count * cell + (count - 1) * gap;
            var label = Label(r);
            var time = Text(TimeText(r), Regular, 12, p.TextMuted);
            var rows = Windows(r).ToList();
            var pcts = rows.Select(rd => Text(S.Percent(rd.Percent), Strong, 12, NumberTone(r, rd))).ToList();
            var ctags = rows.Select(rd => Text(Note(rd), Regular, 11.5, p.TextMuted)).ToList();
            var pctW = pcts.Count > 0 ? pcts.Max(t => t.WidthIncludingTrailingWhitespace) : 0;
            var ctagW = ctags.Count > 0 ? ctags.Max(t => t.WidthIncludingTrailingWhitespace) : 0;
            var left = Math.Max(label.w, time.WidthIncludingTrailingWhitespace);

            return new Block(left + 9 + stripW + 6 + pctW + 3 + ctagW, (dc, x, cy) =>
            {
                label.draw(dc, x, cy - 17 + (16 - label.h) / 2);
                if (time.Width > 0) dc.DrawText(time, new Point(x, cy + 1));
                var sx = x + left + 9;
                var rowH = 15.0;
                var y0 = cy - rowH * rows.Count / 2;
                for (var i = 0; i < rows.Count; i++)
                {
                    var rd = rows[i];
                    var yc = y0 + rowH * i + rowH / 2;
                    var filled = Math.Clamp(rd.Fraction, 0, 1) * count;
                    var on = Tone(r.Id, rd);
                    for (var k = 0; k < count; k++)
                    {
                        var amount = Math.Clamp(filled - k, 0, 1);
                        var c = amount >= 1 ? on : amount > 0 ? Color.FromArgb((byte)(80 + 175 * amount), on.R, on.G, on.B) : p.Track;
                        var px = Math.Round((sx + k * (cell + gap)) * scale) / scale;
                        var py = Math.Round((yc - cell / 2) * scale) / scale;
                        dc.DrawRoundedRectangle(Palette.Brush(c), null, new Rect(px, py, cell, cell), 0.8, 0.8);
                    }
                    var pcx = sx + stripW + 6 + pctW - pcts[i].WidthIncludingTrailingWhitespace;
                    dc.DrawText(pcts[i], new Point(pcx, yc - pcts[i].Height / 2));
                    dc.DrawText(ctags[i], new Point(sx + stripW + 6 + pctW + 3, yc - pcts[i].Height / 2 + pcts[i].Baseline - ctags[i].Baseline));
                }
            });
        }

        // ───── 4. Bars: stacked thin bars with their numbers ─────

        Block Bars(Row r)
        {
            const double barW = 44, bh = 5;
            var label = Label(r);
            var time = Text(TimeText(r), Regular, 12, p.TextMuted);
            var rows = Windows(r).ToList();
            var pcts = rows.Select(rd => Text(S.Percent(rd.Percent), Strong, 12, NumberTone(r, rd))).ToList();
            var tags = rows.Select(rd => Text(Note(rd), Regular, 11.5, p.TextMuted)).ToList();
            var pctW = pcts.Count > 0 ? pcts.Max(t => t.WidthIncludingTrailingWhitespace) : 0;
            var tagW = tags.Count > 0 ? tags.Max(t => t.WidthIncludingTrailingWhitespace) : 0;
            var left = Math.Max(label.w, time.WidthIncludingTrailingWhitespace);

            return new Block(left + 10 + barW + 7 + pctW + 3 + tagW, (dc, x, cy) =>
            {
                label.draw(dc, x, cy - 17 + (16 - label.h) / 2);
                if (time.Width > 0) dc.DrawText(time, new Point(x, cy + 1));
                var bx = x + left + 10;
                var rowH = 16.0;
                var y0 = cy - rowH * rows.Count / 2;
                for (var i = 0; i < rows.Count; i++)
                {
                    var rd = rows[i];
                    var yc = y0 + rowH * i + rowH / 2;
                    var by = Math.Round((yc - bh / 2) * scale) / scale;
                    dc.DrawRoundedRectangle(Palette.Brush(p.Track), null, new Rect(bx, by, barW, bh), 2, 2);
                    if (rd.Fraction > 0.005)
                        dc.DrawRoundedRectangle(Palette.Brush(Tone(r.Id, rd)), null, new Rect(bx, by, Math.Max(bh, barW * rd.Fraction), bh), 2, 2);
                    if (!double.IsNaN(rd.Notch))
                    {
                        var nx = Math.Round((bx + barW * rd.Notch) * scale) / scale - 0.75;
                        dc.DrawRectangle(Palette.Brush(p.Tick), null, new Rect(Math.Clamp(nx, bx, bx + barW - 1.5), by - 2.5, 1.5, bh + 5));
                    }
                    var px = bx + barW + 7 + pctW - pcts[i].WidthIncludingTrailingWhitespace;
                    dc.DrawText(pcts[i], new Point(px, yc - pcts[i].Height / 2));
                    dc.DrawText(tags[i], new Point(bx + barW + 7 + pctW + 3, yc - pcts[i].Height / 2 + pcts[i].Baseline - tags[i].Baseline));
                }
            });
        }

        // ───── 5. Minimal: mark (or name) and numbers, one line ─────

        Block Minimal(Row r)
        {
            var label = Label(r, 13);
            var nums = Numbers(r, size: 14);
            var time = Text(TimeText(r), Regular, 12, p.TextMuted);
            var w = label.w + 6 + nums.w + (time.Width > 0 ? 7 + time.WidthIncludingTrailingWhitespace : 0);
            return new Block(w, (dc, x, cy) =>
            {
                label.draw(dc, x, cy - label.h / 2);
                var nx = x + label.w + 6;
                nums.draw(dc, nx, cy - 10);
                if (time.Width > 0) dc.DrawText(time, new Point(nx + nums.w + 7, cy - time.Height / 2 + 0.5));
            });
        }
    }
}

/// <summary>A 5×7 dot-matrix face for digits, drawn as round dots.</summary>
internal static class DotFont
{
    static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    };

    public static double Height(double pitch) => pitch * 7;
    public static double Width(string s, double pitch) => s.Length == 0 ? 0 : s.Length * pitch * 6 - pitch;

    /// <summary>Draws at top-left; returns the advance width.</summary>
    public static double Draw(DrawingContext dc, string s, Point at, double pitch, Brush brush)
    {
        var r = pitch * 0.5;
        var x = at.X;
        foreach (var ch in s)
        {
            if (Glyphs.TryGetValue(ch, out var rows))
                for (var row = 0; row < 7; row++)
                    for (var col = 0; col < 5; col++)
                        if (rows[row][col] == '#')
                            dc.DrawEllipse(brush, null, new Point(x + col * pitch + pitch / 2, at.Y + row * pitch + pitch / 2), r, r);
            x += pitch * 6;
        }
        return Width(s, pitch);
    }
}

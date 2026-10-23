// Shared modern, dependency-free WinForms design system. Purely visual;
// ownership checks, Steam calls and all recovery decisions remain unchanged.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SE2WorkshopUGCFix;

internal static class ModernUi
{
    internal static readonly Color Background = Color.FromArgb(12, 20, 34);
    internal static readonly Color Surface = Color.FromArgb(21, 33, 51);
    internal static readonly Color SurfaceRaised = Color.FromArgb(28, 44, 66);
    internal static readonly Color Border = Color.FromArgb(52, 70, 94);
    internal static readonly Color Text = Color.FromArgb(238, 246, 255);
    internal static readonly Color Muted = Color.FromArgb(163, 182, 207);
    internal static readonly Color Accent = Color.FromArgb(57, 137, 247);
    internal static readonly Color AccentHover = Color.FromArgb(78, 159, 255);
    internal static readonly Color Success = Color.FromArgb(84, 224, 159);
    internal static readonly Color Warning = Color.FromArgb(255, 196, 104);
    internal static readonly Color Danger = Color.FromArgb(255, 117, 127);

    internal static void Window(Form form)
    {
        form.Font = new Font("Segoe UI", 10F);
        form.ForeColor = Text;
        form.BackColor = Background;
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    internal static Label Label(string caption, float size = 10,
        bool bold = false, Color? color = null)
        => new()
        {
            Text = caption,
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", size,
                bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Text,
            BackColor = Color.Transparent
        };

    internal static void Input(TextBox input, bool multiline = false)
    {
        input.BackColor = SurfaceRaised;
        input.ForeColor = Text;
        input.BorderStyle = BorderStyle.None;
        input.Font = new Font(multiline ? "Cascadia Code" : "Segoe UI",
            multiline ? 9.5F : 10.5F);
        input.Margin = new Padding(0);
    }

    internal static Control InputShell(TextBox input, int padding = 12)
    {
        Input(input);
        var panel = new ModernCard {
            Dock = DockStyle.Fill, Padding = new Padding(padding, 9, padding, 9),
            BackColor = SurfaceRaised, Margin = new Padding(1, 3, 1, 3)
        };
        panel.Controls.Add(input);
        return panel;
    }

    internal static ModernCard Card(int padding = 12)
        => new() {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(padding),
            Margin = new Padding(2, 4, 2, 4)
        };

    internal static void Button(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Border;
        button.FlatAppearance.MouseDownBackColor = primary ? Accent : Surface;
        button.BackColor = primary ? Accent : SurfaceRaised;
        button.ForeColor = Text;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(8, 2, 8, 2);
        button.Resize += (_, _) => RoundButton(button);
        RoundButton(button);
    }

    private static void RoundButton(Button button)
    {
        if (button.Width < 14 || button.Height < 14) return;
        using var path = Rounded(new Rectangle(0, 0,
            button.Width - 1, button.Height - 1), 10);
        var old = button.Region;
        button.Region = new Region(path);
        old?.Dispose();
    }

    internal static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (diameter < 2) {
            path.AddRectangle(rect);
            return path;
        }
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter,
            diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernCard : Panel
{
    internal ModernCard() {
        DoubleBuffered = true;
        BackColor = ModernUi.Surface;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 8 || Height < 8) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(ModernUi.Border, 1);
        using var path = ModernUi.Rounded(
            new Rectangle(0, 0, Width - 2, Height - 2), 13);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class ModernGroupBox : GroupBox
{
    internal ModernGroupBox() {
        DoubleBuffered = true;
        BackColor = ModernUi.Surface;
        ForeColor = ModernUi.Muted;
        Font = new Font("Segoe UI", 9F, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        if (Width > 6 && Height > 24) {
            using var border = new Pen(ModernUi.Border, 1);
            using var path = ModernUi.Rounded(
                new Rectangle(1, 13, Width - 3, Height - 15), 13);
            e.Graphics.DrawPath(border, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(18, 0, Width - 28, 24),
            ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
    }
}

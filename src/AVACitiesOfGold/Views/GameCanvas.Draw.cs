using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVACitiesOfGold.Game;

using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Views;

public sealed partial class GameCanvas
{
    // The layout of the exploring screen.

    private static readonly Typeface Retro = new(new FontFamily("avares://AVACitiesOfGold/Assets/Fonts#VT323"));
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width);
        return p;
    }

    private const uint Ground = 0xFF0C0A10, Panel = 0xFF16121C, Frame = 0xFFB8963C, Ink = 0xFFE8E0D0, Gold = 0xFFFFD860,
        Dim = 0xFF8A8278, Good = 0xFF60D080, Warn = 0xFFE0C040, Bad = 0xFFE05040, Blue = 0xFF80B0FF;

    private static FormattedText Fmt(string text, double size, uint colour) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));

    private static double Measure(string text, double size) => Fmt(text, size, Ink).Width;

    /// <summary>Text with its top-left corner at a point (or centred, or right-aligned, on it).</summary>
    private static void Text(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        var ft = Fmt(text, size, colour);
        double left = align == 0 ? x : align == 1 ? x - ft.Width / 2 : x - ft.Width;
        dc.DrawText(ft, new Point(left, y));
    }

    private static void Shadowed(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        Text(dc, text, x + 2, y + 2, size, 0xC0000000, align);
        Text(dc, text, x, y, size, colour, align);
    }

    private static readonly Dictionary<(string, double, double), List<string>> WrapCache = [];

    /// <summary>Splits text into lines that fit a width.</summary>
    private static List<string> Wrap(string text, double width, double size)
    {
        if (WrapCache.TryGetValue((text, width, size), out var cached)) return cached;
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string tryLine = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(tryLine, size) > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = tryLine;
        }
        if (line.Length > 0) lines.Add(line);
        if (WrapCache.Count > 2000) WrapCache.Clear();
        WrapCache[(text, width, size)] = lines;
        return lines;
    }

    private static void Box(DrawingContext dc, Rect r, uint fill = Panel, uint border = Frame)
    {
        dc.FillRectangle(Brush(fill), r);
        dc.DrawRectangle(Pen(border, 2), r.Deflate(1));
        dc.DrawRectangle(Pen(Shade(border, 0.45), 1), r.Deflate(4));
    }

    public static uint Shade(uint argb, double f)
    {
        uint r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        uint S(uint v) => (uint)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) : v * f, 0, 255);
        return (argb & 0xFF000000) | (S(r) << 16) | (S(g) << 8) | S(b);
    }

    private static uint Mix(uint a, uint b, double t)
    {
        uint C(int s) => (uint)Math.Round(((a >> s) & 0xFF) * (1 - t) + ((b >> s) & 0xFF) * t);
        return 0xFF000000 | (C(16) << 16) | (C(8) << 8) | C(0);
    }


    private double _clock => Engine.Clock;
    private const double Tile = 24;
    private static readonly Rect View = new(16, 16, 992, 600);
    private static readonly Rect Status = new(16, 624, 992, 128);
    private static readonly Rect Lower = new(16, 360, 992, 392);

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(Ground), new Rect(0, 0, ScreenWidth, ScreenHeight));
        switch (g.Mode)
        {
            case Mode.Title:
                DrawTitleArt(dc);
                if (g.Top is { } t) DrawPanel(dc, g.Panels.Count == 1 ? new Rect(312, 470, 400, 190) : new Rect(112, 260, 800, 460), t);
                return;
            case Mode.Court:
                DrawCourt(dc);
                if (g.Top is { } c) DrawPanel(dc, new Rect(112, 330, 800, 410), c);
                return;
            case Mode.Ended:
                DrawTitleArt(dc);
                if (g.Top is { } e) DrawPanel(dc, new Rect(112, 260, 800, 460), e);
                return;
        }
        Box(dc, View.Inflate(4), 0xFF000000);
        using (dc.PushClip(View))
        {
            if (g.ShowMap) DrawChart(dc, View);
            else DrawLand(dc, View);
        }
        DrawStatus(dc);
        if (g.Top is { } p)
        {
            if (p.Kind == PanelKind.Encounter && p.Place is { } s)
            {
                var r = Lower;
                DrawPanel(dc, new Rect(r.X + 300, r.Y, r.Width - 300, r.Height), p);
                Box(dc, new Rect(r.X, r.Y, 292, r.Height), 0xFF101820);
                DrawSettlement(dc, s, new Point(r.X + 146, r.Y + 190), 6);
                Text(dc, s.Name.ToUpperInvariant(), r.X + 146, r.Y + 14, 30, Gold, 1);
                Text(dc, $"{GameEngine.SizeName(s).ToUpperInvariant()} OF THE {g.World.Tribes[s.Tribe].Name.ToUpperInvariant()}", r.X + 146, r.Y + 48, 20, Ink, 1);
            }
            else DrawPanel(dc, Lower, p);
        }
        if (g.InDemo)
        {
            double pulse = 0.6 + 0.4 * Math.Sin(_clock * 4);
            dc.FillRectangle(Brush(0xC0000000), new Rect(352, 40, 320, 50));
            Text(dc, "DEMO - PRESS ANY KEY", 512, 48, 32, Shade(Gold, pulse), 1);
        }
    }

    // ───────────────────────────── the land ─────────────────────────────

    private static uint TerrainColour(Terrain t) => t switch
    {
        Terrain.Deep => 0xFF1A3A6A, Terrain.Shallow => 0xFF2A5A90, Terrain.Beach => 0xFFD8C890, Terrain.Grass => 0xFF7AA850,
        Terrain.Forest => 0xFF3A7A3A, Terrain.Jungle => 0xFF2A6A2A, Terrain.Desert => 0xFFD0B070, Terrain.Hills => 0xFF8A9A5A,
        Terrain.Mountain => 0xFF7A6A5A, Terrain.Snow => 0xFFE8EEF4, _ => 0xFF3A70C0,
    };

    private void DrawLand(DrawingContext dc, Rect v)
    {
        var g = Engine;
        var w = g.World;
        var map = w.Map;
        int cols = (int)(v.Width / Tile) + 1, rows = (int)(v.Height / Tile) + 1;
        var (hx, hy) = g.Here;
        int left = Math.Clamp(hx - cols / 2, 0, Map.Width - cols + 1), top = Math.Clamp(hy - rows / 2, 0, Map.Height - rows + 1);
        int sight = Map.Sight(map[hx, hy]) + 1;
        for (int ty = 0; ty < rows; ty++)
            for (int tx = 0; tx < cols; tx++)
            {
                int x = left + tx, y = top + ty;
                if (x >= Map.Width || y >= Map.Height) continue;
                var r = new Rect(v.X + tx * Tile, v.Y + ty * Tile, Tile, Tile);
                if (!w.HasSeen(x, y)) { dc.FillRectangle(Brush(0xFF05060A), r); continue; }
                bool near = (x - hx) * (x - hx) + (y - hy) * (y - hy) <= sight * sight;
                DrawTile(dc, map, x, y, r, near ? 1 : 0.6);
                if (x >= Map.HomeWaters) dc.FillRectangle(Brush(0x30FFD860), r);
            }
        foreach (var s in w.Settlements.Where(s => s.Found && s.X >= left && s.Y >= top && s.X < left + cols && s.Y < top + rows))
            DrawSettlement(dc, s, new Point(v.X + (s.X - left) * Tile + Tile / 2, v.Y + (s.Y - top) * Tile + Tile * 0.8), 1);
        // The ships (and the shore party, if ashore).
        DrawShip(dc, new Point(v.X + (w.ShipX - left) * Tile + Tile / 2, v.Y + (w.ShipY - top) * Tile + Tile / 2), w.Ships);
        if (!w.Afloat) DrawParty(dc, new Point(v.X + (w.X - left) * Tile + Tile / 2, v.Y + (w.Y - top) * Tile + Tile / 2));
        if (left + cols >= Map.HomeWaters) Text(dc, "HOME WATERS >", v.X + (Map.HomeWaters - left) * Tile + 4, v.Y + 8, 22, Gold);
        if (g.Bumped > 0) dc.FillRectangle(Brush(0x30FF2020), v);
    }

    private void DrawTile(DrawingContext dc, Map map, int x, int y, Rect r, double light)
    {
        var t = map[x, y];
        uint h = (uint)(x * 73856093 ^ y * 19349663);
        uint L(uint c) => Shade(c, light);
        dc.FillRectangle(Brush(L(Shade(TerrainColour(t), 0.94 + (h % 10) / 100.0))), r);
        double cx = r.X + Tile / 2, cy = r.Y + Tile / 2;
        switch (t)
        {
            case Terrain.Deep or Terrain.Shallow:
                if ((h + (int)(_clock * 2)) % 7 == 0) dc.DrawLine(Pen(L(0x60FFFFFF), 1.5), new Point(r.X + 5, cy), new Point(r.X + 13, cy - 2));
                break;
            case Terrain.Grass:
                if (h % 3 == 0) dc.DrawLine(Pen(L(0xFF5A8A3A), 1.5), new Point(cx - 3, cy + 4), new Point(cx - 1, cy - 1));
                break;
            case Terrain.Forest or Terrain.Jungle:
                for (int i = 0; i < (t == Terrain.Jungle ? 3 : 2); i++)
                {
                    double tx = r.X + 5 + ((h >> (i * 3)) % 14), ty = r.Y + 6 + ((h >> (i * 5)) % 12);
                    dc.DrawEllipse(Brush(L(t == Terrain.Jungle ? 0xFF1A5A1A : 0xFF2A6A2A)), null, new Point(tx, ty), 5, 5);
                    dc.FillRectangle(Brush(L(0xFF5A3A1A)), new Rect(tx - 1, ty + 4, 2, 4));
                }
                break;
            case Terrain.Hills:
                dc.DrawEllipse(Brush(L(0xFF9AAA6A)), Pen(L(0xFF6A7A3A)), new Point(cx, cy + 4), 9, 6);
                break;
            case Terrain.Mountain or Terrain.Snow when map.HeightAt(x, y) > 0.84:
            {
                var geo = new StreamGeometry();
                using (var s = geo.Open())
                {
                    s.BeginFigure(new Point(r.X + 2, r.Bottom - 3), true);
                    s.LineTo(new Point(cx, r.Y + 3));
                    s.LineTo(new Point(r.Right - 2, r.Bottom - 3));
                    s.EndFigure(true);
                }
                dc.DrawGeometry(Brush(L(0xFF8A7A6A)), Pen(L(0xFF4A3A2A)), geo);
                dc.DrawEllipse(Brush(L(0xFFF0F0F8)), null, new Point(cx, r.Y + 7), 3, 3);
                break;
            }
            case Terrain.Desert:
                if (h % 2 == 0) dc.FillRectangle(Brush(L(0xFFB09050)), new Rect(r.X + h % 18 + 2, r.Y + (h >> 5) % 18 + 2, 2, 2));
                break;
            case Terrain.River:
                dc.FillRectangle(Brush(L(0xFF7AA850)), r);
                dc.FillRectangle(Brush(L(0xFF3A70C0)), new Rect(r.X + 7, r.Y, 10, Tile));
                break;
        }
    }

    /// <summary>A settlement: huts for a village, more behind a stockade for a town, stone for a city, gold for a golden city.</summary>
    private void DrawSettlement(DrawingContext dc, Settlement s, Point at, double scale)
    {
        var w = Engine.World;
        uint roof = Engine.World.Tribes[s.Tribe].Colour;
        void Hut(double x, double y, double size)
        {
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(new Point(x - size, y), true);
                g.LineTo(new Point(x, y - size * 1.2));
                g.LineTo(new Point(x + size, y));
                g.EndFigure(true);
            }
            dc.FillRectangle(Brush(0xFF8A6A4A), new Rect(x - size * 0.8, y, size * 1.6, size * 0.9));
            dc.DrawGeometry(Brush(roof), Pen(0xFF2A1A10, Math.Max(1, scale * 0.6)), geo);
        }
        double u = 4 * scale;
        if (s.Ruined)
        {
            for (int i = 0; i < 4; i++) dc.FillRectangle(Brush(0xFF5A5A5A), new Rect(at.X - u * 2 + i * u, at.Y - u * (i % 2 + 1), u * 0.9, u * (i % 2 + 1)));
            return;
        }
        switch (s.Size)
        {
            case SettlementSize.Village:
                Hut(at.X - u, at.Y, u * 1.1);
                Hut(at.X + u * 1.2, at.Y + u * 0.3, u);
                break;
            case SettlementSize.Town:
                for (int i = -2; i <= 2; i++) dc.FillRectangle(Brush(0xFF6A4A2A), new Rect(at.X + i * u - 1, at.Y - u * 1.6, scale * 1.4, u * 2));
                Hut(at.X - u * 1.2, at.Y, u);
                Hut(at.X + u * 1.2, at.Y, u);
                Hut(at.X, at.Y - u * 0.6, u * 1.2);
                break;
            case SettlementSize.City:
                dc.FillRectangle(Brush(0xFFB0A890), new Rect(at.X - u * 2.2, at.Y - u * 2, u * 4.4, u * 2));
                dc.FillRectangle(Brush(roof), new Rect(at.X - u * 0.8, at.Y - u * 3.6, u * 1.6, u * 1.6));
                break;
            default:
            {
                // A stepped pyramid of gold, catching the sun.
                double glow = 0.85 + 0.15 * Math.Sin(_clock * 3 + s.Id);
                for (int i = 0; i < 4; i++)
                    dc.FillRectangle(Brush(Shade(0xFFE0B030, glow * (1 - i * 0.05))), new Rect(at.X - u * (2.6 - i * 0.6), at.Y - u * (i + 1) * 0.8, u * (5.2 - i * 1.2), u * 0.8));
                dc.FillRectangle(Brush(0xFFFFF0A0), new Rect(at.X - u * 0.3, at.Y - u * 4, u * 0.6, u * 0.8));
                break;
            }
        }
        if (s.Post)
        {
            dc.DrawLine(Pen(0xFF3A2A1A, Math.Max(1, scale)), new Point(at.X + u * 2, at.Y), new Point(at.X + u * 2, at.Y - u * 3.5));
            dc.FillRectangle(Brush(0xFFC03030), new Rect(at.X + u * 2, at.Y - u * 3.5, u * 1.6, u));
        }
    }

    private void DrawShip(DrawingContext dc, Point at, int ships)
    {
        double bob = Math.Sin(_clock * 3) * 1.5;
        var hull = new StreamGeometry();
        using (var g = hull.Open())
        {
            g.BeginFigure(new Point(at.X - 10, at.Y + 2 + bob), true);
            g.LineTo(new Point(at.X + 10, at.Y + 2 + bob));
            g.LineTo(new Point(at.X + 7, at.Y + 8 + bob));
            g.LineTo(new Point(at.X - 7, at.Y + 8 + bob));
            g.EndFigure(true);
        }
        dc.DrawGeometry(Brush(0xFF6A3A1A), Pen(0xFF2A1A0A), hull);
        dc.DrawLine(Pen(0xFF3A2A1A, 2), new Point(at.X, at.Y + 2 + bob), new Point(at.X, at.Y - 12 + bob));
        dc.FillRectangle(Brush(0xFFF0EAD8), new Rect(at.X - 7, at.Y - 11 + bob, 14, 10));
        dc.DrawEllipse(Brush(0xFFE0B030), null, new Point(at.X, at.Y - 6 + bob), 2.5, 2.5);
        if (ships > 1) Text(dc, ships.ToString(), at.X + 10, at.Y - 18, 18, White, 0);
    }

    private const uint White = 0xFFF2F2F2;

    private static void DrawParty(DrawingContext dc, Point at)
    {
        for (int i = -1; i <= 1; i++)
        {
            double x = at.X + i * 5, y = at.Y + Math.Abs(i) * 2;
            dc.DrawEllipse(Brush(0xFFE0B090), null, new Point(x, y - 6), 2.5, 2.5);
            dc.FillRectangle(Brush(0xFF4A5AA0), new Rect(x - 2, y - 3, 4, 8));
        }
        dc.DrawLine(Pen(0xFF3A2A1A, 1.5), new Point(at.X + 9, at.Y + 5), new Point(at.X + 9, at.Y - 13));
        dc.FillRectangle(Brush(0xFFE0B030), new Rect(at.X + 9, at.Y - 13, 8, 5));
    }

    /// <summary>The whole chart: everything seen so far, with the settlements found.</summary>
    private void DrawChart(DrawingContext dc, Rect v)
    {
        var g = Engine;
        var w = g.World;
        double sx = v.Width / Map.Width, sy = v.Height / Map.Height;
        dc.FillRectangle(Brush(0xFFD8C8A0), v);
        for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                if (!w.HasSeen(x, y)) continue;
                dc.FillRectangle(Brush(TerrainColour(w.Map[x, y])), new Rect(v.X + x * sx, v.Y + y * sy, sx + 0.5, sy + 0.5));
            }
        dc.FillRectangle(Brush(0x40FFD860), new Rect(v.X + Map.HomeWaters * sx, v.Y, v.Width - Map.HomeWaters * sx, v.Height));
        foreach (var s in w.Settlements.Where(s => s.Found))
        {
            var p = new Point(v.X + (s.X + 0.5) * sx, v.Y + (s.Y + 0.5) * sy);
            uint c = s.Ruined ? 0xFF505050 : s.Golden ? 0xFFFFD040 : s.Post ? 0xFFC03030 : w.Tribes[s.Tribe].Colour;
            dc.DrawEllipse(Brush(c), Pen(0xFF101010), p, s.Golden ? 5 : 3.5, s.Golden ? 5 : 3.5);
        }
        var (hx, hy) = g.Here;
        double pulse = 4 + 2 * Math.Sin(_clock * 6);
        dc.DrawEllipse(null, Pen(0xFFFF3030, 2), new Point(v.X + (hx + 0.5) * sx, v.Y + (hy + 0.5) * sy), pulse + 3, pulse + 3);
        Text(dc, "THE CHART", v.X + 12, v.Y + 8, 28, 0xFF3A2A1A);
        Text(dc, $"{w.Explored}% of the land charted. M to close.", v.Right - 12, v.Y + 12, 22, 0xFF3A2A1A, 2);
        Text(dc, "HOME >", v.Right - 12, v.Bottom - 32, 24, 0xFF6A4A1A, 2);
    }

    private void DrawStatus(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        var r = Status;
        Box(dc, r);
        Text(dc, w.Date, r.X + 16, r.Y + 8, 24, Gold);
        Text(dc, $"{WorldState.Ranks[w.Rank]}  -  VOYAGE {w.Voyage}", r.X + 16, r.Y + 34, 22, Ink);
        var (hx, hy) = g.Here;
        var tribe = w.Tribes[Peoples.Owner(w.Tribes, hx, hy)];
        string where = w.Afloat ? (hx >= Map.HomeWaters - 6 ? "near home waters" : Map.Describe(w.Map[hx, hy])) : $"{Map.Describe(w.Map[hx, hy])}, {tribe.Country}";
        Text(dc, where.ToUpperInvariant(), r.X + 16, r.Y + 58, 20, Dim);
        double x = r.X + 330;
        string[] labels = ["SHIPS", "MEN", "FOOD", "GOODS", "GOLD"];
        string[] values = [w.Ships.ToString(), w.Afloat ? w.Men.ToString() : $"{w.Ashore}+{w.Keepers}", $"{w.DaysOfFood}d", w.Goods.ToString(), w.Gold.ToString()];
        for (int i = 0; i < labels.Length; i++)
        {
            Text(dc, labels[i], x + i * 92, r.Y + 8, 20, Dim);
            uint col = i == 2 && w.DaysOfFood < 15 ? Bad : i == 4 ? Gold : Ink;
            Text(dc, values[i], x + i * 92, r.Y + 30, 28, col);
        }
        Text(dc, $"RENOWN {w.Score}", r.Right - 16, r.Y + 8, 22, Gold, 2);
        var log = g.Log.Skip(Math.Max(0, g.Log.Count - 2)).ToList();
        for (int i = 0; i < log.Count; i++) Text(dc, log[i].Text, r.X + 16, r.Y + 80 + i * 22, 20, i == log.Count - 1 ? log[i].Colour : Shade(log[i].Colour, 0.6));
        Text(dc, "ARROWS MOVE  M CHART  F CAMP  ESC CABIN", r.Right - 16, r.Y + 60, 18, 0xFF6A6258, 2);
    }

    // ───────────────────────────── panels ─────────────────────────────

    private void DrawPanel(DrawingContext dc, Rect r, Game.Panel p)
    {
        Box(dc, r);
        double x = r.X + 18, width = r.Width - 36, y = r.Y + 10;
        const double size = 23, lh = 24;
        if (p.Title.Length > 0)
        {
            Text(dc, p.Title, x, y, 28, Gold);
            y += 34;
        }
        foreach (var para in p.Text)
        {
            foreach (var line in Wrap(para, width, size))
            {
                if (y > r.Bottom - lh * 2) break;
                Text(dc, line, x, y, size, Ink);
                y += lh;
            }
            y += 4;
        }
        string? note = p.Options.Count > 0 ? p.Options[p.Selected].Note : null;
        double noteSpace = note is null ? 0 : lh * Math.Min(2, Wrap(note, width, 21).Count) + 6;
        int room = Math.Max(1, (int)((r.Bottom - 10 - noteSpace - y) / lh));
        int first = Math.Clamp(p.Selected - room / 2, 0, Math.Max(0, p.Options.Count - room));
        for (int i = first; i < Math.Min(p.Options.Count, first + room); i++)
        {
            var o = p.Options[i];
            bool sel = i == p.Selected;
            if (sel) dc.FillRectangle(Brush(0xFF3A2E1C), new Rect(x - 6, y, width + 12, lh));
            uint col = !o.Enabled ? 0xFF5A544C : sel ? Gold : Ink;
            Text(dc, $"{(i < 9 ? $"{i + 1}." : "  ")} {o.Label}", x, y, size, col);
            y += lh;
        }
        if (note is not null)
        {
            double ny = r.Bottom - noteSpace - 4;
            foreach (var line in Wrap(note, width, 21).Take(2))
            {
                Text(dc, line, x, ny, 21, Blue);
                ny += lh;
            }
        }
    }

    // ───────────────────────────── the title and the court ─────────────────────────────

    private void DrawTitleArt(DrawingContext dc)
    {
        for (int i = 0; i < 40; i++)
        {
            double t = i / 39.0;
            dc.FillRectangle(Brush(Mix(0xFF1A2050, 0xFFE09050, t)), new Rect(0, i * 10, ScreenWidth, 11));
        }
        dc.DrawEllipse(Brush(0xFFFFE0A0), null, new Point(512, 400), 70, 70);
        for (int i = 0; i < 37; i++)
            dc.FillRectangle(Brush(Mix(0xFF2A4A7A, 0xFF0A1A3A, i / 36.0)), new Rect(0, 400 + i * 10, ScreenWidth, 11));
        // The far shore, and on it a city that shines.
        dc.FillRectangle(Brush(0xFF2A3A2A), new Rect(0, 384, 420, 18));
        for (int i = 0; i < 4; i++)
            dc.FillRectangle(Brush(0xFFE0B030), new Rect(160 - (30 - i * 7), 384 - (i + 1) * 9, (30 - i * 7) * 2, 9));
        for (int i = 0; i < 8; i++)
            dc.DrawLine(Pen(0x40FFF0A0, 2), new Point(512 - 140 + i * 40, 404 + i % 3 * 18), new Point(512 - 110 + i * 40, 404 + i % 3 * 18));
        DrawShip(dc, new Point(700, 430), 1);
        Shadowed(dc, "AVA CITIES OF GOLD", 512, 60, 96, 0xFFFFD860, 1);
        Shadowed(dc, "West, beyond the edge of every chart", 512, 160, 32, 0xFFF0E0C0, 1);
        Text(dc, "Explore a new land, trade with its peoples, and seek the Seven Cities of Gold. A new world every game.", 512, 736, 21, 0xFFB0A0C0, 1);
    }

    private void DrawCourt(DrawingContext dc)
    {
        dc.FillRectangle(Brush(0xFF2A1A20), new Rect(0, 0, ScreenWidth, ScreenHeight));
        for (int i = 0; i < 6; i++)
        {
            double x = 60 + i * 170;
            dc.FillRectangle(Brush(0xFF3A2A30), new Rect(x, 0, 60, 320));
            dc.FillRectangle(Brush(i % 2 == 0 ? 0xFF8A1A2A : 0xFF1A3A7A), new Rect(x + 8, 30, 44, 140));
            dc.DrawEllipse(Brush(0xFFE0B030), null, new Point(x + 30, 90), 12, 12);
        }
        dc.FillRectangle(Brush(0xFF6A1A2A), new Rect(412, 140, 200, 180));
        dc.FillRectangle(Brush(0xFFE0B030), new Rect(452, 110, 120, 40));
        dc.FillRectangle(Brush(0xFF3A1A10), new Rect(0, 300, ScreenWidth, 30));
        Shadowed(dc, "THE ROYAL COURT", 512, 30, 56, Gold, 1);
    }
}

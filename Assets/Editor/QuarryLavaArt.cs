using System;

public static class QuarryLavaArt
{
    public const int Size = 32;
    public const int FrameCount = 4;
    public const string Palette = ".#royw";

    public static char Pixel(int x, int y, int frame, bool surface)
    {
        double phase = (x + frame * 8) * Math.PI * 2 / Size;
        if (surface)
        {
            int crest = 29 + (int)Math.Round(Math.Sin(phase));
            if (y > crest) return '.';
            if (y >= crest - 1) return 'w';
            if (y >= crest - 3) return 'y';
        }

        double vertical = y * Math.PI * 2 / Size;
        double flow = Math.Sin(phase + Math.Sin(vertical)) + Math.Cos(vertical * 2 - phase);
        if (flow > 1.45) return '#';
        if (flow > 0.65) return 'r';
        if (flow < -1.4) return 'y';
        return 'o';
    }
}

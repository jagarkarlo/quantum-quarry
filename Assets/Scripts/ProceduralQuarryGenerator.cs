using System;

public static class ProceduralQuarryGenerator
{
    public static CustomLevelDocument Generate(int seed, int difficultyTier = 2)
    {
        int clampedDifficulty = Math.Max(1, Math.Min(3, difficultyTier));
        uint state = unchecked((uint)seed * 747796405u + 2891336453u);
        if (state == 0) state = 7319u;

        var doc = new CustomLevelDocument
        {
            formatVersion = CustomLevelDocument.CurrentFormatVersion,
            levelId = $"proc-{Math.Abs(seed) % 100000}",
            title = $"Procedural Quarry #{Math.Abs(seed) % 10000:0000}",
            sourceCampaignLevel = 4,
            width = 56,
            height = 26
        };

        int curX = 0;
        int curY = 2;

        for (int x = 0; x <= 5; x++) doc.SetTile(x, curY, "ground");
        doc.SetSpawn(2, curY + 1, 2f, curY + 1f);
        curX = 6;

        int segments = 5 + clampedDifficulty;
        for (int seg = 0; seg < segments; seg++)
        {
            int stepType = NextRange(ref state, 0, 4);
            int segLen = NextRange(ref state, 4, 7);

            if (stepType == 0 && curY < 14)
            {
                int climbHeight = NextRange(ref state, 2, 4);
                doc.SetTile(curX, curY, "ground");
                for (int ly = curY + 1; ly <= curY + climbHeight; ly++)
                    doc.SetTile(curX, ly, "ladder");
                curY += climbHeight;
                curX++;
            }
            else if (stepType == 1 && curY < 13)
            {
                doc.SetTile(curX, curY, "ground");
                int padY = curY + 1;
                doc.SetObject(curX, padY, "jump_pad", curX, curY + 0.5f);
                curY += 3;
                curX += 2;
            }
            else if (stepType == 2 && curY > 3)
            {
                curY -= NextRange(ref state, 1, 2);
                curX++;
            }

            for (int i = 0; i < segLen; i++)
            {
                doc.SetTile(curX + i, curY, "ground");
                if (i == 1)
                {
                    string coinId = (seg >= 2 && NextRange(ref state, 0, 2) == 1) ? "coin_150" : "coin_100";
                    doc.SetObject(curX + i, curY + 1, coinId, curX + i, curY + 1f);
                }
                else if (i == 3 && segLen >= 5)
                {
                    PlaceSegmentFeature(ref state, doc, curX + i, curY, seg, segments, clampedDifficulty);
                }
            }

            curX += segLen;
        }

        for (int x = curX; x <= curX + 4; x++)
            doc.SetTile(x, curY, "ground");

        doc.SetObject(curX + 1, curY + 1, "vent", curX + 1, curY + 0.5f);
        doc.SetExit(curX + 3, curY + 1, curX + 3f, curY + 1f);
        doc.width = Math.Min(CustomLevelDocument.MaxWidth, Math.Max(40, curX + 10));
        return doc;
    }

    static void PlaceSegmentFeature(ref uint state, CustomLevelDocument doc, int x, int floorY,
        int segIndex, int totalSegments, int difficulty)
    {
        if (segIndex == totalSegments / 2)
        {
            doc.SetObject(x, floorY + 1, "bank", x, floorY + 0.5f);
            return;
        }

        int roll = NextRange(ref state, 0, 5);
        if (roll == 0)
        {
            doc.SetObject(x, floorY + 1, "crate_250", x, floorY + 0.5f);
        }
        else if (roll == 1)
        {
            doc.SetObject(x, floorY + 1, difficulty >= 2 ? "red_enemy" : "enemy", x, floorY + 0.5f);
        }
        else if (roll == 2 && difficulty >= 2)
        {
            doc.SetObject(x, floorY + 1, "shielded_enemy", x, floorY + 0.5f);
        }
        else if (roll == 3 && difficulty >= 2)
        {
            doc.SetObject(x, floorY + 1, "sentry", x, floorY + 0.5f);
        }
        else
        {
            doc.SetObject(x, floorY + 1, "coin_200", x, floorY + 1f);
        }
    }

    static int NextRange(ref uint state, int minInclusive, int maxExclusive)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        int span = Math.Max(1, maxExclusive - minInclusive);
        return minInclusive + (int)(state % (uint)span);
    }
}

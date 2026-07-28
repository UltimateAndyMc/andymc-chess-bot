using System;

public partial class AttackTables
{
    public static ulong[] KingAttacks = new ulong[64];
    public static ulong[] KnightAttacks = new ulong[64];

    static AttackTables()
    {
        for (int square = 0; square < 64; square++)
        {
            KingAttacks[square] = GenerateKingAttacks(square);
            KnightAttacks[square] = GenerateKnightAttacks(square);
        }
    }


    private static ulong GenerateKingAttacks(int square)
    {
        ulong attacks = 0;
        int rank = square / 8;
        int file = square % 8;

        for (int fileChange = -1; fileChange <= 1; fileChange++)
        { 
            int newFile = file + fileChange;
            if (newFile < 0 || newFile >= 8) continue;
            for (int rankChange = -1; rankChange <= 1; rankChange++)
            {
                if (fileChange == 0 && rankChange == 0) continue;
                int newRank = rank + rankChange;
                if (newRank < 0 || newRank >= 8) continue;

                int target = newRank * 8 + newFile;
                ulong attackBit = 1UL << target;
                attacks |= attackBit;
            }
        }
        return attacks;
    }
    private static ulong GenerateKnightAttacks(int square)
    {
        ulong attacks = 0;
        int rank = square / 8;
        int file = square % 8;

        int[] fileOffsets = [-2, -2, -1, -1, 1, 1, 2, 2];
        int[] rankOffsets = [-1, 1, -2, 2, -2, 2, -1, 1];

        for (int i = 0; i < 8; i++)
        {
            int newFile = file + fileOffsets[i];
            int newRank = rank + rankOffsets[i];
            if (newFile < 0 || newFile >= 8 || newRank < 0 || newRank >= 8) continue;

            int target = newRank * 8 + newFile;
            ulong attackBit = 1UL << target;
            attacks |= attackBit;
        }
        return attacks;
    }
}

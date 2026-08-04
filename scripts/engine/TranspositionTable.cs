using System;
using System.Runtime.InteropServices;

public enum TTFlag : byte {Exact, LowerBound, UpperBound}
public struct TTEntry()
{
    public ulong Key = 0;
    public float Eval = 0f;
    public byte Depth = 0;
    public TTFlag Flag = TTFlag.Exact;
    public MoveInfo Move = new(-1, -1, Piece.E);
}
public partial class TranspositionTable
{
    private TTEntry[] table;
    private int size;
    private int usedEntries = 0;
    public TranspositionTable(int bits)
    {
        size = (int)Math.Pow(2, bits);
        table = new TTEntry[size];
    }

    // Returns storage used in bytes
    public int GetStorageUsed()
    {
        return usedEntries * Marshal.SizeOf<TTEntry>();
    }
    public void Store(ulong key, float eval, MoveInfo move, byte depth, TTFlag flag)
    {
        int index = (int)(key % (ulong)size);
        if (table[index].Key != key)
        {
            usedEntries++;
        }
        table[index] = new TTEntry { Key = key, Eval = eval, Move = move, Depth = depth, Flag = flag };
    }
    public bool IsStored(ulong key)
    {
        int index = (int)(key % (ulong)size);
        return table[index].Key == key;
    }
    public TTEntry Retrieve(ulong key)
    {
        int index = (int)(key % (ulong)size);
        return table[index];
    }
}

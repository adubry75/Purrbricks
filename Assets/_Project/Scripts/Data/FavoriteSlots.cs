public sealed class FavoriteSlots
{
    readonly int[] values = { -1, -1, -1 };
    public int Get(int slot) => slot >= 0 && slot < 3 ? values[slot] : -1;
    public bool Assign(int slot, int type)
    {
        if (slot < 0 || slot >= 3 || type < -1 || type >= 22) return false;
        for (int i = 0; i < 3; i++) if (values[i] == type) values[i] = -1;
        values[slot] = type;
        return true;
    }
}

using System;

[Serializable]
public class Matrix
{
    public int size = 3;
    [Serializable]
    public class Row
    {
        public int[] elements = new int[1];
    }

    public Row[] rows = new Row[1];

    public int At(int x, int y)
    {
        return rows[y].elements[x];
    }
}

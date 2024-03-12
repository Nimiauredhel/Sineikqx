using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    public const int GRID_SIZE = 64;
}

public enum CellState
{
    None = -1,
    Free = 12,
    Enemy = 37,
    Marked = 62,
    Edge = 87,
    Taken = 100
}
using System.Collections.Generic;
using UnityEngine;

public static class Global
{
    public const int GRID_SIZE = 64;
    public const float COMPLETION_GOAL = 0.8f;
    
    private static System.Random Random = new System.Random();  

    public static void Shuffle<T>(this IList<T> list)  
    {  
        int n = list.Count;  
        while (n > 1) {  
            n--;  
            int k = Random.Next(n + 1);  
            (list[k], list[n]) = (list[n], list[k]);
        }  
    }
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
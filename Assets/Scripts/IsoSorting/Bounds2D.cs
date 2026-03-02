using System;
using UnityEngine;

[Serializable]
public struct Bounds2D
{
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;

    public Bounds2D(Bounds bounds)
    {
        minX = bounds.min.x;
        minY = bounds.min.y;
        maxX = bounds.max.x;
        maxY = bounds.max.y;
    }

    public Bounds2D(float _minX, float _minY, float _maxX, float _maxY)
    {
        minX = _minX;
        minY = _minY;
        maxX = _maxX;
        maxY = _maxY;
    }

    public bool Intersects(Bounds2D other)
    {
        return minX <= other.maxX && other.minX <= maxX &&
               maxY >= other.minY && other.maxY >= minY;
    }

    public bool Contains(Vector2 point)
    {
        return point.x >= minX && point.x <= maxX &&
               point.y >= minY && point.y <= maxY;
    }

    public override string ToString() =>
        $"Min:({minX:F2},{minY:F2}) Max:({maxX:F2},{maxY:F2})";
}
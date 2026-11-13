using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// HEAPING - spots arrive the way a heap grows from the ground: bottom up, always resting on what
/// is already there. A spot's key is its CLIMB: the cheapest way to it through the shape from the
/// ground line (on a grid of 3-unit cells, 8 neighbours each), where a step up costs its length, a
/// step sideways <see cref="SidewaysCost"/> times as much and a step down <see cref="DownCost"/>
/// times - so a tall leg rises from its foot, a leaning arm grows along itself, and a bridge fills
/// in from its supports towards its middle. On top of that, spots nearer the edge than
/// <see cref="EdgeLagReach"/> wait a little (<see cref="EdgeLag"/> per unit: the top of a growing
/// heap is rounded), a slowly varying noise across the shape (<see cref="Lumpiness"/> over
/// <see cref="LumpWidth"/>) makes the growing top lumpy, and each spot gets a random
/// <see cref="Scatter"/>. Keys are in units of climb, so letters standing on one ground line grow
/// together when their spots are sorted together. Seeded; shared managed arithmetic.
/// </summary>
public sealed class HeapArrivalOrder : IArrivalOrder
{
    private const float Cell = 3f;
    private float[] _climb;
    private float _left;
    private float _top;
    private int _columns;
    private int _rows;
    private DrawingRandom _random;
    private long _noiseSeed;

    /// <summary>Creates the order.</summary>
    /// <param name="groundY">The ground line (y): the shape's cells within two cells above it (and below it) are where the heap starts.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="groundY"/> is not finite.</exception>
    public HeapArrivalOrder(float groundY)
    {
        if (!Single.IsFinite(groundY)) { throw new ArgumentOutOfRangeException(nameof(groundY)); }
        GroundY = groundY;
    }

    /// <summary>The ground line, calibrated units (y).</summary>
    public float GroundY { get; }

    /// <summary>The cost of a sideways step relative to a step up; 1.7 by default.</summary>
    public float SidewaysCost { get; set; } = 1.7f;

    /// <summary>The cost of a step down relative to a step up; 2.4 by default.</summary>
    public float DownCost { get; set; } = 2.4f;

    /// <summary>How much later, per unit, spots nearer than <see cref="EdgeLagReach"/> to the edge arrive; 0.6 by default.</summary>
    public float EdgeLag { get; set; } = 0.6f;

    /// <summary>The distance from the edge within which spots lag, calibrated units; 24 by default.</summary>
    public float EdgeLagReach { get; set; } = 24f;

    /// <summary>The size of the lumps along the growing top, units of climb; 15 by default.</summary>
    public float Lumpiness { get; set; } = 15f;

    /// <summary>The width of one lump, calibrated units; 52 by default.</summary>
    public float LumpWidth { get; set; } = 52f;

    /// <summary>A per-spot random spread of the keys, units of climb; 6 by default.</summary>
    public float Scatter { get; set; } = 6f;

    /// <inheritdoc />
    public void Prepare(FormationArea area, long seed)
    {
        if (area == null) { throw new ArgumentNullException(nameof(area)); }
        _random = new DrawingRandom(seed ^ 0x2545F4914F6CDD1DL);
        _noiseSeed = seed;
        _left = area.Bounds.Left;
        _top = area.Bounds.Top;
        _columns = (int)Math.Ceiling(area.Bounds.Width / Cell) + 1;
        _rows = (int)Math.Ceiling(area.Bounds.Height / Cell) + 1;
        var inside = new bool[_columns * _rows];
        _climb = new float[_columns * _rows];
        var queue = new PriorityQueue<int, float>();
        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _columns; x++)
            {
                int i = (y * _columns) + x;
                float px = _left + ((x + 0.5f) * Cell);
                float py = _top + ((y + 0.5f) * Cell);
                inside[i] = area.Contains(px, py);
                _climb[i] = Single.PositiveInfinity;
                if (inside[i] && py >= GroundY - (Cell * 2))
                {
                    _climb[i] = 0;
                    queue.Enqueue(i, 0);
                }
            }
        }

        while (queue.TryDequeue(out int i, out float c))
        {
            if (c > _climb[i]) { continue; }
            int x = i % _columns;
            int y = i / _columns;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) { continue; }
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows) { continue; }
                    int n = (ny * _columns) + nx;
                    if (!inside[n]) { continue; }
                    float vertical = dy < 0 ? 1f : dy > 0 ? DownCost : 0f;
                    float sideways = SidewaysCost * dx;
                    float step = Cell * MathF.Sqrt((sideways * sideways) + (vertical * vertical * dy * dy));
                    if (c + step < _climb[n])
                    {
                        _climb[n] = c + step;
                        queue.Enqueue(n, _climb[n]);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Prepare"/>.</exception>
    public float GetKey(float x, float y, float clearance)
    {
        if (_climb == null) { throw new InvalidOperationException("Prepare the order with the area first."); }
        int cx = Math.Clamp((int)((x - _left) / Cell), 0, _columns - 1);
        int cy = Math.Clamp((int)((y - _top) / Cell), 0, _rows - 1);
        float c = _climb[(cy * _columns) + cx];
        double lump = Lumpiness * DrawingNoise.Gradient(x / LumpWidth, 0.37, _noiseSeed);
        return c + (EdgeLag * MathF.Max(0, EdgeLagReach - clearance)) + (float)lump + (float)_random.Uniform(-Scatter, Scatter);
    }
}

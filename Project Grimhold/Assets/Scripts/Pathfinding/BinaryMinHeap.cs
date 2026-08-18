using System;

/// <summary>
/// A minimal binary min-heap backed by a preallocated array, used by
/// <see cref="AStarPathSolver"/> as the open list.
///
/// Stores (cost, x, y) triples so the solver can extract the node with the
/// lowest F-cost without heap allocations per operation.
///
/// This type is not thread-safe and is intended for single-call-site use
/// within a single <c>FindPath</c> invocation.
/// </summary>
internal sealed class BinaryMinHeap
{
    private readonly (int cost, int x, int y)[] _data;
    private int _count;

    /// <summary>Creates a heap with the given preallocated capacity.</summary>
    internal BinaryMinHeap(int capacity)
    {
        _data  = new (int, int, int)[capacity];
        _count = 0;
    }

    /// <summary>Number of elements currently in the heap.</summary>
    internal int Count => _count;

    /// <summary>Removes all elements without releasing the backing array.</summary>
    internal void Clear()
    {
        _count = 0;
    }

    /// <summary>Inserts a node with the given cost.</summary>
    internal void Push(int cost, int x, int y)
    {
        if (_count >= _data.Length)
        {
            // This should never be reached if the heap capacity equals the grid
            // node count, but guard against it to avoid array index exceptions.
            throw new InvalidOperationException(
                $"{nameof(BinaryMinHeap)} is full (capacity {_data.Length}). " +
                "Increase MaxPathIterations or the heap capacity.");
        }

        _data[_count] = (cost, x, y);
        BubbleUp(_count);
        _count++;
    }

    /// <summary>Returns the element with the lowest cost without removing it.</summary>
    internal (int cost, int x, int y) Peek()
    {
        return _data[0];
    }

    /// <summary>Removes and returns the element with the lowest cost.</summary>
    internal (int cost, int x, int y) Pop()
    {
        (int cost, int x, int y) top = _data[0];
        _count--;
        if (_count > 0)
        {
            _data[0] = _data[_count];
            PushDown(0);
        }
        return top;
    }

    // ── Heap operations ───────────────────────────────────────────────────────

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_data[parent].cost <= _data[index].cost) break;
            Swap(parent, index);
            index = parent;
        }
    }

    private void PushDown(int index)
    {
        while (true)
        {
            int left  = 2 * index + 1;
            int right = 2 * index + 2;
            int smallest = index;

            if (left  < _count && _data[left].cost  < _data[smallest].cost) smallest = left;
            if (right < _count && _data[right].cost < _data[smallest].cost) smallest = right;

            if (smallest == index) break;
            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        (_data[a], _data[b]) = (_data[b], _data[a]);
    }
}

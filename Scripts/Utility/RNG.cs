using System;

public class RNG
{
    private uint _state;

    public RNG()
    {
        // Use the current time to seed the generator
        _state = (uint)DateTime.Now.Ticks;
        if (_state == 0) _state = 1; // Ensure the seed is not zero
    }

    public RNG(uint seed)
    {
        _state = seed;
        if (_state == 0) _state = 1; // Ensure the seed is not zero
    }

    public uint NextUInt32()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    public int NextInt32()
    {
        return (int)NextUInt32();
    }

    public int NextInt32(int minValue, int maxValue)
    {
        return minValue + (int)(NextUInt32() % (maxValue - minValue));
    }

    public double NextDouble()
    {
        return NextUInt32() / (double)uint.MaxValue;
    }

    public float NextFloat()
    {
        return NextUInt32() / (float)uint.MaxValue;
    }
}
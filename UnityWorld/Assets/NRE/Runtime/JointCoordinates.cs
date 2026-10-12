using System;

namespace Nre.UnityWorld
{
    // Unity 6000.3.19f1 exposes three slots for a two-axis spherical joint,
    // but packs its unlocked X/Y or X/Z coordinates into slots 0 and 1.
    // NativeCoordinateProbe independently checks force, position, velocity and
    // named drives against physical rotation. Slot 2 is not the Z coordinate.
    public readonly struct JointCoordinates
    {
        public int ActiveCount { get; }
        public int NativeCount => ActiveCount == 2 ? 3 : ActiveCount;
        public int SecondIndex => ActiveCount == 2 ? 1 : -1;
        public int UnusedIndex => ActiveCount == 2 ? 2 : -1;
        public int SecondPhysicalAxis { get; }

        public JointCoordinates(int activeCount, bool secondIsY)
        {
            if (activeCount < 0 || activeCount > 2) throw new ArgumentOutOfRangeException(nameof(activeCount));
            ActiveCount = activeCount;
            SecondPhysicalAxis = secondIsY ? 1 : 2;
        }

        public int NativeIndex(int commandAxis)
        {
            if (commandAxis < 0 || commandAxis >= ActiveCount)
                throw new ArgumentOutOfRangeException(nameof(commandAxis));
            return commandAxis == 0 ? 0 : SecondIndex;
        }

        public float[] Pack(float first, float second = 0)
        {
            var coordinates = new float[NativeCount];
            if (ActiveCount > 0) coordinates[0] = first;
            if (ActiveCount == 2) coordinates[SecondIndex] = second;
            return coordinates;
        }

        public void ValidateNativeCount(int count)
        {
            if (count != NativeCount)
                throw new InvalidOperationException($"Expected {NativeCount} native coordinates for {ActiveCount} active axes; observed {count}.");
        }
    }
}

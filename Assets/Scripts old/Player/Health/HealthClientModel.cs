using System;

namespace Game
{
    public sealed class HealthClientModel
    {
        public int Current { get; private set; }
        public int Max     { get; private set; }

        public event Action<int,int> OnChanged;

        public void Apply(int current, int max)
        {
            Max     = max < 1 ? 1 : max;
            Current = Math.Clamp(current, 0, Max);
            OnChanged?.Invoke(Current, Max);
        }
    }
}

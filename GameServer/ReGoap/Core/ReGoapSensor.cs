namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Base class for GOAP sensors
    /// Provides default implementation for sensor functionality
    /// </summary>
    public abstract class ReGoapSensor<TKey, TValue> : IReGoapSensor<TKey, TValue>
    {
        protected IReGoapAgent<TKey, TValue> agent;
        protected ReGoapMemory<TKey, TValue> memory;

        public virtual void Init(IReGoapAgent<TKey, TValue> agent)
        {
            this.agent = agent;
            this.memory = agent.GetMemory();
        }

        public abstract void UpdateSensor();
    }
}

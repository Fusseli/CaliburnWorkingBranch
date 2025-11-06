using System;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Base class for GOAP actions
    /// Provides default implementation for common action functionality
    /// </summary>
    public abstract class ReGoapAction<TKey, TValue> : IReGoapAction<TKey, TValue>
    {
        protected ReGoapState<TKey, TValue> preconditions;
        protected ReGoapState<TKey, TValue> effects;
        protected string name;
        protected bool interruptible = true;

        protected ReGoapAction()
        {
            preconditions = new ReGoapState<TKey, TValue>();
            effects = new ReGoapState<TKey, TValue>();
            name = GetType().Name;
        }

        public virtual string GetName()
        {
            return name;
        }

        public virtual ReGoapState<TKey, TValue> GetPreconditions(IReGoapAgent<TKey, TValue> agent)
        {
            return preconditions;
        }

        public virtual ReGoapState<TKey, TValue> GetEffects(IReGoapAgent<TKey, TValue> agent)
        {
            return effects;
        }

        public virtual float GetCost(IReGoapAgent<TKey, TValue> agent, ReGoapState<TKey, TValue> currentState)
        {
            return 1.0f;
        }

        public virtual bool CheckPreconditions(IReGoapAgent<TKey, TValue> agent, ReGoapState<TKey, TValue> currentState)
        {
            return currentState.MeetsGoal(preconditions);
        }

        public abstract bool Run(IReGoapAgent<TKey, TValue> agent,
                                  Action<IReGoapAction<TKey, TValue>> doneCallback,
                                  Action<IReGoapAction<TKey, TValue>> failCallback);

        public virtual bool IsInterruptible()
        {
            return interruptible;
        }

        public override string ToString()
        {
            return GetName();
        }
    }
}

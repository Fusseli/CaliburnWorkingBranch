namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Base class for GOAP goals
    /// Provides default implementation for common goal functionality
    /// </summary>
    public abstract class ReGoapGoal<TKey, TValue> : IReGoapGoal<TKey, TValue>
    {
        protected ReGoapState<TKey, TValue> goalState;
        protected string name;

        protected ReGoapGoal()
        {
            goalState = new ReGoapState<TKey, TValue>();
            name = GetType().Name;
        }

        public virtual string GetName()
        {
            return name;
        }

        public abstract ReGoapState<TKey, TValue> GetGoalState();

        public abstract float GetPriority(ReGoapState<TKey, TValue> currentState);

        public virtual bool IsGoalSatisfied(ReGoapState<TKey, TValue> currentState)
        {
            return currentState.MeetsGoal(GetGoalState());
        }

        public override string ToString()
        {
            return GetName();
        }
    }
}

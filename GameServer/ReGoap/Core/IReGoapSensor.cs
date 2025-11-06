namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Interface for sensors that update the agent's world state
    /// Sensors read game state and write to the agent's memory
    /// </summary>
    public interface IReGoapSensor<TKey, TValue>
    {
        /// <summary>
        /// Initializes the sensor with the agent
        /// Called once when the agent is created
        /// </summary>
        void Init(IReGoapAgent<TKey, TValue> agent);

        /// <summary>
        /// Updates the world state with current information
        /// Called every think tick before planning
        /// </summary>
        void UpdateSensor();
    }
}

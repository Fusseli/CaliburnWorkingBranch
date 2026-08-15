namespace DOL.GS.Scripts
{
    public class RenegadeSpawnerStealth : RenegadeSpawnerPersistent
    {
        protected override eMimicClass GetRandomClassForSpawn()
        {
            return MimicManager.GetRandomStealthClass();
        }
    }
}
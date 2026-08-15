namespace DOL.GS.Scripts
{
    public class MimicSpawnerStealth : MimicSpawnerPersistent
    {
        protected override eMimicClass GetRandomMimicClassForSpawn()
        {
            return MimicManager.GetRandomStealthClass(this.Realm);
        }
    }
}
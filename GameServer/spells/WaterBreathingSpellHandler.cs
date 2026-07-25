using DOL.GS.Effects;

namespace DOL.GS.Spells
{
	[SpellHandlerAttribute("WaterBreathing")]
	public class WaterBreathingSpellHandler : SpellHandler
	{
		public WaterBreathingSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override ECSGameSpellEffect CreateECSEffect(ECSGameEffectInitParams initParams)
        {
            return new WaterBreathingECSEffect(initParams);
        }

        protected override int CalculateEffectDuration(GameLiving target)
        {
            double duration = Spell.Duration;
            duration *= 1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01;
            return (int)duration;
        }
    }
}

using System;
using System.Linq;
using DOL.Database;
using DOL.Events;
using DOL.GS.Keeps;
using DOL.GS.Scripts;

namespace DOL.GS.ServerRules;

[ServerRules(EGameServerType.GST_Casual)]
public class CoopWithRvr : AbstractServerRules
{
    private static AbstractServerRules pveServerRules = new PvEServerRules();
    private static AbstractServerRules normalServerRules = new NormalServerRules();

    public override string RulesDescription()
        => "PvE/Coop rules in PvE zones and realm vs realm in RvR zones.";

    public override bool IsAllowedCharsInAllRealms(GameClient client)
        => true;

    public override bool IsAllowedToGroup(IGamePlayer source, IGamePlayer target, bool quiet)
        => SelectRuleSet((GameObject)source).IsAllowedToGroup(source, target, quiet);

    public override bool IsAllowedToJoinGuild(GamePlayer source, Guild guild)
        => SelectRuleSet(source).IsAllowedToJoinGuild(source, guild);

    public override bool IsAllowedToTrade(GameLiving source, GameLiving target, bool quiet)
        => SelectRuleSet(source).IsAllowedToTrade(source, target, quiet);

    public override bool IsAllowedToUnderstand(GameLiving source, GamePlayer target)
        => SelectRuleSet(source).IsAllowedToUnderstand(source, target);

    public override bool IsSameRealm(GameLiving source, GameLiving target, bool quiet)
    {
        if (source.Group != null && source.Group.IsInTheGroup(target))
            return true;

        return SelectRuleSet(source).IsSameRealm(source, target, quiet);
    }

    public override bool IsAllowedToAttack(GameLiving attacker, GameLiving defender, bool quiet)
    {
        if (attacker is IGamePlayer iGamePlayerAttacker && iGamePlayerAttacker.IsDuelPartner(defender))
            return true;

        if (attacker.Group != null && attacker.Group.IsInTheGroup(defender))
            return false;

        return SelectRuleSet(attacker).IsAllowedToAttack(attacker, defender, quiet);
    }

    public override byte GetColorHandling(GameClient client)
    => SelectRuleSet(client.Player).GetColorHandling(client);

    public override bool IsAllowedToCastSpell(GameLiving caster, GameLiving target, Spell spell, SpellLine spellLine)
                => SelectRuleSet(caster).IsAllowedToCastSpell(caster, target, spell, spellLine);
    public override bool IsAllowedToSpeak(GamePlayer source, string communicationType)
                => SelectRuleSet(source).IsAllowedToSpeak(source, communicationType);
    public override bool IsAllowedToBind(GamePlayer player, DbBindPoint point)
                => SelectRuleSet(player).IsAllowedToBind(player, point);
    public override bool IsAllowedToCraft(GamePlayer player, DbItemTemplate item)
                => SelectRuleSet(player).IsAllowedToCraft(player, item);
    public override bool IsAllowedToClaim(GamePlayer player, Region region)
                => SelectRuleSet(player).IsAllowedToClaim(player, region);
    public override bool IsAllowedToZone(GamePlayer player, Region region)
                => SelectRuleSet(player).IsAllowedToZone(player, region);
    public override string ReasonForDisallowMounting(GamePlayer player)
                => SelectRuleSet(player).ReasonForDisallowMounting(player);
    public override bool CanTakeFallDamage(GamePlayer player)
                => SelectRuleSet(player).CanTakeFallDamage(player);
    public override bool CheckAbilityToUseItem(GameLiving living, DbItemTemplate item)
                => SelectRuleSet(living).CheckAbilityToUseItem(living, item);
    public override int GetObjectSpecLevel(IGamePlayer player, eObjectType objectType)
                => SelectRuleSet((GameObject)player).GetObjectSpecLevel(player, objectType);
    public override int GetObjectBaseSpecLevel(IGamePlayer player, eObjectType objectType)
                => SelectRuleSet((GameObject)player).GetObjectBaseSpecLevel(player, objectType);
    public override void OnNPCKilled(GameNPC killedNPC, GameObject killer)
                => SelectRuleSet(killedNPC).OnNPCKilled(killedNPC, killer);
    public override void OnLivingKilled(GameLiving killedLiving, GameObject killer)
                => SelectRuleSet(killer).OnLivingKilled(killedLiving, killer);
    public override void OnPlayerKilled(GamePlayer killedPlayer, GameObject killer)
                => SelectRuleSet(killer).OnPlayerKilled(killedPlayer, killer);
    public override byte GetLivingRealm(GamePlayer player, GameLiving target)
                => SelectRuleSet(player).GetLivingRealm(player, target);
    public override string GetPlayerName(GamePlayer source, GamePlayer target)
                => SelectRuleSet(source).GetPlayerName(source, target);
    public override string GetPlayerPrefixName(GamePlayer source, GamePlayer target)
                => SelectRuleSet(source).GetPlayerPrefixName(source, target);
    public override string GetPlayerLastName(GamePlayer source, GamePlayer target)
                => SelectRuleSet(source).GetPlayerLastName(source, target);
    public override string GetPlayerGuildName(GamePlayer source, GamePlayer target)
                => SelectRuleSet(source).GetPlayerGuildName(source, target);
    public override string GetPlayerTitle(GamePlayer source, GamePlayer target)
                => SelectRuleSet(source).GetPlayerTitle(source, target);
    public override int GetPlayerRealmPointsTotal(GamePlayer source)
                => SelectRuleSet(source).GetPlayerRealmPointsTotal(source);

    public override void OnGameEntered(DOLEvent e, object sender, EventArgs args)
    {
        base.OnGameEntered(e, sender, args);
        SetNameColorScheme(sender as GamePlayer);
    }

    public override void OnRegionChanged(DOLEvent e, object sender, EventArgs args)
    {
        base.OnRegionChanged(e, sender, args);
        SetNameColorScheme(sender as GamePlayer);
    }

    private AbstractServerRules SelectRuleSet(GameObject obj)
    {
        Zone currentZone = obj.CurrentZone;

        if (obj.CurrentRegion.IsRvR || currentZone != null && currentZone.IsRvR)
        {
            return normalServerRules;
        }

        return pveServerRules;
    }

    private static void SetNameColorScheme(GamePlayer player)
    {
        if (!player.CurrentRegion.IsRvR)
            player.Out.SendRegionColorScheme(3);
        else
            player.Out.SendRegionColorScheme();
    }

	/// <summary>
	/// Reset the keep with special server rules handling
	/// </summary>
	/// <param name="lord">The lord that was killed</param>
	/// <param name="killer">The lord's killer</param>
	public override void ResetKeep(GuardLord lord, GameObject killer)
	{
		base.ResetKeep(lord, killer);
		lord.Component.Keep.Reset((eRealm)killer.Realm);
			
		if (ConquestService.ConquestManager.ActiveObjective != null && ConquestService.ConquestManager.ActiveObjective.Keep == lord.Component.Keep)
		{
				ConquestService.ConquestManager.ConquestCapture(ConquestService.ConquestManager.ActiveObjective.Keep);
		}
			
		if (ConquestService.ConquestManager.GetSecondaryObjectives().FirstOrDefault(conq => conq.Keep == lord.Component.Keep) != null)
		{
			ConquestService.ConquestManager.ConquestSubCapture(lord.Component.Keep);
		}
	}
}
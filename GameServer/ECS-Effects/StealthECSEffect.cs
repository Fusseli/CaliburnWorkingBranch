using System;
using System.Collections.Generic;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using static DOL.GS.GameNPC;
using static ICSharpCode.SharpZipLib.Zip.ExtendedUnixData;

namespace DOL.GS
{
    public class StealthECSGameEffect : ECSGameAbilityEffect
    {
        public StealthECSGameEffect(ECSGameEffectInitParams initParams)
            : base(initParams)
        {
            EffectType = eEffect.Stealth;
            EffectService.RequestStartEffect(this);
        }

        public override ushort Icon { get { return 0x193; } }
        public override string Name { get { return LanguageMgr.GetTranslation(OwnerPlayer?.Client, "Effects.StealthEffect.Name"); } }
        public override bool HasPositiveEffect { get { return true; } }

        public override void OnStartEffect()
        {
            if (Owner is IGamePlayer gamePlayer)
            {
                if (gamePlayer is MimicNPC mimicNPC)
                    mimicNPC.Flags |= eFlags.STEALTH;

                gamePlayer.StartStealthUncoverAction();

                if (gamePlayer.ObjectState == GameObject.eObjectState.Active)
                    gamePlayer.Out.SendMessage(LanguageMgr.GetTranslation(gamePlayer.Client.Account.Language, "GamePlayer.Stealth.NowHidden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                gamePlayer.Out.SendPlayerModelTypeChange(OwnerPlayer, 3);

                if (gamePlayer.EffectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedBuff))
                {
                    foreach (var speedBuff in gamePlayer.EffectListComponent.GetSpellEffects(eEffect.MovementSpeedBuff))
                    {
                        EffectService.RequestDisableEffect(speedBuff);
                    }
                }

                // Cancel pulse effects.
                List<ECSPulseEffect> effects = gamePlayer.EffectListComponent.GetAllPulseEffects();

                for (int i = 0; i < effects.Count; i++)
                    EffectService.RequestImmediateCancelConcEffect(effects[i]);

                gamePlayer.Sprint(false);

                if (gamePlayer.Client.Account.PrivLevel == 1 || gamePlayer.Client.Account.PrivLevel == 0)
                {
                    //GameEventMgr.AddHandler(this, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(Unstealth));
                    foreach (GamePlayer player in gamePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null || player == gamePlayer)
                            continue;

                        if (!player.CanDetect(gamePlayer))
                            player.Out.SendObjectDelete((GameObject)gamePlayer);
                    }

                    gamePlayer.Out.SendUpdateMaxSpeed();
                }

                StealthStateChanged();
            }
        }

        public override void OnStopEffect()
        {
            if (Owner is IGamePlayer gamePlayer)
            {
                if (gamePlayer is MimicNPC mimicNPC)
                    mimicNPC.Flags ^= eFlags.STEALTH;

                gamePlayer.StopStealthUncoverAction();

                if (gamePlayer.ObjectState == GameObject.eObjectState.Active)
                    gamePlayer.Out.SendMessage(LanguageMgr.GetTranslation(gamePlayer.Client.Account.Language, "GamePlayer.Stealth.NoLongerHidden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                gamePlayer.Out.SendPlayerModelTypeChange(OwnerPlayer, 2);

                //GameEventMgr.RemoveHandler(this, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(GamePlayer.Unstealth));
                foreach (GamePlayer otherPlayer in gamePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (otherPlayer == null || otherPlayer == gamePlayer) 
                        continue;

                    /// [Atlas - Takii] This commented code from DOL causes a large (1-2 seconds) delay before the target unstealths.
                    /// It does not seem to cause any issues related to targeting despite the comments.
                    //if a player could see us stealthed, we just update our model to avoid untargetting.
                    // 					if (player.CanDetect(this))
                    // 						player.Out.SendPlayerModelTypeChange(this, 2);
                    // 					else
                    // 						player.Out.SendPlayerCreate(this);

                    if (gamePlayer is GamePlayer)
                    {
                        if (Owner is GamePlayer)
                        {
                            otherPlayer.Out.SendPlayerCreate(OwnerPlayer);
                            otherPlayer.Out.SendLivingEquipmentUpdate(OwnerPlayer);
                        }
                        else if (Owner is MimicNPC mimic)
                        {
                            otherPlayer.Out.SendNPCCreate(mimic);
                        }
                    }
                }

                if (gamePlayer.EffectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedBuff))
                {
                    var speedBuff = gamePlayer.EffectListComponent.GetBestDisabledSpellEffect(eEffect.MovementSpeedBuff);

                    if (speedBuff != null)
                    {
                        speedBuff.IsBuffActive = false;
                        EffectService.RequestEnableEffect(speedBuff);
                    }
                }

                StealthStateChanged();

                // This needs to be restored if we have the Camouflage ability on this server.
                //             if (Owner.HasAbility(Abilities.Camouflage))
                //             {
                //                 IGameEffect camouflage = m_player.EffectList.GetOfType<CamouflageEffect>();
                //                 if (camouflage != null)
                //                     camouflage.Cancel(false);
                //             }
            }
        }

        private void StealthStateChanged()
        {
            if (OwnerPlayer != null)
            {
                OwnerPlayer.Notify(GamePlayerEvent.StealthStateChanged, OwnerPlayer, null);

                if (OwnerPlayer.Client.Account.PrivLevel == 1 || OwnerPlayer.Client.Account.PrivLevel == 0)
                {
                    OwnerPlayer.Out.SendUpdateMaxSpeed();
                }
            }
        }
    }
}
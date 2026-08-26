using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;

namespace DOL.GS
{
    // This component will hold all data related to casting spells.
    public class PlayerCastingComponent : CastingComponent
    {
        private GamePlayer _playerOwner;

        public PlayerCastingComponent(GamePlayer playerOwner) : base(playerOwner)
        {
            _playerOwner = playerOwner;
        }

        public override bool RequestStartCastSpell(Spell spell, SpellLine spellLine, ISpellCastingAbilityHandler spellCastingAbilityHandler = null, GameLiving target = null)
        {
            if (!_playerOwner.ChainedActions.CheckCommandInput(spell, spellLine))
                return false;

            if (_playerOwner.ChainedActions.Execute(spell))
            {
                EntityManager.Add<CastingComponent>(this);
                return true;
            }

            return base.RequestStartCastSpell(spell, spellLine, spellCastingAbilityHandler, target);
        }

        protected override void StartCastSpell(StartCastSpellRequest startCastSpellRequest)
        {
            // Warlock spell weaving: primers attaching secondaries and chambers loading spells.
            if (!HandleWarlockCast(startCastSpellRequest))
                base.StartCastSpell(startCastSpellRequest);
        }

        /// <summary>
        /// Handles everything specific to the Warlock class. Returns true if the request was consumed.
        /// This mirrors the old 'GamePlayer.CastSpell' override from pre-ECS DoL servers.
        /// </summary>
        private bool HandleWarlockCast(StartCastSpellRequest startCastSpellRequest)
        {
            if (_playerOwner.CharacterClass.ID != (int) eCharacterClass.Warlock)
                return false;

            Spell spell = startCastSpellRequest.Spell;
            SpellHandler currentHandler = SpellHandler;

            // Firing a loaded chamber is instantaneous.
            if (spell.SpellType == eSpellType.Chamber && currentHandler == null)
            {
                GameSpellEffect chamberEffect = Spells.SpellHandler.FindEffectOnTarget(_playerOwner, "Chamber", spell.Name);

                if (chamberEffect != null)
                {
                    SpellHandler chamberFireHandler = CreateSpellHandler(startCastSpellRequest);
                    chamberFireHandler.StartSpell(_playerOwner);
                    return true;
                }

                // Unloaded chamber: go through a normal cast, spells are loaded into it while it's being cast.
                return false;
            }

            // A chamber is currently being prepared, clicked spells are loaded into it.
            if (currentHandler is ChamberSpellHandler chamber)
            {
                if (_playerOwner.IsMoving || _playerOwner.IsStrafing)
                {
                    currentHandler.InterruptCasting();
                    return true;
                }

                if (spell.IsPrimary)
                {
                    if (chamber.PrimarySpell == null)
                    {
                        if (spell.SpellType == eSpellType.Bolt && !chamber.Spell.AllowBolt)
                        {
                            SendMessage("This spell cannot be stored in this chamber.", eChatType.CT_SpellResisted);
                        }
                        else
                        {
                            Spell clone = spell.Copy();
                            clone.InChamber = true;
                            clone.CostPower = false;
                            chamber.PrimarySpell = clone;
                            chamber.PrimarySpellLine = startCastSpellRequest.SpellLine;
                            SendMessage($"You load {spell.Name} into your {chamber.Spell.Name}.", eChatType.CT_System);
                            SendMessage($"Select the second spell for your {chamber.Spell.Name}.", eChatType.CT_System);
                        }
                    }
                    else
                    {
                        SendMessage("This spell cannot be stored in this chamber.", eChatType.CT_SpellResisted);
                    }

                    return true;
                }

                if (spell.IsSecondary)
                {
                    if (chamber.PrimarySpell == null)
                        SendMessage("You must store a primary spell first!", eChatType.CT_SpellResisted);
                    else if (chamber.SecondarySpell != null)
                        SendMessage("You have already chosen your spells for this chamber.", eChatType.CT_SpellResisted);
                    else
                    {
                        Spell clone = spell.Copy();
                        clone.CostPower = false;
                        clone.InChamber = true;
                        // Keep PBAE secondaries (Range 0, Radius >0) as PBAE - overriding to 1500 would turn them into targeted spells.
                        if (spell.Range != 0)
                            clone.OverrideRange = chamber.PrimarySpell.Range;
                        chamber.SecondarySpell = clone;
                        chamber.SecondarySpellLine = startCastSpellRequest.SpellLine;
                        SendMessage($"You load {spell.Name} into your {chamber.Spell.Name}.", eChatType.CT_System);
                    }

                    return true;
                }

                // Anything else clicked while preparing a chamber is ignored.
                return true;
            }

            // Attaching a secondary spell while a primary (Cursing) or a primer (Witchcraft)
            // is being cast. Both should land together when the primary/primer finishes.
            if (currentHandler != null && (currentHandler is PrimerSpellHandler || currentHandler.Spell.IsPrimary))
            {
                if (!spell.IsSecondary)
                    return false; // Primaries follow the normal queue rules.

                // Only one secondary per primary/primer.
                if (PendingWarlockSecondary != null)
                {
                    SendMessage("You have already prepared a secondary spell for this cast!", eChatType.CT_SpellResisted);
                    return true;
                }

                Spell clone = spell.Copy();
                clone.CostPower = false;

                if (currentHandler is RangeSpellHandler rangePrimer)
                    clone.OverrideRange = rangePrimer.Spell.Range;
                else if (Spells.SpellHandler.FindEffectOnTarget(_playerOwner, "Range") is GameSpellEffect rangeEff && rangeEff.SpellHandler is RangeSpellHandler)
                    clone.OverrideRange = rangeEff.Spell.Range;

                SetPendingWarlockSecondary(clone, startCastSpellRequest.SpellLine);
                SendMessage("You prepare a secondary spell!", eChatType.CT_SpellResisted);
                return true;
            }

            // Secondary spells can never be cast on their own, they require an active primer effect.
            if (spell.IsSecondary && currentHandler == null)
            {
                GameSpellEffect primerEffect = Spells.SpellHandler.FindEffectOnTarget(_playerOwner, "Powerless")
                    ?? Spells.SpellHandler.FindEffectOnTarget(_playerOwner, "Range")
                    ?? Spells.SpellHandler.FindEffectOnTarget(_playerOwner, "Uninterruptable");

                if (primerEffect == null)
                {
                    SendMessage("You cannot cast this spell directly!", eChatType.CT_SpellResisted);
                    return true;
                }

                Spell clone = spell.Copy();
                clone.CostPower = false;

                if (primerEffect.SpellHandler is RangeSpellHandler)
                    clone.OverrideRange = primerEffect.Spell.Range;

                // The consumed primer effect is cancelled by 'SpellHandler.FinishSpellCast' once this spell fires.
                base.StartCastSpell(new StartCastSpellRequest(clone, startCastSpellRequest.SpellLine, startCastSpellRequest.SpellCastingAbilityHandler, startCastSpellRequest.Target));
                return true;
            }

            // Chambers may not be readied as a follow-up spell.
            if (currentHandler != null && spell.SpellType == eSpellType.Chamber)
            {
                SendMessage("You may not ready this spell as a followup!", eChatType.CT_SpellResisted);
                return true;
            }

            return false;
        }

        private void SendMessage(string message, eChatType chatType)
        {
            _playerOwner.Out.SendMessage(message, chatType, eChatLoc.CL_SystemWindow);
        }

        protected override bool CanCastSpell()
        {
            if (_playerOwner.effectListComponent.ContainsEffectForEffectType(eEffect.Volley))
            {
                _playerOwner.Out.SendMessage("You can't cast spells while Volley is active!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (_playerOwner.IsCrafting)
            {
                _playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(_playerOwner.Client.Account.Language, "GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                _playerOwner.craftComponent.StopCraft();
                _playerOwner.CraftTimer = null;
                _playerOwner.Out.SendCloseTimerWindow();
            }

            if (_playerOwner.IsSalvagingOrRepairing)
            {
                _playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(_playerOwner.Client.Account.Language, "GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                _playerOwner.CraftTimer.Stop();
                _playerOwner.CraftTimer = null;
                _playerOwner.Out.SendCloseTimerWindow();
            }

            if (_playerOwner.IsStunned)
            {
                _playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(_playerOwner.Client.Account.Language, "GamePlayer.CastSpell.CantCastStunned"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (_playerOwner.IsMezzed)
            {
                _playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(_playerOwner.Client.Account.Language, "GamePlayer.CastSpell.CantCastMezzed"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (_playerOwner.IsSilenced)
            {
                _playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(_playerOwner.Client.Account.Language, "GamePlayer.CastSpell.CantCastFumblingWords"), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }
    }
}

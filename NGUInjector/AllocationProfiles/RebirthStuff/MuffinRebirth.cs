﻿using NGUInjector.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NGUInjector.AllocationProfiles.RebirthStuff
{
    internal class MuffinRebirth : TimeRebirth
    {
        internal bool ShouldAutoBuyMuffins { get; set; }

        //TODO:Test!
        private double _23h = 60 * 23;//60 * 60 * 3;
        private double _24h = 60 * 24;//60 * 60 * 5;
        private bool _shouldMuffin = true;
        private ConsumablesManager.Consumable _muffinConsumable;

        public MuffinRebirth()
        {
            RebirthTime = _24h;
            ConsumablesManager.Consumables.TryGetValue(ConsumablesManager.ConsumableType.MUFFIN, out _muffinConsumable);
        }

        internal override bool RebirthAvailable()
        {
            if (!Main.Settings.AutoRebirth)
                return false;

            RebirthTime = _24h;
            _shouldMuffin = true;

            if(_muffinConsumable == null)
            {
                _shouldMuffin = false;
                return base.RebirthAvailable();
            }

            //Do 24 hour Rebirths if we have any challenges, or if the 5 O'Clock Shadow Perk or Beast Fertilizer Quirk aren't maxed
            if (CharObj.challenges.inChallenge || ChallengeTargets.Length > 0 || Main.Character.adventure.itopod.perkLevel[21] < Main.Character.adventureController.itopod.maxLevel[21] || Main.Character.beastQuest.quirkLevel[13] < Main.Character.beastQuestPerkController.maxLevel[13])
            {
                _shouldMuffin = false;
            }

            //Do 24 hour Rebirths if we don't have any muffins and aren't configured to purchase more or don't have the AP to purchase more
            if (_muffinConsumable.GetCount() == 0 && (!ShouldAutoBuyMuffins || !_muffinConsumable.Buy(1, out _)))
            {
                _shouldMuffin = false;
            }

            //Cycle between 24 and 23 hours  (24h -> Activate Muffin if possible -> Rebirth -> 23h -> Rebirth)
            if (_shouldMuffin)
            {
                bool muffinIsActive = (_muffinConsumable.GetIsActive() ?? false);
                double muffinTimeLeft = (_muffinConsumable.GetTimeLeft() ?? 0);

                Main.LogDebug($"MuffinActive:{muffinIsActive} | MuffinTimeLeft:{muffinIsActive}");

                if (muffinTimeLeft > 0 && !muffinIsActive)
                {
                    RebirthTime = _23h;
                }
            }

            return base.RebirthAvailable();
        }

        protected override bool PreRebirth()
        {
            if (base.PreRebirth())
                return true;

            if (_muffinConsumable == null)
            {
                return false;
            }

            if (_shouldMuffin && _muffinConsumable != null && (_muffinConsumable.GetIsActive() ?? false) && (_muffinConsumable.GetTimeLeft() ?? 0) <= 0)
            {
                if (_muffinConsumable.GetCount() <= 0)
                {
                    if (ShouldAutoBuyMuffins)
                    {
                        _muffinConsumable.Buy(1, out _);
                    }
                    else
                    {
                        Main.Log("No muffins available for rebirth and breakpoint not configured to auto-purchase");
                    }
                }

                _muffinConsumable.Use(1);
            }

            return false;
        }
    }
}

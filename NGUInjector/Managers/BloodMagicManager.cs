using System;
using static NGUInjector.Main;

namespace NGUInjector.Managers
{
    public static class BloodMagicManager
    {
        public abstract class Spell
        {
            protected string name;
            protected int cooldown;
            protected double minBlood;

            protected abstract bool Unlocked { get; }

            protected abstract double Time { get; }

            protected abstract double Threshold { get; }

            protected abstract bool CastOnRebirth { get; }

            protected Spell(string name, int cooldown, double minBlood)
            {
                this.name = name;
                this.cooldown = cooldown;
                this.minBlood = minBlood;
            }

            protected abstract double Effect(double bloodPoints);

            protected abstract void CastSpell();

            public void Cast(bool rebirth = false)
            {
                if (!Settings.CastBloodSpells)
                    return;

                if (!Unlocked)
                    return;

                var forced = rebirth && CastOnRebirth;
                double threshold = Threshold;
                if (!forced && threshold < 1.0)
                    return;

                if (Time < cooldown)
                {
                    if (forced)
                        Log($"Casting Failed: Blood Spell {name} - On Cooldown");
                    return;
                }

                double bloodPoints = _character.bloodMagic.bloodPoints;
                if (bloodPoints < minBlood)
                {
                    if (forced || Time < cooldown + 10f)
                        Log($"Casting Failed: Blood Spell {name} - Below minimum blood threshold of {minBlood:F0}");
                    return;
                }

                double effect = Effect(bloodPoints);
                if (!forced && threshold.CompareTo(effect) > 0)
                {
                    if (Time < cooldown + 10f)
                        Log($"Casting Failed: Blood Spell {name} - Below configured power threshold ({effect:F0} of {threshold:F0}) and not force cast on rebirth");
                    return;
                }

                CastSpell();
                Log($"Casting Blood Spell {name} @ {effect:F0} power");
            }
        }

        public class IronPill : Spell
        {
            protected override bool Unlocked => true;

            protected override double Time => _character.bloodMagic.adventureSpellTime.totalseconds;

            protected override double Threshold => Settings.IronPillThreshold;

            protected override bool CastOnRebirth => Settings.IronPillOnRebirth;

            public IronPill(string name, int cooldown, double minBlood) : base(name, cooldown, minBlood) { }

            protected override double Effect(double bloodPoints)
            {
                var result = Math.Floor(Math.Pow(bloodPoints, 0.25));
                if (_character.settings.rebirthDifficulty >= difficulty.evil)
                    result *= _character.adventureController.itopod.ironPillBonus();
                return result;
            }

            protected override void CastSpell() => _bloodSpells.castAdventurePowerupSpell();
        }

        public class GuffA : Spell
        {
            protected override bool Unlocked => _character.adventure.itopod.perkLevel[72] >= 1L;

            protected override double Time => _character.bloodMagic.macguffin1Time.totalseconds;

            protected override double Threshold => Settings.BloodMacGuffinAThreshold;

            protected override bool CastOnRebirth => Settings.BloodMacGuffinAOnRebirth;

            public GuffA(string name, int cooldown, double minBlood) : base(name, cooldown, minBlood) { }

            protected override double Effect(double bloodPoints)
            {
                var result = Math.Log(bloodPoints / _bloodSpells.minMacguffin1Blood(), 10.0) + 1.0;
                result *= _character.wishesController.totalBloodGuffbonus();
                return Math.Floor(result);
            }

            protected override void CastSpell() => _bloodSpells.castMacguffin1Spell();
        }

        public class GuffB : Spell
        {
            protected override bool Unlocked => _character.adventure.itopod.perkLevel[73] >= 1L && _character.settings.rebirthDifficulty >= difficulty.evil;

            protected override double Time => _character.bloodMagic.macguffin2Time.totalseconds;

            protected override double Threshold => Settings.BloodMacGuffinBThreshold;

            protected override bool CastOnRebirth => Settings.BloodMacGuffinBOnRebirth;

            public GuffB(string name, int cooldown, double minBlood) : base(name, cooldown, minBlood) { }

            protected override double Effect(double bloodPoints) => Math.Floor(Math.Log(bloodPoints / _bloodSpells.minMacguffin2Blood(), 20.0) + 1.0);

            protected override void CastSpell() => _bloodSpells.castMacguffin2Spell();
        }

        private static readonly Character _character = Main.Character;
        private static readonly RebirthPowerSpell _bloodSpells = _character.bloodSpells;

        public static readonly Spell ironPill = new IronPill("Iron Pill", _bloodSpells.adventureSpellCooldown, _bloodSpells.minAdventureBlood());
        public static readonly Spell guffA = new GuffA("MacGuffin α", _bloodSpells.macguffin1Cooldown, _bloodSpells.minMacguffin1Blood());
        public static readonly Spell guffB = new GuffB("MacGuffin β", _bloodSpells.macguffin2Cooldown, _bloodSpells.minMacguffin2Blood());
    }
}

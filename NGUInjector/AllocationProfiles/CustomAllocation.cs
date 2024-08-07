using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NGUInjector.AllocationProfiles.Breakpoints;
using NGUInjector.AllocationProfiles.RebirthStuff;
using NGUInjector.Managers;
using SimpleJSON;
using static NGUInjector.Main;

namespace NGUInjector.AllocationProfiles
{
    public class CustomAllocation
    {
        private readonly Character _character = Main.Character;
        private BreakpointWrapper _wrapper;
        private readonly string _allocationPath;
        private readonly string _profileName;

        public bool IsAllocationRunning;

        public CustomAllocation(string profilesDir, string profile)
        {
            _allocationPath = Path.Combine(profilesDir, profile + ".json");
            _profileName = profile;
        }

        public void ReloadAllocation()
        {
            if (File.Exists(_allocationPath))
            {
                try
                {
                    _wrapper = new BreakpointWrapper(JSON.Parse(File.ReadAllText(_allocationPath))["Breakpoints"]);

                    Log(_wrapper.BuildAllocationString(_profileName));

                    DoAllocations();
                }
                catch (Exception e)
                {
                    Log("Failed to load allocation file. Resave to reload");
                    Log(e.Message);
                    Log(e.StackTrace);
                    _wrapper = new BreakpointWrapper();
                }
            }
            else
            {
                var emptyAllocation = @"{
    ""Breakpoints"": {
      ""Magic"": [
        {
          ""Time"": 0,
          ""Priorities"": []
        }
      ],
      ""Energy"": [
        {
          ""Time"": 0,
          ""Priorities"": []
        }
      ],
    ""R3"": [
        {
          ""Time"": 0,
          ""Priorities"": []
        }
      ],
      ""Gear"": [
        {
          ""Time"": 0,
          ""ID"": []
        }
      ],
      ""Wandoos"": [
        {
          ""Time"": 0,
          ""OS"": 0
        }
      ],
      ""Beards"": [
        {
          ""Time"": 0,
          ""List"": []
        }
      ],
      ""Diggers"": [
        {
          ""Time"": 0,
          ""List"": []
        }
      ],
      ""NGUDiff"": [
        {
          ""Time"": 0,
          ""Diff"": 0
        }
      ],
      ""RebirthTime"": -1,
      ""Challenges"": []
    }
  }
        ";

                Log("Created empty allocation profile. Please update allocation.json");
                using (var writer = new StreamWriter(File.Open(_allocationPath, FileMode.CreateNew)))
                {
                    writer.WriteLine(emptyAllocation);
                    writer.Flush();
                }
            }
        }

        public void DoAllocations()
        {
            if (!Settings.GlobalEnabled)
                return;

            if (IsAllocationRunning)
                return;

            var preventMagicAllocation = Settings.MoneyPitRunMode && Main.Character.machine.realBaseGold <= 0.0 && MoneyPitManager.NeedsLowerTier();

            try
            {
                long originalInput = Main.Character.energyMagicPanel.energyMagicInput;
                IsAllocationRunning = true;

                if (Settings.ManageNGUDiff && Main.Character.buttons.ngu.interactable)
                    _wrapper.ngus.Swap();
                if (Settings.ManageGear && Main.Character.buttons.inventory.interactable)
                    _wrapper.gear.Swap();
                if (Settings.ManageWishes && !preventMagicAllocation)
                {
                    if (Settings.ManageEnergy)
                        _character.removeMostEnergy();
                    if (Settings.ManageMagic)
                        _character.removeMostMagic();
                    if (Settings.ManageR3)
                        _character.removeAllRes3();
                    WishManager.Allocate();
                }
                if (Settings.ManageEnergy)
                    _wrapper.energy.Swap();
                if (Settings.ManageMagic && !preventMagicAllocation)
                    _wrapper.magic.Swap();
                if (Settings.ManageR3)
                    _wrapper.r3.Swap();
                if (Settings.ManageWishes && !preventMagicAllocation)
                {
                    // Allocating to wishes again because there can be spare resources
                    WishManager.Allocate(true);
                    WishManager.UpdateWishMenu();
                }
                if (Settings.ManageConsumables)
                    _wrapper.consumables.Swap();
                if (Settings.ManageBeards && Main.Character.buttons.beards.interactable)
                    _wrapper.beards.Swap();
                if (Settings.ManageDiggers && Main.Character.buttons.diggers.interactable)
                {
                    _wrapper.diggers.Swap();
                    DiggerManager.RecapDiggers();
                }
                if (Settings.ManageWandoos && Main.Character.buttons.wandoos.interactable)
                    _wrapper.wandoos.Swap();

                Main.Character.energyMagicPanel.energyRequested.text = originalInput.ToString(CultureInfo.InvariantCulture);
                Main.Character.energyMagicPanel.validateInput();
            }
            catch (Exception e)
            {
                LogDebug($"Error while allocating: {e}");
            }
            finally
            {
                IsAllocationRunning = false;
            }
        }

        public bool DoRebirth()
        {
            if (_wrapper == null)
                return false;

            var rbs = _wrapper.rebirth.Where(x => x.RebirthTime >= 0.0);
            if (!rbs.Any())
                return false;

            if (rbs.Any(x => x.RebirthTime <= _character.rebirthTime.totalseconds))
                rbs = rbs.Where(x => x.RebirthTime <= _character.rebirthTime.totalseconds);

            var rb = rbs.AllMaxBy(x => x.RebirthTime).First();

            if (rb.RebirthAvailable(out _))
            {
                if (_character.bossController.isFighting || _character.bossController.nukeBoss)
                {
                    Log("Delaying rebirth while boss fight is in progress");
                    return true;
                }
            }
            else
            {
                return false;
            }

            if (rb.DoRebirth())
            {
                _wrapper.energy.Reset();
                _wrapper.magic.Reset();
                _wrapper.r3.Reset();
                _wrapper.gear.Reset();
                _wrapper.beards.Reset();
                _wrapper.diggers.Reset();
                _wrapper.wandoos.Reset();
                _wrapper.ngus.Reset();
                _wrapper.consumables.Reset();
            }

            return true;
        }

        public void CastBloodSpells()
        {
            if (!Settings.CastBloodSpells)
                return;

            var needCast = _wrapper.rebirth.Length == 0;
            foreach (TimeRebirth rb in _wrapper.rebirth)
            {
                if (rb.RebirthTime - _character.rebirthTime.totalseconds >= 30 * 60)
                {
                    needCast = true;
                    break;
                }
            }

            if (!needCast)
                return;

            BloodMagicManager.guffB.Cast();
            BloodMagicManager.guffA.Cast();
            BloodMagicManager.ironPill.Cast();
        }

        public static double ParseTime(JSONNode timeNode)
        {
            var time = 0;

            if (timeNode.IsObject)
            {
                foreach (var N in timeNode)
                {
                    if (N.Value.IsNumber)
                    {
                        switch (N.Key.ToLower())
                        {
                            case "h":
                                time += 60 * 60 * N.Value.AsInt;
                                break;
                            case "m":
                                time += 60 * N.Value.AsInt;
                                break;
                            default:
                                time += N.Value.AsInt;
                                break;
                        }
                    }
                }
            }

            if (timeNode.IsNumber)
                time = timeNode.AsInt;

            return time;
        }
    }

    public class BreakpointWrapper
    {
        public TimeRebirth[] rebirth = new TimeRebirth[0];
        public EnergyBreakpoints energy = new EnergyBreakpoints();
        public MagicBreakpoints magic = new MagicBreakpoints();
        public R3Breakpoints r3 = new R3Breakpoints();
        public GearBreakpoints gear = new GearBreakpoints();
        public DiggerBreakpoints diggers = new DiggerBreakpoints();
        public BeardBreakpoints beards;
        public WandoosBreakpoints wandoos = new WandoosBreakpoints();
        public NGUDiffBreakpoints ngus = new NGUDiffBreakpoints();
        public ConsumablesBreakpoints consumables = new ConsumablesBreakpoints();

        public BreakpointWrapper(JSONNode parsed)
        {
            var rb = parsed["Rebirth"];
            var rbtime = parsed["RebirthTime"];

            if (rb == null)
            {
                if (rbtime != null)
                {
                    var newRebirth = TimeRebirth.CreateRebirth(CustomAllocation.ParseTime(rbtime), 0.0, "time");
                    Array.Resize(ref rebirth, 1);
                    rebirth[0] = newRebirth;
                }
            }
            else
            {
                var rbs = new List<TimeRebirth>();
                foreach (var bp in rb.Children)
                {
                    if (bp["Type"] == null)
                        continue;

                    var type = bp["Type"].Value.ToUpper();
                    if (type != "TIME" && bp["Target"] == null)
                        continue;

                    var target = type == "TIME" ? 0.0 : bp["Target"].AsDouble;
                    var time = 0.0;
                    if (bp["Time"] != null)
                        time = CustomAllocation.ParseTime(bp["Time"]);

                    var newRebirth = TimeRebirth.CreateRebirth(time, target, type);
                    if (newRebirth != null)
                        rbs.Add(newRebirth);
                }
                rebirth = rbs.ToArray();
            }

            BaseRebirth.ParseChallenges(parsed["Challenges"].AsArray.Children.Select(bp => bp.Value.ToUpper()).ToArray());
            energy = new EnergyBreakpoints(parsed["Energy"]);
            magic = new MagicBreakpoints(parsed["Magic"]);
            r3 = new R3Breakpoints(parsed["R3"]);
            gear = new GearBreakpoints(parsed["Gear"]);
            diggers = new DiggerBreakpoints(parsed["Diggers"]);
            beards = new BeardBreakpoints(parsed["Beards"], diggers);
            wandoos = new WandoosBreakpoints(parsed["Wandoos"]);
            ngus = new NGUDiffBreakpoints(parsed["NGUDiff"]);
            consumables = new ConsumablesBreakpoints(parsed["Consumables"]);
        }

        public BreakpointWrapper()
        {
            beards = new BeardBreakpoints(diggers);
        }

        public string BuildAllocationString(string profileName)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Loaded Custom Allocation from profile '{profileName}'");
            builder.AppendLine($"{energy.Length} Energy Breakpoints");
            builder.AppendLine($"{magic.Length} Magic Breakpoints");
            builder.AppendLine($"{r3.Length} R3 Breakpoints");
            builder.AppendLine($"{gear.Length} Gear Breakpoints");
            builder.AppendLine($"{beards.Length} Beard Breakpoints");
            builder.AppendLine($"{diggers.Length} Digger Breakpoints");
            builder.AppendLine($"{wandoos.Length} Wandoos Breakpoints");
            builder.AppendLine($"{ngus.Length} NGU Difficulty Breakpoints");
            builder.AppendLine($"{consumables.Length} Consumable Breakpoints");
            if (rebirth?.Length > 0)
                builder.AppendLine($"{rebirth.Length} Rebirth Breakpoints");
            else
                builder.AppendLine($"Rebirth Disabled.");

            return builder.ToString();
        }
    }
}

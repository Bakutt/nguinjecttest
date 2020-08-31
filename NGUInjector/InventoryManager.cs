﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NGUInjector
{
    internal class InventoryManager
    {
        private readonly Character _character;
        private readonly StreamWriter _outputWriter;
        private readonly InventoryController _controller;

        private readonly int[] _pendants = { 53, 76, 94, 142, 170, 229, 295, 388, 430, 504 };
        private readonly int[] _lootys = { 67, 128, 169, 230, 296, 389, 431, 505 };
        private readonly int[] _wandoos = {66, 169};
        internal static int[] BoostBlacklist;


        //Wandoos 98, Giant Seed, Wandoos XL, Lonely Flubber, Wanderer's Cane
        private readonly int[] _filterExcludes = { 66, 92, 163, 120, 154 };
        public InventoryManager()
        {
            _character = Main.Character;
            _outputWriter = Main.OutputWriter;
            _controller = Main.Controller;
            BoostBlacklist = new int[]{};
        }
        
        internal void BoostEquipped()
        {
            // Boost Equipped Slots
            if (!BoostBlacklist.Contains(_character.inventory.head.id))
                _controller.applyAllBoosts(-1);
            if (!BoostBlacklist.Contains(_character.inventory.chest.id))
                _controller.applyAllBoosts(-2);
            if (!BoostBlacklist.Contains(_character.inventory.legs.id))
                _controller.applyAllBoosts(-3);
            if (!BoostBlacklist.Contains(_character.inventory.boots.id))
                _controller.applyAllBoosts(-4);
            if (!BoostBlacklist.Contains(_character.inventory.weapon.id))
                _controller.applyAllBoosts(-5);

            if (_controller.weapon2Unlocked() && !BoostBlacklist.Contains(_character.inventory.head.id))
                _controller.applyAllBoosts(-6);
        }

        internal void BoostAccessories()
        {
            for (var i = 10000; _controller.accessoryID(i) < _controller.accessorySpaces(); i++)
            {
                if (!BoostBlacklist.Contains(_character.inventory.accs[_controller.accessoryID(i)].id))
                    _controller.applyAllBoosts(i);
            }
        }

        internal void BoostInventory(int[] items, ih[] ih)
        {
            foreach (var item in items)
            {
                //Find all inventory slots that match this item name
                var targets =
                    ih.Where(x => x.id == item && !BoostBlacklist.Contains(x.id)).ToArray();

                switch (targets.Length)
                {
                    case 0:
                        continue;
                    case 1:
                        _controller.applyAllBoosts(targets[0].slot);
                        continue;
                    default:
                        //Find the highest level version of the item (locked = +100) and apply boosts to it
                        _controller.applyAllBoosts(targets.MaxItem().slot);
                        break;
                }
            }
        }

        private void ChangePage(int slot)
        {
            var page = (int)Math.Floor((double)slot / 60);
            _controller.changePage(page);
        }

        internal void BoostInfinityCube()
        {
            _controller.infinityCubeAll();
            _controller.updateInventory();
        }

        internal void MergeEquipped()
        {
            // Boost Equipped Slots
            _controller.mergeAll(-1);
            _controller.mergeAll(-2);
            _controller.mergeAll(-3);
            _controller.mergeAll(-4);
            _controller.mergeAll(-5);

            if (_controller.weapon2Unlocked())
            {
                _controller.mergeAll(-6);
            }

            //Boost Accessories
            for (var i = 10000; _controller.accessoryID(i) < _controller.accessorySpaces(); i++)
            {
                _controller.mergeAll(i);
            }
        }

        internal void MergeBoosts(ih[] ci)
        {
            var grouped = ci.Where(x =>
                x.id <= 40 && !_character.inventory.inventory[x.slot].removable &&
                !_character.inventory.itemList.itemMaxxed[x.id]);

            foreach (var target in grouped)
            {
                if (target.level == 100)
                {
                    _outputWriter.WriteLine($"Removing protection from {target.name} in slot {target.slot}");
                    _character.inventory.inventory[target.slot].removable = false;
                    continue;
                }

                if (ci.Count(x => x.id == target.id) <= 1) continue;
                _outputWriter.WriteLine($"Merging {target.name} in slot {target.slot}");
                _controller.mergeAll(target.slot);
            }
            _outputWriter.Flush();
        }

        internal void MergeInventory(ih[] ci)
        {
            var grouped =
                ci.Where(x => x.id > 40 && x.level < 100).GroupBy(x => x.id).Where(x => x.Count() > 1);

            foreach (var item in grouped)
            {
                var target = item.MaxItem();

                _outputWriter.WriteLine($"Merging {target.name} in slot {target.slot}");
                _controller.mergeAll(target.slot);
            }
            _outputWriter.Flush();
        }

        internal void MergeGuffs()
        {
            for (var id = 1000000; id - 1000000 < _character.inventory.macguffins.Count; ++id)
                _controller.mergeAll(id);
        }

        internal void ManagePendant(ih[] ci)
        {
            var grouped = ci.Where(x => _pendants.Contains(x.id));
            foreach (var item in grouped)
            {
                if (item.level != 100) continue;
                var temp = _character.inventory.inventory[item.slot];
                if (!temp.removable) continue;
                var ic = _controller.inventory[item.slot];
                _outputWriter.WriteLine();
                typeof(ItemController).GetMethod("consumeItem", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(ic, null);
            }
        }

        internal void ManageLooty(ih[] ci)
        {
            var grouped = ci.Where(x => _lootys.Contains(x.id));
            foreach (var item in grouped)
            {
                if (item.level != 100) continue;
                var temp = _character.inventory.inventory[item.slot];
                if (!temp.removable) continue;
                var ic = _controller.inventory[item.slot];
                typeof(ItemController).GetMethod("consumeItem", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(ic, null);
            }
        }

        internal void ManageWandoos(ih[] ci)
        {
            var win = ci.Where(x => x.id == _wandoos[0]).DefaultIfEmpty(null).FirstOrDefault();
            if (win != null)
            {
                if (win.level > _character.wandoos98.OSlevel)
                {
                    var ic = _controller.inventory[win.slot];
                    typeof(ItemController).GetMethod("consumeItem", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.Invoke(ic, null);
                }
            }
        }

        #region Filtering
        internal void EnsureFiltered(ih[] ci)
        {
            var targets = ci.Where(x => x.level == 100);
            foreach (var target in targets)
            {
                FilterItem(target.id);
            }

            FilterEquip(_character.inventory.head);
            FilterEquip(_character.inventory.boots);
            FilterEquip(_character.inventory.chest);
            FilterEquip(_character.inventory.legs);
            FilterEquip(_character.inventory.weapon);
            if (_character.inventoryController.weapon2Unlocked())
                FilterEquip(_character.inventory.weapon2);

            foreach (var acc in _character.inventory.accs)
            {
                FilterEquip(acc);
            }
        }

        void FilterItem(int id)
        {
            if (_pendants.Contains(id) || _lootys.Contains(id) || _filterExcludes.Contains(id) || id < 40)
                return;

            _character.inventory.itemList.itemFiltered[id] = true;
        }

        void FilterEquip(Equipment e)
        {
            if (e.level == 100)
            {
                FilterItem(e.id);
            }
        }
        #endregion

    }
}

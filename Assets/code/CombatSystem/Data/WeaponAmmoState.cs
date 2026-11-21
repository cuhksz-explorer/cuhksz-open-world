using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    [Serializable]
    public class WeaponAmmoSlot
    {
        public int weaponId;
        public int magazine;
        public int reserve;
    }

    public static class WeaponAmmoState
    {
        static readonly Dictionary<int, WeaponAmmoSlot> _slots = new Dictionary<int, WeaponAmmoSlot>();

        public static event Action OnAmmoChanged;

        public static int GetMagazine(int weaponId)
        {
            EnsureWeapon(weaponId);
            return _slots.TryGetValue(weaponId, out var s) ? s.magazine : 0;
        }

        public static int GetReserve(int weaponId)
        {
            EnsureWeapon(weaponId);
            return _slots.TryGetValue(weaponId, out var s) ? s.reserve : 0;
        }

        public static bool HasAmmoInMagazine(int weaponId) => GetMagazine(weaponId) > 0;

        public static bool CanReload(int weaponId, WeaponEntry entry)
        {
            if (entry == null)
                return false;
            EnsureWeapon(weaponId, entry);
            if (!_slots.TryGetValue(weaponId, out var s))
                return false;
            int magSize = WeaponStatsHelper.GetMagazineSize(entry);
            return s.magazine < magSize && s.reserve > 0;
        }

        public static bool TryConsumeRound(int weaponId, WeaponEntry entry)
        {
            EnsureWeapon(weaponId, entry);
            if (!_slots.TryGetValue(weaponId, out var s) || s.magazine <= 0)
                return false;
            s.magazine--;
            NotifyChanged();
            return true;
        }

        public static bool TryReload(int weaponId, WeaponEntry entry)
        {
            if (!CanReload(weaponId, entry))
                return false;

            var s = _slots[weaponId];
            int magSize = WeaponStatsHelper.GetMagazineSize(entry);
            int needed = magSize - s.magazine;
            int moved = Mathf.Min(needed, s.reserve);
            s.magazine += moved;
            s.reserve -= moved;
            NotifyChanged();
            return true;
        }

        public static void EnsureWeapon(int weaponId, WeaponEntry entry = null)
        {
            if (weaponId <= 0)
                return;
            if (_slots.ContainsKey(weaponId))
                return;

            if (entry == null)
            {
                var db = WeaponDatabase.Load();
                entry = db != null ? db.GetById(weaponId) : null;
            }

            int mag = entry != null ? WeaponStatsHelper.GetMagazineSize(entry) : 12;
            int reserve = entry != null ? WeaponStatsHelper.GetReserveAmmo(entry) : 60;
            _slots[weaponId] = new WeaponAmmoSlot
            {
                weaponId = weaponId,
                magazine = mag,
                reserve = reserve
            };
        }

        public static void RefillOwnedWeapons(IReadOnlyList<int> ownedIds)
        {
            if (ownedIds == null)
                return;
            var db = WeaponDatabase.Load();
            for (int i = 0; i < ownedIds.Count; i++)
            {
                int id = ownedIds[i];
                var entry = db != null ? db.GetById(id) : null;
                _slots[id] = new WeaponAmmoSlot
                {
                    weaponId = id,
                    magazine = entry != null ? WeaponStatsHelper.GetMagazineSize(entry) : 12,
                    reserve = entry != null ? WeaponStatsHelper.GetReserveAmmo(entry) : 60
                };
            }
            NotifyChanged();
        }

        public static string SerializeJson()
        {
            var list = new List<WeaponAmmoSlot>(_slots.Values);
            return JsonUtility.ToJson(new WeaponAmmoSaveWrapper { slots = list });
        }

        public static void ApplyFromJson(string json)
        {
            _slots.Clear();
            if (string.IsNullOrWhiteSpace(json))
                return;
            try
            {
                var wrapper = JsonUtility.FromJson<WeaponAmmoSaveWrapper>(json);
                if (wrapper?.slots == null)
                    return;
                for (int i = 0; i < wrapper.slots.Count; i++)
                {
                    var s = wrapper.slots[i];
                    if (s == null || s.weaponId <= 0)
                        continue;
                    _slots[s.weaponId] = s;
                }
            }
            catch
            {
                _slots.Clear();
            }
            NotifyChanged();
        }

        public static void Clear() => _slots.Clear();

        static void NotifyChanged()
        {
            OnAmmoChanged?.Invoke();
            WeaponPersistenceBridge.MarkDirty();
        }

        [Serializable]
        class WeaponAmmoSaveWrapper
        {
            public List<WeaponAmmoSlot> slots = new List<WeaponAmmoSlot>();
        }
    }

    public static class WeaponStatsHelper
    {
        public static int GetMagazineSize(WeaponEntry entry)
        {
            if (entry == null)
                return 12;
            if (entry.magazineSize > 0)
                return entry.magazineSize;
            return entry.penStyle switch
            {
                PenStyle.Ballpoint => 30,
                PenStyle.Fineliner => 20,
                PenStyle.Fountain => 8,
                PenStyle.Rollerball => 25,
                _ => 24
            } + RarityMagBonus(entry.rarity);
        }

        public static int GetReserveAmmo(WeaponEntry entry)
        {
            if (entry == null)
                return 60;
            if (entry.maxReserveAmmo > 0)
                return entry.maxReserveAmmo;
            return GetMagazineSize(entry) * 4 + RarityMagBonus(entry.rarity) * 3;
        }

        public static float GetReloadTime(WeaponEntry entry)
        {
            if (entry == null)
                return 2.4f;
            if (entry.reloadTime > 0f)
                return entry.reloadTime;
            float baseTime = entry.penStyle switch
            {
                PenStyle.Ballpoint => 2.0f,
                PenStyle.Fineliner => 2.3f,
                PenStyle.Gel => 2.2f,
                PenStyle.Rollerball => 2.4f,
                PenStyle.Fountain => 3.0f,
                _ => 2.4f
            };
            return baseTime + RarityMagBonus(entry.rarity) * 0.05f;
        }

        static int RarityMagBonus(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Purple => 2,
                WeaponRarity.Orange => 4,
                WeaponRarity.Red => 6,
                _ => 0
            };
        }
    }
}

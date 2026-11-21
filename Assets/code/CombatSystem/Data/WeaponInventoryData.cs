using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    [Serializable]
    public class OwnedWeaponSlot
    {
        public int weaponId;
    }

    [Serializable]
    public class WeaponSavePayload
    {
        public List<OwnedWeaponSlot> weaponInventory = new List<OwnedWeaponSlot>();
        public int equippedWeaponId = WeaponDatabase.DefaultStarterWeaponId;
        public int previousWeaponId;
        public string ammoSaveJson = "";
    }

    public static class WeaponInventoryData
    {
        public const int MaxWeapons = 36;

        static readonly List<int> _ownedIds = new List<int>();
        static int _equippedId = WeaponDatabase.DefaultStarterWeaponId;
        static int _previousId;

        public static event Action OnChanged;

        public static int EquippedWeaponId => _equippedId;
        public static int PreviousWeaponId => _previousId;
        public static IReadOnlyList<int> OwnedWeaponIds => _ownedIds;

        public static void EnsureInitialized()
        {
            if (_ownedIds.Count > 0)
            {
                var db = WeaponDatabase.Load();
                for (int i = 0; i < _ownedIds.Count; i++)
                {
                    var entry = db != null ? db.GetById(_ownedIds[i]) : null;
                    WeaponAmmoState.EnsureWeapon(_ownedIds[i], entry);
                }
                return;
            }
            ResetToStarterLoadout();
        }

        public static void ResetToStarterLoadout()
        {
            _ownedIds.Clear();
            _ownedIds.Add(WeaponDatabase.DefaultStarterWeaponId);
            _equippedId = WeaponDatabase.DefaultStarterWeaponId;
            _previousId = 0;
            WeaponAmmoState.Clear();
            WeaponAmmoState.EnsureWeapon(WeaponDatabase.DefaultStarterWeaponId);
            NotifyChanged();
        }

        public static bool IsFull => _ownedIds.Count >= MaxWeapons;

        public static bool OwnsWeapon(int weaponId)
        {
            for (int i = 0; i < _ownedIds.Count; i++)
            {
                if (_ownedIds[i] == weaponId)
                    return true;
            }
            return false;
        }

        public static bool TryAddWeapon(int weaponId)
        {
            if (weaponId <= 0 || OwnsWeapon(weaponId) || IsFull)
                return false;
            _ownedIds.Add(weaponId);
            var db = WeaponDatabase.Load();
            var entry = db != null ? db.GetById(weaponId) : null;
            WeaponAmmoState.EnsureWeapon(weaponId, entry);
            NotifyChanged();
            return true;
        }

        public static bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= _ownedIds.Count)
                return false;
            int removedId = _ownedIds[index];
            _ownedIds.RemoveAt(index);
            if (_equippedId == removedId)
            {
                _previousId = 0;
                _equippedId = _ownedIds.Count > 0 ? _ownedIds[0] : WeaponDatabase.DefaultStarterWeaponId;
                if (!_ownedIds.Contains(_equippedId) && _ownedIds.Count > 0)
                    _equippedId = _ownedIds[0];
            }
            if (_previousId == removedId)
                _previousId = 0;
            NotifyChanged();
            return true;
        }

        public static bool TryEquip(int weaponId)
        {
            if (!OwnsWeapon(weaponId) || _equippedId == weaponId)
                return false;
            _previousId = _equippedId;
            _equippedId = weaponId;
            NotifyChanged();
            return true;
        }

        public static bool TryQuickSwitch()
        {
            if (_previousId <= 0 || !OwnsWeapon(_previousId))
                return false;
            int current = _equippedId;
            _equippedId = _previousId;
            _previousId = current;
            NotifyChanged();
            return true;
        }

        public static bool TrySwitchNext()
        {
            if (_ownedIds.Count <= 1)
                return false;
            int index = _ownedIds.IndexOf(_equippedId);
            if (index < 0)
                index = 0;
            int nextIndex = (index + 1) % _ownedIds.Count;
            return TryEquip(_ownedIds[nextIndex]);
        }

        public static bool TrySwitchPrevious()
        {
            if (_ownedIds.Count <= 1)
                return false;
            int index = _ownedIds.IndexOf(_equippedId);
            if (index < 0)
                index = 0;
            int prevIndex = (index - 1 + _ownedIds.Count) % _ownedIds.Count;
            return TryEquip(_ownedIds[prevIndex]);
        }

        public static string SerializeJson()
        {
            var payload = new WeaponSavePayload
            {
                equippedWeaponId = _equippedId,
                previousWeaponId = _previousId
            };
            for (int i = 0; i < _ownedIds.Count; i++)
                payload.weaponInventory.Add(new OwnedWeaponSlot { weaponId = _ownedIds[i] });
            payload.ammoSaveJson = WeaponAmmoState.SerializeJson();
            return JsonUtility.ToJson(payload);
        }

        public static void ApplyFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                EnsureInitialized();
                return;
            }

            WeaponSavePayload payload;
            try
            {
                payload = JsonUtility.FromJson<WeaponSavePayload>(json);
            }
            catch
            {
                EnsureInitialized();
                return;
            }

            if (payload == null || payload.weaponInventory == null || payload.weaponInventory.Count == 0)
            {
                EnsureInitialized();
                return;
            }

            _ownedIds.Clear();
            for (int i = 0; i < payload.weaponInventory.Count && _ownedIds.Count < MaxWeapons; i++)
            {
                int id = payload.weaponInventory[i]?.weaponId ?? 0;
                if (id > 0 && !_ownedIds.Contains(id))
                    _ownedIds.Add(id);
            }

            if (_ownedIds.Count == 0)
                _ownedIds.Add(WeaponDatabase.DefaultStarterWeaponId);

            _equippedId = payload.equippedWeaponId;
            if (!OwnsWeapon(_equippedId))
                _equippedId = _ownedIds[0];

            _previousId = payload.previousWeaponId;
            if (_previousId > 0 && !OwnsWeapon(_previousId))
                _previousId = 0;

            WeaponAmmoState.ApplyFromJson(payload.ammoSaveJson);
            var db = WeaponDatabase.Load();
            for (int i = 0; i < _ownedIds.Count; i++)
            {
                var entry = db != null ? db.GetById(_ownedIds[i]) : null;
                WeaponAmmoState.EnsureWeapon(_ownedIds[i], entry);
            }

            NotifyChanged();
        }

        public static void Clear()
        {
            _ownedIds.Clear();
            _equippedId = WeaponDatabase.DefaultStarterWeaponId;
            _previousId = 0;
            WeaponAmmoState.Clear();
        }

        static void NotifyChanged()
        {
            OnChanged?.Invoke();
            WeaponPersistenceBridge.MarkDirty();
        }
    }
}

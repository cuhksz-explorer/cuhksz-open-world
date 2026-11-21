using UnityEngine;

namespace GameControls
{
    public static class ControlBindingStore
    {
        const string PrefsKeyV1 = "GameControls.Profile_v1";
        const string PrefsKeyV2 = "GameControls.Profile_v2";

        static ControlBindingProfile _profile;

        public static ControlBindingProfile Profile => _profile ??= Load();

        public static void Save(ControlBindingProfile profile)
        {
            if (profile == null)
                return;
            _profile = profile;
            PlayerPrefs.SetString(PrefsKeyV2, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static void ResetToDefault()
        {
            _profile = CreateDefaultProfile();
            Save(_profile);
        }

        public static void Reload()
        {
            _profile = Load();
        }

        public static ControlBindingProfile Load()
        {
            if (PlayerPrefs.HasKey(PrefsKeyV2))
            {
                try
                {
                    var json = PlayerPrefs.GetString(PrefsKeyV2);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var loaded = JsonUtility.FromJson<ControlBindingProfile>(json);
                        if (loaded != null && loaded.bindings != null && loaded.bindings.Count > 0)
                            return EnsureAllActions(loaded);
                    }
                }
                catch
                {
                    // fall through
                }
            }

            if (PlayerPrefs.HasKey(PrefsKeyV1))
                return EnsureAllActions(MigrateFromV1());

            return CreateDefaultProfile();
        }

        static ControlBindingProfile MigrateFromV1()
        {
            var profile = new ControlBindingProfile();
            try
            {
                var json = PlayerPrefs.GetString(PrefsKeyV1);
                var v1 = JsonUtility.FromJson<ControlBindingProfileV1>(json);
                if (v1?.bindings != null)
                {
                    for (int i = 0; i < v1.bindings.Count; i++)
                    {
                        var e = v1.bindings[i];
                        if (e == null || e.primaryKey == KeyCode.None)
                            continue;
                        profile.bindings.Add(new KeyBindingEntry
                        {
                            action = e.action,
                            chord = KeyChord.FromKey(e.primaryKey)
                        });
                    }
                }
            }
            catch
            {
                profile = new ControlBindingProfile();
            }

            Save(EnsureAllActions(profile));
            return Profile;
        }

        static ControlBindingProfile EnsureAllActions(ControlBindingProfile profile)
        {
            var defaults = CreateDefaultProfile();
            for (int i = 0; i < defaults.bindings.Count; i++)
            {
                var def = defaults.bindings[i];
                var existing = profile.FindKeyBinding(def.action);
                if (existing == null)
                    profile.bindings.Add(new KeyBindingEntry { action = def.action, chord = def.chord });
                else if (existing.chord.IsEmpty)
                    existing.chord = def.chord;
            }

            return profile;
        }

        static ControlBindingProfile CreateDefaultProfile()
        {
            var profile = new ControlBindingProfile();
            void Add(GameControlAction action, KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
            {
                profile.bindings.Add(new KeyBindingEntry
                {
                    action = action,
                    chord = KeyChord.FromKey(key, ctrl, shift, alt)
                });
            }

            Add(GameControlAction.MoveForward, KeyCode.W);
            Add(GameControlAction.MoveBack, KeyCode.S);
            Add(GameControlAction.MoveLeft, KeyCode.A);
            Add(GameControlAction.MoveRight, KeyCode.D);
            Add(GameControlAction.Run, KeyCode.LeftShift);
            Add(GameControlAction.Jump, KeyCode.Space);
            Add(GameControlAction.Crouch, KeyCode.LeftControl);
            Add(GameControlAction.Interact, KeyCode.F);
            Add(GameControlAction.TogglePhone, KeyCode.F1);
            Add(GameControlAction.OpenMap, KeyCode.M);
            Add(GameControlAction.OpenInventory, KeyCode.B);
            Add(GameControlAction.OpenQuest, KeyCode.J);
            Add(GameControlAction.OpenMultiplayer, KeyCode.K);
            Add(GameControlAction.ToggleCamera, KeyCode.V);
            Add(GameControlAction.OpenFriends, KeyCode.C);
            Add(GameControlAction.OpenFlagAuction, KeyCode.O);
            Add(GameControlAction.OpenShop, KeyCode.T);
            return profile;
        }
    }
}

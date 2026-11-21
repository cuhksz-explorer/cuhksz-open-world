using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameControls
{
    [Serializable]
    public struct KeyChord
    {
        public KeyCode key;
        public bool ctrl;
        public bool shift;
        public bool alt;

        public bool IsEmpty => key == KeyCode.None;

        public bool Equals(KeyChord other) =>
            key == other.key && ctrl == other.ctrl && shift == other.shift && alt == other.alt;

        public static KeyChord FromKey(KeyCode k, bool useCtrl = false, bool useShift = false, bool useAlt = false) =>
            new() { key = k, ctrl = useCtrl, shift = useShift, alt = useAlt };
    }

    [Serializable]
    public class KeyBindingEntry
    {
        public GameControlAction action;
        public KeyChord chord;

        public KeyCode PrimaryKey => chord.key;
    }

    [Serializable]
    public class ControlBindingProfile
    {
        public List<KeyBindingEntry> bindings = new();

        public KeyBindingEntry FindKeyBinding(GameControlAction action)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
                if (entry != null && entry.action == action)
                    return entry;
            }

            return null;
        }

        public KeyBindingEntry GetOrCreate(GameControlAction action)
        {
            var existing = FindKeyBinding(action);
            if (existing != null)
                return existing;
            existing = new KeyBindingEntry { action = action };
            bindings.Add(existing);
            return existing;
        }

        public KeyChord GetChord(GameControlAction action)
        {
            var entry = FindKeyBinding(action);
            return entry != null ? entry.chord : default;
        }
    }

    [Serializable]
    class ControlBindingProfileV1
    {
        public List<KeyBindingEntryV1> bindings = new();
    }

    [Serializable]
    class KeyBindingEntryV1
    {
        public GameControlAction action;
        public KeyCode primaryKey = KeyCode.None;
        public KeyCode secondaryKey = KeyCode.None;
    }
}

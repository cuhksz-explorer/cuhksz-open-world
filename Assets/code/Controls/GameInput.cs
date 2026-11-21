using UnityEngine;

namespace GameControls
{
    public static class GameInput
    {
        public static bool IsRebinding { get; set; }

        static bool _keyBindingsPanelOpen;

        public static void SetKeyBindingsPanelOpen(bool open) => _keyBindingsPanelOpen = open;

        public static bool BlocksGameplayHotkeys() => IsRebinding || _keyBindingsPanelOpen;

        public static KeyChord GetChord(GameControlAction action) =>
            ControlBindingStore.Profile.GetChord(action);

        public static KeyCode GetPrimaryKey(GameControlAction action) => GetChord(action).key;

        public static bool GetKeyDown(GameControlAction action)
        {
            if (BlocksGameplayHotkeys())
                return false;
            return MatchChordDown(GetChord(action));
        }

        public static bool GetKey(GameControlAction action)
        {
            if (BlocksGameplayHotkeys())
                return false;
            return MatchChordHeld(GetChord(action));
        }

        public static Vector2 GetMovementAxis()
        {
            if (BlocksGameplayHotkeys())
                return Vector2.zero;

            float h = 0f;
            float v = 0f;
            if (GetKey(GameControlAction.MoveRight))
                h += 1f;
            if (GetKey(GameControlAction.MoveLeft))
                h -= 1f;
            if (GetKey(GameControlAction.MoveForward))
                v += 1f;
            if (GetKey(GameControlAction.MoveBack))
                v -= 1f;
            return new Vector2(h, v);
        }

        public static string FormatChord(GameControlAction action) => FormatChord(GetChord(action));

        public static string FormatChord(KeyChord chord)
        {
            if (chord.IsEmpty)
                return "--";

            var parts = new System.Collections.Generic.List<string>(4);
            if (chord.ctrl)
                parts.Add("Ctrl");
            if (chord.shift)
                parts.Add("Shift");
            if (chord.alt)
                parts.Add("Alt");
            parts.Add(FormatKeyName(chord.key));
            return string.Join("+", parts);
        }

        public static string GetActionLabel(GameControlAction action) => action switch
        {
            GameControlAction.MoveForward => Loc.Choose("前进", "Forward"),
            GameControlAction.MoveBack => Loc.Choose("后退", "Back"),
            GameControlAction.MoveLeft => Loc.Choose("左移", "Left"),
            GameControlAction.MoveRight => Loc.Choose("右移", "Right"),
            GameControlAction.Run => Loc.Choose("奔跑", "Run"),
            GameControlAction.Jump => Loc.Choose("跳跃", "Jump"),
            GameControlAction.Crouch => Loc.Choose("蹲下", "Crouch"),
            GameControlAction.Interact => Loc.Choose("互动", "Interact"),
            GameControlAction.TogglePhone => Loc.Choose("手机", "Phone"),
            GameControlAction.OpenMap => Loc.Choose("地图", "Map"),
            GameControlAction.OpenInventory => Loc.Choose("背包", "Inventory"),
            GameControlAction.OpenQuest => Loc.Choose("任务", "Quests"),
            GameControlAction.OpenMultiplayer => Loc.Choose("联机", "Multiplayer"),
            GameControlAction.ToggleCamera => Loc.Choose("视角", "Camera"),
            GameControlAction.OpenFriends => Loc.Choose("好友", "Friends"),
            GameControlAction.OpenFlagAuction => Loc.Choose("竞拍", "Auction"),
            GameControlAction.OpenShop => Loc.Choose("商城", "Shop"),
            _ => action.ToString()
        };

        public static bool TryCaptureChordDown(out KeyChord chord)
        {
            chord = default;
            if (!Input.anyKeyDown)
                return false;

            if (Input.GetKeyDown(KeyCode.Escape))
                return false;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            var main = DetectMainKeyDown();
            if (main == KeyCode.None)
                return false;

            if (main == KeyCode.LeftControl || main == KeyCode.RightControl)
                ctrl = false;
            if (main == KeyCode.LeftShift || main == KeyCode.RightShift)
                shift = false;
            if (main == KeyCode.LeftAlt || main == KeyCode.RightAlt)
                alt = false;

            chord = new KeyChord { key = main, ctrl = ctrl, shift = shift, alt = alt };
            return true;
        }

        static bool MatchChordDown(KeyChord chord)
        {
            if (chord.IsEmpty)
                return false;
            if (!Input.GetKeyDown(chord.key))
                return false;
            return ModifiersMatch(chord);
        }

        static bool MatchChordHeld(KeyChord chord)
        {
            if (chord.IsEmpty)
                return false;
            if (!Input.GetKey(chord.key))
                return false;
            return ModifiersMatch(chord);
        }

        static bool ModifiersMatch(KeyChord chord)
        {
            if (IsModifierKey(chord.key))
                return true;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // 只要求「绑定里写明的修饰键必须按下」，不要求未绑定的修饰键必须松开。
            // 否则 Shift 疾跑 / Ctrl 蹲下时，WASD 会因 shift==true 而全部失效。
            if (chord.ctrl && !ctrl) return false;
            if (chord.shift && !shift) return false;
            if (chord.alt && !alt) return false;
            return true;
        }

        static KeyCode DetectMainKeyDown()
        {
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None || k == KeyCode.Escape)
                    continue;
                if (Input.GetKeyDown(k))
                    return k;
            }

            return KeyCode.None;
        }

        static bool IsModifierKey(KeyCode k) =>
            k == KeyCode.LeftControl || k == KeyCode.RightControl
            || k == KeyCode.LeftShift || k == KeyCode.RightShift
            || k == KeyCode.LeftAlt || k == KeyCode.RightAlt;

        static string FormatKeyName(KeyCode k) => k switch
        {
            KeyCode.LeftShift => "Shift",
            KeyCode.RightShift => "Shift",
            KeyCode.LeftControl => "Ctrl",
            KeyCode.RightControl => "Ctrl",
            KeyCode.LeftAlt => "Alt",
            KeyCode.RightAlt => "Alt",
            KeyCode.Return => "Enter",
            KeyCode.KeypadEnter => "NumEnter",
            KeyCode.Space => "Space",
            KeyCode.Mouse0 => Loc.Choose("鼠标左键", "LMB"),
            KeyCode.Mouse1 => Loc.Choose("鼠标右键", "RMB"),
            KeyCode.Mouse2 => Loc.Choose("鼠标中键", "MMB"),
            _ => k.ToString()
        };
    }
}

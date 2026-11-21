namespace GameControls
{
    public static class KeyBindingConflictChecker
    {
        public static bool TryAssign(
            ControlBindingProfile profile,
            GameControlAction action,
            KeyChord chord,
            out GameControlAction conflictAction)
        {
            conflictAction = default;
            if (profile == null || chord.IsEmpty)
                return false;

            for (int i = 0; i < profile.bindings.Count; i++)
            {
                var entry = profile.bindings[i];
                if (entry == null || entry.action == action || entry.chord.IsEmpty)
                    continue;
                if (!entry.chord.Equals(chord))
                    continue;
                conflictAction = entry.action;
                return false;
            }

            var target = profile.GetOrCreate(action);
            target.chord = chord;
            return true;
        }
    }
}

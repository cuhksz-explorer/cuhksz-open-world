using UnityEngine;

namespace PhoneUI
{
    static class PhoneAnimatorUtil
    {
        public static bool HasParameter(Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name))
                return false;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.name == name && p.type == type)
                    return true;
            }

            return false;
        }

        public static void SetBoolIfExists(Animator animator, string name, bool value)
        {
            if (!HasParameter(animator, name, AnimatorControllerParameterType.Bool))
                return;
            animator.SetBool(name, value);
        }

        public static void SetTriggerIfExists(Animator animator, string name)
        {
            if (!HasParameter(animator, name, AnimatorControllerParameterType.Trigger))
                return;
            animator.SetTrigger(name);
        }

        public static void ResetTriggerIfExists(Animator animator, string name)
        {
            if (!HasParameter(animator, name, AnimatorControllerParameterType.Trigger))
                return;
            animator.ResetTrigger(name);
        }
    }
}

using GameControls;
using UnityEngine;

public class KeyInputController : MonoBehaviour
{
    private Animator animator;

    [Header("Key Parameters")]
    public string wParam = "W";
    public string aParam = "A";
    public string sParam = "S";
    public string dParam = "D";
    public string runParam = "Run";
    public string crouchParam = "Crouch";

    private bool wPressed, aPressed, sPressed, dPressed;
    bool _hasCrouchParam;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("Animator component not found!");
        }
        else
        {
            CheckAnimatorParameters();
            ResetAllParameters();
        }
    }

    public void BindAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;
        if (animator != null)
        {
            CheckAnimatorParameters();
            ResetAllParameters();
        }
    }

    void Update()
    {
        if (animator == null) return;
        if (ChairSitState.BlocksGameplay)
        {
            ResetAllParameters();
            return;
        }

        bool wCurrent = GameInput.GetKey(GameControlAction.MoveForward);
        bool aCurrent = GameInput.GetKey(GameControlAction.MoveLeft);
        bool sCurrent = GameInput.GetKey(GameControlAction.MoveBack);
        bool dCurrent = GameInput.GetKey(GameControlAction.MoveRight);

        bool crouchPressed = GameInput.GetKey(GameControlAction.Crouch);
        if (_hasCrouchParam)
            animator.SetBool(crouchParam, crouchPressed);

        bool shiftPressed = GameInput.GetKey(GameControlAction.Run) && !crouchPressed;
        animator.SetBool(runParam, shiftPressed);

        if (crouchPressed)
        {
            ResetMovementParameters();
            wPressed = wCurrent;
            aPressed = aCurrent;
            sPressed = sCurrent;
            dPressed = dCurrent;
            return;
        }

        bool oppositePressed = (wCurrent && sCurrent) || (aCurrent && dCurrent);
        if (oppositePressed)
        {
            ResetMovementParameters();
            return;
        }

        int keyCount = (wCurrent ? 1 : 0) + (aCurrent ? 1 : 0) +
                      (sCurrent ? 1 : 0) + (dCurrent ? 1 : 0);

        if (keyCount == 1)
        {
            ResetMovementParameters();

            if (wCurrent) animator.SetBool(wParam, true);
            if (aCurrent) animator.SetBool(aParam, true);
            if (sCurrent) animator.SetBool(sParam, true);
            if (dCurrent) animator.SetBool(dParam, true);
        }
        else if (keyCount > 1)
        {
            ResetMovementParameters();

            if (wCurrent)
                animator.SetBool(wParam, true);
            else if (sCurrent)
                animator.SetBool(sParam, true);
            else if (aCurrent)
                animator.SetBool(aParam, true);
            else if (dCurrent)
                animator.SetBool(dParam, true);
        }
        else
        {
            ResetMovementParameters();
        }

        wPressed = wCurrent;
        aPressed = aCurrent;
        sPressed = sCurrent;
        dPressed = dCurrent;
    }

    void ResetAllParameters()
    {
        if (animator != null)
        {
            animator.SetBool(wParam, false);
            animator.SetBool(aParam, false);
            animator.SetBool(sParam, false);
            animator.SetBool(dParam, false);
            animator.SetBool(runParam, false);
            if (_hasCrouchParam)
                animator.SetBool(crouchParam, false);
        }
    }

    void ResetMovementParameters()
    {
        if (animator != null)
        {
            animator.SetBool(wParam, false);
            animator.SetBool(aParam, false);
            animator.SetBool(sParam, false);
            animator.SetBool(dParam, false);
        }
    }

    void CheckAnimatorParameters()
    {
        bool hasW = false, hasA = false, hasS = false, hasD = false, hasRun = false;
        _hasCrouchParam = false;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == wParam && param.type == AnimatorControllerParameterType.Bool)
                hasW = true;
            if (param.name == aParam && param.type == AnimatorControllerParameterType.Bool)
                hasA = true;
            if (param.name == sParam && param.type == AnimatorControllerParameterType.Bool)
                hasS = true;
            if (param.name == dParam && param.type == AnimatorControllerParameterType.Bool)
                hasD = true;
            if (param.name == runParam && param.type == AnimatorControllerParameterType.Bool)
                hasRun = true;
            if (param.name == crouchParam && param.type == AnimatorControllerParameterType.Bool)
                _hasCrouchParam = true;
        }

        if (!hasW) Debug.LogError($"Animator parameter '{wParam}' not found!");
        if (!hasA) Debug.LogError($"Animator parameter '{aParam}' not found!");
        if (!hasS) Debug.LogError($"Animator parameter '{sParam}' not found!");
        if (!hasD) Debug.LogError($"Animator parameter '{dParam}' not found!");
        if (!hasRun) Debug.LogError($"Animator parameter '{runParam}' not found!");
    }
}

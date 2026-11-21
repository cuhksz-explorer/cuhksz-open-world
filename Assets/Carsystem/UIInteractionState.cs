public static class UIInteractionState
{
    public static bool IsNpcShopOpen { get; private set; }
    public static bool IsDrivingHudActive { get; private set; }

    public static bool BlocksWorldPrompt => IsNpcShopOpen || IsDrivingHudActive;

    public static void SetNpcShopOpen(bool open)
    {
        IsNpcShopOpen = open;
    }

    public static void SetDrivingHudActive(bool active)
    {
        IsDrivingHudActive = active;
    }
}


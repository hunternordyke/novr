namespace NOVR.MotionControls;

public enum ControllerButton
{
    None, Trigger, Primary, Secondary, Menu, Shoulder, Top, Outside, StickClick,
    ShoulderOrPrimary, OutsideOrSecondary
}

// The in-headset help uses the same hand/profile choices as input sampling.
internal static class ControllerButtonNames
{
    internal static string Describe(ControllerButton button, bool left, bool frame)
    {
        if (button == ControllerButton.ShoulderOrPrimary) button = frame ? ControllerButton.Shoulder : ControllerButton.Primary;
        if (button == ControllerButton.OutsideOrSecondary) button = frame ? ControllerButton.Outside : ControllerButton.Secondary;
        return button switch
        {
            ControllerButton.Trigger => "Trigger",
            ControllerButton.StickClick => "Thumbstick click",
            ControllerButton.Primary => frame ? (left ? "D-pad down" : "A") : (left ? "X" : "A"),
            ControllerButton.Secondary => frame ? (left ? "D-pad right" : "X") : (left ? "Y" : "B"),
            ControllerButton.Menu => frame ? (left ? "View" : "Menu") : "Menu (if available)",
            ControllerButton.Shoulder => frame ? "Shoulder" : "Shoulder (if available)",
            ControllerButton.Top => frame ? (left ? "D-pad up" : "Y") : "Unavailable",
            ControllerButton.Outside => frame ? (left ? "D-pad left" : "B") : "Unavailable",
            _ => "Unassigned"
        };
    }
}

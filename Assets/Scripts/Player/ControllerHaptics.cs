using UnityEngine;
using UnityEngine.XR;

/// <summary>Short vibration cues on the hand controllers.</summary>
public static class ControllerHaptics
{
    public const float CueAmplitude = 0.35f;
    public const float CueSeconds = 0.08f;

    /// <summary>
    /// Hand an XRI interactor belongs to, from the name of its controller object in the rig
    /// ("Left Controller" / "Right Controller"). Defaults to the right hand.
    /// </summary>
    public static XRNode HandOf(Transform interactor)
    {
        for (Transform t = interactor; t != null; t = t.parent)
        {
            if (t.name.StartsWith("Left")) return XRNode.LeftHand;
            if (t.name.StartsWith("Right")) return XRNode.RightHand;
        }
        return XRNode.RightHand;
    }

    /// <summary>Sends one short pulse to the given hand. Does nothing if the device has no haptics.</summary>
    public static void Pulse(XRNode hand, float amplitude = CueAmplitude, float seconds = CueSeconds)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        if (!device.isValid) return;
        if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0u, amplitude, seconds);
        }
    }
}

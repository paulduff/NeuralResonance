namespace NRE.SimAvatar;

public readonly record struct AvatarToolSignal(
    AvatarToolAction Action,
    AvatarToolDirection Direction,
    double Strength)
{
    public static AvatarToolSignal None { get; } = new(AvatarToolAction.None, AvatarToolDirection.Forward, 0.0);

    public bool HasAction => Action != AvatarToolAction.None && Strength > 0.0;
}

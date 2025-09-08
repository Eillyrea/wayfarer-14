namespace Content.Server._Coyote.SpaceJanitor;

/// <summary>
/// This is a thing that, when added to an entity, will make the SpaceJanitorSystem track it.
/// </summary>
[RegisterComponent]
public sealed partial class SpaceJanitorComponent : Component
{
    /// <summary>
    /// This is the time when the system first found the entity in space.
    /// </summary>
    public TimeSpan FoundInSpaceTime = TimeSpan.Zero;
}

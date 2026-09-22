using System;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Compatibility name for existing scenes. New code should use InteractableObject for both UI and 3D.
    /// </summary>
    [Obsolete("Use InteractableObject for both UI and 3D targets.", false)]
    public class InteractableUIElement : InteractableObject
    {
    }
}

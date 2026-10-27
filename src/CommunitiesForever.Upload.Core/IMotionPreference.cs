namespace CommunitiesForever.Upload.Core;

/// <summary>Whether the user wants animations (Windows: Settings, Accessibility, Visual effects, Animation effects).</summary>
public interface IMotionPreference
{
    bool AnimationsEnabled { get; }
}

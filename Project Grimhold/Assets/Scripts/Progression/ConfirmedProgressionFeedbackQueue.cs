using System;
using System.Collections.Generic;

/// <summary>Application-owned transient delivery queue, populated only by newly committed Raid receipts.</summary>
public sealed class ConfirmedProgressionFeedbackQueue
{
    private readonly Dictionary<ProfileId, Queue<ExperienceApplicationResult>> _pending = new();

    public void Record(ProfileId profileId, ExperienceApplicationResult result)
    {
        if (!profileId.IsValid || result.PreviousLevel < 1 ||
            result.ResultingLevel < result.PreviousLevel ||
            (result.ResultingLevel == result.PreviousLevel && result.ResultingExperience <= result.PreviousExperience))
        {
            return;
        }

        if (!_pending.TryGetValue(profileId, out var queue))
        {
            queue = new Queue<ExperienceApplicationResult>();
            _pending.Add(profileId, queue);
        }
        queue.Enqueue(result);
    }

    public bool TryTake(ProfileId profileId, out ExperienceApplicationResult result)
    {
        result = default;
        if (!_pending.TryGetValue(profileId, out var queue) || queue.Count == 0)
            return false;
        result = queue.Dequeue();
        if (queue.Count == 0) _pending.Remove(profileId);
        return true;
    }

    public void Clear() => _pending.Clear();
}

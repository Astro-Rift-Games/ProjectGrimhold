/// <summary>
/// Pure Doc 18 §5 contact resolution: orders contacts spatially and deterministically, deduplicates
/// each subject per event, applies the re-impact policy and stops at the first termination condition.
/// Contacts after termination are discarded.
/// </summary>
public static class ProjectileContactResolver
{
    /// <summary>
    /// Sorts in place by <see cref="ProjectileContact.CompareTo"/> and keeps only the nearest entry of each
    /// target or interceptor slot, so a subject receives at most one resolution per event.
    /// </summary>
    public static int SortAndDeduplicate(ProjectileContact[] contacts, int count)
    {
        // Insertion sort: tiny bounded buffers, stable and allocation-free.
        for (int i = 1; i < count; i++)
        {
            ProjectileContact current = contacts[i];
            int j = i - 1;
            while (j >= 0 && contacts[j].CompareTo(current) > 0)
            {
                contacts[j + 1] = contacts[j];
                j--;
            }

            contacts[j + 1] = current;
        }

        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            bool duplicate = false;
            for (int k = 0; k < kept && !duplicate; k++)
                duplicate = contacts[k].IsSameSubject(contacts[i]);
            if (!duplicate) contacts[kept++] = contacts[i];
        }

        return kept;
    }

    /// <param name="contacts">Contacts already ordered by <see cref="SortAndDeduplicate"/>.</param>
    /// <param name="eventTick">Authoritative tick identifying this resolution event.</param>
    /// <param name="pulseDue">For periodic pulses, whether this event is a pulse; ignored otherwise.</param>
    /// <param name="impactsApplied">Impacts already applied before this event.</param>
    public static ProjectileContactResolution Resolve<TSink>(ProjectileContact[] contacts, int count,
        in ProjectileImpactSettings settings, ProjectileImpactLedger ledger, int eventTick, bool pulseDue,
        int impactsApplied, ref TSink sink) where TSink : IProjectileContactSink
    {
        for (int i = 0; i < count; i++)
        {
            ProjectileContact contact = contacts[i];
            switch (contact.Kind)
            {
                case ProjectileContactKind.Interceptor:
                    // A consumed defense terminates; an already-consumed one no longer exists physically.
                    if (sink.TryIntercept(in contact))
                        return new ProjectileContactResolution(true, i, impactsApplied);
                    break;

                case ProjectileContactKind.Blocker:
                    return new ProjectileContactResolution(true, i, impactsApplied);

                case ProjectileContactKind.Target:
                    if (!ShouldImpact(in contact, in settings, ledger, eventTick, pulseDue)) break;
                    // Count before the external call: a reentrant resolver cannot grant extra impacts.
                    impactsApplied++;
                    sink.ApplyImpact(in contact);
                    if (impactsApplied >= settings.MaxImpactCount)
                        return new ProjectileContactResolution(true, i, impactsApplied);
                    break;
            }
        }

        return new ProjectileContactResolution(false, -1, impactsApplied);
    }

    private static bool ShouldImpact(in ProjectileContact contact, in ProjectileImpactSettings settings,
        ProjectileImpactLedger ledger, int eventTick, bool pulseDue)
    {
        switch (settings.Policy)
        {
            case ProjectileReimpactPolicy.OncePerTarget:
                return ledger.TryRecordFirstImpact(contact.EntityIdValue, eventTick);
            case ProjectileReimpactPolicy.OncePerContact:
                return ledger.TryBeginContact(contact.EntityIdValue, eventTick);
            case ProjectileReimpactPolicy.PeriodicPulse:
                return pulseDue;
            default:
                return false;
        }
    }
}

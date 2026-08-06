using System;

public enum SessionStartupMode
{
    None = 0,
    FreshSession,
    HostMigrationResume
}

public readonly struct SessionStartupContext
{
    public SessionStartupMode Mode { get; }

    public bool IsValid => Mode == SessionStartupMode.FreshSession || Mode == SessionStartupMode.HostMigrationResume;

    public bool ShouldExecuteHostBootstrap => Mode == SessionStartupMode.FreshSession;
    public bool ShouldInitializeMatchPhase => Mode == SessionStartupMode.FreshSession;
    public bool ShouldExecuteInitialSceneBootstrap => Mode == SessionStartupMode.FreshSession;

    public SessionStartupContext(SessionStartupMode mode)
    {
        if (mode != SessionStartupMode.FreshSession && mode != SessionStartupMode.HostMigrationResume)
        {
            throw new ArgumentException($"Invalid startup mode: {mode}", nameof(mode));
        }

        Mode = mode;
    }

    public static SessionStartupContext FreshSession => new SessionStartupContext(SessionStartupMode.FreshSession);
    public static SessionStartupContext HostMigrationResume => new SessionStartupContext(SessionStartupMode.HostMigrationResume);
}

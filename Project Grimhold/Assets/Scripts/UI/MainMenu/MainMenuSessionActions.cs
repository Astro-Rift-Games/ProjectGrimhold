public readonly struct MainMenuSessionActions
{
    public bool PlayVisible { get; }
    public bool PlayInteractable { get; }
    public bool LoginVisible { get; }
    public bool LogoutVisible { get; }
    public bool SecondaryInteractable { get; }

    public MainMenuSessionActions(bool isAuthenticated, bool isBusy)
    {
        PlayVisible = isAuthenticated;
        PlayInteractable = isAuthenticated && !isBusy;
        LoginVisible = !isAuthenticated;
        LogoutVisible = isAuthenticated;
        SecondaryInteractable = !isBusy;
    }
}

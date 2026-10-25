public readonly struct MainMenuSessionActions
{
    public bool PlayVisible => true;
    public bool PlayInteractable { get; }
    public bool LoginVisible { get; }
    public bool LogoutVisible { get; }
    public bool SecondaryInteractable { get; }

    public MainMenuSessionActions(bool isAuthenticated, bool isBusy)
    {
        PlayInteractable = isAuthenticated && !isBusy;
        LoginVisible = !isAuthenticated;
        LogoutVisible = isAuthenticated;
        SecondaryInteractable = !isBusy;
    }
}

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface INavigationService
    {
        // this is the from the existing INavigationService
        void NavigateToHelpPage();
        void NavigateToSettingsPage();
        void NavigateToChatPage();

        // from IXDSNavigationService
        object Parameter { get; }
        void Initialize();
        bool CanGoBack { get; }

        // Below here, I just added the members of MvvmLight's INavigationService:

        //
        // Summary:
        //     The key corresponding to the currently displayed page.
        string CurrentPageKey { get; }

        //
        // Summary:
        //     If possible, instructs the navigation service to discard the current page and
        //     display the previous page on the navigation stack.
        void GoBack();
        //
        // Summary:
        //     Instructs the navigation service to display a new page corresponding to the given
        //     key. Depending on the platforms, the navigation service might have to be configured
        //     with a key/page list.
        //
        // Parameters:
        //   pageKey:
        //     The key corresponding to the page that should be displayed.
        void NavigateTo(string pageKey);
        //
        // Summary:
        //     Instructs the navigation service to display a new page corresponding to the given
        //     key, and passes a parameter to the new page. Depending on the platforms, the
        //     navigation service might have to be Configure with a key/page list.
        //
        // Parameters:
        //   pageKey:
        //     The key corresponding to the page that should be displayed.
        //
        //   parameter:
        //     The parameter that should be passed to the new page.
        void NavigateTo(string pageKey, object parameter);

    }
}
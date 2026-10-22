using System;

using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class AddHostingAccount : ChromelessWindow
    {
        public AddHostingAccount()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private async void OnSure(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ViewModels.AddHostingAccount vm && await vm.SaveAsync())
                Close();
        }

        private async void OnSignInWithBrowser(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ViewModels.AddHostingAccount vm && await vm.SignInWithBrowserAsync())
                Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as ViewModels.AddHostingAccount)?.CancelSignIn();
            base.OnClosed(e);
        }

        private void OnCancel(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            Close();
        }
    }
}

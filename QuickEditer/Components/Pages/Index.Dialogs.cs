namespace QuickEditer.Components.Pages
{
    public partial class Index
    {
        private void OnConfirmCloseTabYes()
        {
            _confirmCloseTabTcs?.TrySetResult("yes");
        }

        private void OnConfirmCloseTabNo()
        {
            _confirmCloseTabTcs?.TrySetResult("no");
        }

        private void OnConfirmCloseTabCancel()
        {
            _confirmCloseTabTcs?.TrySetResult("cancel");
        }

        private void OnCloseConfirmCloseTab()
        {
            _showConfirmCloseTab = false;
            _confirmCloseTabTcs?.TrySetResult("cancel");
        }
    }
}
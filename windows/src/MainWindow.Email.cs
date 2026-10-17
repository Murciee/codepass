using System;
using System.Windows;
using System.Windows.Controls;

namespace Codepass
{
    sealed partial class MainWindow
    {
        void InitializeEmailPage(FrameworkElement root)
        {
            emailPage = new EmailSettingsPage(root, cfg,
                delegate { return !closeCompleted && IsVisible && lockOverlay.Visibility != Visibility.Visible; },
                delegate { Config draft; return ReadDraft(SettingsSection.Email, out draft) ? draft : null; },
                EmailWatcher.TestConnection);
            emailPage.SaveRequested += delegate(SettingsSection section) { QueueSection(section); };
            emailPage.ToastRequested += ShowToast;
            emailPage.FeedbackRequested += delegate(string text) { SettingsFeedback(text, false); };
            emailPage.SimulateRequested += delegate { SimulateTestMessage(true); };
            emailPage.AttachMultilineScrolling(ScrollMultilineTextBox);
        }

        public void UpdateEmailStatus()
        {
            string storage = string.Join("\n", new string[] { Config.LastSaveError ?? "", History.LastSaveError ?? "" }).Trim();
            if (lblStorageError != null) lblStorageError.Text = storage;
            Border banner = Find<Border>((FrameworkElement)Content, "storageErrorBanner");
            if (banner != null) banner.Visibility = storage.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (emailPage != null) emailPage.UpdateStatus(Program.EmailConfigured(), QqPopupWatcher.LastSignalText);
            UpdateReceiveSummary(); UpdatePcReceiverStatus();
        }

    }
}

using System.Windows;
using LogLens.Core;

namespace LogLens.App;

public partial class InvestigationWindow : Window
{
    public InvestigationWindow(Incident? incident, InvestigationStore store)
    {
        InitializeComponent(); WindowTheme.Attach(this);
        DataContext = new InvestigationViewModel(incident, store, () => MessageBox.Show(this,
            "Delete all investigation history stored by LogLens on this PC? This cannot be undone.", "Clear local history", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    }
}

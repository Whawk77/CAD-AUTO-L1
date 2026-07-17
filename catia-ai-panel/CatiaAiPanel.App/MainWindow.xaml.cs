using System.Windows;

namespace CatiaAiPanel.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CatiaDockingService _docking;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _docking = new CatiaDockingService(this);
        Loaded += (_, _) => _docking.Start();
        Closed += (_, _) => _docking.Dispose();
    }

    private async void ConfirmExecute_Click(object sender, RoutedEventArgs e)
    {
        var proposal = _viewModel.CurrentProposal;
        if (proposal is null || !_viewModel.CanExecute) return;
        var effects = proposal.ExpectedEffects.Count == 0 ? "未声明具体影响" : string.Join("\n• ", proposal.ExpectedEffects.Prepend(""));
        var target = proposal.TargetDocumentMode == Core.TargetDocumentMode.NewPart
            ? "新建 CATPart（当前参考文档不修改）"
            : "当前 CATIA 文档";
        var answer = MessageBox.Show(
            $"即将在{target}中执行 {proposal.OperationKind} 宏。\n\n预期影响：{effects}\n\n继续执行？",
            "逐版确认 CATIA 宏",
            MessageBoxButton.YesNo,
            proposal.OperationKind == Core.OperationKind.Write ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) await _viewModel.ExecuteConfirmedAsync();
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.MacroCode)) Clipboard.SetText(_viewModel.MacroCode);
    }

    private async void DeleteMeasurement_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedMeasurement;
        if (selected is null) return;
        var answer = MessageBox.Show(
            $"删除测量尺寸“{selected.Name} = {selected.Value:G10} {selected.Unit}”？\n\n删除后不会在后续同步中自动恢复；重新开始一次原生测量后仍可再次记录。",
            "删除测量尺寸",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes) await _viewModel.DeleteSelectedMeasurementAsync();
    }

    private async void ClearMeasurements_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Measurements.Count == 0) return;
        var answer = MessageBox.Show(
            $"清空当前文档的全部 {_viewModel.Measurements.Count} 条测量尺寸？\n\n此操作不会修改 CATIA 模型，但本地测量账本无法自动恢复。",
            "清空测量账本",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes) await _viewModel.ClearMeasurementNotebookAsync();
    }
}

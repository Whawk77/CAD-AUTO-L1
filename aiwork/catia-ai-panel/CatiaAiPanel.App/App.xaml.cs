using System.Windows;
using CatiaAiPanel.Core;

namespace CatiaAiPanel.App;

public partial class App : Application
{
    private CatiaSession? _catia;
    private CodexConversation? _codex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _catia = new CatiaSession();
        _codex = new CodexConversation();
        var serializer = new CatiaContextSerializer();
        var safety = new MacroSafetyValidator();
        var executor = new MacroExecutor(_catia, safety, serializer);
        var workflow = new AiWorkflowController(
            _catia,
            _codex,
            executor,
            new PromptBuilder(serializer),
            new SessionStore());
        var viewModel = new MainViewModel(_catia, workflow, safety);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _codex?.Dispose();
        if (_catia is not null) _catia.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}

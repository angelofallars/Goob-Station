using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared._Shitmed.Autodoc;
using Content.Shared._Shitmed.Autodoc.Components;
using Content.Shared._Shitmed.Autodoc.Systems;
using Robust.Client.AutoGenerated;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client._Shitmed.Autodoc;

[GenerateTypedNameReferences]
public sealed partial class AutodocWindow : FancyWindow
{
    private IEntityManager _entMan;
    private IPlayerManager _player;
    private SharedAutodocSystem _autodoc;

    private EntityUid _owner;
    private bool _active;
    private int _programCount = 0;

    public event Action<string>? OnCreateProgram;
    public event Action<int>? OnToggleProgramSafety;
    public event Action<int>? OnRemoveProgram;
    public event Action<int, IAutodocStep, int>? OnAddStep;
    public event Action<int, int>? OnRemoveStep;
    public event Action<int>? OnStart;
    public event Action? OnStop;

    private DialogWindow? _dialog;
    private AutodocProgramWindow? _currentProgram;

    public AutodocWindow(EntityUid owner, IEntityManager entMan, IPlayerManager player)
    {
        RobustXamlLoader.Load(this);

        _entMan = entMan;
        _player = player;
        _autodoc = entMan.System<SharedAutodocSystem>();

        _owner = owner;

        OnClose += () =>
        {
            _dialog?.Close();
            _currentProgram?.Close();
        };

        CreateProgramButton.OnPressed += _ =>
        {
            if (_dialog != null)
            {
                _dialog.MoveToFront();
                return;
            }

            if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
                return;

            var field = "title";
            var prompt = Loc.GetString("autodoc-program-title");
            var placeholder = Loc.GetString("autodoc-program-title-placeholder", ("number", comp.Programs.Count + 1));
            var entry = new QuickDialogEntry(field, QuickDialogEntryType.ShortText, prompt, placeholder);
            var entries = new List<QuickDialogEntry> { entry };
            _dialog = new DialogWindow(CreateProgramButton.Text!, entries);
            _dialog.OnConfirmed += responses =>
            {
                var title = responses[field].Trim();
                if (title.Length < 1 || title.Length > comp.MaxProgramTitleLength)
                    return;

                OnCreateProgram?.Invoke(title);
            };

            // prevent MoveToFront being called on a closed window and double closing
            _dialog.OnClose += () => _dialog = null;
        };

        AbortButton.AddStyleClass("Caution");
        AbortButton.OnPressed += _ => OnStop?.Invoke();

        UpdateActive();
        UpdatePrograms();
    }

    public void UpdateActive()
    {
        if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
            return;

        // UI must be in the inactive state by default, since this wont run when inactive at startup
        var active = _entMan.HasComponent<ActiveAutodocComponent>(_owner);
        if (active == _active)
            return;

        _active = active;

        CreateProgramButton.Disabled = active || _programCount >= comp.MaxPrograms;
        AbortButton.Disabled = !active;
        foreach (var button in Programs.Children)
        {
            ((Button) button).Disabled = active;
        }

        if (!active)
            return;

        // close windows that can only be open when inactive
        _dialog?.Close();
        _currentProgram?.Close();
    }

    private void UpdatePrograms()
    {
        if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
            return;

        var count = comp.Programs.Count;
        if (count == _programCount)
            return;

        _programCount = count;

        CreateProgramButton.Disabled = _active || _programCount >= comp.MaxPrograms;

        Programs.RemoveAllChildren();
        for (int i = 0; i < comp.Programs.Count; i++)
        {
            var button = new Button()
            {
                Text = comp.Programs[i].Title
            };
            var index = i;
            button.OnPressed += _ => OpenProgram(index);
            button.Disabled = _active;
            Programs.AddChild(button);
        }
    }

    private void OpenProgram(int index)
    {
        if (!_entMan.TryGetComponent<AutodocComponent>(_owner, out var comp))
            return;

        // no editing multiple programs at once
        if (_currentProgram is {} existing)
            existing.Close();

        var window = new AutodocProgramWindow(_owner, comp.Programs[index]);
        window.OnToggleSafety += () => OnToggleProgramSafety?.Invoke(index);
        window.OnRemoveProgram += () =>
        {
            OnRemoveProgram?.Invoke(index);
            Programs.RemoveChild(index);
        };
        window.OnAddStep += (step, stepIndex) => OnAddStep?.Invoke(index, step, stepIndex);
        window.OnRemoveStep += step => OnRemoveStep?.Invoke(index, step);
        window.OnStart += () =>
        {
            if (_active)
                return;

            OnStart?.Invoke(index);

            // predict it starting the program
            _entMan.EnsureComponent<ActiveAutodocComponent>(_owner);
        };
        window.OnClose += () => _currentProgram = null;
        _currentProgram = window;

        window.OpenCentered();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateActive();
        UpdatePrograms();
    }
}

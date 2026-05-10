using System.Windows.Input;

namespace SpaceMousePilot.ViewModels;

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();

    public void NotifyCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? _) => !_running && (canExecute?.Invoke() ?? true);

    public async void Execute(object? _)
    {
        if (!CanExecute(null))
            return;

        _running = true;

        NotifyCanExecuteChanged();

        try
        { 
            await execute(); 
        }
        finally
        {
            _running = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

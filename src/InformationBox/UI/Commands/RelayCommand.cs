using System;
using System.Windows.Input;

namespace InformationBox.UI.Commands;

/// <summary>
/// Lightweight relay command for binding.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// Initializes the command with the delegates to run.
    /// </summary>
    /// <param name="execute">Action invoked when the command executes.</param>
    /// <param name="canExecute">Optional predicate that enables/disables the command.</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute((T?)parameter);

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}

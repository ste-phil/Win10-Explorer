using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Explorer.Helper
{
    public class Command : ICommand
    {
        private Action<object> action;
        private Func<bool> canExecute;

        public event EventHandler CanExecuteChanged;

        public Command(Action<object> action, Func<bool> canExecute)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute();
        }

        public void Execute(object parameter)
        {
            action(parameter);
        }

        public void CanExceuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }

    public class GenericCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private Action<T> action;
        private Func<T, bool> canExecute;

        public GenericCommand(Action<T> action, Func<T, bool> canExecute)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            action((T) parameter);
        }

        public void CanExceuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }
}

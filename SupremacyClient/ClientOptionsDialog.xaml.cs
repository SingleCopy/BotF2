// ClientSettingsWindow.xaml.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System.Windows.Input;

namespace Supremacy.Client
{
    /// <summary>
    /// Interaction logic for ClientSettingsWindow.xaml
    /// </summary>
    public partial class ClientOptionsDialog
    {
        public ClientOptionsDialog()
        {
            InitializeComponent();

            InputBindings.Add(
                new KeyBinding(
                    GenericCommands.CancelCommand,
                    Key.Escape,
                    ModifierKeys.None));

            InputBindings.Add(
                new KeyBinding(
                    GenericCommands.AcceptCommand,
                    Key.Enter,
                    ModifierKeys.None));

            CommandBindings.Add(
                new CommandBinding(
                    GenericCommands.CancelCommand,
                    OnGenericCommandsCancelCommandExecuted));

            CommandBindings.Add(
                new CommandBinding(
                    GenericCommands.AcceptCommand,
                    OnGenericCommandsAcceptCommandExecuted));

            CommandBindings.Add(
                new CommandBinding(
                    GenericCommands.TracesSetAllCommand,
                    OnGenericCommandsTracesSetAllCommandExecuted));

            CommandBindings.Add(
                new CommandBinding(
                    GenericCommands.TracesSetSomeCommand,
                    OnGenericCommandsTracesSetSomeCommandExecuted));

            CommandBindings.Add(
                new CommandBinding(
                    GenericCommands.TracesSetNoneCommand,
                    OnGenericCommandsTracesSetNoneCommandExecuted));
        }

        private void OnGenericCommandsCancelCommandExecuted(object source, ExecutedRoutedEventArgs e)
        {
            ClientSettings.Current.Reload();
            Close();
        }

        private void OnGenericCommandsAcceptCommandExecuted(object source, ExecutedRoutedEventArgs e)
        {
            SaveChangesAndHide();
        }

        private void SaveChangesAndHide()
        {
            ClientSettings.Current.Save();
            Close();
        }

        private void OnGenericCommandsTracesSetAllCommandExecuted(object source, ExecutedRoutedEventArgs e)
        {
            ClientSettings.Current.TracesAudio = false;

            ClientSettings.Current.Save();
            ClientSettings.Current.Reload();
        }

        private void OnGenericCommandsTracesSetSomeCommandExecuted(object source, ExecutedRoutedEventArgs e)
        {
            ClientSettings.Current.TracesAudio = false;

            ClientSettings.Current.Save();
            ClientSettings.Current.Reload();
        }

        private void OnGenericCommandsTracesSetNoneCommandExecuted(object source, ExecutedRoutedEventArgs e)
        {
            ClientSettings.Current.TracesAudio = false;

            ClientSettings.Current.Save();
            ClientSettings.Current.Reload();
        }
    }
}
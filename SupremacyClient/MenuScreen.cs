// File:MenuScreen.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Windows.Input;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;
using Supremacy.Annotations;
using Supremacy.Client.Commands;
using Supremacy.Client.Events;
using NavigationCommands = Supremacy.Client.Commands.NavigationCommands;
using Supremacy.UI;
using Supremacy.Utility;

namespace Supremacy.Client
{
    public sealed class MenuScreen : PriorityGameScreen<object>
    {
        #region Static Members
        public static readonly RoutedCommand SinglePlayerCommand;
        public static readonly RoutedCommand MultiplayerCommand;
        public static readonly RoutedCommand ContinueCommand;
        public static readonly RoutedCommand OptionsCommand;
        public static readonly RoutedCommand TracesCommand;
        public static readonly RoutedCommand FakeCommand;
        public static readonly RoutedCommand CreditsCommand;
        public static readonly RoutedCommand LoadGameCommand;
        public static readonly RoutedCommand SaveGameCommand;
        public static readonly RoutedCommand RetireCommand;
        public static readonly RoutedCommand ExitCommand;
        private readonly Animation _Animation = new Animation();

        //_MenuAnimation method in the AsteroidView class

        static MenuScreen()
        {
            SaveGameCommand = new RoutedCommand("SaveGame", typeof(MenuScreen));
            SinglePlayerCommand = new RoutedCommand("SinglePlayer", typeof(MenuScreen));
            MultiplayerCommand = new RoutedCommand("Multiplayer", typeof(MenuScreen));
            ContinueCommand = new RoutedCommand("Continue", typeof(MenuScreen));
            OptionsCommand = new RoutedCommand("Options", typeof(MenuScreen));
            //TracesCommand = new RoutedCommand("Traces", typeof(MenuScreen));
            CreditsCommand = new RoutedCommand("Credits", typeof(MenuScreen));
            LoadGameCommand = new RoutedCommand("LoadGame", typeof(MenuScreen));

            RetireCommand = new RoutedCommand("Retire", typeof(MenuScreen));
            ExitCommand = new RoutedCommand("Exit", typeof(MenuScreen));
        }
        #endregion

        public AsteroidsView MenuAnimation { get; }

        public Animation Animation => _Animation;

        public MenuScreen([NotNull] IUnityContainer container) : base(container)
        {
            IsActiveChanged += OnIsActiveChanged;

            //this.IsActive = true;

            //ClientCommands.ShowSaveGameDialog.RegisterCommand(SaveGameCommand);

            _ = CommandBindings.Add(
                new CommandBinding(
                    LoadGameCommand,
                    delegate
                    {
                        NavigationCommands.ActivateScreen.Execute(StandardGameScreens.MenuScreen);
                        _ = ServiceLocator.Current.GetInstance<LoadGameDialog>().ShowDialog();
                    },
                    (s, e) => e.CanExecute = ClientCommands.LoadGame.CanExecute(null)));

            _ = CommandBindings.Add(
                new CommandBinding(
                    SaveGameCommand,
                    delegate
                    {
                        NavigationCommands.ActivateScreen.Execute(StandardGameScreens.MenuScreen);
                        _ = ServiceLocator.Current.GetInstance<SaveGameDialog>().ShowDialog();
                    },
                    (s, e) => e.CanExecute = ClientCommands.SaveGame.CanExecute(null)));

            _ = CommandBindings.Add(
                new CommandBinding(
                    MultiplayerCommand,
                    delegate
                    {
                        NavigationCommands.ActivateScreen.Execute(StandardGameScreens.MenuScreen);
                        ServiceLocator.Current.GetInstance<MultiplayerSetupScreen>().Show();
                    },
                    (s, e) => e.CanExecute = ClientCommands.JoinMultiplayerGame.CanExecute(null)));
            MenuAnimation = new AsteroidsView();

            GameLog.Client.UI.DebugFormat("MenuScreen initialized");

        }

        private void OnIsActiveChanged(object sender, EventArgs e)
        {
            if (!IsActive)
            {
                return;
            }

            ClientEvents.ScreenActivated.Publish(new ScreenActivatedEventArgs(StandardGameScreens.MenuScreen));
        }

        protected override void OnContextMenuOpening(System.Windows.Controls.ContextMenuEventArgs e)
        {
            if (IsVisible)
            {
                e.Handled = true;
                return;
            }
            base.OnContextMenuOpening(e);
        }
    }
}
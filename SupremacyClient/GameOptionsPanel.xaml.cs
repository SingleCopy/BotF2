// GameOptionsPanel.xaml.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Microsoft.Practices.ServiceLocation;
using Supremacy.Game;
using Supremacy.Types;
using Supremacy.Utility;
using System;
using System.Windows.Media.Imaging;

namespace Supremacy.Client
{
    /// <summary>
    /// Interaction logic for GameOptionsPanel.xaml
    /// </summary>
    public partial class GameOptionsPanel
    {
        #region Fields
        private GameOptions _options;
        #endregion

        #region Events
        public event DefaultEventHandler OptionsChanged;
        #endregion

        #region Constructors
        public GameOptionsPanel()
        {
            InitializeComponent();

            //PlayerNameSPInput.Text = "Choose your name";
            lstGalaxySize.ItemsSource = EnumHelper.GetValues<GalaxySize>();
            lstGalaxyShape.ItemsSource = EnumHelper.GetValues<GalaxyShape>();
            lstPlanetDensity.ItemsSource = EnumHelper.GetValues<PlanetDensity>();
            lstStarDensity.ItemsSource = EnumHelper.GetValues<StarDensity>();
            lstMinorRaces.ItemsSource = EnumHelper.GetValues<MinorRaceFrequency>();
            lstTechLevel.ItemsSource = EnumHelper.GetValues<StartingTechLevel>();
            //lstIntroPlayable.ItemsSource = EnumHelper.GetValues<IntroPlayable>();
            lstFederationPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstRomulanPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstKlingonPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstCardassianPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstDominionPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstBorgPlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();
            lstTerranEmpirePlayable.ItemsSource = EnumHelper.GetValues<EmpirePlayable>();

            //PlayerNameSPInput.SelectionChanged += (sender, args) => { OnOptionsChanged(); TrySetLastPlayerName(); };
            lstGalaxySize.SelectionChanged += (sender,args) => OnOptionsChanged();
            lstGalaxyShape.SelectionChanged += (sender, args) => { OnOptionsChanged(); UpdateGalaxyImage(); };
            lstPlanetDensity.SelectionChanged += (sender, args) => OnOptionsChanged();
            lstStarDensity.SelectionChanged += (sender, args) => OnOptionsChanged();
            lstMinorRaces.SelectionChanged += (sender, args) => OnOptionsChanged();
            lstTechLevel.SelectionChanged += (sender, args) => OnOptionsChanged();
            //lstIntroPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged(); };
            lstFederationPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged(); };
            lstRomulanPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged(); };
            lstKlingonPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged(); };
            lstCardassianPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged();  };
            lstDominionPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged();  };
            lstBorgPlayable.SelectionChanged += (sender, args) => { OnOptionsChanged();  };
            lstTerranEmpirePlayable.SelectionChanged += (sender, args) => { OnOptionsChanged(); };

            //try
            //{
            //    PlayerNameSPInput.Text = StorageManager.ReadSetting<string, string>("LastPlayerName");
            //}
            //catch { }

            Loaded += (sender, args) => UpdateGalaxyImage();

            Options = ServiceLocator.Current.GetInstance<GameOptions>();
        }
        #endregion

        #region Properties
        public GameOptions Options
        {
            get
            {
                if (_options == null)
                    _options = ServiceLocator.Current.GetInstance<GameOptions>();
                return _options;
            }
            set
            {
                _options = value;
                DataContext = _options;
                if (IsLoaded)
                    UpdateGalaxyImage();
            }
        }
        #endregion

        #region Methods
        private void OnOptionsChanged()
        {
            if (OptionsChanged != null)
                OptionsChanged();
        }

        private void UpdateGalaxyImage()
        {
            try
            {
                var imageSource = new BitmapImage(
                    new Uri(
                        "vfs:///Resources/Images/Galaxies/" + lstGalaxyShape.SelectedItem + ".png",
                        UriKind.Absolute));
                GalaxyImage.Source = imageSource;
            }
            catch (Exception e) //ToDo: how to handle this exception? Set to default "missing image"?
            {
                GameLog.LogException(e);
            }
        }

        void TryGetLastPlayerName(string PlayerNameSP)
        {
            try
            {
                //PlayerNameSPInput.Text = "R1D3";
                //PlayerNameSPInput.Text = StorageManager.ReadSetting<string, string>("LastPlayerName");
                //PlayerNameSP.CaretIndex = PlayerNameSP.Text.Length;
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }

        }

        void TrySetLastPlayerName()
        {
            try
            {
                // not finished yet

                //string playerName = PlayerNameSPInput.Text;
                //if (playerName.Length > 0)
                //{
                //    StorageManager.WriteSetting("LastPlayerName", playerName);
                //}
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }
        }

        private void UpdatePlayerNameSP()
        {
            try
            {
                TrySetLastPlayerName();
            }
            catch (Exception e) //ToDo: how to handle this exception? Set to default "missing image"?
            {
                GameLog.LogException(e);
            }
        }
        
        #endregion
    }
}
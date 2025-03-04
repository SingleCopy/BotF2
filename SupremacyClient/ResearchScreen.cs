// File:ResearchScreen.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Microsoft.Practices.Unity;

using Supremacy.Annotations;
using Supremacy.Client.Themes;
using Supremacy.Client.Views;
using Supremacy.Diplomacy;
using Supremacy.Economy;
using Supremacy.Encyclopedia;
using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Resources;
using Supremacy.Tech;
using Supremacy.Types;
using Supremacy.Utility;

namespace Supremacy.Client
{
    [TemplatePart(Name = "PART_ResearchFieldItemsHost", Type = typeof(Border))]
    [TemplatePart(Name = "PART_ResearchMatrixHost", Type = typeof(Border))]
    [TemplatePart(Name = "PART_ApplicationDetailsHost", Type = typeof(Border))]
    [TemplatePart(Name = "PART_EncyclopediaEntries", Type = typeof(TreeView))]
    [TemplatePart(Name = "PART_SearchText", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_EncyclopediaViewer", Type = typeof(FlowDocumentScrollViewer))]
    public sealed class ResearchScreen
        : GameScreen<ScienceScreenPresentationModel>, IScienceScreenView, IWeakEventListener
    {
        private Border _researchFieldItemsControl;
        private Border _researchMatrixHost;
        private Border _applicationDetailsHost;
        private readonly Grid _researchFieldGrid;
        private readonly Grid _researchMatrixGrid;
        private DependencyObject _selectedApplication;
        private TreeView _encyclopediaEntryListView;
        private TextBox _searchText;
        private FlowDocumentScrollViewer _encyclopediaViewer;

        public ResearchScreen([NotNull] IUnityContainer container) : base(container)
        {
            _researchFieldGrid = new Grid();
            _researchMatrixGrid = new Grid();
            _selectedApplication = null;

            LoadEncyclopediaEntries();

            SetValue(Grid.IsSharedSizeScopeProperty, true);


            if (ThemeHelper.TryLoadThemeResources(out ResourceDictionary themeResources))
            {
                Resources.MergedDictionaries.Add(themeResources);
            }
        }

        private void LoadEncyclopediaEntries()
        {
            int playerCivId = AppContext.LocalPlayer.EmpireID;
            Entities.Civilization playerCiv = AppContext.LocalPlayer.Empire;
            CivilizationManager civManager = GameContext.Current.CivilizationManagers[playerCivId];
            TechTree techTree = new TechTree();

            techTree.Merge(AppContext.LocalPlayerEmpire.TechTree);

            foreach (Entities.Civilization civ in GameContext.Current.Civilizations)
            {
                if (DiplomacyHelper.IsMember(civ, playerCiv))
                {
                    techTree.Merge(GameContext.Current.TechTrees[civ]);
                }
            }

            //IOrderedEnumerable<IGrouping<EncyclopediaCategory, IEncyclopediaEntry>> groups = (
            //                 from civ in GameContext.Current.Civilizations
            //                 let diplomacyStatus = DiplomacyHelper.GetForeignPowerStatus(playerCiv, civ)
            //                 where (diplomacyStatus != ForeignPowerStatus.NoContact) || (civ.CivID == playerCivId)
            //                 let raceEntry = civ.Race as IEncyclopediaEntry
            //                 where raceEntry != null
            //                 select raceEntry
            //             )
            IOrderedEnumerable<IGrouping<EncyclopediaCategory, IEncyclopediaEntry>> groups = (
                 from civ in GameContext.Current.Civilizations
                 let diplomacyStatus = DiplomacyHelper.GetForeignPowerStatus(playerCiv, civ)
                 where (diplomacyStatus != ForeignPowerStatus.NoContact) || (civ.CivID == playerCivId)
                 let raceEntry = civ.Race as IEncyclopediaEntry
                 where raceEntry != null
                 select raceEntry
             )
    .Concat(

                    from design in techTree
                    where TechTreeHelper.MeetsTechLevels(civManager, design)
                    let designEntry = design as IEncyclopediaEntry
                    where designEntry != null
                    select designEntry
                )
                .OrderBy(o => o.EncyclopediaHeading)
                .GroupBy(o => o.EncyclopediaCategory)
                .OrderBy(o => o.Key);

            Style groupStyle = new Style(
                typeof(TreeViewItem),
                Application.Current.FindResource(typeof(TreeViewItem)) as Style);
            Style itemStyle = new Style(
                typeof(TreeViewItem),
                Application.Current.FindResource(typeof(TreeViewItem)) as Style);

            groupStyle.Triggers.Add(
                new Trigger { Property = ItemsControl.HasItemsProperty, Value = false });
            ((Trigger)groupStyle.Triggers[0]).Setters.Add(
                new Setter(
                    VisibilityProperty,
                    Visibility.Collapsed));

            itemStyle.Setters.Add(
                new Setter(
                    ForegroundProperty,
                    new DynamicResourceExtension("DefaultTextBrush")));
            itemStyle.Setters.Add(
                new Setter(
                    HeaderedContentControl.HeaderProperty,
                    new Binding("EncyclopediaHeading")));

            groupStyle.Seal();
            itemStyle.Seal();

            if (_encyclopediaEntryListView == null)
            {
                return;
            }

            _encyclopediaEntryListView.Items.Clear();

            foreach (IGrouping<EncyclopediaCategory, IEncyclopediaEntry> group in groups)
            {
                TreeViewItem groupItem = new TreeViewItem();
                ICollectionView entriesView = CollectionViewSource.GetDefaultView(group);
                entriesView.Filter = FilterEncyclopediaEntry;
                groupItem.Style = groupStyle;
                groupItem.SetResourceReference(
                    ForegroundProperty,
                    "HeaderTextBrush");
                groupItem.Resources.Add(typeof(TreeViewItem), itemStyle);
                groupItem.Header = group.Key;
                groupItem.ItemsSource = entriesView;
                groupItem.IsExpanded = true;  // EncyclopediaGroups
                //groupItem.IsExpanded = false;
                _ = _encyclopediaEntryListView.Items.Add(groupItem);

                //_text = groupItem
                //Console.WriteLine(_text);
            }
        }

        private bool FilterEncyclopediaEntry(object value)
        {
            string searchText = string.Empty;

            if (!(value is IEncyclopediaEntry entry))
            {
                return false;
            }

            if (_searchText != null)
            {
                searchText = _searchText.Text.Trim();
            }

            if (searchText == string.Empty)
            {
                return true;
            }

            string[] words = searchText.Split(
                new[] { ' ', ',', ';' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                string lcWord = word.ToLowerInvariant();
                return entry.EncyclopediaHeading.ToLowerInvariant().Contains(lcWord)
                        || entry.EncyclopediaText.ToLowerInvariant().Contains(lcWord);
            }

            return false;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_researchMatrixHost != null)
            {
                BuildApplicationMatrix();
                _researchMatrixHost.Child = _researchMatrixGrid;
            }

            if (_researchFieldItemsControl == null)
            {
                return;
            }

            BuildResearchFields();
            _researchFieldItemsControl.Child = _researchFieldGrid;
        }

        private void BuildResearchFields()
        {
            int column = 0;
            ResearchPool pool = AppContext.LocalPlayerEmpire.Research;
            _researchFieldGrid.Children.Clear();
            _researchFieldGrid.ColumnDefinitions.Clear();
            foreach (ResearchField field in GameContext.Current.ResearchMatrix.Fields)
            {
                ResearchFieldData data = new ResearchFieldData(field, pool);
                ContentControl dataContainer = new ContentControl();

                _researchFieldGrid.ColumnDefinitions.Add(new ColumnDefinition());

                dataContainer.IsTabStop = false;
                dataContainer.Content = data;
                dataContainer.SetValue(Grid.ColumnProperty, column++);

                _ = _researchFieldGrid.Children.Add(dataContainer);
            }
        }

        private void BuildApplicationMatrix()
        {
            ResearchPool pool = AppContext.LocalPlayerEmpire.Research;
            _researchMatrixGrid.Children.Clear();
            _researchMatrixGrid.ColumnDefinitions.Clear();
            _researchMatrixGrid.RowDefinitions.Clear();
            foreach (ResearchField field in GameContext.Current.ResearchMatrix.Fields)
            {
                Grid internalGrid = null;
                int internalRow = 0;
                int row = -1;
                _researchMatrixGrid.ColumnDefinitions.Add(new ColumnDefinition());
                foreach (ResearchApplication application in field.Applications)
                {
                    ResearchApplicationData data = new ResearchApplicationData(application, pool);
                    ContentControl dataContainer = new ContentControl();
                    if (row != data.TechLevel)
                    {
                        if (internalGrid != null)
                        {
                            _ = _researchMatrixGrid.Children.Add(internalGrid);
                        }
                        row = data.TechLevel;
                        internalRow = 0;
                        if (row >= _researchMatrixGrid.RowDefinitions.Count)
                        {
                            _researchMatrixGrid.RowDefinitions.Add(new RowDefinition());
                            _researchMatrixGrid.RowDefinitions[row].Height = new GridLength(1.0, GridUnitType.Auto);
                        }
                        internalGrid = new Grid
                        {
                            VerticalAlignment = VerticalAlignment.Stretch
                        };
                        internalGrid.ColumnDefinitions.Add(new ColumnDefinition());
                        internalGrid.SetValue(Grid.ColumnProperty, field.FieldID);
                        internalGrid.SetValue(Grid.RowProperty, row);
                        internalGrid.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                    }
                    if (internalGrid != null)
                    {
                        internalGrid.RowDefinitions.Add(new RowDefinition());
                        internalGrid.RowDefinitions[internalRow].SharedSizeGroup = "MatrixRow";
                    }
                    dataContainer.IsTabStop = false;
                    dataContainer.Focusable = false;
                    dataContainer.Content = data;
                    dataContainer.SetValue(Grid.RowProperty, internalRow++);
                    dataContainer.MouseLeftButtonDown += ApplicationContainer_MouseLeftButtonDown;
                    if (internalGrid != null)
                    {
                        _ = internalGrid.Children.Add(dataContainer);
                    }
                }
                if (internalGrid != null)
                {
                    _ = _researchMatrixGrid.Children.Add(internalGrid);
                }
            }
        }

        private void ApplicationContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsEnabled)
            {
                if (_selectedApplication != null)
                {
                    _selectedApplication.SetValue(Selector.IsSelectedProperty, false);
                    _selectedApplication = null;
                }
                if (sender is ContentControl control)
                {
                    control.SetValue(Selector.IsSelectedProperty, true);
                    _selectedApplication = sender as DependencyObject;
                    if (_applicationDetailsHost != null)
                    {
                        ContentControl detailsContainer = new ContentControl
                        {
                            Content = new ResearchApplicationDetails(
                                                       ((ResearchApplicationData)
                                                        control.Content).Application,
                                                       AppContext.LocalPlayerEmpire)
                        };
                        _applicationDetailsHost.Child = detailsContainer;
                    }
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_encyclopediaEntryListView != null)
            {
                _encyclopediaEntryListView.SelectedItemChanged -=
                    EncyclopediaEntryListView_SelectedItemChanged;
            }
            if (_searchText != null)
            {
                _searchText.TextChanged -= OnSearchTextChanged;
            }

            _researchFieldItemsControl = GetTemplateChild("PART_ResearchFieldItemsHost") as Border;
            _researchMatrixHost = GetTemplateChild("PART_ResearchMatrixHost") as Border;
            _applicationDetailsHost = GetTemplateChild("PART_ApplicationDetailsHost") as Border;
            _encyclopediaEntryListView = GetTemplateChild("PART_EncyclopediaEntries") as TreeView;
            _searchText = GetTemplateChild("PART_SearchText") as TextBox;
            _encyclopediaViewer = GetTemplateChild("PART_EncyclopediaViewer") as FlowDocumentScrollViewer;

            if (_encyclopediaEntryListView != null)
            {
                _encyclopediaEntryListView.SelectedItemChanged +=
                    EncyclopediaEntryListView_SelectedItemChanged;
                LoadEncyclopediaEntries();
            }
            if (_encyclopediaViewer != null)
            {
                _encyclopediaViewer.Document = null;
            }
            if (_searchText != null)
            {
                _searchText.TextChanged += OnSearchTextChanged;
            }
        }

        private void EncyclopediaEntryListView_SelectedItemChanged(
            object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if ((_encyclopediaViewer != null)
                && (_encyclopediaEntryListView.SelectedItem != null)
                && (_encyclopediaEntryListView.SelectedItem is IEncyclopediaEntry entry))
            {
                _encyclopediaViewer.Document = GenerateEncyclopediaDocument(
                    entry);
            }
        }

        private FlowDocument GenerateEncyclopediaDocument(IEncyclopediaEntry entry)
        {
            if (entry == null)
            {
                return new FlowDocument();
            }

            TechObjectDesign design = entry as TechObjectDesign;
            FlowDocument doc = new FlowDocument();
            EncyclopediaImageConverter imageConverter = new EncyclopediaImageConverter();
            ResearchFieldImageConverter fiendImageConverter = new ResearchFieldImageConverter();


            //// Begin of Encyclopedia-HEADER
            Run headerRun = new Run(entry.EncyclopediaHeading);
            Paragraph headerBlock = new Paragraph(headerRun)
            {
                FontFamily = FindResource(ClientResources.DefaultFontFamilyKey) as FontFamily,
                FontSize = 16d * 96d / 72d,
                Foreground = FindResource(ClientResources.HeaderTextForegroundBrushKey) as Brush
            };

            if (entry.EncyclopediaCategory == EncyclopediaCategory.Races)
            {
                doc.Blocks.Add(headerBlock);
            }

            doc.FontFamily = FindResource(ClientResources.DefaultFontFamilyKey) as FontFamily;
            doc.FontSize = 12d * 96d / 72d;
            doc.Foreground = FindResource(ClientResources.DefaultTextForegroundBrushKey) as Brush;
            doc.TextAlignment = TextAlignment.Left;
            //// END of Encyclopedia-HEADER


            // Begin of Encyclopedia-IMAGE
            Border image = new Border();

            List<Paragraph> paragraphs = TextHelper.TrimParagraphs(entry.EncyclopediaText).Split(
                new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries).Select(o => new Paragraph(new Run(o))).ToList();

            if (entry.EncyclopediaCategory == EncyclopediaCategory.Races)
            {
                doc.Blocks.AddRange(paragraphs);
            }

            Paragraph firstParagraph = paragraphs.FirstOrDefault();
            if (firstParagraph == null)
            {
                firstParagraph = new Paragraph();
                doc.Blocks.Add(firstParagraph);
            }

            if (imageConverter.Convert(
                entry.EncyclopediaImage,
                typeof(BitmapImage),
                null,
                null) is BitmapImage imageSource)
            {
                //var imageWidth = imageSource.Width;
                //var imageHeight = imageSource.Height;

                //var imageRatio = imageWidth / imageHeight;
                //if (imageRatio >= 1.0)
                //{
                //    imageWidth = Math.Max(400, Math.Min(imageWidth, 576));
                //    imageHeight = imageWidth / imageRatio;
                //}
                //else
                //{
                //    imageHeight = Math.Max(400, Math.Min(imageHeight, 480));
                //    imageWidth = imageHeight * imageRatio;
                //}

                //image.Width = imageWidth;
                //image.Height = imageHeight;

                image.Width = 576;
                image.Height = 480;

                image.BorderBrush = Brushes.White;
                image.BorderThickness = new Thickness(0.0); // 0 = turned off
                image.CornerRadius = new CornerRadius(14.0);
                image.Background = new ImageBrush(imageSource) { Stretch = Stretch.UniformToFill };

                Thickness imageMargin = new Thickness(14, 0, 0, 14);
                Floater imageFloater = new Floater
                {
                    Blocks = { new BlockUIContainer(image) },
                    Margin = imageMargin,
                    Width = image.Width,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Padding = new Thickness(0)
                };

                if (firstParagraph.Inlines.Any())
                {
                    firstParagraph.Inlines.InsertBefore(firstParagraph.Inlines.First(), imageFloater);
                }
                else
                {
                    firstParagraph.Inlines.Add(imageFloater);
                }
            }
            // END of Encyclopedia-HEADER


            // Begin of Encyclopedia-PARAGRAPHS
            //doc.Blocks.AddRange(paragraphs);

            if (design != null)
            {
                ContentControl statsControl = new ContentControl
                {
                    Margin = new Thickness(0, 5, 0, 0),
                    Width = 300,  // old: 320
                    Content = new TechObjectDesignViewModel
                    {
                        Design = design,
                        Civilization = AppContext.LocalPlayer.Empire
                    },
                    Style = FindResource("TechObjectInfoPanelStyle") as Style
                };

                Paragraph statsBlock = new Paragraph(new InlineUIContainer(statsControl))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0)
                };

                //doc.Blocks.Add(statsBlock);

                Table techTable = new Table();
                techTable.RowGroups.Add(new TableRowGroup());
                techTable.RowGroups[0].Rows.Add(new TableRow());
                foreach (ResearchField field in GameContext.Current.ResearchMatrix.Fields)
                {
                    TechCategory techCategory = field.TechCategory;
                    TableColumn column = new TableColumn();
                    Border techIcon = new Border();
                    TextBlock techTextShadow = new TextBlock { Effect = new BlurEffect { Radius = 6 } };
                    TextBlock techText = new TextBlock();

                    if (design.TechRequirements[techCategory] < 1)
                    {
                        techIcon.Opacity = 0.25;
                    }

                    ImageBrush imageBrush = new ImageBrush(
                        fiendImageConverter.Convert(field, typeof(BitmapImage), null, null)
                        as ImageSource)
                    { Stretch = Stretch.Uniform };

                    techIcon.Width = 45;  // old 56
                    techIcon.Height = 36;  // old 45
                    techIcon.Padding = new Thickness(4);
                    techIcon.BorderBrush = Brushes.White;
                    techIcon.BorderThickness = new Thickness(2.0);
                    techIcon.CornerRadius = new CornerRadius(7.0);
                    techIcon.Background = imageBrush;

                    techTextShadow.Text = design.TechRequirements[techCategory].ToString();
                    techTextShadow.Foreground = Brushes.Black;
                    techTextShadow.SetResourceReference(TextBlock.FontFamilyProperty, ClientResources.DefaultFontFamilyKey);
                    techTextShadow.FontWeight = FontWeights.Bold;
                    techTextShadow.FontSize = 16 * (96d / 72d);
                    techTextShadow.HorizontalAlignment = HorizontalAlignment.Right;
                    techTextShadow.VerticalAlignment = VerticalAlignment.Bottom;

                    techText.Text = design.TechRequirements[techCategory].ToString();
                    techText.Foreground = Brushes.White;
                    techText.SetResourceReference(TextBlock.FontFamilyProperty, ClientResources.DefaultFontFamilyKey);
                    techText.FontWeight = FontWeights.Normal;
                    techText.FontSize = 16 * (96d / 72d);
                    techText.HorizontalAlignment = HorizontalAlignment.Right;
                    techText.VerticalAlignment = VerticalAlignment.Bottom;

                    techIcon.Child = new Grid { Children = { techTextShadow, techText } };
                    techIcon.ToolTip = string.Format(
                        "{0} Level {1}",
                        ResourceManager.GetString(field.Name),
                        design.TechRequirements[techCategory]);

                    techIcon.UseLayoutRounding = true;
                    techIcon.CacheMode = new BitmapCache { SnapsToDevicePixels = true };

                    _ = BindingOperations.SetBinding(
                        techIcon.CacheMode,
                        BitmapCache.RenderAtScaleProperty,
                        new Binding
                        {
                            Source = Application.Current.MainWindow,
                            Path = new PropertyPath(ClientProperties.ScaleFactorProperty),
                            Mode = BindingMode.OneWay
                        });

                    BlockUIContainer techIconContainer = new BlockUIContainer(techIcon);

                    techTable.Columns.Add(column);
                    techTable.RowGroups[0].Rows[0].Cells.Add(new TableCell(techIconContainer));
                }


                techTable.ClearFloaters = WrapDirection.Right;
                techTable.Margin = new Thickness(0, 10, 0, 0);  // old: 14 instead 10
                techTable.CellSpacing = 5.0;  // old: 7 instead 5

                //doc.Blocks.Add(techTable);  // Requirements Tech Level

                // Begin of Encyclopedia-HEADER
                //var headerRun = new Run(entry.EncyclopediaHeading);
                //var headerBlock = new Paragraph(headerRun)
                //{
                //    FontFamily = FindResource(ClientResources.DefaultFontFamilyKey) as FontFamily,
                //    FontSize = 16d * 96d / 72d,
                //    Foreground = FindResource(ClientResources.HeaderTextForegroundBrushKey) as Brush
                //};

                //doc.Blocks.Add(headerBlock);

                doc.FontFamily = FindResource(ClientResources.DefaultFontFamilyKey) as FontFamily;
                doc.FontSize = 12d * 96d / 72d;
                doc.Foreground = FindResource(ClientResources.DefaultTextForegroundBrushKey) as Brush;
                doc.TextAlignment = TextAlignment.Left;
                // END of Encyclopedia-HEADER

                doc.Blocks.AddRange(paragraphs);  // Description

                doc.Blocks.Add(statsBlock);  // Statistic data

                doc.Blocks.Add(techTable);  // Requirements Tech Level

                //doc.Foreground = FindResource(ClientResources.HeaderTextForegroundBrushKey) as Brush;
                //doc.TextAlignment = TextAlignment.Center;
                //doc.FontSize += 4;

                doc.Blocks.Add(headerBlock);
            }

            // END of Encyclopedia-PARAGRAPHS

            return doc;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                (Action)RefreshEncyclopediaEntries);
        }

        private void RefreshEncyclopediaEntries()
        {
            if (_encyclopediaEntryListView == null)
            {
                return;
            }

            IEnumerable<ICollectionView> groupViews = (from groupItem in _encyclopediaEntryListView.Items.OfType<TreeViewItem>()
                                                       select groupItem.ItemsSource).OfType<ICollectionView>();

            foreach (ICollectionView groupView in groupViews)
            {
                groupView.Refresh();
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size result = base.ArrangeOverride(arrangeBounds);
            foreach (ColumnDefinition column in _researchFieldGrid.ColumnDefinitions)
            {
                column.Width = new GridLength(
                    1.0 / _researchFieldGrid.ColumnDefinitions.Count
                    * _researchMatrixHost.ActualWidth,
                    GridUnitType.Pixel);
            }
            foreach (ColumnDefinition column in _researchMatrixGrid.ColumnDefinitions)
            {
                column.Width = new GridLength(
                    1.0 / _researchMatrixGrid.ColumnDefinitions.Count
                    * _researchMatrixHost.ActualWidth,
                    GridUnitType.Pixel);
            }
            return result;
        }

        public override void RefreshScreen()
        {
            base.RefreshScreen();
            if (_researchMatrixHost != null)
            {
                BuildApplicationMatrix();
            }
            if (_researchFieldItemsControl != null)
            {
                BuildResearchFields();
            }
            foreach (Distribution<int> distribution in
                AppContext.LocalPlayerEmpire.Research.Distributions.Children)
            {
                PropertyChangedEventManager.AddListener(
                    distribution,
                    this,
                    string.Empty);
            }
            if (_applicationDetailsHost != null)
            {
                _applicationDetailsHost.Child = null;
            }
            LoadEncyclopediaEntries();
        }

        public void SelectApplication(ResearchApplication application)
        {
            if (_researchMatrixGrid == null)
            {
                return;
            }

            FrameworkElement parent = _researchMatrixGrid.Parent as FrameworkElement;
            while ((parent != null)
                   && !(parent.Parent is Selector))
            {
                parent = parent.Parent as FrameworkElement;
            }
            if ((parent != null) && parent.IsDescendantOf(this))
            {
                parent.SetValue(Selector.IsSelectedProperty, true);
            }
            _researchMatrixGrid.BringIntoView();

            if (_selectedApplication != null)
            {
                _selectedApplication.SetValue(Selector.IsSelectedProperty, false);
                _selectedApplication = null;
            }
            if (_applicationDetailsHost != null)
            {
                if (application != null)
                {
                    foreach (Grid internalGrid in _researchMatrixGrid.Children)
                    {
                        foreach (ContentControl appContainer in internalGrid.Children)
                        {
                            if (((ResearchApplicationData)appContainer.Content).Application == application)
                            {
                                ContentControl detailsContainer = new ContentControl();

                                _selectedApplication = appContainer;
                                _selectedApplication.SetValue(Selector.IsSelectedProperty, true);

                                detailsContainer.Content = new ResearchApplicationDetails(
                                    application,
                                    AppContext.LocalPlayerEmpire);
                                _applicationDetailsHost.Child = detailsContainer;

                                break;
                            }
                        }
                    }
                }
                else
                {
                    _applicationDetailsHost.Child = null;
                }
            }
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            PlayerOrderService.AddOrder(
                new UpdateResearchOrder(
                    AppContext.LocalPlayerEmpire.Civilization));
            return true;
        }
    }

    public class ResearchFieldData
    {
        private readonly ResearchPool _pool;

        public ResearchField Field { get; }

        public Distribution<int> Distribution => _pool.Distributions[Field.FieldID];

        public int TechLevel => _pool.GetTechLevel(Field);

        public Percentage Progress => _pool.GetCurrentProject(Field).Progress.PercentFilled;

        public Percentage Bonus => _pool.Bonuses[Field.FieldID];

        public ResearchProject CurrentProject => _pool.GetCurrentProject(Field);

        public ResearchFieldData(ResearchField field, ResearchPool pool)
        {
            Field = field ?? throw new ArgumentNullException("field");
            _pool = pool ?? throw new ArgumentNullException("pool");
        }
    }

    public class ResearchApplicationData
    {
        private readonly ResearchPool _pool;

        //var civManager = GameContext.Current.CivilizationManagers[Owner];

        public string DisplayText
        {
            get
            {
                StringBuilder result = new StringBuilder(Application.Level + " > " + ResourceManager.GetString(Application.Name));
                if (IsResearching)
                {
                    _ = result.AppendFormat(
                        " ( {0:0%} )",
                        _pool.GetCurrentProject(Application.Field).Progress.PercentFilled);

                    // now in SitRep
                    //GameLog.Client.Research.DebugFormat("Turn {2}: {1} done to Research {0}", _application.Field.TechCategory.ToString()
                    //    , _pool.GetCurrentProject(_application.Field).Progress.PercentFilled, GameContext.Current.TurnNumber);
                    //civManager.SitRepEntries.Add(new ResearchStatusSitRepEntry(Owner, finishedApp, newDesigns))
                }
                return result.ToString();
            }
        }

        public string DisplayTextTooltip
        {
            get
            {
                StringBuilder result = new StringBuilder(
                    Application.Field.TechCategory
                    + " " + ResourceManager.GetString("LEVEL")
                    + " " + Application.Level
                    + " > " + Application.ResearchCost + " " + ResourceManager.GetString("POINTS")
                    + " " + ResourceManager.GetString("FOR")
                    + "  " + ResourceManager.GetString(Application.Name));


                if (IsResearching)
                {
                    _ = result.AppendFormat(
                        " ({0:0%})",
                        _pool.GetCurrentProject(Application.Field).Progress.PercentFilled);

                    // now in SitRep
                    //GameLog.Client.Research.DebugFormat("Turn {2}: {1} done to Research {0}", _application.Field.TechCategory.ToString()
                    //    , _pool.GetCurrentProject(_application.Field).Progress.PercentFilled, GameContext.Current.TurnNumber);
                    //civManager.SitRepEntries.Add(new ResearchStatusSitRepEntry(Owner, finishedApp, newDesigns))
                }
                return result.ToString();
            }
        }

        public ResearchApplication Application { get; }

        public bool IsResearched => _pool.IsResearched(Application);

        public bool IsResearching => _pool.IsResearching(Application);

        public int TechLevel => Application.Level;

        public ResearchApplicationData(ResearchApplication application, ResearchPool pool)
        {
            Application = application ?? throw new ArgumentNullException("application");
            _pool = pool ?? throw new ArgumentNullException("pool");
        }
    }

    public class ResearchApplicationDetails
    {
        private readonly CivilizationManager _civManager;

        public ResearchApplication Application { get; }

        public bool IsResearched => _civManager.Research.IsResearched(Application);

        public bool IsResearching => _civManager.Research.IsResearching(Application);

        public int TechLevel => Application.Level;

        public ICollection<TechObjectDesign> DependentBuildings
        {
            get
            {
                List<TechObjectDesign> results = new List<TechObjectDesign>();
                TechCategory techCategory = TechCategory.BioTech;
                foreach (ResearchField field in GameContext.Current.ResearchMatrix.Fields)
                {
                    if (field.Applications.Contains(Application))
                    {
                        techCategory = field.TechCategory;
                        break;
                    }
                }
                if (Application.Level > 0)
                {
                    foreach (ProductionFacilityDesign design in _civManager.TechTree.ProductionFacilityDesigns)
                    {
                        if ((design.TechRequirements[techCategory] == Application.Level)
                            && !results.Contains(design))
                        {
                            results.Add(design);
                        }
                    }
                    foreach (Buildings.BuildingDesign design in _civManager.TechTree.BuildingDesigns)
                    {
                        if ((design.TechRequirements[techCategory] == Application.Level)
                            && !results.Contains(design))
                        {
                            results.Add(design);
                        }
                    }
                    foreach (OrbitalBatteryDesign design in _civManager.TechTree.OrbitalBatteryDesigns)
                    {
                        if ((design.TechRequirements[techCategory] == Application.Level)
                            && !results.Contains(design))
                        {
                            results.Add(design);
                        }
                    }
                    foreach (ShipyardDesign design in _civManager.TechTree.ShipyardDesigns)
                    {
                        if ((design.TechRequirements[techCategory] == Application.Level)
                            && !results.Contains(design))
                        {
                            results.Add(design);
                        }
                    }
                    foreach (StationDesign design in _civManager.TechTree.StationDesigns)
                    {
                        if ((design.TechRequirements[techCategory] == Application.Level)
                            && !results.Contains(design))
                        {
                            results.Add(design);
                        }
                    }
                }
                return results;
            }
        }

        public ICollection<ShipDesign> DependentShips
        {
            get
            {
                List<ShipDesign> results = new List<ShipDesign>();
                TechCategory techCategory = TechCategory.BioTech;
                foreach (ResearchField field in GameContext.Current.ResearchMatrix.Fields)
                {
                    if (field.Applications.Contains(Application))
                    {
                        techCategory = field.TechCategory;
                        break;
                    }
                }
                foreach (ShipDesign design in _civManager.TechTree.ShipDesigns)
                {
                    if ((design.TechRequirements[techCategory] == Application.Level)
                        && (Application.Level > 0) && !results.Contains(design))
                    {
                        results.Add(design);
                    }
                }
                return results;
            }
        }

        public ResearchApplicationDetails(ResearchApplication application, CivilizationManager civManager)
        {
            Application = application ?? throw new ArgumentNullException("application");
            _civManager = civManager ?? throw new ArgumentNullException("civManager");
        }
    }
}

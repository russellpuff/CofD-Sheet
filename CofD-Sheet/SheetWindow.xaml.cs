﻿using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using StrEnum = System.Collections.Generic.IEnumerable<string>;

namespace CofD_Sheet
{
    public partial class SheetWindow : Window
    {
        //------------------------------------------------PRIVATE VARIABLES------------------------------------------------//
#nullable disable
        BitmapImage health_healthy, health_bashing, health_lethal, health_aggravated, blank_portrait,
            type_npc, type_mortal, type_ephemeral, type_vampire, type_mage, type_werewolf;
#nullable enable
        const string alpha = "abcdefghijklmnopqrstuvwxyz";
        string portraitPath = "none";
        readonly Dictionary<string, int> skillboxes = new();
        readonly List<DataItem> dataGridItems = new();
        readonly List<Character> allCharacters = new();
        Character characterToSave;
        bool unsaved = false, deactivateDangerousEvents = false; // On form load and clear, some events fuck shit sideways. 
        // For use with panning across beeg dpi or smol window. 
        bool isPanning = false;
        Point lastMousePosition = new();
        //-----------------------------------------------------------------------------------------------------------------//
        Grid grid;
        RowDefinitionCollection rows;
        #region form-load
        public SheetWindow()
        {
            InitializeComponent();
            InitializeImages();
            LoadSBDictionary();
            LoadNPCList();
            ClearForm(); // This triggers a few things necessary for a fresh, clean form. 
            characterToSave = new();
            unsaved = false;
            grid = swAbilitiesGrid;
            rows = swAbilitiesGrid.RowDefinitions;
        }
        
        private void InitializeImages()
        {
            health_healthy = new BitmapImage(new Uri("pack://application:,,,/Resources/healthy_transparent.png"));
            health_bashing = new BitmapImage(new Uri("pack://application:,,,/Resources/bashing_yellow.png"));
            health_lethal = new BitmapImage(new Uri("pack://application:,,,/Resources/lethal_yellow.png"));
            health_aggravated = new BitmapImage(new Uri("pack://application:,,,/Resources/aggravated_yellow.png"));
            blank_portrait = new BitmapImage(new Uri("pack://application:,,,/Resources/blank_portrait.png"));
            type_npc = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_npc.png"));
            type_mortal = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_mortal.png"));
            type_ephemeral = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_ephemeral.png"));
            type_vampire = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_vampire.png"));
            type_mage = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_mage.png"));
            type_werewolf = new BitmapImage(new Uri("pack://application:,,,/Resources/type_icon_werewolf.png"));
            swPortraitImage.Source = blank_portrait;
            foreach (UIElement c in swHealthStateGrid.Children)
            {
                if (c.GetType() == typeof(Image))
                {
                    Image im = (Image)c;
                    if (im.Name.Contains("HealthState")) { im.Source = health_healthy; }
                }
            }
        }

        private void LoadSBDictionary()
        {
            for (int i = 1; i <= 16; ++i) { skillboxes.Add($"swSkillComboBox{i}", 0); }
        }

        private void LoadNPCList()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "/Characters/";
            Directory.CreateDirectory(path);

            DirectoryInfo di = new(path);
            foreach (var file in di.GetFiles("*.chr"))
            {
                try
                {
                    Character n = DecompressFile(file);
                    allCharacters.Add(n);
                    dataGridItems.Add(new DataItem() 
                    { 
                        Name = n.Name, 
                        ID = n.ID.ToString(), 
                        Image = n.Type switch
                        {
                            TemplateType.NPC => type_npc,
                            TemplateType.Mortal => type_mortal,
                            TemplateType.Ephemeral => type_ephemeral,
                            TemplateType.Vampire => type_vampire,
                            TemplateType.Mage => type_mage,
                            TemplateType.Werewolf => type_werewolf,
                            _ => null
                        },
                        TypeToolTip = n.Type switch
                        {
                            TemplateType.NPC => "NPC",
                            TemplateType.Mortal => "Mortal",
                            TemplateType.Ephemeral => "Ephemeral",
                            TemplateType.Vampire => "Vampire",
                            TemplateType.Mage => "Mage",
                            TemplateType.Werewolf => "Werewolf",
                            _ => null
                        },
                        Visible = true
                    });
                }
                catch
                {
                    string m = "File " + file.Name + " was corrupted and could not be loaded.\n";
                    MessageBox.Show(m, "Corrupt file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            swCharactersDataGrid.ItemsSource = dataGridItems;
            SortDataGrid(swCharactersDataGrid);
        }
#nullable disable
        private static Character DecompressFile(FileInfo file)
        {
            using FileStream fs = new(file.FullName, FileMode.Open, FileAccess.Read);
            using MemoryStream ums = new();
            using (DeflateStream ds = new(fs, CompressionMode.Decompress)) { ds.CopyTo(ums); }
            ums.Position = 0;
            XmlSerializer xs = new(typeof(Character));
            return (Character)xs.Deserialize(ums);
#nullable enable
        }
        #endregion

        #region save-character
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        { SaveCharacter(); }

        private bool PromptToSaveChanges()
        { // True means the user handled their decision and wants to proceed. False means they canclled.
            const string m = "You have unsaved changes. Would you like to save them first?";
            var result = MessageBox.Show(m, "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel) { return false; }
            if (result == MessageBoxResult.Yes) { SaveCharacter(); }
            return true;
        }

        private bool SaveCharacter()
        {
            deactivateDangerousEvents = true;
            try
            {
                characterToSave.Name = swNameTextBox.Text;
                characterToSave.Age = swAgeNumUpDown.Value != null ? (int)swAgeNumUpDown.Value : 1;
                characterToSave.Virtue = swVirtueComboBox.Text;
                characterToSave.Vice = swViceComboBox.Text;
                characterToSave.Concept = swConceptTextBox.Text;
                characterToSave.HealthDots = (int)swHealthDotsTop.Value;
                characterToSave.WillpowerDots = (int)swWillpowerDotsTop.Value;
                characterToSave.Integrity = (int)swIntegrityDotsTop.Value;
                characterToSave.Size = swSizeNumUpDown.Value != null ? (int)swSizeNumUpDown.Value : 1;
                characterToSave.Defense = swDefenseNumUpDown.Value != null ? (int)swDefenseNumUpDown.Value : 0;
                characterToSave.Armor = swArmorTextBox.Text;
                characterToSave.Initiative = swInitiativeNumUpDown.Value != null ? (int)swInitiativeNumUpDown.Value : 0;
                characterToSave.Description = ""; //swDescriptionTextBox.Text;
                characterToSave.Intelligence = (int)swAttributeIntelligenceDotsTop.Value;
                characterToSave.Wits = (int)swAttributeWitsDotsTop.Value;
                characterToSave.Resolve = (int)swAttributeResolveDotsTop.Value;
                characterToSave.Strength = (int)swAttributeStrengthDotsTop.Value;
                characterToSave.Dexterity = (int)swAttributeDexterityDotsTop.Value;
                characterToSave.Stamina = (int)swAttributeStaminaDotsTop.Value;
                characterToSave.Presence = (int)swAttributePresenceDotsTop.Value;
                characterToSave.Manipulation = (int)swAttributeManipulationDotsTop.Value;
                characterToSave.Composure = (int)swAttributeComposureDotsTop.Value;
                characterToSave.Conditions = swConditionsTextBox.Text;
                characterToSave.Aspirations = swAspirationsTextBox.Text;
                characterToSave.WillpowerCurrent = 0;
                characterToSave.Type = (TemplateType)swTypeComboBox.SelectedIndex; // This SHOULD properly translate 1:1. 

                for (int i = 0; i < 16; ++i)
                {
                    // Skills
                    ComboBox c = (ComboBox)swMiddleGrid.FindName($"swSkillComboBox{i + 1}");
                    characterToSave.Skills[i] = c.Text;
                    RatingBar r = (RatingBar)swMiddleGrid.FindName($"swSkill{i + 1}DotsTop");
                    characterToSave.SkillDots[i] = (int)r.Value;
                    if (i < 10)
                    {
                        // Merits
                        TextBox t = (TextBox)swMiddleGrid.FindName($"swMeritTextBox{i + 1}");
                        characterToSave.Merits[i] = t.Text;
                        r = (RatingBar)swMiddleGrid.FindName($"swMerit{i + 1}DotsTop");
                        characterToSave.MeritDots[i] = (int)r.Value;

                        // Health states
                        Image im = (Image)swMiddleGrid.FindName($"swHealthState{i + 1}");
                        characterToSave.HealthStates[i] = GetHealthStateAsNumber(im);

                        // Willpower current
                        CheckBox x = (CheckBox)swMiddleGrid.FindName($"swWillpowerCheck{i + 1}");
                        if (x.IsChecked == true) { ++characterToSave.WillpowerCurrent; }
                    }
                }

                try
                {
                    if (swPortraitImage.Source != blank_portrait && !portraitPath.Contains("/Characters/"))
                    {
                        string[] oldpath = portraitPath.Split('.');
                        string newpath = AppDomain.CurrentDomain.BaseDirectory + "/Characters/" + characterToSave.ID + ".png";
                        File.Copy(portraitPath, newpath, true);
                    }
                } catch (IOException)
                {
                    string[] missingFile = portraitPath.Split('\\');
                    string m = "Error: cannot find " + missingFile[^1];
                    m += "\nSkipping portrait save process. Nothing else should be affected.";
                    MessageBox.Show(m, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                CompressFile();
                swSaveToggle.IsChecked = true;
                unsaved = false;
                // If in list, update. Otherwise, add to list.  
                if (!allCharacters.Any(n => n.ID == characterToSave.ID)) { 
                    allCharacters.Add(characterToSave);
                    dataGridItems.Add(new DataItem()
                    {
                        Name = characterToSave.Name,
                        ID = characterToSave.ID.ToString(),
                        Image = characterToSave.Type switch
                        {
                            TemplateType.NPC => type_npc,
                            TemplateType.Mortal => type_mortal,
                            TemplateType.Ephemeral => type_ephemeral,
                            TemplateType.Vampire => type_vampire,
                            TemplateType.Mage => type_mage,
                            TemplateType.Werewolf => type_werewolf,
                            _ => null
                        },
                        TypeToolTip = characterToSave.Type switch
                        {
                            TemplateType.NPC => "NPC",
                            TemplateType.Mortal => "Mortal",
                            TemplateType.Ephemeral => "Ephemeral",
                            TemplateType.Vampire => "Vampire",
                            TemplateType.Mage => "Mage",
                            TemplateType.Werewolf => "Werewolf",
                            _ => null
                        },
                        Visible = true
                    });
                } else
                {
                    int idx = dataGridItems.FindIndex(di => di.ID == characterToSave.ID);
                    dataGridItems[idx].Name = characterToSave.Name;
                }
                swCharacterSearchBox.Text = "";
                swCharactersDataGrid.Items.Refresh();
                SortDataGrid(swCharactersDataGrid);
            }
            catch (IOException ex)
            {
                string m = ex.Message;
                //string m = "There was an error trying to save this character.\n";
                //m += $"\n\nTry deleting  and then try again.";
                MessageBox.Show(m, "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            deactivateDangerousEvents = false;
            return true;
        }

        public static void SortDataGrid(DataGrid dataGrid, int columnIndex = 0, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = dataGrid.Columns[columnIndex];

            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            dataGrid.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            // Apply sort
            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }

        private void CompressFile()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "/Characters/" + characterToSave.ID.ToString() + ".chr";
            using MemoryStream ms = new();
            XmlSerializer xs = new(typeof(Character));
            xs.Serialize(ms, characterToSave);
            ms.Position = 0;
            using MemoryStream cms = new();
            using (DeflateStream ds = new(cms, CompressionMode.Compress, true)) { ms.CopyTo(ds); }
            FileStream fs = File.Create(path);
            cms.WriteTo(fs);
            fs.Close();
        }

        private int GetHealthStateAsNumber(Image im)
        {
            if (im.Source == health_healthy) { return 0; }
            if (im.Source == health_bashing) { return 1; }
            if (im.Source == health_lethal) { return 2; }
            return 3;
        }
        #endregion

        #region events
        private void SheetWindow_Closing(object sender, CancelEventArgs e) {
            if (unsaved)
            {
                var result = MessageBox.Show("Do you really want to exit?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (swCharacterFilterComboBox.SelectedIndex == -1) { return; }
            swCharacterSearchBox.Text = string.Empty;
            if (swCharacterFilterComboBox.SelectedIndex == 0) 
            { 
                foreach(var dataitem in dataGridItems) { dataitem.Visible = true; }
                swCharacterFilterComboBox.SelectedIndex = -1; 
                return; 
            }

            var targetType = swCharacterFilterComboBox.SelectedIndex switch
            {
                1 => type_npc,
                2 => type_mortal,
                3 => type_ephemeral,
                4 => type_vampire,
                5 => type_mage,
                6 => type_werewolf,
                _ => null
            };

            foreach (var dataitem in dataGridItems)
            {
                if (dataitem.Image == targetType) { dataitem.Visible = true; }
                else { dataitem.Visible = false; }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            string m = "New, amazing, fantastic, strange, and even dubious settings to be added uh, later.";
            MessageBox.Show(m, "", MessageBoxButton.OK);
        }

        private void Dots_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (deactivateDangerousEvents) { return; }
            SetUnsaved();
            RatingBar rb = (RatingBar)sender;
            string dotname = rb.Name;
            bool istop = false;

            if (dotname.Contains("Top"))
            {
                dotname = dotname.Replace("Top", "");
                dotname += "Bottom";
                istop = true;
            }
            else
            {
                dotname = dotname.Replace("Bottom", "");
                dotname += "Top";
            }

            RatingBar other = (RatingBar)((Grid)rb.Parent).FindName(dotname);
            // Bottom max switch to 9 and 10 again is to interrupt spinning animation.
            // I can't figure out how else to disable it.
#nullable disable
            var cv = new BrushConverter();
            Brush gold = (Brush)cv.ConvertFromString("#FFFFC107");
            // Prevent valuechanged event from firing lol.
            deactivateDangerousEvents = true;
            rb.Visibility = other.Visibility = Visibility.Visible; // Enable visibility if hidden. 
            if (istop)
            {
                rb.Max = (int)rb.Value;
                rb.Foreground = gold;
                other.Max = 9;
                other.Max = 10;
                other.Value = 0;
            }
            else
            {
                other.Max = (int)rb.Value;
                other.Value = rb.Value;
                other.Foreground = gold;
                rb.Max = 9;
                rb.Max = 10;
                rb.Value = 0;
            }
            deactivateDangerousEvents = false;
#nullable enable
        }

        private void SkillComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (deactivateDangerousEvents) { return; }
            ComboBox cb = (ComboBox)sender;
            ComboBoxItem? cbi = null;
#nullable disable // shut up shut up shut up shut up shut
            if (e.RemovedItems.Count > 0) { cbi = (ComboBoxItem)e.RemovedItems[0]; }
#nullable enable
            CBIterator(true, cb, cbi);
            CBIterator(false);
        }

        private void CBIterator(bool firstpass, ComboBox? original = null, ComboBoxItem? prevItem = null)
        { // If (firstpass), compare dupes. Otherwise update styles.
            foreach (Control c in swMiddleGrid.Children)
            {
                if (c.GetType() == typeof(ComboBox))
                {
                    ComboBox other = (ComboBox)c;
                    if (firstpass && original != null)
                    {
                        if (original == other) { continue; }
                        ComboBoxItem orig = (ComboBoxItem)original.SelectedItem;
                        ComboBoxItem oth = (ComboBoxItem)other.SelectedItem;

                        if (oth != null)
                        {
                            if (orig.Content.Equals(oth.Content))
                            {
                                ++skillboxes[original.Name];
                                ++skillboxes[other.Name];
                            }
                            if (prevItem != null && (String)oth.Content == (String)prevItem.Content)
                            {
                                --skillboxes[original.Name];
                                --skillboxes[other.Name];
                            }
                        }
                    } else
                    {
                        string style = (skillboxes[other.Name] > 0) ? "RedComboBox" : "DefaultComboBox";
                        other.Style = FindResource(style) as Style; 
                    }
                }
            }
        }

        private void HealthState_LeftClick(object sender, MouseButtonEventArgs e)
        {
            SetUnsaved();
            Image im = (Image)sender;
            if (im.Source == health_healthy) { im.Source = health_bashing; return; }
            if (im.Source == health_bashing) { im.Source = health_lethal; return; }
            if (im.Source == health_lethal) { im.Source = health_aggravated; return; }
            if (im.Source == health_aggravated) { im.Source = health_healthy; return; }
        }

#nullable disable
        private void Rectangle_MouseEnter(object sender, MouseEventArgs e) 
        {
            var cv = new BrushConverter();
            Brush b = (Brush)cv.ConvertFromString("#FFFFC107");
            try
            {
                System.Windows.Shapes.Rectangle rect = (System.Windows.Shapes.Rectangle)sender;
                rect.Stroke = b;
                return;
            }
            catch { }
            try
            {
                Image im = (Image)sender;
                swPortraitRectangle.Stroke = b;
            }
            catch
            {
                //swRollSectionRectangle.Stroke = b;
            }
        }

        private void Rectangle_MouseLeave(object sender, MouseEventArgs e) {
            var cv = new BrushConverter();
            Brush b = (Brush)cv.ConvertFromString("#FF707070");
            swPortraitRectangle.Stroke = b;
            //swRollSectionRectangle.Stroke = b;
        }
#nullable enable
        private void AddCharacterButton_Click(object? sender, RoutedEventArgs? e)
        { ClearForm(); }

        private void ClearForm()
        {
            if (unsaved && !PromptToSaveChanges()) { return; }
            deactivateDangerousEvents = true;
            IEnumerable<Image> images = GetChildren(swMajorGrid).OfType<Image>();
            foreach(Image image in images) { image.Source = health_healthy; }

            swPortraitImage.Source = blank_portrait;
            swCharacterSearchBox.Text = string.Empty;
            swCharacterFilterComboBox.SelectedIndex = -1;
            swCharactersDataGrid.SelectedIndex = -1;

            foreach (var x in swSheetGrid.Children)
            {
                if (x is Grid g) { ClearGridChildren(g); }
            }
            // Shitty patch fix to a problem where the icons vanish on form clear.
            // Despite this code not affecting it in any way. 
            SortDataGrid(swCharactersDataGrid);
            swTypeComboBox.SelectedIndex = 0; // This should fire the event to change the UI layout to NPC.

            characterToSave = new();
            deactivateDangerousEvents = false;
        }

        private static void ClearGridChildren(Grid g)
        {
            foreach (var c in g.Children)
            {
                if (c is TextBox tb)
                {
                    tb.Text = "";
                    continue;
                }
                if (c is ComboBox cb)
                {
                    cb.Text = "";
                    continue;
                }
                if (c is RatingBar rb)
                {
                    if (rb.Name.Contains("Top"))
                    {
                        rb.Max = rb.Name.Contains("Attribute") ? 1 : 0;
                        rb.Value = rb.Name.Contains("Attribute") ? 1 : 0;
                    }
                    continue;
                }
                if (c is NumericUpDown nd)
                {
                    nd.Value = 0;
                }
                if (c is Grid d)
                {
                    foreach (var t in d.Children)
                    {
                        if (t is CheckBox cx) { cx.IsChecked = false; }
                    }
                }
            }
        }

        private static IEnumerable<Visual> GetChildren(Visual parent, bool recurse = true)
        {
            if (parent != null)
            {
                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    // Retrieve child visual at specified index value.
                    if (VisualTreeHelper.GetChild(parent, i) is Visual child)
                    {
                        yield return child;

                        if (recurse)
                        {
                            foreach (var grandChild in GetChildren(child, true))
                            {
                                yield return grandChild;
                            }
                        }
                    }
                }
            }
        }

        private void Portrait_Click(object? sender, MouseButtonEventArgs? e)
        {
            if (e != null && e.ClickCount == 2)
            {
                OpenPortraitFile();
                SetUnsaved();
            }
        }

        private void HealthState_RightClick(object sender, MouseButtonEventArgs e)
        {
            Image im = (Image)sender;
            im.Source = health_healthy;
        }

        private void FullRollButton_Click(object sender, RoutedEventArgs e)
        {
            if (unsaved)
            {
                const string m = "NPC must be saved before Dice Roller can be used. Save now?";
                var result = MessageBox.Show(m, "Dice Roller", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes && SaveCharacter()) { } // Just a funky way of checking both at the same time.
                else { return; }
            }

            if (!ValidateSkillsMerits())
            {
                string m = "Detected skills or merits that have dots but no name, or vice versa.\n";
                m += "The die roller breaks if you have any skills or merits that break this rule. There is currently no fix besides ";
                m += "forcing you to manually change it.";
                MessageBox.Show(m, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DiceWindow dw = new(characterToSave.Skills, characterToSave.SkillDots, characterToSave.Merits, characterToSave.MeritDots, characterToSave.HealthDots,
                characterToSave.WillpowerDots, characterToSave.Integrity, characterToSave.Intelligence, characterToSave.Wits, characterToSave.Resolve,
                characterToSave.Strength, characterToSave.Dexterity, characterToSave.Stamina, characterToSave.Presence, characterToSave.Manipulation, characterToSave.Composure);
            dw.Show();
        }

        private bool ValidateSkillsMerits()
        {
            for (int i = 0; i < 16; ++i)
            {
                if (string.IsNullOrEmpty(characterToSave.Skills[i]) && (characterToSave.SkillDots[i] > 0)) { return false; }
                if (!string.IsNullOrEmpty(characterToSave.Skills[i]) && (characterToSave.SkillDots[i] == 0)) { return false; }
                if (i < 10)
                {
                    if (string.IsNullOrEmpty(characterToSave.Merits[i]) && (characterToSave.MeritDots[i] > 0)) { return false; }
                    if (!string.IsNullOrEmpty(characterToSave.Merits[i]) && (characterToSave.MeritDots[i] == 0)) { return false; }
                }
            }
            return true;
        }
#nullable disable
        // Event fires to warn user of unsaved changed (if applicable) before quick loading
        // next character. Gives them a chance to atone for their sins and cancel.
        private void DataGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //Get the item being clicked on.
            var myItem = (e.OriginalSource as FrameworkElement).DataContext as DataItem;
            //Check if item is different from currently selected item
            if (myItem != swCharactersDataGrid.SelectedItem)
            {
                if (unsaved && !PromptToSaveChanges()) { e.Handled = true; }
                else
                {
                    // Reselect selected item if saved, which flushes that.
                    swCharactersDataGrid.SelectedItem = myItem;
                    // Reinvoke event either way.
                    swCharactersDataGrid.Dispatcher.BeginInvoke(
                       new Action(() =>
                       {
                           RoutedEventArgs args = new MouseButtonEventArgs(e.MouseDevice, 0, e.ChangedButton)
                           { RoutedEvent = UIElement.MouseDownEvent };
                           (e.OriginalSource as UIElement).RaiseEvent(args);
                       }),
                       System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }


        // Sheet "quick load" function.
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (deactivateDangerousEvents) { return; }
            //deactivateDangerousEvents = true;

            int idx = swCharactersDataGrid.SelectedIndex;
            if (idx == -1) {
                deactivateDangerousEvents = false;
                return; 
            }
            DataItem di = (DataItem)swCharactersDataGrid.SelectedItems[0];
            characterToSave = allCharacters.Find(n => n.ID == di.ID);
            if (characterToSave == null) {
                deactivateDangerousEvents = false;
                return; 
            }
#nullable enable
            string path = AppDomain.CurrentDomain.BaseDirectory + "/Characters/" + characterToSave.ID + ".png";
            if (File.Exists(path)) { swPortraitImage.Source = LoadImage(path); } else
            {
                swPortraitImage.Source = blank_portrait;
                portraitPath = "none";
            }

            swNameTextBox.Text = characterToSave.Name;
            swAgeNumUpDown.Value = characterToSave.Age;
            swVirtueComboBox.Text = characterToSave.Virtue;
            swViceComboBox.Text = characterToSave.Vice;
            swConceptTextBox.Text = characterToSave.Concept;
            swHealthDotsTop.Max = characterToSave.HealthDots;
            swHealthDotsTop.Value = characterToSave.HealthDots;
            swWillpowerDotsTop.Max = characterToSave.WillpowerDots;
            swWillpowerDotsTop.Value = characterToSave.WillpowerDots;
            swIntegrityDotsTop.Max = characterToSave.Integrity;
            swIntegrityDotsTop.Value = characterToSave.Integrity;
            swSizeNumUpDown.Value = characterToSave.Size;
            swSpeedNumUpDown.Value = characterToSave.Speed;
            swDefenseNumUpDown.Value = characterToSave.Defense;
            swArmorTextBox.Text = characterToSave.Armor;
            swInitiativeNumUpDown.Value = characterToSave.Initiative;
            //swDescriptionTextBox.Text = characterToSave.Description;
            swAttributeIntelligenceDotsTop.Max = characterToSave.Intelligence;
            swAttributeIntelligenceDotsTop.Value = characterToSave.Intelligence;
            swAttributeWitsDotsTop.Max = characterToSave.Wits;
            swAttributeWitsDotsTop.Value = characterToSave.Wits;
            swAttributeResolveDotsTop.Max = characterToSave.Resolve;
            swAttributeResolveDotsTop.Value = characterToSave.Resolve;
            swAttributeStrengthDotsTop.Max = characterToSave.Strength;
            swAttributeStrengthDotsTop.Value = characterToSave.Strength;
            swAttributeDexterityDotsTop.Max = characterToSave.Dexterity;
            swAttributeDexterityDotsTop.Value = characterToSave.Dexterity;
            swAttributeStaminaDotsTop.Max = characterToSave.Stamina;
            swAttributeStaminaDotsTop.Value = characterToSave.Stamina;
            swAttributePresenceDotsTop.Max = characterToSave.Presence;
            swAttributePresenceDotsTop.Value = characterToSave.Presence;
            swAttributeManipulationDotsTop.Max = characterToSave.Manipulation;
            swAttributeManipulationDotsTop.Value = characterToSave.Manipulation;
            swAttributeComposureDotsTop.Max = characterToSave.Composure;
            swAttributeComposureDotsTop.Value = characterToSave.Composure;
            swConditionsTextBox.Text = characterToSave.Conditions;
            swAspirationsTextBox.Text = characterToSave.Aspirations;
            swTypeComboBox.SelectedIndex = (int)characterToSave.Type; // Should translate 1:1
            for (int i = 0; i < 16; ++i)
            {
                // Skills
                ComboBox c = (ComboBox)swMiddleGrid.FindName($"swSkillComboBox{i + 1}");
                c.Text = characterToSave.Skills[i];
                RatingBar r = (RatingBar)swMiddleGrid.FindName($"swSkill{i + 1}DotsTop");
                r.Max = characterToSave.SkillDots[i];
                r.Value = characterToSave.SkillDots[i];
                if (i < 10)
                {
                    // Merits
                    TextBox t = (TextBox)swMiddleGrid.FindName($"swMeritTextBox{i + 1}");
                    t.Text = characterToSave.Merits[i];
                    r = (RatingBar)swMiddleGrid.FindName($"swMerit{i + 1}DotsTop");
                    r.Max = characterToSave.MeritDots[i];
                    r.Value = characterToSave.MeritDots[i];

                    // Health states
                    Image im = (Image)swMiddleGrid.FindName($"swHealthState{i + 1}");
                    switch (characterToSave.HealthStates[i])
                    {
                        case 0: im.Source = health_healthy; break;
                        case 1: im.Source = health_bashing; break;
                        case 2: im.Source = health_lethal; break;
                        case 3: im.Source = health_aggravated; break;
                    }
                }
                for (int j = 1; j <= characterToSave.WillpowerCurrent; ++j)
                {
                    CheckBox x = (CheckBox)swMiddleGrid.FindName($"swWillpowerCheck{j}");
                    x.IsChecked = true;
                }
            }
            //deactivateDangerousEvents = false;
            unsaved = false;
            swSaveToggle.IsChecked = true;
        }

#nullable disable // Piss off and die. It's not fucking possible for these to be null.
        private void QuickRollButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            int die = (int)swQuickRollModNumUpDown.Value;
            bool rote = (bool)swRoteToggleButton.IsChecked;
            int again = 10;
            if ((bool)sw9AgainRadio.IsChecked) { again = 9; }
            if ((bool)sw9AgainRadio.IsChecked) { again = 8; }

            DieRoller dr = new(die, rote, again);
            dr.Roll();
            */
        }
#nullable enable

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        { DisplaySearchResults(); }

        private void OpenPortraitFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            { // I copied these filters from the internet. What the fuck is .jfif?
                FileName = "Image",
                DefaultExt = ".png",
                Filter = "All Images Files (*.png;*.jpeg;*.gif;*.jpg;*.bmp;*.tiff;*.tif)|" +
                "*.png;*.jpeg;*.gif;*.jpg;*.bmp;*.tiff;*.tif" +
            "|PNG Portable Network Graphics (*.png)|*.png" +
            "|JPEG File Interchange Format (*.jpg *.jpeg *jfif)|*.jpg;*.jpeg;*.jfif" +
            "|BMP Windows Bitmap (*.bmp)|*.bmp"
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                swPortraitImage.Source = LoadImage(dialog.FileName);
                SetUnsaved();
            }
        }

        private BitmapImage LoadImage(string path)
        {
            portraitPath = path;
            BitmapImage image = new();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            BitmapImage? ret = image;
            return ret;
        }

        // Event code for panning by pressing the middle mouse button.
        private void ScrollViewer_PreviewMouseDown(object sender, MouseEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                isPanning = true;
                lastMousePosition = e.GetPosition(swWindowScrollViewer);
                swWindowScrollViewer.CaptureMouse();
                Mouse.OverrideCursor = Cursors.ScrollAll; // Change to panning cursor. 
            }
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning && e.MiddleButton == MouseButtonState.Pressed)
            {
                var newMousePosition = e.GetPosition(swWindowScrollViewer);
                double delta_x = newMousePosition.X - lastMousePosition.X;
                double delta_y = newMousePosition.Y - lastMousePosition.Y;

                swWindowScrollViewer.ScrollToHorizontalOffset(
                    swWindowScrollViewer.HorizontalOffset - delta_x);
                swWindowScrollViewer.ScrollToVerticalOffset(
                    swWindowScrollViewer.VerticalOffset - delta_y);

                lastMousePosition = newMousePosition;
            }
        }

        private void ScrollViewer_PreviewMouseUp(object sender, MouseEventArgs e)
        {
            if(e.MiddleButton == MouseButtonState.Released)
            {
                isPanning = false;
                swWindowScrollViewer.ReleaseMouseCapture();
                Mouse.OverrideCursor = null;
            }
        }
        // End panning code.

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void NewAbilityButton_Click(object sender, RoutedEventArgs e)
        {
            unsaved = true;
            Button button = (Button)sender;
            int row_idx = Grid.GetRow(button); // Get current row of this button.
            button.IsEnabled = false; // Disable button until user accepts or cancels. 

            swAbilitiesGrid.Height += 260; // Make room for new row (40px) and editor (260px), default allotment is 50px for this row. 
            swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(260); // Change row height to make room for editor. 
            RowDefinition newButtonRow = new() { Height = new GridLength(40) };
            swAbilitiesGrid.RowDefinitions.Insert(row_idx + 1, newButtonRow); // Inject new row.
            Grid.SetRow(button, row_idx + 1); // Move button
            AssembleTemporaryAbilityControls(row_idx);
        }

        private void AssembleTemporaryAbilityControls(int row_idx, bool editing = false, string title = "", string desc = "")
        {
            // Create title box and add to grid.
            TextBox tempTitlebox = new()
            {
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Top
            };
            HintAssist.SetHint(tempTitlebox, "Ability Name");
            if (editing) { tempTitlebox.Text = title; }
            tempTitlebox.SetResourceReference(TextBox.StyleProperty, "MaterialDesignOutlinedTextBox");
            swAbilitiesGrid.Children.Add(tempTitlebox);
            Grid.SetRow(tempTitlebox, row_idx);

            // Create description box and add to grid.
            TextBox tempDescriptionBox = new()
            {
                FontSize = 14,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 64, 0, 30)
            };
            HintAssist.SetHint(tempDescriptionBox, "Ability Description");
            if (editing) { tempDescriptionBox.Text = desc; }
            tempDescriptionBox.SetResourceReference(StyleProperty, "MaterialDesignOutlinedTextBox");
            swAbilitiesGrid.Children.Add(tempDescriptionBox);
            Grid.SetRow(tempDescriptionBox, row_idx);

            // Create buttons and add to grid.
            Button tempAcceptButton = new()
            {
                Height = 24,
                Width = 50,
                Content = new PackIcon() { Kind = PackIconKind.Check },
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3, 3, 58, 3),
            };
            tempAcceptButton.Click += AcceptNewAbilityButton_Click; // Edit has no bearing on this event.
            tempAcceptButton.SetResourceReference(StyleProperty, "MaterialDesignFlatMidBgButton");
            swAbilitiesGrid.Children.Add(tempAcceptButton);
            Grid.SetRow(tempAcceptButton, row_idx);

            Button tempCancelButton = new()
            {
                Height = 24,
                Width = 50,
                Content = new PackIcon() { Kind = PackIconKind.Cancel },
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(57, 3, 3, 3)
            };
            if (editing) // Different event based on editing. 
            {   // Apply old title and desc to be restored on cancel edit.
                tempCancelButton.Click += (sender, e) =>
                { CancelEditAbilityButton_Click(sender, e, title, desc); };
            } 
            else { tempCancelButton.Click += CancelNewAbilityButton_Click; }
            tempCancelButton.SetResourceReference(StyleProperty, "MaterialDesignPaperButton");
            swAbilitiesGrid.Children.Add(tempCancelButton);
            Grid.SetRow(tempCancelButton, row_idx);
        }

        private void AcceptNewAbilityButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int row_idx = Grid.GetRow(button);
            // Get the textboxes. This returns a list with both boxes, the title in index 0 and description in 1.
            var textboxes = swAbilitiesGrid.Children
                .OfType<TextBox>()
                .Where(tb => Grid.GetRow(tb) == row_idx)
                .ToList();

            AssemblePermanentAbilityControls(textboxes[0].Text, textboxes[1].Text, row_idx);
            swNewAbilityButton.IsEnabled = true;
        }

        private void AssemblePermanentAbilityControls(string title, string desc, int row_idx)
        {
            // Modify grid to accept the expander.
            ClearTempAbilityGridControls(row_idx);
            swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(50);
            swAbilitiesGrid.Height -= 210; // Trim excess space.

            // Create expander.
            TrackedExpander tempExpander = new()
            {
                VerticalAlignment = VerticalAlignment.Top,
                Header = title,
                Foreground = Brushes.White,
                IsExpanded = false,
                Margin = new Thickness(0, 2, 0, 2)
            };
            tempExpander.SetResourceReference(StyleProperty, "GoldMaterialDesignExpander");
            Grid.SetRow(tempExpander, row_idx);
            tempExpander.Expanded += AbilityExpander_ExpandedOrCollapsed;
            tempExpander.Collapsed += AbilityExpander_ExpandedOrCollapsed;
            tempExpander.ActualHeightChanged += Expander_HeightChanged;

            // Create context menu for expander. 
            ContextMenu tempContextMenu = new();
            MenuItem menuEdit = new() { Header = "Edit" };
            menuEdit.Click += ExpanderEdit_OnClick;
            menuEdit.CommandParameter = tempExpander;
            MenuItem menuDelete = new() { Header = "Delete" };
            menuDelete.Click += (sender, e) => ExpanderDelete_OnClick(sender, e); // workaround to mismatched delegate
            menuDelete.CommandParameter = tempExpander;
            tempContextMenu.Items.Add(menuEdit);
            tempContextMenu.Items.Add(menuDelete);
            tempExpander.ContextMenu = tempContextMenu;

            // Create stackpanel child of expander.
            StackPanel tempStackPanel = new()
            {
                Margin = new Thickness(24, 8, 24, 16),
                Orientation = Orientation.Vertical
            };
            tempStackPanel.SetResourceReference(ForegroundProperty, "MaterialDesignBody");

            // Create textblock child of stackpanel.
            TextBlock tempTextBlock = new() { Text = desc };
            tempTextBlock.SetResourceReference(StyleProperty, "HorizontalExpanderContentTextBlock");

            // Assemble creation.
            tempStackPanel.Children.Add(tempTextBlock);
            tempExpander.Content = tempStackPanel;
            swAbilitiesGrid.Children.Add(tempExpander);
        }

        private void CancelNewAbilityButton_Click(object sender, RoutedEventArgs e)
        {
            // Modify grid back to the default.
            Button button = (Button)sender;
            int row_idx = Grid.GetRow(button);
            ClearTempAbilityGridControls(row_idx);
            swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(50);
            swAbilitiesGrid.Height -= 220;
            Grid.SetRow(swNewAbilityButton, row_idx); // Return button to its old spot.
            swAbilitiesGrid.RowDefinitions.RemoveAt(row_idx + 1); // Delete row allocated for button.
            swNewAbilityButton.IsEnabled = true;
        }

        private void CancelEditAbilityButton_Click(object sender, RoutedEventArgs e, string oldTitle, string oldDesc)
        {
            int row_idx = Grid.GetRow((Button)sender);
            AssemblePermanentAbilityControls(oldTitle, oldDesc, row_idx);
        }

        private void ClearTempAbilityGridControls(int row_idx)
        {
            var controlsToDelete = swAbilitiesGrid.Children
            .OfType<FrameworkElement>()
            .Where(element => Grid.GetRow(element) == row_idx)
            .ToList();

            foreach (var control in controlsToDelete)
            {
#nullable disable
                if (control is Button button)
                { // Unsubscribe from events. 
                    PackIcon pi = button.Content as PackIcon;
                    if (pi.Kind == PackIconKind.Check) { button.Click -= AcceptNewAbilityButton_Click; }
                    else { button.Click -= CancelNewAbilityButton_Click; }
                }
                swAbilitiesGrid.Children.Remove(control);
#nullable enable
            }
        }

        private void ExpanderEdit_OnClick(object sender, RoutedEventArgs e)
        {
#nullable disable
            MenuItem mi = (MenuItem)sender;
            TrackedExpander exp = mi.CommandParameter as TrackedExpander;
            exp.IsExpanded = false;
            int row_idx = Grid.GetRow(exp);

            swAbilitiesGrid.Height += 210; // Only increase large enough to fit editor (260px needed, have: 50px).  
            swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(260); // Change row height to make room for editor. 

            StackPanel stack = exp.Content as StackPanel;
            TextBlock block = stack.Children.OfType<TextBlock>().FirstOrDefault();
#nullable enable
            string title = (string)exp.Header;
            string desc = string.Empty;
            if (block is not null) { desc = block.Text; }
            ExpanderDelete_OnClick(sender, e, editing: true); // Run code to delete the expander. It will be reassembled later.
            AssembleTemporaryAbilityControls(row_idx, editing: true, title, desc);
        }

        private void ExpanderDelete_OnClick(object sender, RoutedEventArgs e, bool editing = false)
        {
            MenuItem mi = (MenuItem)sender;
#nullable disable
            TrackedExpander exp = mi.CommandParameter as TrackedExpander;
            int row_idx = Grid.GetRow(exp);
            MenuItem menuEdit = exp.ContextMenu.Items[0] as MenuItem;
            MenuItem menuDelete = exp.ContextMenu.Items[1] as MenuItem;
            // Unsubscribe from events.
            menuEdit.Click -= ExpanderEdit_OnClick;
            menuDelete.Click -= (sender, e) => ExpanderDelete_OnClick(sender, e);
            exp.ContextMenu = null;
            swAbilitiesGrid.Children.Remove(exp);
            if (!editing) // Don't delete row if editing. 
            {
                swAbilitiesGrid.RowDefinitions.RemoveAt(row_idx);
                // Shift remaining controls up one row. 
                for (int i = row_idx; i < swAbilitiesGrid.RowDefinitions.Count; i++)
                {
                    UIElement child = swAbilitiesGrid.Children.Cast<UIElement>()
                                            .FirstOrDefault(e => Grid.GetRow(e) == i);
                    if (child is not null) { Grid.SetRow(child, i - 1); }
                }
            }
#nullable enable
        }

        /*
         * These two events manage some important shit but they're a bit convoluted in how they work. This documentation explains. 
         * 
         * The problem: Dynamically added expanders are placed in grid rows, the grid rows need to adjust their height to allow
         * the expander to expand fully. However, it's not possible to know how big the expander is going to be upon firing the Expanded
         * event, nor is it possible to measure the expander or its text block when expanded in a grid row too small for it. 
         * The solution: create a custom expander control that tracks the ActualHeight value and fires an event when that value changes. 
         * On expand, extend the grid row to an arbitrarily large value (1000px) and force the grid to render its children,
         * when the expander renders properly, the ActualHeight value goes up to accompodate, firing the HeightChanged event. 
         * In this second event, the height is measured and the grid is readjusted to the correct height. 
         * When the expander is collapsed, reset the grid row height to 50px. 
         * There's some guards in place due to the HeightChanged event firing several times unintentionally, it'll fire multiple times as
         * the expander is first created (at ~half height and again at 50px), and it will fire 2+ times when adjusting the grid properly,
         * to prevent the grid from being shrunk too much, it checks to see if the grid will become smaller than its smallest possible value,
         * if so, the event is firing too much and it returns from subsequent runs. 
         */

        private void Expander_HeightChanged(object? sender, EventArgs e)
        {
            if(sender is null) { return; }
            TrackedExpander exp = (TrackedExpander)sender;
            if (exp.IsExpanded)
            {
                int row_idx = Grid.GetRow(exp);
                double height = exp.ActualHeight;
                if (height > 50)
                {
                    int desiredRowHeight = (int)Math.Ceiling(height) + 4; // Add some extra padding on the bottom, the textblock needs it.
                    int targetGridHeight = (int)(swAbilitiesGrid.Height - (1000 - desiredRowHeight));
                    if (targetGridHeight < 160) { return; } // Shitty patchfix LOL
                    swAbilitiesGrid.Height = targetGridHeight;
                    swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(desiredRowHeight);
                }
            }
        }

        private void AbilityExpander_ExpandedOrCollapsed(object sender, RoutedEventArgs e)
        { 
            TrackedExpander culprit = (TrackedExpander)sender;
            int row_idx = Grid.GetRow(culprit);
            if (culprit.IsExpanded)
            {
                swAbilitiesGrid.Height += 950;
                swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(1000);
                swAbilitiesGrid.UpdateLayout();
            }
            else
            {
                double targetHeight = swAbilitiesGrid.Height - 
                    (swAbilitiesGrid.RowDefinitions[row_idx].Height.Value - 50);
                if (targetHeight < 1) { return; }
                swAbilitiesGrid.Height = targetHeight;
                swAbilitiesGrid.RowDefinitions[row_idx].Height = new GridLength(50);
            }
        }
        #endregion

        #region context-menu
        private void Save_OnClick(object sender, RoutedEventArgs e) { _ = SaveCharacter(); }

        private void ChooseImage_OnClick(object sender, RoutedEventArgs e) {
            OpenPortraitFile();
            SetUnsaved();
        }

        private void RemoveImage_OnClick(object sender, RoutedEventArgs e)
        {
            swPortraitImage.Source = blank_portrait;
            portraitPath = "none";
        }

        private void CharactersDataGridDelete_OnClick(object sender, RoutedEventArgs e)
        {
            int idx = swCharactersDataGrid.SelectedIndex;
            if (idx == -1) { return; }
            string m = "Are you sure you want to delete " + characterToSave.Name + "?";
            var result = MessageBox.Show(m, "Delete NPC?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No) { return; }
            string id = characterToSave.ID;
            allCharacters.RemoveAll(x => x.ID == characterToSave.ID);
            dataGridItems.RemoveAll(x => x.ID == characterToSave.ID);
            swCharactersDataGrid.SelectedIndex = 0;
            string path = AppDomain.CurrentDomain.BaseDirectory + $"/Characters/{id}.chr";
            File.Delete(path);
            path = AppDomain.CurrentDomain.BaseDirectory + $"/Characters/{id}.png";
            if (File.Exists(path)) { File.Delete(path); }
            SortDataGrid(swCharactersDataGrid);
        }

        private void Clear_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            ContextMenu cm = (ContextMenu)mi.CommandParameter;
            if (cm.PlacementTarget is TextBox tb)
            {
                tb.Text = "";
            }
            if (cm.PlacementTarget is ComboBox cb)
            {
                deactivateDangerousEvents = true;
                cb.SelectedIndex = -1;
                deactivateDangerousEvents = false;
            }
            if (cm.PlacementTarget is RatingBar rb)
            {
                deactivateDangerousEvents = true;
                string dotname = rb.Name;
                bool istop = false;

                if (dotname.Contains("Top"))
                {
                    dotname = dotname.Replace("Top", "");
                    dotname += "Bottom";
                    istop = true;
                }
                else
                {
                    dotname = dotname.Replace("Bottom", "");
                    dotname += "Top";
                }
                RatingBar other = (RatingBar)((Grid)rb.Parent).FindName(dotname);

                if (istop)
                {
                    other.Max = 9;
                    other.Max = 10;
                    other.Value = 0;
                    rb.Max = rb.Name.Contains("Attribute") ? 1 : 0;
                    rb.Value = rb.Name.Contains("Attribute") ? 1 : 0;
                    rb.Visibility = rb.Name.Contains("Attribute") ? Visibility.Visible : Visibility.Hidden; // Hide top layer of dots to mask visual bug. 
                }
                else
                {
                    rb.Max = 9;
                    rb.Max = 10;
                    rb.Value = 0;
                    other.Max = other.Name.Contains("Attribute") ? 1 : 0;
                    other.Value = other.Name.Contains("Attribute") ? 1 : 0;
                    other.Visibility = rb.Name.Contains("Attribute") ? Visibility.Visible : Visibility.Hidden;
                }
                deactivateDangerousEvents = false;
            }
        }

        private void RollInit_OnClick(object sender, RoutedEventArgs e)
        {
            Random r = new();
            int d = r.Next(1, 10);
            int i = (int)(swAttributeDexterityDotsTop.Value + swAttributeComposureDotsTop.Value);
            string m = $"Rolled: {d}\nMod: {i}\nTotal: {d + i}";
            MessageBox.Show(m, "Initiative", MessageBoxButton.OK);
        }
        #endregion

        #region unsaved-jumble
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        { SetUnsaved();}

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        { SetUnsaved(); }

        private void NumUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        { SetUnsaved(); }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        { SetUnsaved(); }

        private void SetUnsaved()
        {
            unsaved = true;
            swSaveToggle.IsChecked = false;
        }
        #endregion

        #region Peter-Norvig-spellcheck
#nullable disable
        private void DisplaySearchResults()
        {
            try
            {
                string[] args = swCharacterSearchBox.Text.Split(' ');
                args[0] = args[0].ToLower();

                string items = "";
                foreach (DataItem d in dataGridItems) { items += d.Name + " "; }
                items = items.ToLower();

                var nWords = (from Match m in Regex.Matches(items, "[a-z]+")
                              group m.Value by m.Value)
                             .ToDictionary(gr => gr.Key, gr => gr.Count());

                static StrEnum nullIfEmpty(StrEnum c) => c.Any() ? c : null;

                StrEnum candidates;
                try
                {
                    candidates = nullIfEmpty(new[] { args[0] }.Where(nWords.ContainsKey))
                    ?? nullIfEmpty(Edits(args[0]).Where(nWords.ContainsKey))
                    ?? nullIfEmpty((from e1 in Edits(args[0])
                                    from e2 in Edits(e1)
                                    where nWords.ContainsKey(e2)
                                    select e2).Distinct());
                } catch
                {
                    candidates = nullIfEmpty(from p in nWords where p.Key.StartsWith(args[0]) select p.Key);
                }

                IOrderedEnumerable<string> suggestions = args.OrderBy(ar => ar.Length); // Nonsense to avoid exception.
                if (candidates != null)
                {
                    suggestions = (from cand in candidates
                                   orderby (nWords.ContainsKey(cand) ? nWords[cand] : 1)
                                   descending
                                   select cand);
                }
                foreach (DataItem d in dataGridItems)
                {
                    string[] names = d.Name.ToLower().Split(' ');
                    if (d.Name.ToLower().Contains(args[0]) || (candidates != null & suggestions.Intersect(names).Any()))
                    {
                        d.Visible = true;
                    }
                    else
                    {
                        d.Visible = false;
                    }
                }
            }
            catch // Deletion fucking dies if it has to check less than 1 character. 
            {
                foreach (DataItem d in dataGridItems) { d.Visible = true; }
            }
            //swDataGrid.Items.Refresh();
        }
#nullable enable
        static StrEnum Edits(string w)
        {
            // Deletion
            return (from i in Enumerable.Range(0, w.Length)
                    select string.Concat(w.AsSpan(0, i), w.AsSpan(i + 1)))
             // Transposition
             .Union(from i in Enumerable.Range(0, w.Length - 1)
                    select string.Concat(w.AsSpan(0, i), w.AsSpan(i + 1, 1), w.AsSpan(i, 1), w.AsSpan(i + 2)))
             // Alteration
             .Union(from i in Enumerable.Range(0, w.Length)
                    from c in alpha
                    select w[..i] + c + w[(i + 1)..])
             // Insertion
             .Union(from i in Enumerable.Range(0, w.Length + 1)
                    from c in alpha
                    select w[..i] + c + w[i..]);

        }
        #endregion
    }
}
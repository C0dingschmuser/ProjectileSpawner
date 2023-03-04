using System.Reflection;
using System.Runtime.InteropServices;
using Cosmoteer.Gui;
using Halfling;
using Cosmoteer;
using Cosmoteer.Simulation;
using Cosmoteer.Bullets;
using Halfling.Geometry;
using Halfling.Timing;
using Cosmoteer.Data;
using Cosmoteer.Input;
using Halfling.Input;
using System.Runtime.CompilerServices;
using Cosmoteer.Game;
using Cosmoteer.Ships;
using Halfling.Gui;
using Halfling.Application;
using ScrollBar = Halfling.Gui.ScrollBar;
using Label = Halfling.Gui.Label;
using AutoSizeMode = Halfling.Gui.AutoSizeMode;
using Halfling.Gui.Components.DataBinding;
using Cosmoteer.Ships.Fires;
using Halfling.Gui.Components.Toggles;
using Cosmoteer.Bullets.Hits;
using Cosmoteer.Simulation.HitEffects;
using Cosmoteer.Ships.Buffs;
using Cosmoteer.Game.Gui;
using Cosmoteer.Bullets.Death;

[assembly: IgnoresAccessChecksTo("Cosmoteer")]
[assembly: IgnoresAccessChecksTo("HalflingCore")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}

namespace ProjectileSpawner
{
    public enum FireMode
    {
        Place,
        Erase
    }

    public enum FireProperty
    {
        Damage,
        Spread,
        Kill
    }

    public static class Utils
    {
        public static string GetPercent(float input)
        {
            return String.Format("{0:P2}", input);
        }

        private static Widget CreateToggledCategory(string name, bool expanded, out LayoutBox box, out ToggleButton btn)
        {
            LayoutBox wrapBox = new LayoutBox();
            wrapBox.NineSlice.Flags = NineSliceFlags.None;
            wrapBox.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            wrapBox.Children.LayoutAlgorithm = LayoutAlgorithms.StretchTopToBottom;
            wrapBox.Children.WidgetPadding = new Vector2(10f, 10f);
            btn = new ToggleButton(ToggleButton.Style.Expander);
            btn.Text = name;
            btn.IsSelected = expanded;
            wrapBox.AddChild(btn);
            box = new LayoutBox(WidgetRules.Instance.CategoryBox);
            box.NineSlice.Flags = NineSliceFlags.None;
            box.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            box.Children.LayoutAlgorithm = LayoutAlgorithms.StretchTopToBottom;
            box.Children.WidgetPadding = new Vector2(10f, 10f);
            wrapBox.AddChild(box);
            btn.ComponentToggles[box] = new SelectionStateToggle(btn.SelectionController, onWhenSelected: true);
            return wrapBox;
        }

        public static LayoutBox[] CreateCategoryBox(LayoutBox<Widget, Widget> parent, string name, bool expanded = true, int fontSize = 14)
        {
            LayoutBox box = new LayoutBox();
            box.CopySettingsFrom(WidgetRules.Instance.HollowWidget);
            box.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            box.Children.BorderPadding = new Borders(10f);
            box.Children.WidgetPadding = new Vector2(10f, 10f);
            box.Children.LayoutAlgorithm = LayoutAlgorithms.StretchTopToBottom;
            parent.AddChild(box);
            box.AddChild(CreateToggledCategory(name, expanded, out LayoutBox ret, out ToggleButton btn));

            btn.StateNormalTextRenderer.FontSize = fontSize;
            btn.StateHighlightedTextRenderer.FontSize = fontSize;
            btn.StatePressedTextRenderer.FontSize = fontSize;
            btn.StateDisabledTextRenderer.FontSize = fontSize;

            LayoutBox[] values = { ret, box };

            return values;
        }

        public static TextEditField CreateEditLabel(LayoutBox<Widget, Widget> parent, string labelText = "", string editText = "", CharFilter? filter = null, float editWidth = 50, int fontSize = 14)
        {
            if(filter == null)
            {
                filter = CharFilters.SignedInteger;
            }

            LayoutBox box = new LayoutBox();
            box.Height = 28f;
            box.NineSlice.Flags = NineSliceFlags.None;
            box.Children.LayoutAlgorithm = LayoutAlgorithms.StretchLeftToRight;
            box.Children.BorderPadding = new Borders(0, 0f, 0f, 0f);
            box.Children.WidgetPadding = new Vector2(4, 0);

            parent.AddChild(box);

            TextEditField editField = new TextEditField(TextEditField.Style.SingleLine);

            editField.Width = editWidth;
            editField.Text = editText;
            editField.StateEnabledTextRenderer.FontSize = fontSize;
            editField.StateDisabledTextRenderer.FontSize = fontSize;
            editField.TextEditController.CharFilter = filter;
            editField.SelfInputActive = true;

            box.AddChild(editField);

            Label bulletDmgLabel = new Label();
            bulletDmgLabel.TextRenderer.HAlignment = Halfling.Graphics.Text.HAlignment.Left;
            bulletDmgLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            bulletDmgLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            bulletDmgLabel.AutoSize.Bounds = box;
            bulletDmgLabel.Text = labelText;
            bulletDmgLabel.TextRenderer.FontSize = fontSize;

            box.AddChild(bulletDmgLabel);

            return editField;
        }

        public static TextEditField[] CreateRangeInput(LayoutBox<Widget, Widget> parent, string labelText, string min, string max, CharFilter? filter = null, int fontSize = 14, float editWidth = 50)
        {
            if (filter == null)
            {
                filter = CharFilters.SignedInteger;
            }
            
            LayoutBox mainBox = new LayoutBox();
            mainBox.Height = 56f;
            mainBox.NineSlice.Flags = NineSliceFlags.None;
            mainBox.Children.LayoutAlgorithm = LayoutAlgorithms.StretchTopToBottom;
            mainBox.Children.BorderPadding = new Borders(0, 0f, 0f, 0f);

            parent.AddChild(mainBox);

            Label label = new Label();
            label.TextRenderer.HAlignment = Halfling.Graphics.Text.HAlignment.Left;
            label.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            label.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            label.AutoSize.Bounds = mainBox;
            label.Text = labelText;
            label.TextRenderer.FontSize = fontSize;

            mainBox.AddChild(label);

            LayoutBox box = new LayoutBox();
            box.Height = 28f;
            box.NineSlice.Flags = NineSliceFlags.None;
            box.Children.LayoutAlgorithm = LayoutAlgorithms.StretchLeftToRight;
            box.Children.BorderPadding = new Borders(0, 0f, 0f, 0f);
            box.Children.WidgetPadding = new Vector2(4, 0);

            mainBox.AddChild(box);

            TextEditField editField = new TextEditField(TextEditField.Style.SingleLine);

            editField.Width = editWidth;
            editField.Text = min;
            editField.StateEnabledTextRenderer.FontSize = fontSize;
            editField.StateDisabledTextRenderer.FontSize = fontSize;
            editField.TextEditController.CharFilter = filter;
            editField.SelfInputActive = true;

            box.AddChild(editField);

            Label label2 = new Label();
            label2.TextRenderer.HAlignment = Halfling.Graphics.Text.HAlignment.Left;
            label2.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            label2.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            label2.AutoSize.Bounds = box;
            label2.Text = "Min";
            label2.TextRenderer.FontSize = fontSize;

            box.AddChild(label2);

            TextEditField editField2 = new TextEditField(TextEditField.Style.SingleLine);

            editField2.Width = editWidth;
            editField2.Text = max;
            editField2.StateEnabledTextRenderer.FontSize = fontSize;
            editField2.StateDisabledTextRenderer.FontSize = fontSize;
            editField2.TextEditController.CharFilter = filter;
            editField2.SelfInputActive = true;

            box.AddChild(editField2);

            Label label3 = new Label();
            label3.TextRenderer.HAlignment = Halfling.Graphics.Text.HAlignment.Left;
            label3.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            label3.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            label3.AutoSize.Bounds = box;
            label3.Text = "Max";
            label3.TextRenderer.FontSize = fontSize;

            box.AddChild(label3);

            TextEditField[] values = { editField, editField2 };
            return values;
        }

        public static ToggleButton CreateCheckbox(LayoutBox<Widget, Widget> parent, string text, bool state, int fontSize = 14)
        {
            ToggleButton cb = new ToggleButton(ToggleButton.Style.Check);
            cb.Text = text;
            cb.StateNormalTextRenderer.FontSize = fontSize;
            cb.StateDisabledTextRenderer.FontSize = fontSize;
            cb.StateHighlightedTextRenderer.FontSize = fontSize;
            cb.StatePressedTextRenderer.FontSize = fontSize;
            cb.Height = 32f;
            cb.IsSelected = state;
            parent.AddChild(cb);

            return cb;
        }
    }

    public class Main
    {
        private static readonly List<FireMode> SELECTABLE_FIRE_MODES = new List<FireMode>
        {
            FireMode.Place,
            FireMode.Erase
        };

        private static List<WeaponsHolder> SELECTABLE_WEAPONS = new List<WeaponsHolder>();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int MessageBox(int hWnd, string text, string caption, uint type);

        public static Cosmoteer.Game.GameRoot? gameRoot;
        public static Cosmoteer.Simulation.SimRoot? simRoot;
        public static MethodInfo? HotkeyPressed;
        public static Keyboard? keyboard;
        public static WeaponsToolbox? weaponsToolBox;

        private static Label? shipLabel;

        private static WeaponsHolder? selectedWeapon;

        private static FireMode fireMode = FireMode.Place;
        private static FireRules? fireRules;

        private static Ship? currentShip;
        private static Ship? emptyShip;

        private static bool loaded = false;
        private static bool inBuilder = false;
        private static bool projectilesEnabled = true;
        private static bool firesEnabled = false;

        public static int bulletAmount = 1;
        public static int fireRadius = 1;

        private static List<WeaponCategoryHolder> weaponCategories = new List<WeaponCategoryHolder>();

        [UnmanagedCallersOnly]
        public static void InitializePatches()
        {
            keyboard = Halfling.App.Keyboard; 

            //bool mouseLeftPressed = Halfling.App.Mouse.LeftButton.WasPressed;

            Halfling.App.Director.FrameEnded += Worker;
        }

        public static void MsgBox(string text, string caption)
        {
            MessageBox(0, text, caption, 0);
        }

        public static void BuildEnter(object? sender, EventArgs e)
        {
            inBuilder = true;
        }

        public static void BuildLeave(object? sender, EventArgs e)
        {
            inBuilder = false;
        }

        public static void Worker(object? sender, EventArgs e)
        {
            if (Cosmoteer.GameApp.Instance == null) return;

            IAppState? currentState = App.Director.States.OfType<IAppState>().FirstOrDefault();

            if(currentState != null)
            {

                bool result = keyboard.HotkeyPressed(ViKey.K, true);

                if(Main.gameRoot != null)
                {
                    if (result && Main.gameRoot.p_showMap)
                    {
                        MsgBox(currentState.GetType().ToString(), "Test");
                    }
                }

                if (currentState.GetType() == typeof(TitleScreen))
                {

                }
            }

            GameRoot? game = App.Director.States.OfType<GameRoot>().FirstOrDefault();

            if(game != null && !loaded) 
            {
                gameRoot = game;
                simRoot = game.Sim;
                
                loaded = true;
                inBuilder = false;

                gameRoot.Gui.ShipGui.ToolboxOpened += BuildEnter;
                gameRoot.Gui.ShipGui.ToolboxClosed += BuildLeave;

                ShipRules shipRules = GameApp.Rules.Ships[0];
                FireRules preset = shipRules.Fire;

                fireRules = new FireRules();
                fireRules.DamagePerUpdate = preset.DamagePerUpdate;
                fireRules.SpreadChancePerUpdate = preset.SpreadChancePerUpdate;
                fireRules.KillChancePerUpdate = preset.KillChancePerUpdate;

                ShipRules emptyShipRules = GameApp.Rules.Ships[0];
                emptyShip = new Cosmoteer.Ships.Ship(emptyShipRules, "", -2);

                LoadWeapons();
                CreateUI();

            } else if(loaded)
            {
                if(game != Main.gameRoot)
                {
                    loaded = false;
                }
            }

            if(loaded && currentState.GetType() == typeof(GameRoot) && !Main.gameRoot.p_showMap && !inBuilder)
            {
                object[] parameters = new object[] { 23, true };

                bool result = Halfling.App.Mouse.LeftButton.WasPressed; //keyboard.HotkeyPressed(ViKey.L, true);

                if (result && projectilesEnabled && !Main.weaponsToolBox.IsMouseOver)
                {
                    if(bulletAmount == 1)
                    {
                        SpawnBullet(Main.simRoot.WorldMouseLoc);
                    } else
                    {
                        Vector2 loc = Main.simRoot.WorldMouseLoc;
                        Direction rot = Main.simRoot.Camera.Rotation;
                        float degrees = rot.ToDegrees();

                        float spread = 1f;
                        float startX = loc.X - spread / 2 - (((bulletAmount / 2) - 1) * spread);

                        for(int i = 0; i < bulletAmount; i++)
                        {
                            float x = startX + i * spread;
                            float y = loc.Y;

                            //rotate coordinate system by degrees
                            float xCoord = x * MathF.Cos(degrees) + y * MathF.Sin(degrees);
                            float yCoord = y * MathF.Cos(degrees) * x * MathF.Sin(degrees);

                            Vector2 pos = new Vector2(xCoord, yCoord);

                            SpawnBullet(pos);
                        }
                    }
                }

                result = keyboard.HotkeyPressed(ViKey.K, true);

                if (result && firesEnabled)
                {
                    Ship? ship = Main.simRoot.Ships.FindUnderPoint(Main.simRoot.WorldMouseLoc, null);
                    if (ship != null)
                    {
                        currentShip = ship;

                        fireRules = ship.Fires.Rules;

                        if(shipLabel != null)
                        {
                            shipLabel.Text = ship.Name;
                        }

                        IntVector2 loc = Ship.GetCellFromShipLoc(ship.TransformPointFromWorld(Main.simRoot.WorldMouseLoc));

                        if(fireRadius == 1)
                        {
                            if (fireMode == FireMode.Place)
                            {
                                SpawnFire(ship, loc);
                            }
                            else
                            {
                                if (ship.Fires.HasFireAt(loc))
                                {
                                    ship.Fires.RemoveFire(loc);
                                }
                            }
                        } else
                        {
                            IntVector2 start = new IntVector2(loc.X - fireRadius, loc.Y - fireRadius);

                            int max = fireRadius * 2;

                            for (int y = 0; y < max; y++)
                            {
                                for (int x = 0; x < max; x++)
                                {
                                    IntVector2 pos = new IntVector2(start.X + x, start.Y + y);

                                    if(fireMode == FireMode.Place)
                                    {
                                        SpawnFire(ship, pos);
                                    } else
                                    {
                                        if(ship.Fires.HasFireAt(pos))
                                        {
                                            ship.Fires.RemoveFire(pos);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if(keyboard.HotkeyPressed(ViKey.PlatformCmdCtrl, ViKey.K, true))
                {
                    Main.weaponsToolBox.SelfActive = !Main.weaponsToolBox.SelfActive;
                }
            }
        }

        public void SetFireProperty(FireProperty fireProperty, float value)
        {
            FireRules rules = fireRules;

            switch (fireProperty)
            {
                default:
                case FireProperty.Damage:
                    rules.DamagePerUpdate = (int)Math.Round(value);
                    break;
                case FireProperty.Spread:
                    rules.SpreadChancePerUpdate = value;
                    break;
                case FireProperty.Kill:
                    rules.KillChancePerUpdate = value;
                    break;
            }
        }

        public float GetFireProperty(FireProperty fireProperty)
        {
            FireRules rules = fireRules;

            switch(fireProperty)
            {
                default:
                case FireProperty.Damage:
                    return rules.DamagePerUpdate;
                    break;
                case FireProperty.Spread:
                    return rules.SpreadChancePerUpdate;
                    break;
                case FireProperty.Kill:
                    return rules.KillChancePerUpdate;
                    break;
            }
        }

        private static void SpawnBullet(Vector2 spawnLoc)
        {
            BulletRules rules = selectedWeapon.rules;

            Angle minSpread = 0; //rules.Spread.Min.GetValue(base.Part);
            Angle maxSpread = 0; //Rules.Spread.Max.GetValue(base.Part);
            Angle dirOffset = 0; //(Rules.EvenSpread ? ((Angle)((float)minSpread + burstProgress * ((float)maxSpread - (float)minSpread))) : base.Rand.Angle(minSpread, maxSpread));

            Direction rot = Main.simRoot.Camera.Rotation;
            float degrees = rot.ToDegrees();
            degrees -= 90;
            rot = Direction.FromDegrees(degrees);

            Direction worldSpawnDir = rot;
            ITarget? target = null;

            Vector2 empty = Vector2.Zero;

            BulletParams bp = new BulletParams(Main.simRoot, emptyShip, null, spawnLoc, target, null, spawnLoc, worldSpawnDir, empty, empty, Main.simRoot.LogicalTime);

            Bullet bullet = new Bullet(rules, bp, null);

            gameRoot?.Sim.Bullets.Add(bullet);
        }

        private static void SpawnFire(Ship ship, IntVector2 loc)
        {
            if (!ship.Fires.CanAddFireAt(loc)) return;

            FireManager fireManager = ship.Fires;

            fireManager.Rules.DamagePerUpdate = fireRules.DamagePerUpdate;
            fireManager.Rules.SpreadChancePerUpdate= fireRules.SpreadChancePerUpdate;
            fireManager.Rules.KillChancePerUpdate= fireRules.KillChancePerUpdate;

            Time lifetime = fireManager.Rand.Time(fireManager.Rules.FireExtinguishTime);
            int health = (int)Mathx.Ceiling((double)lifetime * (double)GameApp.Rules.Crew.CrewUpdatesPerSecond);
            Fire fire = new Fire(fireManager.Ship.Rules, loc, health, fireManager.Sim?.CurrentActionSourcePlayer ?? fireManager.Ship.Metadata.PlayerIndex);

            fireManager.AddFire(fire);
        }

        private static void LoadWeapons()
        {
            SELECTABLE_WEAPONS.Clear();

            foreach (KeyValuePair<Cosmoteer.Data.ID<BulletRules>, BulletRules> tmp in GameApp.Rules._bulletRulesById)
            {
                WeaponsHolder holder = new WeaponsHolder();
                holder.rules = tmp.Value;
                holder.ID = tmp.Key;

                SELECTABLE_WEAPONS.Add(holder);
            }

            selectedWeapon = SELECTABLE_WEAPONS[0];
        }

        private static void LoadWeaponVars(LayoutBox<Widget, Widget> parent)
        {
            foreach (WeaponCategoryHolder holder in weaponCategories)
            {
                holder.trueParent.Unparent();
            }

            weaponCategories.Clear();

            BulletRules rules = selectedWeapon.rules;
            foreach (var r in rules.Components)
            {
                WeaponCategoryHolder cat = new WeaponCategoryHolder();
                cat.rules = r;
                weaponCategories.Add(cat);
            }

            CreateWeaponUI(parent);
        }

        private static bool CheckRecursive(Type t)
        {
            if (t == typeof(HitRules) || t == typeof(MultiHitEffectRules) || t.IsSubclassOf(typeof(HitEffectRules)) || t == typeof(HitEffectRules) || 
                t.IsSubclassOf(typeof(BaseBulletDeathRules)) || t == typeof(BaseBulletDeathRules)) {
                return true;
            } else return false;
        }

        private static int RecursiveReadFields(Type t, object obj, LayoutBox<Widget, Widget> parent, List<WeaponDataHolder>? list = null)
        {
            if(list == null)
            {
                list = new List<WeaponDataHolder>();
            }

            int old = list.Count;

            FieldInfo[] fields = t.GetFields();
            foreach (FieldInfo fi in fields)
            {
                object? propValue = fi.GetValue(obj);

                if (propValue != null)
                {
                    if(fi.FieldType.IsArray)
                    {
                        LayoutBox newBox = Utils.CreateCategoryBox(parent, fi.Name, false)[0];
                        foreach(object o in (Array)propValue)
                        {
                            LayoutBox newBox2 = Utils.CreateCategoryBox(newBox, o.GetType().Name, false)[0];
                            RecursiveReadFields(o.GetType(), o, newBox2, list);
                        }
                    } else
                    {
                        if (fi.FieldType == typeof(bool) || fi.FieldType == typeof(bool?))
                        {
                            ToggleButton btn = Utils.CreateCheckbox(parent, fi.Name, (bool)propValue);

                            WeaponDataHolder wdh = new WeaponDataHolder();
                            wdh.parent = parent;
                            wdh.fInfo = fi;
                            wdh.toggle = btn;
                            wdh.obj = propValue;
                            list.Add(wdh);

                            btn.SelectionChanged += delegate
                            {
                                fi.SetValue(obj, btn.IsSelected);
                            };
                        }
                        else if (fi.FieldType == typeof(BuffableInt) || fi.FieldType == typeof(BuffableInt?))
                        {
                            BuffableInt realValue = (BuffableInt)propValue;
                            TextEditField editField =
                                Utils.CreateEditLabel(parent, fi.Name, realValue.BaseValue.ToString(), CharFilters.SignedInteger);

                            WeaponDataHolder wdh = new WeaponDataHolder();
                            wdh.parent = parent;
                            wdh.fInfo = fi;
                            wdh.edit = editField;
                            wdh.obj = propValue;
                            list.Add(wdh);

                            editField.TextChanged += delegate
                            {
                                bool ok = Int32.TryParse(editField.Text, out int newValue);
                                if (ok)
                                {
                                    realValue.BaseValue = newValue;
                                    fi.SetValue(obj, realValue);
                                }
                            };
                            editField.FocusController.Defocused += delegate
                            {
                                BuffableInt realValue = (BuffableInt)fi.GetValue(obj);
                                editField.Text = realValue.BaseValue.ToString();
                            };
                        }
                        else if (fi.FieldType == typeof(BuffableFloat) || fi.FieldType == typeof(BuffableFloat?))
                        {
                            BuffableFloat realValue = (BuffableFloat)propValue;
                            TextEditField editField =
                                Utils.CreateEditLabel(parent, fi.Name, realValue.BaseValue.ToString(), CharFilters.SignedDecimal);

                            WeaponDataHolder wdh = new WeaponDataHolder();
                            wdh.parent = parent;
                            wdh.fInfo = fi;
                            wdh.edit = editField;
                            wdh.obj = propValue;
                            list.Add(wdh);

                            editField.TextChanged += delegate
                            {
                                bool ok = float.TryParse(editField.Text, out float newValue);

                                if (ok)
                                {
                                    realValue.BaseValue = newValue;
                                    fi.SetValue(obj, realValue);
                                }
                            };
                            editField.FocusController.Defocused += delegate
                            {
                                BuffableFloat realValue = (BuffableFloat)fi.GetValue(obj);
                                editField.Text = realValue.BaseValue.ToString();
                            };
                        }
                        else if(fi.FieldType == typeof(Range<BuffableInt>) || fi.FieldType == typeof(Range<BuffableInt>?))
                        {
                            Range<BuffableInt> range = (Range<BuffableInt>)propValue;
                            TextEditField[] editFields = 
                                Utils.CreateRangeInput(parent, fi.Name, range.Min.BaseValue.ToString(), range.Max.BaseValue.ToString());

                            editFields[0].TextChanged += delegate
                            {
                                bool ok = Int32.TryParse(editFields[0].Text, out int newValue);
                                if(ok)
                                {
                                    range.Min.BaseValue = newValue;
                                    fi.SetValue(obj, range);
                                }
                            };
                            editFields[0].FocusController.Defocused += delegate
                            {
                                Range<BuffableInt> realValue = (Range<BuffableInt>)fi.GetValue(obj);
                                editFields[0].Text = realValue.Min.BaseValue.ToString();
                            };

                            editFields[1].TextChanged += delegate
                            {
                                bool ok = Int32.TryParse(editFields[1].Text, out int newValue);
                                if (ok)
                                {
                                    range.Max.BaseValue = newValue;
                                    fi.SetValue(obj, range);
                                }
                            };
                            editFields[1].FocusController.Defocused += delegate
                            {
                                Range<BuffableInt> realValue = (Range<BuffableInt>)fi.GetValue(obj);
                                editFields[1].Text = realValue.Max.BaseValue.ToString();
                            };

                            //WeaponDataHolder weaponDataHolder = new WeaponDataHolder();
                            //WeaponDataHolder weaponDataHolder2 = new WeaponDataHolder();
                        }
                        else if(fi.FieldType == typeof(Range<BuffableFloat>) || fi.FieldType == typeof(Range<BuffableFloat>?))
                        {
                            Range<BuffableFloat> range = (Range<BuffableFloat>)propValue;
                            TextEditField[] editFields =
                                Utils.CreateRangeInput(parent, fi.Name, range.Min.BaseValue.ToString(), range.Max.BaseValue.ToString(), CharFilters.SignedDecimal);

                            editFields[0].TextChanged += delegate
                            {
                                bool ok = float.TryParse(editFields[0].Text, out float newValue);
                                if (ok)
                                {
                                    range.Min.BaseValue = newValue;
                                    fi.SetValue(obj, range);
                                }
                            };
                            editFields[0].FocusController.Defocused += delegate
                            {
                                Range<BuffableFloat> realValue = (Range<BuffableFloat>)fi.GetValue(obj);
                                editFields[0].Text = realValue.Min.BaseValue.ToString();
                            };

                            editFields[1].TextChanged += delegate
                            {
                                bool ok = float.TryParse(editFields[1].Text, out float newValue);
                                if (ok)
                                {
                                    range.Max.BaseValue = newValue;
                                    fi.SetValue(obj, range);
                                }
                            };
                            editFields[1].FocusController.Defocused += delegate
                            {
                                Range<BuffableFloat> realValue = (Range<BuffableFloat>)fi.GetValue(obj);
                                editFields[1].Text = realValue.Max.BaseValue.ToString();
                            };
                        }
                        else if (CheckRecursive(fi.FieldType))
                        {
                            LayoutBox[] widgets = Utils.CreateCategoryBox(parent, fi.Name, false);
                            int count = RecursiveReadFields(fi.FieldType, propValue, widgets[0], list);

                            if (count == 0)
                            {
                                widgets[1].Unparent();
                            }
                        }
                    }
                }
            }

            int result = list.Count - old;
            return result;
        }

        private static void CreateWeaponUI(LayoutBox<Widget, Widget> parent)
        {
            foreach(WeaponCategoryHolder holder in weaponCategories)
            {
                LayoutBox[] widgets = Utils.CreateCategoryBox(parent, holder.rules.GetType().ToString(), false);

                LayoutBox newCatBox = widgets[0];
                holder.parent = parent;
                holder.trueParent = widgets[1];
                holder.box = newCatBox;

                Type t = holder.rules.GetType();
                RecursiveReadFields(t, holder.rules, newCatBox);
            }
        }

        private static void CreateUI(bool first = true)
        {
            WeaponsToolbox weaponsToolBox = new WeaponsToolbox(Main.gameRoot);
            weaponsToolBox.SelfActive = false;
            weaponsToolBox.Rect = new Rect(10f, 70f, 450f, 500f);
            weaponsToolBox.ResizeController.MinSize = new Vector2(450f, 500f);
            weaponsToolBox.ResizeController.MaxSize = new Vector2(700f, 1024f);

            LayoutBox weaponsBox = Utils.CreateCategoryBox(weaponsToolBox, "Weapons", true)[0];

            DropList weaponsList = new DropList();
            weaponsList.Text = SELECTABLE_WEAPONS[0].rules.ID.ToString();
            weaponsList.StateNormalTextRenderer.FontSize = 14;
            weaponsList.StateDisabledTextRenderer.FontSize = 14;
            weaponsList.StateHighlightedTextRenderer.FontSize = 14;
            weaponsList.StatePressedTextRenderer.FontSize = 14;

            weaponsBox.AddChild(weaponsList);

            ToggleButton enableProjectileButton = Utils.CreateCheckbox(weaponsBox, "Enable (Spawn with mouse click)", true);
            enableProjectileButton.SelectionChanged += delegate
            {
                projectilesEnabled = enableProjectileButton.IsSelected;
            };

            /*TextEditField bulletAmountEdit = Utils.CreateEditLabel(weaponsBox, "Amount of Bullets", bulletAmount.ToString(), CharFilters.UnsignedDecimal);
            bulletAmountEdit.TextChanged += delegate
            {
                bool ok = Int32.TryParse(bulletAmountEdit.Text, out int newValue);
                if (ok && newValue > 0)
                {
                    bulletAmount = newValue;
                }
            };*/

            LayoutBox advancedOptionsBox = Utils.CreateCategoryBox(weaponsBox, "Advanced Options", false)[0];

            DataBinder<SelectableButton, WeaponsHolder> weaponBinding = weaponsList.ListBox.Children.BindToData(SELECTABLE_WEAPONS, delegate (WeaponsHolder holder)
            {
                ListItem listItem = new ListItem();
                listItem.Text = holder.rules.ID.ToString();
                return listItem;
            });
            weaponsList.Clicked += delegate
            {
                projectilesEnabled = false;
            };
            weaponsList.ListBox.SelectionManager.WidgetSelected += delegate
            {
                selectedWeapon = weaponBinding.GetDataForWidget(weaponsList.SelectedWidget);
                projectilesEnabled = false;
                enableProjectileButton.IsSelected = false;
                LoadWeaponVars(advancedOptionsBox);
            };

            #region FireSettings

            LayoutBox fireBox = Utils.CreateCategoryBox(weaponsToolBox, "Fire Settings", false)[0];

            DropList fireModeList = new DropList();
            fireModeList.Text = "Spawn";
            fireModeList.StateNormalTextRenderer.FontSize = 14;
            fireModeList.StateDisabledTextRenderer.FontSize = 14;
            fireModeList.StateHighlightedTextRenderer.FontSize = 14;
            fireModeList.StatePressedTextRenderer.FontSize = 14;
            DataBinder<SelectableButton, FireMode> fireModeBinding = fireModeList.ListBox.Children.BindToData(SELECTABLE_FIRE_MODES, delegate (FireMode mode)
            {
                ListItem listItem = new ListItem();
                switch (mode)
                {
                    case FireMode.Place:
                        listItem.Text = "Spawn";
                        break;
                    case FireMode.Erase:
                        listItem.Text = "Erase";
                        break;
                }
                return listItem;
            });
            fireModeList.ListBox.SelectionManager.WidgetSelected += delegate
            {
                fireMode = fireModeBinding.GetDataForWidget(fireModeList.SelectedWidget);
            };
            fireBox.AddChild(fireModeList);

            //Enabled
            ToggleButton enableFireButton = Utils.CreateCheckbox(fireBox, "Enable (Spawn with K Key)", false);
            enableFireButton.SelectionChanged += delegate
            {
                firesEnabled = enableFireButton.IsSelected;
            };

            //Radius

            TextEditField radiusUpdate = Utils.CreateEditLabel(fireBox, " Spawn radius", fireRadius.ToString());
            radiusUpdate.TextChanged += delegate
            {
                Int32.TryParse(radiusUpdate.Text, out int newRadius);

                if(newRadius == 0)
                {
                    newRadius = 1;
                }

                fireRadius = newRadius;
            };
            radiusUpdate.FocusController.Defocused += delegate
            {
                radiusUpdate.Text = fireRadius.ToString();
            };

            //info

            Label infoLabel = new Label();
            infoLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            infoLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            infoLabel.Text = "--- Ship fire settings ---";
            infoLabel.TextRenderer.FontSize = 14;
            fireBox.AddChild(infoLabel);

            Label shipLabel = new Label();
            shipLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            shipLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            shipLabel.Text = "Ship: None";
            shipLabel.TextRenderer.FontSize = 14;
            fireBox.AddChild(shipLabel);
            Main.shipLabel = shipLabel;

            //Dmg

            TextEditField dmgUpdate = Utils.CreateEditLabel(fireBox, " Damage per Update (pU)", fireRules.DamagePerUpdate.ToString());
            dmgUpdate.TextChanged += delegate
            {
                Int32.TryParse(dmgUpdate.Text, out int newDmg);
                fireRules.DamagePerUpdate = newDmg;
            };
            dmgUpdate.FocusController.Defocused += delegate
            {
                dmgUpdate.Text = fireRules.DamagePerUpdate.ToString();
            };

            //Spread chance

            Label spreadLabel = new Label();
            spreadLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            spreadLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            spreadLabel.Text = "Spread chance pU: " + Utils.GetPercent(fireRules.SpreadChancePerUpdate);
            spreadLabel.TextRenderer.FontSize = 14;
            fireBox.AddChild(spreadLabel);

            ScrollBar spreadSlider = new ScrollBar(WidgetRules.Instance.HorizontalSlider);
            spreadSlider.MinValue = 0;
            spreadSlider.MaxValue = 1;
            spreadSlider.PageSize = 0.1f;
            spreadSlider.IncrementAmount = 0.1f;
            spreadSlider.Value = fireRules.SpreadChancePerUpdate;

            fireBox.AddChild(spreadSlider);

            spreadSlider.ValueChanged += delegate
            {
                fireRules.SpreadChancePerUpdate = spreadSlider.Value;
                spreadLabel.Text = "Spread chance pU: " + Utils.GetPercent(fireRules.SpreadChancePerUpdate);
            };

            //Kill chance

            Label killLabel = new Label();
            killLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            killLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            killLabel.Text = "Kill chance pU: " + Utils.GetPercent(fireRules.KillChancePerUpdate);
            killLabel.TextRenderer.FontSize = 14;
            fireBox.AddChild(killLabel);

            ScrollBar killSlider = new ScrollBar(WidgetRules.Instance.HorizontalSlider);
            killSlider.MinValue = 0;
            killSlider.MaxValue = 1;
            killSlider.PageSize = 0.1f;
            killSlider.IncrementAmount = 0.1f;
            killSlider.Value = fireRules.KillChancePerUpdate;

            fireBox.AddChild(killSlider);

            killSlider.ValueChanged += delegate
            {
                fireRules.KillChancePerUpdate = killSlider.Value;
                killLabel.Text = "Kill chance pU: " + Utils.GetPercent(fireRules.KillChancePerUpdate);
            };

            #endregion

            gameRoot.Gui.FloatingWindows.AddChild(weaponsToolBox);

            LoadWeaponVars(advancedOptionsBox);

            Main.weaponsToolBox = weaponsToolBox;
            weaponsToolBox.SelfActive = true;
        }
    }

    public class WeaponsToolbox : WindowBox
    {
        public WeaponsToolbox(GameRoot game)
        {
            //base.TitleTextProvider = Strings.KeyString("CreativeMode/Doodads");
            base.TitleText = "Projectiles";
            base.BoundsProvider = game.Gui.RootWidget;
            base.Children.LayoutAlgorithm = LayoutAlgorithms.StretchTopToBottom;
            base.Children.BorderPadding = new Borders(10f);
            base.Children.WidgetPadding = new Vector2(10f, 10f);

            //Halfling.Gui.

            //game.MapShown += OnCloseClicked;
        }
    }

    public class WeaponsHolder
    {
        public BulletRules rules;
        public ID<BulletRules> ID;
    }

    public class WeaponCategoryHolder
    {
        public BulletComponentRules? rules;
        public LayoutBox<Widget, Widget>? parent;
        public LayoutBox<Widget, Widget>? trueParent;
        public LayoutBox<Widget, Widget>? box;
    }

    public class WeaponDataHolder
    {
        public FieldInfo? fInfo;
        public object obj;
        public TextEditField? edit;
        public ToggleButton? toggle;
        public LayoutBox<Widget, Widget>? parent;
        public LayoutBox<Widget, Widget>? box;
    }
}
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
using System;
using System.Diagnostics;
using System.Security.Policy;
using Cosmoteer.Ships.Parts;
using Cosmoteer.Ships.Parts.Weapons;

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

    public enum SpawnShape
    {
        Line,
        Square,
        Circle
    }

    public class Main
    {
        private static readonly List<FireMode> SELECTABLE_FIRE_MODES = new List<FireMode>
        {
            FireMode.Place,
            FireMode.Erase
        };

        private static readonly List<SpawnShape> SELECTABLE_SPAWN_SHAPES = new List<SpawnShape>
        {
            SpawnShape.Line,
            SpawnShape.Square,
            SpawnShape.Circle,
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
        private static TextEditField? rangeTextField;

        private static WeaponsHolder? selectedWeapon;
        private static SpawnShape selectedSpawnShape = SpawnShape.Line;

        private static FireMode fireMode = FireMode.Place;
        private static FireRules? fireRules;

        private static Ship? currentShip;
        private static Ship? emptyShip;
        private static List<CustomBeamEmitter> customBeamEmitters = new List<CustomBeamEmitter>();

        private static bool loaded = false;
        private static bool inBuilder = false;
        private static bool firingBeam = false;
        private static bool projectilesEnabled = true;
        private static bool firesEnabled = false;

        public static int bulletAmount = 1;
        public static int circleRadius = 64;
        public static int fireRadius = 1;

        private static List<WeaponCategoryHolder> weaponCategories = new List<WeaponCategoryHolder>();

        [UnmanagedCallersOnly]
        public static void InitializePatches()
        {
            keyboard = Halfling.App.Keyboard; 

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
                        //MsgBox(currentState.GetType().ToString(), "Test");
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

                Vector2 mloc = Main.simRoot.WorldMouseLoc;
                Direction mrot = Main.simRoot.Camera.Rotation;
                float degrees = mrot.ToDegrees();
                degrees -= 90;
                mrot = Direction.FromDegrees(degrees);

                if(projectilesEnabled && projectilesEnabled && !Main.weaponsToolBox.IsMouseOver)
                {
                    if(selectedWeapon.bRules != null)
                    {
                        if (App.Mouse.LeftButton.IsDown)
                        {
                            if (!firingBeam)
                            {
                                firingBeam = true;

                                customBeamEmitters.Clear();

                                if(bulletAmount == 1)
                                {
                                    CustomBeamEmitter testIon = new CustomBeamEmitter(selectedWeapon.bRules);

                                    testIon._dummyShip = emptyShip;
                                    testIon.StartEmission(mloc, mrot, true);
                                    customBeamEmitters.Add(testIon);
                                } 
                                else
                                {
                                    if(selectedSpawnShape == SpawnShape.Line || selectedSpawnShape == SpawnShape.Square)
                                    {
                                        List<Vector2> points = Utils.GetPoints(selectedSpawnShape, mloc, bulletAmount);

                                        for (int i = 0; i < points.Count; i++)
                                        {
                                            Vector2 pos = Utils.Rotate(points[i], mloc, mrot.ToRadians());
                                            Vector2 offset = mloc - pos;

                                            CustomBeamEmitter testIon = new CustomBeamEmitter(selectedWeapon.bRules);

                                            testIon._dummyShip = emptyShip;
                                            testIon._offset = offset;
                                            testIon.StartEmission(pos, mrot, true);
                                            customBeamEmitters.Add(testIon);
                                        }
                                    }
                                    else if (selectedSpawnShape == SpawnShape.Circle)
                                    {
                                        List<Vector2> points = Utils.GetPoints(selectedSpawnShape, mloc, bulletAmount, circleRadius);

                                        if(rangeTextField != null)
                                        {
                                            rangeTextField.Text = circleRadius.ToString();
                                        }

                                        for (int i = 0; i < points.Count; i++)
                                        {
                                            Vector2 pos = Utils.Rotate(points[i], mloc, mrot.ToRadians());
                                            Vector2 offset = mloc - pos;
                                            Direction r = new Direction(Utils.GetAngle(mloc, pos));

                                            BuffableFloat range = selectedWeapon.bRules.Range;
                                            range.BaseValue = circleRadius;
                                            selectedWeapon.bRules.Range = range;

                                            CustomBeamEmitter testIon = new CustomBeamEmitter(selectedWeapon.bRules);

                                            testIon._dummyShip = emptyShip;
                                            testIon._offset = offset;
                                            testIon.StartEmission(pos, r, true);
                                            customBeamEmitters.Add(testIon);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < customBeamEmitters.Count; i++)
                                {
                                    if(bulletAmount == 1)
                                    {
                                        customBeamEmitters[i]._worldLocation = mloc;
                                        customBeamEmitters[i]._worldDirection = mrot;
                                    } else
                                    {
                                        if(selectedSpawnShape == SpawnShape.Circle)
                                        {
                                            customBeamEmitters[i]._worldLocation = mloc + customBeamEmitters[i]._offset;
                                            //rotation update not needed
                                        } else
                                        {
                                            customBeamEmitters[i]._worldLocation = mloc + customBeamEmitters[i]._offset;
                                            customBeamEmitters[i]._worldDirection = mrot;
                                        }
                                    }
                                    customBeamEmitters[i].DoEmitBeam();
                                }
                            }
                        }
                        else
                        {
                            if (selectedWeapon.bRules != null)
                            {
                                //reset beam emitter

                                if (firingBeam)
                                {
                                    firingBeam = false;

                                    for (int i = 0; i < customBeamEmitters.Count; i++)
                                    {
                                        customBeamEmitters[i].StopEmission();
                                    }
                                }
                            }
                        }
                    }

                    if(result)
                    {
                        if(selectedWeapon.bRules == null)
                        {
                            //only enter when no beam emitter

                            Direction rot = Main.simRoot.Camera.Rotation;
                            float ndegrees = rot.ToDegrees();
                            ndegrees -= 90;
                            rot = Direction.FromDegrees(ndegrees);

                            if (bulletAmount == 1)
                            {
                                SpawnBullet(Main.simRoot.WorldMouseLoc, rot);
                            }
                            else
                            {
                                List<Vector2> points = Utils.GetPoints(selectedSpawnShape, mloc, bulletAmount, circleRadius);

                                if(selectedSpawnShape == SpawnShape.Circle)
                                {
                                    if (rangeTextField != null)
                                    {
                                        rangeTextField.Text = circleRadius.ToString();
                                    }
                                }

                                for (int i = 0; i < points.Count; i++)
                                {
                                    Vector2 pos = Utils.Rotate(points[i], mloc, mrot.ToRadians());

                                    if(selectedSpawnShape == SpawnShape.Circle)
                                    {
                                        rot = new Direction(Utils.GetAngle(pos, mloc));
                                        SpawnBullet(pos, rot, circleRadius);
                                    }
                                    else
                                    {
                                        SpawnBullet(pos, rot);
                                    }

                                }

                                //Vector2 loc = Main.simRoot.WorldMouseLoc;
                                //Direction rot = Main.simRoot.Camera.Rotation;
                            }
                        }
                    }
                }

                //if (result && projectilesEnabled && !Main.weaponsToolBox.IsMouseOver && )
                //{
                    //Only enter when non-beam-emitter

                    /*if(customBeamEmitter == null)
                    {
                        beamEmitterRules.Width = 10;
                        HitRules hO = beamEmitterRules.HitOperational;
                        
                        MultiHitEffectRules multi = hO.HitEffects;
                        foreach(HitEffectRules hOr in multi.Effects)
                        {
                            if(hOr.GetType() == typeof(DamageEffectRules))
                            {
                                DamageEffectRules dmg = (DamageEffectRules)hOr;
                                BuffableInt val = dmg.Damage;
                                val.BaseValue = 9900000;
                                dmg.Damage = val;
                            }
                        }

                        hO = beamEmitterRules.HitStructural;
                        multi = hO.HitEffects;
                        foreach (HitEffectRules hOr in multi.Effects)
                        {
                            if (hOr.GetType() == typeof(DamageEffectRules))
                            {
                                DamageEffectRules dmg = (DamageEffectRules)hOr;
                                BuffableInt val = dmg.Damage;
                                val.BaseValue = 9900000;
                                dmg.Damage = val;
                            }
                        }

                        CustomBeamEmitter testIon = new CustomBeamEmitter(beamEmitterRules);
                        testIon._currentRange = 9999;

                        testIon._dummyShip = emptyShip;
                        testIon.StartEmission(mloc, mrot, true);
                        customBeamEmitter = testIon;
                        //MsgBox("emitstart", "");
                    }*/
                //}

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

        private static void SpawnBullet(Vector2 spawnLoc, Direction rot, float newRange = -1)
        {
            BulletRules rules = selectedWeapon.rules;

            if(newRange > 0)
            {
                BuffableFloat range = rules.Range;
                range.BaseValue = newRange;
                rules.Range = range;
            }

            Angle minSpread = 0; //rules.Spread.Min.GetValue(base.Part);
            Angle maxSpread = 0; //Rules.Spread.Max.GetValue(base.Part);
            Angle dirOffset = 0; //(Rules.EvenSpread ? ((Angle)((float)minSpread + burstProgress * ((float)maxSpread - (float)minSpread))) : base.Rand.Angle(minSpread, maxSpread));

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

            List<PartComponentRules> bemList = new List<PartComponentRules>();

            foreach( KeyValuePair<Cosmoteer.Data.ID<ShipRules>, ShipRules> tmp in GameApp.Rules._shipRulesById)
            {
                PartRules[] parts = tmp.Value.Parts;
                foreach( PartRules part in parts )
                {
                    foreach (PartComponentRules pcr in part.PhysicalComponents)
                    {
                        if(pcr.GetType() == typeof(BeamEmitterRules))
                        {
                            BeamEmitterRules bemtmp = (BeamEmitterRules)pcr;

                            if(bemtmp.SerialID.Equals("BeamEmitter") && !bemList.Contains(bemtmp))
                            {
                                bemList.Add(bemtmp);

                                if(bemList.Count == 1)
                                {
                                    WeaponsHolder holder = new WeaponsHolder();
                                    holder.bRules = bemtmp;
                                    holder.bID = pcr.ID;

                                    SELECTABLE_WEAPONS.Add(holder);
                                }
                            }
                        }
                    }
                }
            }

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

            List<Widget> childList = new List<Widget>();
            foreach (Widget child in parent.Children)
            {
                childList.Add(child);
                //child.Unparent();
            }
            foreach (Widget child in childList)
            {
                child.Unparent();
            }

            if (selectedWeapon.rules != null)
            {
                BulletRules rules = selectedWeapon.rules;
                foreach (var r in rules.Components)
                {
                    WeaponCategoryHolder cat = new WeaponCategoryHolder();
                    cat.rules = r;
                    weaponCategories.Add(cat);
                }

                CreateWeaponUI(parent);
            }
            else
            {
                BeamEmitterRules rules = selectedWeapon.bRules;
                CreateWeaponUI(parent, false, rules);
            }
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

                            if (fi.Name.Equals("Range") && rangeTextField == null)
                            {
                                rangeTextField = editField;
                            }

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

        private static void CreateWeaponUI(LayoutBox<Widget, Widget> parent, bool categories = true, BeamEmitterRules? bem = null)
        {
            rangeTextField = null;

            if(categories)
            {
                Type t = selectedWeapon.rules.GetType();
                RecursiveReadFields(t, selectedWeapon.rules, parent);

                foreach (WeaponCategoryHolder holder in weaponCategories)
                {
                    LayoutBox[] widgets = Utils.CreateCategoryBox(parent, holder.rules.GetType().ToString(), false);

                    LayoutBox newCatBox = widgets[0];
                    holder.parent = parent;
                    holder.trueParent = widgets[1];
                    holder.box = newCatBox;

                    t = holder.rules.GetType();
                    RecursiveReadFields(t, holder.rules, newCatBox);
                }
            } else
            {
                Type t = bem.GetType();
                RecursiveReadFields(t, bem, parent);
            }
        }

        private static void CreateUI(bool first = true)
        {
            WeaponsToolbox weaponsToolBox = new WeaponsToolbox(Main.gameRoot);
            weaponsToolBox.SelfActive = false;
            weaponsToolBox.Rect = new Rect(10f, 70f, 450f, 540f);
            weaponsToolBox.ResizeController.MinSize = new Vector2(450f, 540f);
            weaponsToolBox.ResizeController.MaxSize = new Vector2(700f, 1024f);

            LayoutBox weaponsBox = Utils.CreateCategoryBox(weaponsToolBox, "Weapons", true)[0];

            DropList weaponsList = new DropList();
            if (SELECTABLE_WEAPONS[0].rules != null)
            {
                weaponsList.Text = SELECTABLE_WEAPONS[0].rules.ID.ToString();
            } else
            {
                weaponsList.Text = SELECTABLE_WEAPONS[0].bID.ToString();
            }
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

            Label angleInfoLabel = new Label();
            angleInfoLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            angleInfoLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            angleInfoLabel.Text = "Change angle of Projectile by turning camera with Q & E Key";
            angleInfoLabel.TextRenderer.FontSize = 14;
            weaponsBox.AddChild(angleInfoLabel);

            TextEditField bulletAmountEdit = Utils.CreateEditLabel(weaponsBox, "Amount of Bullets", bulletAmount.ToString(), CharFilters.UnsignedDecimal);
            bulletAmountEdit.TextChanged += delegate
            {
                bool ok = Int32.TryParse(bulletAmountEdit.Text, out int newValue);
                if (ok && newValue > 0)
                {
                    bulletAmount = newValue;
                }
            };

            Label shapeInfoLabel = new Label();
            shapeInfoLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            shapeInfoLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            shapeInfoLabel.Text = "--- Spawn formation (if amount > 1) ---";
            shapeInfoLabel.TextRenderer.FontSize = 14;
            weaponsBox.AddChild(shapeInfoLabel);

            TextEditField radiusEdit = Utils.CreateEditLabel(weaponsBox, "Circle Radius", circleRadius.ToString(), CharFilters.UnsignedInteger);
            radiusEdit.TextChanged += delegate
            {
                bool ok = Int32.TryParse(radiusEdit.Text, out int newValue);
                if (ok && newValue > 0)
                {
                    circleRadius = newValue;
                }
            };
            radiusEdit.SelfInputActive = false;

            DropList shapeList = new DropList();
            shapeList.Text = SELECTABLE_SPAWN_SHAPES[0].ToString();
            shapeList.StateNormalTextRenderer.FontSize = 14;
            shapeList.StateDisabledTextRenderer.FontSize = 14;
            shapeList.StateHighlightedTextRenderer.FontSize = 14;
            shapeList.StatePressedTextRenderer.FontSize = 14;
            weaponsBox.AddChild(shapeList);

            DataBinder<SelectableButton, SpawnShape> shapeBinding = shapeList.ListBox.Children.BindToData(SELECTABLE_SPAWN_SHAPES, delegate (SpawnShape shape)
            {
                ListItem listItem = new ListItem();
                listItem.Text = shape.ToString();
                return listItem;
            });
            shapeList.Clicked += delegate
            {
                projectilesEnabled = false;
            };
            shapeList.ListBox.SelectionManager.WidgetSelected += delegate
            {
                selectedSpawnShape = shapeBinding.GetDataForWidget(shapeList.SelectedWidget);
                projectilesEnabled = false;
                enableProjectileButton.IsSelected = false;

                if(selectedSpawnShape == SpawnShape.Circle)
                {
                    radiusEdit.SelfInputActive = true;
                } else
                {
                    radiusEdit.SelfInputActive = false;
                }
            };

            LayoutBox advancedOptionsBox = Utils.CreateCategoryBox(weaponsBox, "Advanced Options", false)[0];

            DataBinder<SelectableButton, WeaponsHolder> weaponBinding = weaponsList.ListBox.Children.BindToData(SELECTABLE_WEAPONS, delegate (WeaponsHolder holder)
            {
                ListItem listItem = new ListItem();
                if(holder.rules != null)
                {
                    listItem.Text = holder.rules.ID.ToString();
                }
                else
                {
                    listItem.Text = holder.bRules.ID.ToString();
                }
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

            Label dcLabel = new Label();
            dcLabel.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            dcLabel.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            dcLabel.Text = "Have an Idea or found a bug?";
            dcLabel.TextRenderer.FontSize = 14;
            weaponsToolBox.AddChild(dcLabel);

            Halfling.Gui.Button dcBtn = new Halfling.Gui.Button();
            dcBtn.AutoSize.AutoWidthMode = AutoSizeMode.Enable;
            dcBtn.AutoSize.AutoHeightMode = AutoSizeMode.Enable;
            dcBtn.Text = "Join my Discord";
            dcBtn.Clicked += delegate
            {
                Process.Start(new ProcessStartInfo("https://discord.gg/dAvpvRGS8d") { UseShellExecute = true });
            };
            weaponsToolBox.AddChild(dcBtn);

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
        public BeamEmitterRules? bRules;
        public ID<PartComponentRules>? bID;
        public BulletRules? rules;
        public ID<BulletRules>? ID;
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
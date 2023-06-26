using Cosmoteer.Gui;
using Halfling.Geometry;
using Halfling.Input;
using Halfling.Gui;
using Label = Halfling.Gui.Label;
using AutoSizeMode = Halfling.Gui.AutoSizeMode;
using Halfling.Gui.Components.Toggles;

namespace ProjectileSpawner
{
    public static class Utils
    {
        public static string GetPercent(float input)
        {
            return String.Format("{0:P2}", input);
        }

        public static float GetAngle(Vector2 start, Vector2 end)
        {
            return MathF.Atan2(end.Y - start.Y, end.X - start.X);
        }

        public static List<Vector2> GetPoints(SpawnShape shape, Vector2 center, int amount, float arg2 = 0)
        {
            List<Vector2> points = new List<Vector2>();

            switch (shape)
            {
                case SpawnShape.Line:
                    float spread = 1f;
                    float startX = 0 - spread / 2 - (((amount / 2) - 1) * spread);

                    for (int i = 0; i < amount; i++)
                    {
                        float x = 0;
                        float y = startX + i * spread;

                        Vector2 pos = new Vector2(x, y);

                        points.Add(pos);
                    }
                    points = Utils.CenterPointsAround(points, center);
                    break;
                case SpawnShape.Square:
                    int count = Convert.ToInt32(MathF.Sqrt(amount));

                    for (int y = 0; y < count; y++)
                    {
                        for (int x = 0; x < count; x++)
                        {
                            Vector2 pos = new Vector2(x, y);

                            points.Add(pos);
                        }
                    }

                    points = Utils.CenterPointsAround(points, center);
                    break;
                case SpawnShape.Circle:
                    points = Utils.GenerateCirclePoints(arg2, amount);
                    points = Utils.CenterPointsAround(points, center);
                    break;
            }

            return points;
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
            if (filter == null)
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

        public static Vector2 Rotate(Vector2 vector, Vector2 pivot, float angle)
        {
            // Translate the vector so that the pivot point is at the origin
            Vector2 translatedVector = vector - pivot;

            // Apply the rotation to the translated vector
            float angleInRadians = angle;
            float cos = MathF.Cos(angleInRadians);
            float sin = MathF.Sin(angleInRadians);
            float x = translatedVector.X * cos - translatedVector.Y * sin;
            float y = translatedVector.X * sin + translatedVector.Y * cos;

            // Translate the rotated vector back to its original position
            Vector2 rotatedVector = new Vector2(x, y) + pivot;
            return rotatedVector;
        }

        public static List<Vector2> GenerateCirclePoints(float r, int n)
        {
            List<Vector2> points = new List<Vector2>();
            float angle = 360.0f / n;

            const float deg2rad = (MathF.PI * 2) / 360.0f;

            for (int i = 0; i < n; i++)
            {
                float x = r * MathF.Cos(angle * i * deg2rad);
                float y = r * MathF.Sin(angle * i * deg2rad);
                points.Add(new Vector2(x, y));
            }

            return points;
        }

        public static List<Vector2> CenterPointsAround(List<Vector2> points, Vector2 center)
        {
            // Find the average of all the points
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }
            Vector2 average = sum / points.Count;

            // Calculate the offset required to center all the points around the specified center
            Vector2 offset = center - average;

            // Apply the offset to all the points
            for (int i = 0; i < points.Count; i++)
            {
                points[i] += offset;
            }

            // Return the centered points
            return points;
        }

    }
}

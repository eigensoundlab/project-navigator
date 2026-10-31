// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

// Gutter column for the new Hierarchy window (default since Unity 6.6): a color stripe, a pin dot and
// the note as tooltip for GameObjects marked in Project Navigator. The column is registered through the
// public Hierarchy column API; users can hide or move it from the Hierarchy header's right-click menu.
// Define PROJECT_NAVIGATOR_NO_HIERARCHY_COLUMN to compile the package without it.
#if UNITY_6000_5_OR_NEWER && !PROJECT_NAVIGATOR_NO_HIERARCHY_COLUMN
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectNavigator
{
    internal static class HierarchyGutterColumn
    {
        const string k_ColumnId = "ProjectNavigator/Gutter";
        const string k_RowName = "pn-gutter";
        const string k_StripeName = "pn-gutter-stripe";
        const string k_DotName = "pn-gutter-dot";

        static readonly Color k_PinDot = new Color(1f, 0.85f, 0.3f, 0.95f);

        [HierarchyViewColumnDescriptor(k_ColumnId)]
        internal static void CreateColumn(HierarchyViewColumnDescriptor desc)
        {
            desc.Title = "Nav";
            desc.Tooltip = "Project Navigator: pinned objects, colors and notes";
            desc.DefaultPriority = -10;   // left of the Name column
            desc.DefaultWidth = 24;
            desc.DefaultVisibility = true;
        }

        [HierarchyViewCellDescriptor(k_ColumnId, typeof(HierarchyGameObjectHandler))]
        internal static void CreateCell(HierarchyViewCellDescriptor desc)
        {
            desc.BindCell = cell =>
            {
                var handler = (HierarchyGameObjectHandler)cell.Handler;
                var gameObject = handler.GetGameObject(cell.Node);
                var style = SceneNavigator.StyleFor(gameObject);

                var row = cell.Q(k_RowName);
                if (row == null)
                {
                    row = new VisualElement { name = k_RowName, pickingMode = PickingMode.Ignore };
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.flexGrow = 1;
                    row.style.height = Length.Percent(100);

                    var stripe = new VisualElement { name = k_StripeName, pickingMode = PickingMode.Ignore };
                    stripe.style.width = 3;
                    stripe.style.height = 14;
                    stripe.style.marginLeft = 2;
                    stripe.style.borderTopLeftRadius = 1;
                    stripe.style.borderTopRightRadius = 1;
                    stripe.style.borderBottomLeftRadius = 1;
                    stripe.style.borderBottomRightRadius = 1;
                    row.Add(stripe);

                    var dot = new VisualElement { name = k_DotName, pickingMode = PickingMode.Ignore };
                    dot.style.width = 6;
                    dot.style.height = 6;
                    dot.style.marginLeft = 3;
                    dot.style.borderTopLeftRadius = 3;
                    dot.style.borderTopRightRadius = 3;
                    dot.style.borderBottomLeftRadius = 3;
                    dot.style.borderBottomRightRadius = 3;
                    dot.style.backgroundColor = k_PinDot;
                    row.Add(dot);

                    cell.Add(row);
                }

                var stripeElement = row.Q(k_StripeName);
                var dotElement = row.Q(k_DotName);
                stripeElement.style.display = style.hasColor ? DisplayStyle.Flex : DisplayStyle.None;
                if (style.hasColor) stripeElement.style.backgroundColor = style.color;
                dotElement.style.display = style.pinned ? DisplayStyle.Flex : DisplayStyle.None;

                cell.tooltip = style.HasNote
                    ? style.note
                    : style.pinned ? "Pinned in Project Navigator" : "";

                cell.IsDefaultValue = !style.Visible;
            };
        }
    }
}
#endif

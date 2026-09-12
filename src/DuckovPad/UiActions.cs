using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuckovPad
{
    /// <summary>Use the game's existing click, transfer and drop rules from controller focus.</summary>
    internal sealed class UiActions
    {
        private static readonly MethodInfo InventoryQuickMove = typeof(InventoryDisplay).GetMethod(
            "NotifyItemDoubleClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SlotQuickMove = typeof(SlotDisplay).GetMethod(
            "OnItemDisplayDoubleClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LastItemClick = typeof(ItemDisplay).GetField(
            "lastClickTime", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public void Confirm(GameObject target, Vector2 position)
        {
            if (target == null || !target.activeInHierarchy) return;
            var selected = ItemUIUtilities.SelectedItemDisplay;
            var source = selected != null ? selected.GetComponentInParent<IItemDragSource>() as Component : null;
            if (target != null && source != null && source.gameObject != target
                && target.GetComponent<IDropHandler>() != null)
            {
                // Select, navigate, confirm: no mouse-button hold and no drag threshold.
                // The game validates capacity, equipment compatibility, stacks and hotbar slots.
                var data = new PointerEventData(EventSystem.current)
                {
                    position = position,
                    button = PointerEventData.InputButton.Left,
                    pointerDrag = source.gameObject
                };
                bool placed = false;
                void OnPut(ItemStatsSystem.Item item, bool pickup) { placed = true; }
                ItemUIUtilities.OnPutItem += OnPut;
                try { ExecuteEvents.Execute(target, data, ExecuteEvents.dropHandler); }
                finally { ItemUIUtilities.OnPutItem -= OnPut; }
                if (placed || (data.used && target.GetComponent<ItemShortcutEditorEntry>() != null))
                    ItemUIUtilities.Select(null);
                return;
            }
            var display = GetDisplay(target);
            if (display != null && target.GetComponent<ItemShortcutEditorEntry>() != null && display.Target != null)
            {
                ItemUIUtilities.Select(selected == display ? null : display);
                return;
            }
            ClickItemOrControl(target, position, PointerEventData.InputButton.Left);
        }

        private static ItemDisplay GetDisplay(GameObject target)
        {
            if (target == null) return null;
            if (target.GetComponent<InventoryEntry>() == null && target.GetComponent<SlotDisplay>() == null
                && target.GetComponent<ItemShortcutEditorEntry>() == null && target.GetComponent<ItemDisplay>() == null)
                return null;
            return target.GetComponentInChildren<ItemDisplay>();
        }

        /// <summary>
        /// Map a hovered item visual back to its slot. The tooltip reports the inner
        /// <see cref="ItemDisplay"/>, but entry/slot rules live on the parents.
        /// </summary>
        public static GameObject ResolveSlot(GameObject target)
        {
            if (target == null) return null;
            if (target.GetComponent<InventoryEntry>() != null || target.GetComponent<SlotDisplay>() != null
                || target.GetComponent<ItemShortcutEditorEntry>() != null
                || target.GetComponent<ItemDisplay>() == null)
                return target;
            for (var parent = target.transform.parent; parent != null; parent = parent.parent)
            {
                var go = parent.gameObject;
                if (go.GetComponent<InventoryEntry>() != null || go.GetComponent<SlotDisplay>() != null
                    || go.GetComponent<ItemShortcutEditorEntry>() != null)
                    return go;
            }
            return target;
        }

        private static ItemStatsSystem.Item ItemOf(GameObject target)
        {
            target = ResolveSlot(target);
            if (target == null) return null;
            var entry = target.GetComponent<InventoryEntry>();
            if (entry != null) return entry.Item;
            var slot = target.GetComponent<SlotDisplay>();
            if (slot != null) return slot.GetItem();
            return GetDisplay(target)?.Target;
        }

        public void ClickItemOrControl(GameObject target, Vector2 position, PointerEventData.InputButton button)
        {
            if (target == null || !target.activeInHierarchy || EventSystem.current == null) return;
            var selectedButton = target.GetComponent<Button>();
            if (selectedButton != null)
            {
                if (!selectedButton.isActiveAndEnabled || !selectedButton.IsInteractable()) return;
                // The highlight identifies this exact object. Never raycast again and
                // accidentally activate a neighbour as the popup moves or resizes.
                var menu = ItemOperationMenu.Instance;
                bool inOperations = menu != null && menu.open && target.transform.IsChildOf(menu.transform);
                selectedButton.OnPointerClick(new PointerEventData(EventSystem.current)
                {
                    position = position, button = button, clickTime = Time.unscaledTime, clickCount = 1
                });
                if (button == PointerEventData.InputButton.Left && inOperations && menu != null && menu.open)
                    menu.Close();
                return;
            }
            var display = GetDisplay(target);
            if (display == null || display.Target == null)
            {
                Click(position, button);
                return;
            }
            // The centre of a slot may raycast to its frame instead of its ItemDisplay.
            // Use the item's actual handler so a focused occupied slot can always be grabbed.
            LastItemClick?.SetValue(display, Time.unscaledTime - 1f);
            display.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                position = position, button = button, clickTime = Time.unscaledTime, clickCount = 1
            });
        }

        public void ToggleLock(GameObject target)
        {
            var entry = target != null ? target.GetComponent<InventoryEntry>() : null;
            if (entry != null && !entry.Disabled && entry.Master != null) entry.ToggleLock();
        }

        /// <summary>Direct drop on the focused slot. Same rules as the X key, without needing hover.</summary>
        public bool CanDrop(GameObject target)
        {
            target = ResolveSlot(target);
            if (target == null) return false;
            var entry = target.GetComponent<InventoryEntry>();
            if (entry != null)
                return !entry.Disabled && entry.CanOperate && entry.Item != null && entry.Item.CanDrop;
            var slot = target.GetComponent<SlotDisplay>();
            if (slot != null)
            {
                var item = slot.GetItem();
                return slot.Editable && item != null && item.CanDrop;
            }
            var display = GetDisplay(target);
            return display != null && display.Target != null && display.CanDrop;
        }

        public void Drop(GameObject target)
        {
            try
            {
                if (!CanDrop(target)) return;
                var item = ItemOf(target);
                var main = CharacterMainControl.Main;
                if (item == null || main == null) return;
                item.Drop(main, createRigidbody: true);
                ItemUIUtilities.Select(null);
            }
            catch (Exception e)
            {
                Log.Warn("Controller drop failed: " + e.Message);
            }
        }

        /// <summary>Direct use on the focused slot. Same rules as the keyboard use key.</summary>
        public bool CanUse(GameObject target)
        {
            target = ResolveSlot(target);
            if (target == null || CharacterMainControl.Main == null) return false;
            var entry = target.GetComponent<InventoryEntry>();
            if (entry != null)
                return !entry.Disabled && entry.CanOperate && entry.Item != null
                    && entry.Item.IsUsable(CharacterMainControl.Main);
            var slot = target.GetComponent<SlotDisplay>();
            if (slot != null)
            {
                var item = slot.GetItem();
                return slot.Editable && item != null && item.IsUsable(CharacterMainControl.Main);
            }
            var display = GetDisplay(target);
            return display != null && display.CanUse;
        }

        public void Use(GameObject target)
        {
            try
            {
                if (!CanUse(target)) return;
                var main = CharacterMainControl.Main;
                var item = ItemOf(target);
                if (main == null || item == null) return;
                main.UseItem(item);
                ItemUIUtilities.Select(null);
            }
            catch (Exception e)
            {
                Log.Warn("Controller use failed: " + e.Message);
            }
        }

        private static readonly MethodInfo RefreshWishlistInfo = typeof(ItemHoveringUI).GetMethod(
            "NotifyRefreshWishlistInfo", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>Wishlist mark on the focused item. Same toggle as the N key.</summary>
        public bool TryGetMark(GameObject target, out int typeId, out bool wishlisted)
        {
            typeId = -1;
            wishlisted = false;
            try
            {
                var item = ItemOf(target);
                if (item == null || ItemWishlist.Instance == null) return false;
                typeId = item.TypeID;
                if (typeId < 0) return false;
                wishlisted = ItemWishlist.Instance.IsManuallyWishlisted(typeId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void ToggleMark(GameObject target)
        {
            try
            {
                if (!TryGetMark(target, out int typeId, out bool wishlisted)) return;
                if (wishlisted) ItemWishlist.RemoveFromWishlist(typeId);
                else ItemWishlist.AddToWishList(typeId);
                // Internal to the game assembly; refresh via reflection so the
                // tooltip indicator updates without needing another hover.
                try { RefreshWishlistInfo?.Invoke(null, null); }
                catch (Exception e) { Log.Warn("Wishlist refresh failed: " + e.Message); }
            }
            catch (Exception e)
            {
                Log.Warn("Controller wishlist mark failed: " + e.Message);
            }
        }

        public void Click(Vector2 position, PointerEventData.InputButton button)
        {
            if (EventSystem.current == null) return;
            // Repeated controller confirms remain single clicks; Y owns quick transfer.
            var data = new PointerEventData(EventSystem.current)
            {
                position = position, pressPosition = position, button = button,
                clickCount = 1, clickTime = Time.unscaledTime, eligibleForClick = true
            };
            _hits.Clear();
            EventSystem.current.RaycastAll(data, _hits);
            foreach (var hit in _hits)
            {
                if (!(hit.module is GraphicRaycaster)) continue;
                // Match UiSnap's exposure rules: a tooltip may cover the selected
                // control, but is never the recipient of its confirm click.
                if (hit.gameObject.GetComponentInParent<ItemHoveringUI>() != null) continue;
                var itemDisplay = hit.gameObject.GetComponentInParent<ItemDisplay>();
                if (itemDisplay != null) LastItemClick?.SetValue(itemDisplay, Time.unscaledTime - 1f);
                bool inOperations = ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open
                    && hit.gameObject.transform.IsChildOf(ItemOperationMenu.Instance.transform);
                data.pointerCurrentRaycast = hit;
                data.pointerPressRaycast = hit;
                var pressed = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
                data.pointerPress = pressed;
                if (pressed != null) ExecuteEvents.Execute(pressed, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerClickHandler);
                if (inOperations && ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open)
                    ItemOperationMenu.Instance.Close();
                return;
            }
        }

        public void QuickMove(GameObject target)
        {
            if (target == null || EventSystem.current == null) return;
            var data = new PointerEventData(EventSystem.current);
            var entry = target.GetComponent<InventoryEntry>();
            if (entry != null && !entry.Disabled && entry.CanOperate && entry.Item != null && entry.Master != null)
            {
                InventoryQuickMove?.Invoke(entry.Master, new object[] { entry, data });
                ItemUIUtilities.Select(null);
                return;
            }
            var slot = target.GetComponent<SlotDisplay>();
            if (slot != null && slot.Editable && slot.GetItem() != null)
            {
                SlotQuickMove?.Invoke(slot, new object[] { target.GetComponentInChildren<ItemDisplay>(), data });
                ItemUIUtilities.Select(null);
            }
        }
    }
}

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PicoShot.Localization
{
    public class LocalizationSearchablePopup : EditorWindow
    {
        private string _searchText = "";
        private Vector2 _scrollPosition;
        private List<string> _items;
        private List<string> _filteredItems;
        private Action<int> _onItemSelected;
        private int _selectedIndex;
        private static readonly Vector2 Size = new(300, 320);
        private bool _focusSearchField;
        private int _hoveredIndex = -1;
        private float _lastClickTime;
        private const float DoubleClickTime = 0.4f;

        public static void Show(Rect activatorRect, string[] items, int selectedIndex, Action<int> onItemSelected)
        {
            var window = CreateInstance<LocalizationSearchablePopup>();
            window._items = new List<string>(items);
            window._filteredItems = new List<string>(items);
            window._selectedIndex = selectedIndex;
            window._onItemSelected = onItemSelected;
            window._focusSearchField = true;

            var windowPos = GUIUtility.GUIToScreenPoint(new Vector2(activatorRect.x, activatorRect.y));
            window.position = new Rect(windowPos.x, windowPos.y + activatorRect.height, Size.x, Size.y);

            window.ShowPopup();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSearchBar();
            DrawItems();
            HandleInput();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                Close();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (_focusSearchField)
            {
                GUI.SetNextControlName("SearchField");
                _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
                EditorGUI.FocusTextInControl("SearchField");
                _focusSearchField = false;
            }
            else
            {
                _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _searchText = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.Layout)
            {
                _filteredItems = _items.Where(item =>
                    string.IsNullOrEmpty(_searchText) ||
                    item.ToLower().Contains(_searchText.ToLower())
                ).ToList();
            }
        }

        private void DrawItems()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var i = 0; i < _filteredItems.Count; i++)
            {
                var isSelected = _items.IndexOf(_filteredItems[i]) == _selectedIndex;
                var isHovered = i == _hoveredIndex;

                var itemRect = EditorGUILayout.GetControlRect(GUILayout.Height(25));

                if (itemRect.Contains(Event.current.mousePosition))
                {
                    if (_hoveredIndex != i)
                    {
                        _hoveredIndex = i;
                        Repaint();
                    }
                }

                if (isHovered)
                {
                    EditorGUI.DrawRect(itemRect, new Color(0.7f, 0.7f, 0.7f, 0.3f));
                }
                else if (isSelected)
                {
                    EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.4f, 0.8f, 0.3f));
                }

                if (!GUI.Button(itemRect, _filteredItems[i], EditorStyles.label)) continue;
                var timeSinceLastClick = Time.realtimeSinceStartup - _lastClickTime;
                if (timeSinceLastClick < DoubleClickTime)
                {
                    var actualIndex = _items.IndexOf(_filteredItems[i]);
                    _onItemSelected?.Invoke(actualIndex);
                    Close();
                }

                _lastClickTime = Time.realtimeSinceStartup;
            }

            if (_filteredItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No matches found", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleInput()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Return:
                        if (_filteredItems.Count > 0)
                        {
                            var actualIndex = _items.IndexOf(_filteredItems[0]);
                            _onItemSelected?.Invoke(actualIndex);
                            Close();
                        }

                        break;
                    case KeyCode.Escape:
                        Close();
                        break;
                }
            }

            if (Event.current.type != EventType.MouseLeaveWindow) return;
            _hoveredIndex = -1;
            Repaint();
        }
    }
}
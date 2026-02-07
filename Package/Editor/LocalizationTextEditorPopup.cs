using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace PicoShot.Localization
{
    public class LocalizationTextEditorPopup : EditorWindow
    {
        private string _text = "";
        private Action<string> _onSave;
        private Vector2 _scrollPosition;
        private GUIStyle _richTextStyle;

        private const float WindowWidth = 400f;
        private const float WindowHeight = 300f;

        public static void Open(string initialText, Action<string> saveCallback)
        {
            var window = GetWindow<LocalizationTextEditorPopup>("Text Editor");
            window._text = initialText;
            window._onSave = saveCallback;
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth * 2, WindowHeight * 2);
            window.position = new Rect(
                (Screen.currentResolution.width - WindowWidth) / 2,
                (Screen.currentResolution.height - WindowHeight) / 2,
                WindowWidth,
                WindowHeight
            );
            window.Show();
        }

        private void InitializeStyles()
        {
            _richTextStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                richText = true
            };
        }

        private static string RemoveRichText(string input)
        {
            return Regex.Replace(input, @"<color=.*?>|</color>", "");
        }

        private static string ApplySyntaxHighlighting(string input)
        {
            var sb = new StringBuilder();
            var i = 0;
            while (i < input.Length)
            {
                switch (input[i])
                {
                    case '{':
                        {
                            var closingIndex = input.IndexOf('}', i);
                            if (closingIndex != -1)
                            {
                                var token = input.Substring(i, closingIndex - i + 1);
                                if (Regex.IsMatch(token, @"^\{\d+\}$"))
                                {
                                    var number = token.Substring(1, token.Length - 2);
                                    sb.Append("<color=#569CD6>{</color>");
                                    sb.Append($"<color=#B5CEA8>{number}</color>");
                                    sb.Append("<color=#569CD6>}</color>");
                                }
                                else
                                {
                                    sb.Append($"<color=red>{token}</color>");
                                }
                                i = closingIndex + 1;
                            }
                            else
                            {
                                sb.Append("<color=red>{</color>");
                                i++;
                            }

                            break;
                        }
                    case '}':
                        sb.Append("<color=red>}</color>");
                        i++;
                        break;
                    default:
                        {
                            var nextBrace = input.IndexOfAny(new[] { '{', '}' }, i);
                            if (nextBrace == -1) nextBrace = input.Length;
                            sb.Append(input.Substring(i, nextBrace - i));
                            i = nextBrace;
                            break;
                        }
                }
            }
            return sb.ToString();
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Edit Text", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            var displayText = ApplySyntaxHighlighting(_text);
            var newText = EditorGUILayout.TextArea(displayText, _richTextStyle, GUILayout.ExpandHeight(true));

            if (newText != displayText)
            {
                _text = RemoveRichText(newText);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.Width(100), GUILayout.Height(30)))
            {
                _onSave?.Invoke(_text);
                Close();
            }
            if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(30)))
            {
                Close();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }
    }
}

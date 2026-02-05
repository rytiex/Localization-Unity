using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using UnityEngine.Events;


namespace DMS.Language
{
    [AddComponentMenu("DMS/Language/Language Text Component")]
    public class LocalizationTextComponent : MonoBehaviour
    {
        [Header("Language Settings")]
        [Tooltip("Key to look up in the language database")]
        public string languageKey;

        [Tooltip("-1 for string, >= 0 for array element")]
        public int arrayIndex = -1;

        [Tooltip("Max array elements for dropdowns (0 = no limit)")]
        public int arraySizeLimit;

        [Header("Format Parameters")]
        [Tooltip("Optional parameters for string formatting")]
        public string[] formatParameters;

        [Header("Events")] public UnityEvent<string> onTextUpdated;

        private TMP_Text _tmpText;
        private TMP_Dropdown _tmpDropdown;
        private string _lastTranslation;
        private bool _isInitialized;
        private readonly List<string> _formatArgs = new();
        private readonly List<System.Func<string, string>> _textProcessors = new();

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            LocalizationManager.Subscribe(UpdateText);
            UpdateText();
        }

        private void OnDisable()
        {
            LocalizationManager.Unsubscribe(UpdateText);
        }

        private void OnDestroy()
        {
            LocalizationManager.Unsubscribe(UpdateText);
        }

        private void Start()
        {
            UpdateText();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (_isInitialized) return;

            _tmpText = GetComponent<TMP_Text>();
            _tmpDropdown = GetComponent<TMP_Dropdown>();

            ValidateSetup();

            _isInitialized = true;
        }

        private void ValidateSetup()
        {
            if (_tmpText == null && _tmpDropdown == null)
            {
                Debug.LogError($"[LanguageTextComponent] No supported text component found on {gameObject.name}", this);
            }
        }

        #endregion

        #region Public Methods

        public void UpdateText()
        {
            if (!_isInitialized) Initialize();

            if (string.IsNullOrEmpty(languageKey)) return;

            try
            {
                UpdateTextComponent();
                UpdateDropdownComponent();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LanguageTextComponent] Error updating text on {gameObject.name}: {ex}", this);
            }
        }

        public void SetFormatParameters(params string[] parameters)
        {
            formatParameters = parameters;
            UpdateText();
        }

        public void SetLanguageKey(string key)
        {
            if (languageKey == key) return;
            languageKey = key;
            UpdateText();
        }

        public void SetArrayIndex(int index)
        {
            if (arrayIndex == index) return;
            arrayIndex = index;
            UpdateText();
        }

        public void AddTextProcessor(System.Func<string, string> processor)
        {
            if (processor != null)
                _textProcessors.Add(processor);
        }

        public void RemoveTextProcessor(System.Func<string, string> processor)
        {
            _textProcessors.Remove(processor);
        }

        public void ClearTextProcessors()
        {
            _textProcessors.Clear();
        }

        public void Cleanup()
        {
            LocalizationManager.Unsubscribe(UpdateText);
            _textProcessors.Clear();

            if (_tmpText != null)
            {
                _tmpText.text = string.Empty;
                _tmpText.ForceMeshUpdate(true, true);
            }

            _lastTranslation = null;
        }

        private void ForceRefresh()
        {
            _lastTranslation = null;
            _isInitialized = false;
            Initialize();
            UpdateText();
        }

        #endregion

        #region Private Methods

        private string ProcessText(string text)
        {
            foreach (var processor in _textProcessors)
            {
                text = processor(text);
            }

            return text;
        }

        private void UpdateTextComponent()
        {
            if (_tmpText == null) return;

            string translatedText = GetTranslatedText();
            translatedText = ProcessText(translatedText);

            if (_lastTranslation == translatedText) return;

            _tmpText.text = translatedText;
            _lastTranslation = translatedText;
            onTextUpdated?.Invoke(translatedText);
        }

        private void UpdateDropdownComponent()
        {
            if (_tmpDropdown == null) return;

            var options = LocalizationManager.GetArray(languageKey)?.Where(opt => !string.IsNullOrEmpty(opt)).ToArray();
            if (options == null || !options.Any()) return;

            if (arraySizeLimit > 0 && options.Length > arraySizeLimit)
            {
                options = options.Take(arraySizeLimit).ToArray();
            }

            options = options.Select(ProcessText).ToArray();

            int selectedValue = _tmpDropdown.value;
            _tmpDropdown.ClearOptions();

            var dropdownOptions = options.Select(opt => new TMP_Dropdown.OptionData(opt)).ToList();
            _tmpDropdown.AddOptions(dropdownOptions);

            if (selectedValue < options.Length)
            {
                _tmpDropdown.value = selectedValue;
            }
        }

        private string GetTranslatedText()
        {
            PrepareFormatArguments();

            if (arrayIndex >= 0)
            {
                return LocalizationManager.GetArrayText(languageKey, arrayIndex);
            }

            return _formatArgs.Count > 0
                ? LocalizationManager.GetText(languageKey, _formatArgs.ToArray())
                : LocalizationManager.GetText(languageKey);
        }

        private void PrepareFormatArguments()
        {
            _formatArgs.Clear();

            if (formatParameters == null || formatParameters.Length == 0) return;

            foreach (var param in formatParameters)
            {
                if (string.IsNullOrEmpty(param)) continue;
                _formatArgs.Add(param);
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _isInitialized)
            {
                UpdateText();
            }
        }

        [UnityEditor.CustomEditor(typeof(LocalizationTextComponent))]
        public class LanguageTextComponentEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var component = (LocalizationTextComponent)target;
                if (GUILayout.Button("Update Text"))
                {
                    component.ForceRefresh();
                }
            }
        }
#endif
    }
}
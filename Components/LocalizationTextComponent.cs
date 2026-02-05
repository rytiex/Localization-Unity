using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PicoShot.Localization
{
    /// <summary>
    /// Component that binds a TMP_Text or TMP_Dropdown to the localization system.
    /// Automatically updates when language changes.
    /// </summary>
    [AddComponentMenu("UI/Localized Text")]
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class LocalizationTextComponent : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Localization")]
        [Tooltip("The translation key to look up")]
        [SerializeField] private string translationKey;

        [Tooltip("For array values: -1 = use first element as string, >= 0 = specific array index")]
        [SerializeField] private int arrayIndex = -1;

        [Tooltip("For dropdowns: maximum number of array elements to use (0 = unlimited)")]
        [SerializeField] private int arraySizeLimit;

        [Header("Formatting")]
        [Tooltip("Optional format parameters. Use {0}, {1}, etc. in translation text")]
        [SerializeField] private string[] formatParameters = Array.Empty<string>();

        [Header("Events")]
        [Tooltip("Called when the text is updated")]
        [SerializeField] private UnityEvent<string> onTextUpdated;

        #endregion

        #region Properties

        public string TranslationKey
        {
            get => translationKey;
            set
            {
                if (translationKey == value) return;
                translationKey = value;
                UpdateText();
            }
        }

        public int ArrayIndex
        {
            get => arrayIndex;
            set
            {
                if (arrayIndex == value) return;
                arrayIndex = value;
                UpdateText();
            }
        }

        public int ArraySizeLimit
        {
            get => arraySizeLimit;
            set
            {
                if (arraySizeLimit == value) return;
                arraySizeLimit = value;
                UpdateText();
            }
        }

        public string[] FormatParameters
        {
            get => formatParameters;
            set
            {
                formatParameters = value ?? Array.Empty<string>();
                UpdateText();
            }
        }

        public bool IsDropdown => _tmpDropdown != null;

        #endregion

        #region Private Fields

        private TMP_Text _tmpText;
        private TMP_Dropdown _tmpDropdown;
        private string _lastText;
        private bool _isInitialized;
        private readonly List<Func<string, string>> _textProcessors = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += UpdateText;
            UpdateText();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= UpdateText;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= UpdateText;
            _textProcessors.Clear();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _isInitialized)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) UpdateText();
                };
            }
        }
        #endif

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (_isInitialized) return;

            _tmpText = GetComponent<TMP_Text>();
            _tmpDropdown = GetComponent<TMP_Dropdown>();

            if (_tmpText == null && _tmpDropdown == null)
            {
                Debug.LogError($"[LocalizationTextComponent] No TMP_Text or TMP_Dropdown found on {gameObject.name}", this);
            }

            _isInitialized = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the text component with the current translation.
        /// </summary>
        public void UpdateText()
        {
            if (!isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(translationKey)) return;

            try
            {
                if (_tmpDropdown != null)
                {
                    UpdateDropdown();
                }
                else
                {
                    UpdateTextComponent();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationTextComponent] Error updating text on {gameObject.name}: {ex.Message}", this);
            }
        }

        /// <summary>
        /// Sets format parameters and updates the text.
        /// </summary>
        public void SetFormatParameters(params string[] parameters)
        {
            FormatParameters = parameters;
        }

        /// <summary>
        /// Sets a single format parameter at the specified index.
        /// </summary>
        public void SetFormatParameter(int index, string value)
        {
            if (formatParameters == null)
            {
                formatParameters = new string[index + 1];
            }
            else if (index >= formatParameters.Length)
            {
                Array.Resize(ref formatParameters, index + 1);
            }

            if (formatParameters[index] != value)
            {
                formatParameters[index] = value;
                UpdateText();
            }
        }

        /// <summary>
        /// Adds a text processor that can modify the text before display.
        /// </summary>
        public void AddTextProcessor(Func<string, string> processor)
        {
            if (processor != null && !_textProcessors.Contains(processor))
            {
                _textProcessors.Add(processor);
                UpdateText();
            }
        }

        /// <summary>
        /// Removes a text processor.
        /// </summary>
        public void RemoveTextProcessor(Func<string, string> processor)
        {
            if (_textProcessors.Remove(processor))
            {
                UpdateText();
            }
        }

        /// <summary>
        /// Clears all text processors.
        /// </summary>
        public void ClearTextProcessors()
        {
            _textProcessors.Clear();
            UpdateText();
        }

        /// <summary>
        /// Forces a refresh of the text.
        /// </summary>
        public void ForceRefresh()
        {
            _lastText = null;
            Initialize();
            UpdateText();
        }

        #endregion

        #region Private Methods

        private void UpdateTextComponent()
        {
            if (_tmpText == null) return;

            string text = GetTranslatedText();
            text = ApplyProcessors(text);

            if (_lastText == text) return;

            _tmpText.text = text;
            _lastText = text;
            
            onTextUpdated?.Invoke(text);
        }

        private void UpdateDropdown()
        {
            if (_tmpDropdown == null) return;

            var options = LocalizationManager.GetArray(translationKey);
            if (options == null || options.Length == 0)
            {
                Debug.LogWarning($"[LocalizationTextComponent] No array data found for key '{translationKey}'", this);
                return;
            }

            // Filter out empty options
            options = options.Where(opt => !string.IsNullOrEmpty(opt)).ToArray();

            // Apply size limit
            if (arraySizeLimit > 0 && options.Length > arraySizeLimit)
            {
                options = options.Take(arraySizeLimit).ToArray();
            }

            // Apply text processors
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ApplyProcessors(options[i]);
            }

            int selectedValue = _tmpDropdown.value;
            _tmpDropdown.ClearOptions();
            _tmpDropdown.AddOptions(options.ToList());

            // Restore selection if still valid
            if (selectedValue < options.Length)
            {
                _tmpDropdown.value = selectedValue;
            }
            else if (options.Length > 0)
            {
                _tmpDropdown.value = 0;
            }

            _tmpDropdown.RefreshShownValue();
        }

        private string GetTranslatedText()
        {
            if (arrayIndex >= 0)
            {
                return LocalizationManager.GetArrayText(translationKey, arrayIndex);
            }

            if (formatParameters != null && formatParameters.Length > 0)
            {
                return LocalizationManager.GetText(translationKey, formatParameters);
            }

            return LocalizationManager.GetText(translationKey);
        }

        private string ApplyProcessors(string text)
        {
            foreach (var processor in _textProcessors)
            {
                if (processor != null)
                {
                    text = processor(text);
                }
            }
            return text;
        }

        #endregion
    }
}

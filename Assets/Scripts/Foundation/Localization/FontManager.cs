using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace Foundation.Localization
{
    public class FontManager : MonoBehaviour
    {
        // シングルトンインスタンス
        public static FontManager Instance { get; private set; }

        // 各言語とフォントアセットのマッピング配列
        public FontAssetMapping[] fontMappings;

        // 登録されたTextMeshProUGUIコンポーネントのリスト
        [SerializeField] private List<TextMeshProUGUI> registeredTexts = new List<TextMeshProUGUI>();

        // 起動時の処理
        private void Awake()
        {
            // 既にインスタンスが存在していたら、重複を防ぐために破棄
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // シングルトンインスタンスを設定
            Instance = this;

            // シーンを跨いでも破棄されないように設定
            DontDestroyOnLoad(gameObject);


            // ロケール変更イベントの購読
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            // 現在のロケールに基づいてフォントを設定
            SetFontForCurrentLocale();

            // 新しいシーンが読み込まれたときのイベントに登録
            SceneManager.sceneLoaded += OnSceneLoaded;
        }


        public void SetFontForCurrentLocale()
        {
            // 現在のロケールを取得
            Locale currentLocale = LocalizationSettings.SelectedLocale;

            if (currentLocale != null)
            {
                // 現在のロケールに基づいてフォントを設定
                OnLocaleChanged(currentLocale);
            }
        }

        // TextMeshProUGUIコンポーネントを登録するためのメソッド
        public void RegisterTextMeshPro(TextMeshProUGUI tmp)
        {
            // 既にリストに含まれていなければ追加
            if (!registeredTexts.Contains(tmp))
            {
                registeredTexts.Add(tmp);
            }
        }

        // オブジェクトが破棄されるときの処理
        private void OnDestroy()
        {
            // イベントハンドラの登録解除
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }


        private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 0.2秒遅延 遅延させないと先に処理が実行されてしまってフォントアセットが切り替わらない。
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

            // 遅延後の処理
            // 例: 現在のロケールに基づいてフォントを設定
            SetFontForCurrentLocale();

            // 必要に応じて他の処理をここに追加
        }


        private void OnLocaleChanged(Locale newLocale)
        {
            SupportedLanguage currentLanguage = (SupportedLanguage)Enum.Parse(typeof(SupportedLanguage),
                newLocale.Identifier.Code.Replace("-", "_"));
            TMP_FontAsset targetFontAsset = null;

            foreach (var mapping in fontMappings)
            {
                if (mapping.language == currentLanguage)
                {
                    targetFontAsset = mapping.fontAsset;
                    break;
                }
            }

            if (targetFontAsset != null)
            {
                foreach (var tmp in registeredTexts)
                {
                    tmp.font = targetFontAsset;
                }
            }
        }
    }
}
using TMPro;
using UnityEngine;

namespace Foundation.Localization
{
    public class TMPFontRegistrar : MonoBehaviour
    {
        private void Start()
        {
            // FontManagerが存在し、このオブジェクトにTextMeshProUGUIコンポーネントがある場合、
            // このTextMeshProUGUIをFontManagerに登録する
            var textMeshPro = GetComponent<TextMeshProUGUI>();
            if (FontManager.Instance != null && textMeshPro != null)
            {
                FontManager.Instance.RegisterTextMeshPro(textMeshPro);

                //途中でテキストがアクティブになった場合はフォントが適用されないため
                //フォント設定の関数を実行
                FontManager.Instance.SetFontForCurrentLocale();
            }
        }
    }
}
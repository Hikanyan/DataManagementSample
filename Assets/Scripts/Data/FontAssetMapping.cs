using TMPro;

namespace Data
{
    // シリアライズ可能なクラスで、各言語と対応するTMP_FontAssetをマッピングします。
    [System.Serializable]
    public class FontAssetMapping
    {
        public SupportedLanguage language; // SupportedLanguage 列挙型
        public TMP_FontAsset fontAsset; // その言語のフォントアセット
    }
}
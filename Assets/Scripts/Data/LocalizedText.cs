﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Collections;
using MD;
using Cysharp.Threading.Tasks;

namespace Outgame
{
    /// <summary>
    /// ローカライズ対応テキスト
    /// </summary>
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] TMPro.TextMeshProUGUI _text;
        [SerializeField, LocalizeText] string _textKey;

        private void Awake()
        {
            _text.text = "";
        }

        void Start()
        {
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.WaitUntil(() => MasterData.IsLoadingComplete) ;
                
                var text = MasterData.GetLocalizedText(_textKey);
                if (text == null) return;

                _text.text = text;
            }).Forget();
        }

        public void SetString(string key)
        {
            _textKey = key;

            var text = MasterData.GetLocalizedText(_textKey);
            if (text == null) return;

            _text.text = text;
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            var text = MasterData.GetLocalizedText(_textKey);
            if (text == null) return;

            _text.text = text;
        }
#endif
    }
}
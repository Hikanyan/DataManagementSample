using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using static Network.WebRequest;

namespace MD
{
    /// <summary>
    /// マスターデータ管理クラス
    /// </summary>
    public class MasterData
    {
        //設定系
        private string _uri = "";
        private const string DataPrefix = "MasterData";

        //シングルトン運用
        private static readonly MasterData _instance = new MasterData();
        public static MasterData Instance => _instance;

        //ゲーム中のマスターデータ
        /// <summary>
        /// 整形済みデータ
        /// </summary>
        class PrettyData<TK, T>
        {
            private readonly Dictionary<TK, T> _data = new Dictionary<TK, T>();

            public static PrettyData<TK, T> Create(T[] data, Func<T, TK> mapper)
            {
                PrettyData<TK, T> ret = new PrettyData<TK, T>();
                foreach (var d in data)
                {
                    TK key = mapper.Invoke(d);
                    if (key == null) continue;

                    if (ret._data.ContainsKey(key))
                    {
                        Debug.Log($"duplicate key: {key}");
                        continue;
                    }

                    ret._data.Add(key, d);
                }

                return ret;
            }

            public T GetData(TK key)
            {
                return _data.TryGetValue(key, out var value) ? value : default(T);
            }

#if UNITY_EDITOR
            public TK[] GetKeys()
            {
                return _data.Keys.ToArray();
            }
#endif
        }

        //マスタデータ
        //NOTE: そもそもコードで参照するのであればべた書きもあり

        //公開マスターデータ
        public static List<Chapter> Chapters => _instance._chapters;
        public static List<Quest> Quests => _instance._quests;
        public static List<GameEvent> Events => _instance._events;

        //リスト型
        private readonly List<Chapter> _chapters = new List<Chapter>();
        private readonly List<Quest> _quests = new List<Quest>();
        private readonly List<GameEvent> _events = new List<GameEvent>();

        //辞書配列
        private PrettyData<int, Card> _cardMaster = new PrettyData<int, Card>();
        private PrettyData<int, Chapter> _chapterMaster = new PrettyData<int, Chapter>();
        private PrettyData<int, Quest> _questMaster = new PrettyData<int, Quest>();
        private PrettyData<int, Item> _itemMaster = new PrettyData<int, Item>();
        private PrettyData<int, GameEvent> _eventMaster = new PrettyData<int, GameEvent>();
        private PrettyData<string, TextData> _textMaster = new PrettyData<string, TextData>();

        //読み込み管理
        public static bool IsLoadingComplete => _instance._isInit;

        private bool _isInit = false;
        private bool _useCache = false;
        private Action _onLoadCallback = null;
        private Dictionary<string, int> _versionInfos = new Dictionary<string, int>();

        string GetFileName(string sheetName)
        {
            return $"{DataPrefix}/{sheetName}.json";
        }

        public async UniTask<int> Setup(bool useCache = true, Action callback = null)
        {
            _uri = GameSetting.MasterDataAPIURI;

            _useCache = useCache;

            //マスタ読み込み
            Debug.Log("MasterData Load Start.");

            //NOTE: そもそもコードで参照するのであればべた書きもあり
            List<UniTask> masterDataDownloads = new List<UniTask>()
            {
                LoadMasterData<TextMaster>("JP_Text"),
                LoadMasterData<TextMaster>("EN_Text"),
                LoadMasterData<CardMaster>("Card"),
                LoadMasterData<ChapterMaster>("Chapter"),
                LoadMasterData<QuestMaster>("Quest"),
                LoadMasterData<EventMaster>("Event"),
                LoadMasterData<ItemMaster>("Item"),
                LoadMasterData<EffectMaster>("Effect"),
            };
            await UniTask.WhenAll(masterDataDownloads);
            await ConstructMasterData();

            Debug.Log("MasterData Load Done.");
            _isInit = true;
            callback?.Invoke();

            return 0;
        }

        /// <summary>
        /// 最後の整形処理をする
        /// </summary>
        private async UniTask ConstructMasterData()
        {
            //マスタ結合 or 整形

            //テキストマスタを設定する
            var jpText = await LocalData.LoadAsync<TextMaster>(GetFileName("JP_Text"));
            _textMaster = PrettyData<string, TextData>.Create(jpText.Data,
                line => string.IsNullOrEmpty(line.Key) ? null : line.Key);

            //カードマスタをマージする
            var card = await LocalData.LoadAsync<CardMaster>(GetFileName("Card"));
            var item = await LocalData.LoadAsync<ItemMaster>(GetFileName("Item"));
            var chapter = await LocalData.LoadAsync<ChapterMaster>(GetFileName("Chapter"));
            var quest = await LocalData.LoadAsync<QuestMaster>(GetFileName("Quest"));
            var effect = await LocalData.LoadAsync<EffectMaster>(GetFileName("Effect"));
            var evt = await LocalData.LoadAsync<EventMaster>(GetFileName("Event"));
            var efectList = PrettyData<int, EffectData>.Create(effect.Data, line => line.Id);

            List<Card> cards = new List<Card>();
            foreach (var c in card.Data)
            {
                //カードデータを組み合わせていく
                Card d = new Card
                {
                    Id = c.Id,
                    Name = c.Name,
                    Rare = c.Rare,
                    Resource = c.Resource,
                    Effect = efectList.GetData(c.EffectId)
                };
                cards.Add(d);
            }

            _cardMaster = PrettyData<int, Card>.Create(cards.ToArray(), line => line.Id);

            //アイテムマスタをマージする
            List<Item> items = new List<Item>();
            foreach (var i in item.Data)
            {
                //アイテムデータを組み合わせていく
                Item d = new Item
                {
                    Id = i.Id,
                    Name = i.Name,
                    Type = (ItemType)i.Type,
                    Resource = i.Resource,
                    Effect = efectList.GetData(i.EffectId)
                };
                items.Add(d);
            }

            _itemMaster = PrettyData<int, Item>.Create(items.ToArray(), line => line.Id);

            //イベントマスタを整形する
            _events.Clear();
            foreach (var ev in evt.Data)
            {
                //イベントデータを組み合わせていく
                GameEvent d = new GameEvent
                {
                    Id = ev.Id,
                    Name = ev.Name,
                    Resource = ev.Resource,
                    StartAt = DateTime.Parse(ev.StartAt),
                    GameEndAt = DateTime.Parse(ev.GameEndAt),
                    EndAt = DateTime.Parse(ev.EndAt)
                };
                _events.Add(d);
            }

            _eventMaster = PrettyData<int, GameEvent>.Create(_events.ToArray(), line => line.Id);

            //クエストマスタをマージしていく
            _quests.Clear();
            foreach (var q in quest.Data)
            {
                Quest d = new Quest
                {
                    Id = q.Id,
                    Name = q.Name,
                    Resource = q.Resource,
                    ChapterId = q.ChapterId,
                    MovePoint = q.MovePoint
                };
                _quests.Add(d);
            }

            _chapters.Clear();
            foreach (var c in chapter.Data)
            {
                Chapter d = new Chapter
                {
                    Id = c.Id,
                    Name = c.Name,
                    Resource = c.Resource,
                    QuestType = c.QuestType,
                    Condition = c.Condition,
                    QuestList = _quests.Where(q => q.ChapterId == c.Id).ToList()
                };
                _chapters.Add(d);
            }

            _questMaster = PrettyData<int, Quest>.Create(_quests.ToArray(), line => line.Id);
            _chapterMaster = PrettyData<int, Chapter>.Create(_chapters.ToArray(), line => line.Id);
        }

        /// <summary>
        /// マスタデータ読み込み関数
        /// </summary>
        /// <typeparam name="T">マスタの型</typeparam>
        /// <param name="sheetName">シート名</param>
        private async UniTask LoadMasterData<T>(string sheetName) where T : MasterDataBase
        {
            var filename = GetFileName(sheetName);
            var data = await LocalData.LoadAsync<T>(filename);
            bool isUpdate = data == null || !_useCache;
            if (!isUpdate && _versionInfos.ContainsKey(sheetName))
            {
                Debug.Log($"Server: {_versionInfos[sheetName]} > Local: {data.Version}");
                isUpdate = _versionInfos[sheetName] > data.Version;
            }

            if (isUpdate)
            {
                string json = await GetRequest($"{_uri}?sheet={sheetName}");
                Debug.Log(json);
                T dt = JsonUtility.FromJson<T>(json);
                await LocalData.SaveAsync(filename, dt);
                Debug.Log($"Network download. : {filename} / {json}");
            }
            else
            {
                Debug.Log($"Local file used. : {filename}");
            }
        }

        //データ取得用ラッパー
        //TODO:

        /// <summary>
        /// テキスト取得
        /// </summary>
        /// <param name="key">テキストのキー</param>
        /// <returns>テキスト</returns>
        public static string GetLocalizedText(string key)
        {
            return _instance._textMaster.GetData(key)?.Text;
        }

        /// <summary>
        /// カード取得
        /// </summary>
        /// <param name="id">カードのId</param>
        /// <returns>カード情報</returns>
        public static Card GetCard(int id)
        {
            return _instance._cardMaster.GetData(id);
        }

        /// <summary>
        /// アイテム取得
        /// </summary>
        /// <param name="id">アイテムのId</param>
        /// <returns>アイテム情報</returns>
        public static Item GetItem(int id)
        {
            return _instance._itemMaster.GetData(id);
        }

        /// <summary>
        /// チャプターデータ取得
        /// </summary>
        /// <paramね="id">チャプターId</param>
        /// <returns>チャプター情報</returns>
        public static Chapter GetChapter(int id)
        {
            return _instance._chapterMaster.GetData(id);
        }

        /// <summary>
        /// クエストデータ取得
        /// </summary>
        /// <param name="id">クエストId</param>
        /// <returns>クエスト情報</returns>
        public static Quest GetQuest(int id)
        {
            return _instance._questMaster.GetData(id);
        }

        /// <summary>
        /// イベントデータ取得
        /// </summary>
        /// <param name="id">イベントId</param>
        /// <returns>イベント情報</returns>
        public static GameEvent GetEvent(int id)
        {
            return _instance._eventMaster.GetData(id);
        }

#if UNITY_EDITOR
        public static string[] GetTextKeys()
        {
            return _instance._textMaster.GetKeys();
        }
#endif
    }
}

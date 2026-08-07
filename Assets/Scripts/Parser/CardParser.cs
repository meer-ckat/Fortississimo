using System.Collections.Generic;
using UnityEngine;

public static class CardParser
{
    public const string CardFolder = "Data/Card";

    public const string MainDeck = "Main";
    public const string SpecialDeck = "Special";

    // 파일명(확장자 제외) -> 덱
    private static Dictionary<string, List<BaseCardData>> decks;
    private static List<string> deckNames;

    // 씬에 로더 오브젝트를 두지 않아도, Awake 순서가 어떻든 동작하도록 지연 로드로 둔다
    public static IReadOnlyList<string> DeckNames
    {
        get
        {
            EnsureLoaded();
            return deckNames;
        }
    }

    public static bool TryGetDeck(string deckName, out List<BaseCardData> cards)
    {
        EnsureLoaded();

        if(!string.IsNullOrEmpty(deckName) && decks.TryGetValue(deckName, out var found))
        {
            // 호출한 쪽에서 리스트를 건드려도 캐시가 오염되지 않게 복사본을 넘긴다
            cards = new List<BaseCardData>(found);
            return true;
        }

        cards = null;
        return false;
    }

    public static List<BaseCardData> GetDeck(string deckName)
    {
        if(TryGetDeck(deckName, out var cards))
            return cards;

        Debug.LogError($"덱 \"{deckName}\"을 찾을 수 없음. Resources/{CardFolder} 안에 {deckName}.json이 있는지 확인.");
        return new List<BaseCardData>();
    }

    // 카드 파일을 추가/수정한 뒤 다시 읽고 싶을 때
    public static void Reload()
    {
        decks = null;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if(decks != null)
            return;

        decks = new Dictionary<string, List<BaseCardData>>();
        deckNames = new List<string>();

        // .json은 Unity가 TextAsset으로 임포트하므로 폴더째로 읽힌다
        TextAsset[] files = Resources.LoadAll<TextAsset>(CardFolder);

        foreach(var file in files)
        {
            var cards = Parse(file);
            if(cards == null)
                continue;

            decks[file.name] = cards;
            deckNames.Add(file.name);
        }

        Debug.Log($"카드 덱 {decks.Count}개 로드됨: {string.Join(", ", deckNames)}");
    }

    private static List<BaseCardData> Parse(TextAsset file)
    {
        CardDeckJson json;
        try
        {
            json = JsonUtility.FromJson<CardDeckJson>(file.text);
        }
        catch(System.Exception e)
        {
            Debug.LogError($"[{file.name}] JSON 파싱 실패: {e.Message}");
            return null;
        }

        if(json == null || json.cards == null || json.cards.Count == 0)
        {
            Debug.LogError($"[{file.name}] cards가 비어있음. 루트가 {{\"cards\":[...]}} 형태인지 확인.");
            return null;
        }

        var cards = new List<BaseCardData>(json.cards.Count);
        foreach(var cardJson in json.cards)
            cards.Add(cardJson.ToCardData(file.name));

        return cards;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// JsonUtility 파싱 전용 DTO. BaseCardData를 직접 파싱하지 않는 이유는 MapJson과 같다:
// 1. JsonUtility는 enum을 정수로만 읽어서 손으로 쓰기 나쁨 -> string으로 받고 변환
// 2. Sprite 참조를 JSON에 못 담음 -> Resources 경로 문자열로 받고 로드
// 3. JsonUtility는 루트 배열([...])을 못 읽음 -> CardDeckJson으로 한 겹 감쌈

[Serializable]
public class CardDeckJson
{
    public string displayName;
    public List<CardJson> cards = new List<CardJson>();
}

[Serializable]
public class CardJson
{
    public string title;
    public string desc;
    public string image;    // Resources 기준 경로, 확장자 없이. 비워두면 이미지 없음
    public string rps;      // "Rock" | "Paper" | "Scissor"
    public int moveBlockMe;
    public int moveBlockOps;
    public int moveMe;
    public int moveOps;
    public float probability;
    public string cardType; // "Good" | "SuperGood" | "Bad" | "SuperBad" | "Joker"

    public BaseCardData ToCardData(string context)
    {
        // 어느 카드가 문제인지 로그에서 바로 보이게 이름을 붙여 넘긴다
        string where = string.IsNullOrEmpty(title) ? context : $"{context}/{title}";

        if(string.IsNullOrEmpty(title))
            Debug.LogWarning($"[{context}] title이 비어있는 카드가 있음");

        return new BaseCardData
        {
            Title = title,
            Desc = desc,
            Image = LoadImage(where),
            RPS = (int)ParseEnum(rps, RPSType.Rock, "rps", where),
            moveBlockMe = moveBlockMe,
            moveBlockOps = moveBlockOps,
            moveMe = moveMe,
            moveOps = moveOps,
            probability = probability,
            cardType = ParseEnum(cardType, CardType.Good, "cardType", where),
        };
    }

    Sprite LoadImage(string context)
    {
        if(string.IsNullOrEmpty(image))
            return null;

        Sprite sprite = Resources.Load<Sprite>(image);
        if(sprite == null)
            Debug.LogWarning($"[{context}] image \"{image}\"를 Resources에서 못 찾음 -> 이미지 없음으로 처리");

        return sprite;
    }

    // 빈 값도 경고한다. 키 이름을 틀리면(rps -> RPS) JsonUtility가 조용히 넘어가는데,
    // ScriptableObject 시절 딱 이 오타 때문에 카드가 엉뚱한 값으로 돌아간 적이 있다.
    static T ParseEnum<T>(string raw, T fallback, string field, string context) where T : struct, Enum
    {
        if(string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning($"[{context}] {field}가 비어있음 -> {fallback}로 처리");
            return fallback;
        }

        // Enum.TryParse는 "7" 같은 숫자 문자열도 통과시키므로 IsDefined로 한 번 더 거른다
        if(Enum.TryParse(raw, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            return parsed;

        Debug.LogWarning($"[{context}] 알 수 없는 {field} \"{raw}\" -> {fallback}로 처리");
        return fallback;
    }
}

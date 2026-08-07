using System;
using System.Collections.Generic;
using UnityEngine;

// JsonUtility 파싱 전용 DTO. Cell 구조체를 직접 파싱하지 않는 이유:
// 1. JsonUtility는 enum을 정수로만 읽어서 손으로 쓰기 나쁨 -> string으로 받고 변환
// 2. Color를 {"r":1,"g":0,...}로 읽어서 손으로 쓰기 나쁨 -> "#RRGGBB"로 받고 변환
// 3. JsonUtility는 루트 배열([...])을 못 읽음 -> MapJson으로 한 겹 감쌈

[Serializable]
public class MapJson
{
    public string displayName;
    public List<CellJson> cells = new List<CellJson>();
}

[Serializable]
public class CellJson
{
    public float x;
    public float y;
    public string eventType;
    public string color;

    public Cell ToCell(string context)
    {
        Cell cell = new Cell();
        cell.pos = new Vector2(x, y);
        cell.eventType = ParseEventType(context);
        cell.color = ParseColor(context);
        return cell;
    }

    EventType ParseEventType(string context)
    {
        if(string.IsNullOrEmpty(eventType))
            return EventType.normal;

        // Enum.TryParse는 "7" 같은 숫자 문자열도 통과시키므로 IsDefined로 한 번 더 거른다
        if(Enum.TryParse(eventType, true, out EventType parsed) && Enum.IsDefined(typeof(EventType), parsed))
            return parsed;

        Debug.LogWarning($"[{context}] 알 수 없는 eventType \"{eventType}\" -> normal로 처리");
            return EventType.normal;
    }

    Color ParseColor(string context)
    {
        if(string.IsNullOrEmpty(color))
            return Color.white;

        if(ColorUtility.TryParseHtmlString(color, out Color parsed))
            return parsed;

        Debug.LogWarning($"[{context}] 알 수 없는 color \"{color}\" -> white로 처리");
        return Color.white;
    }
}
